/*
 * QUANTCONNECT.COM - Democratizing Finance, Empowering Individuals.
 * Lean Algorithmic Trading Engine v2.0. Copyright 2014 QuantConnect Corporation.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
*/

using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net.Http;
using System.Text.RegularExpressions;
using CsvHelper;
using CsvHelper.Configuration;
using QuantConnect.Logging;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Provides symbol mapping between LEAN symbols and Fyers symbols
    /// Fyers symbol format: EXCHANGE:SYMBOL-SERIES (e.g., NSE:SBIN-EQ, NSE:NIFTY24DECFUT)
    /// </summary>
    public class FyersSymbolMapper : ISymbolMapper
    {
        // Cache for LEAN symbol to Fyers symbol mapping
        private readonly ConcurrentDictionary<Symbol, string> _leanToFyersCache = new();

        // Cache for Fyers symbol to LEAN symbol mapping
        private readonly ConcurrentDictionary<string, Symbol> _fyersToLeanCache = new();

        // Symbol master data by Fyers symbol
        private readonly ConcurrentDictionary<string, FyersInstrument> _instruments = new();

        // Known symbols list
        private readonly List<Symbol> _knownSymbols = new();

        // Lock for thread-safe symbol master loading
        private readonly object _loadLock = new();
        private bool _isLoaded;

        /// <summary>
        /// Gets the list of known symbols from the symbol master
        /// </summary>
        public IReadOnlyList<Symbol> KnownSymbols => _knownSymbols.AsReadOnly();

        /// <summary>
        /// Creates a new instance of FyersSymbolMapper
        /// </summary>
        public FyersSymbolMapper()
        {
        }

        /// <summary>
        /// Loads symbol master files from Fyers
        /// </summary>
        /// <param name="segments">Segments to load (CM, FO, CD, COM)</param>
        public void LoadSymbolMaster(params string[] segments)
        {
            lock (_loadLock)
            {
                if (_isLoaded)
                    return;

                var segmentsToLoad = segments.Length > 0 ? segments : new[] { "NSE_CM", "NSE_FO", "BSE_CM", "MCX_COM" };

                foreach (var segment in segmentsToLoad)
                {
                    try
                    {
                        var url = GetSymbolMasterUrl(segment);
                        if (string.IsNullOrEmpty(url))
                        {
                            Log.Trace($"FyersSymbolMapper.LoadSymbolMaster: Unknown segment {segment}");
                            continue;
                        }

                        LoadSymbolMasterFromUrl(url, segment);
                        Log.Trace($"FyersSymbolMapper.LoadSymbolMaster: Loaded {segment}");
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"FyersSymbolMapper.LoadSymbolMaster: Failed to load {segment} - {ex.Message}");
                    }
                }

                _isLoaded = true;
            }
        }

        /// <summary>
        /// Converts a LEAN symbol to Fyers symbol format
        /// </summary>
        /// <param name="symbol">LEAN symbol</param>
        /// <returns>Fyers symbol (e.g., NSE:SBIN-EQ)</returns>
        public string GetBrokerageSymbol(Symbol symbol)
        {
            if (symbol == null)
                throw new ArgumentNullException(nameof(symbol));

            // Check cache first
            if (_leanToFyersCache.TryGetValue(symbol, out var cachedSymbol))
                return cachedSymbol;

            var fyersSymbol = ConvertLeanToFyers(symbol);
            _leanToFyersCache[symbol] = fyersSymbol;
            return fyersSymbol;
        }

        /// <summary>
        /// Converts a Fyers symbol to LEAN symbol
        /// </summary>
        /// <param name="brokerageSymbol">Fyers symbol (e.g., NSE:SBIN-EQ)</param>
        /// <param name="securityType">Security type</param>
        /// <param name="market">Market</param>
        /// <param name="expirationDate">Expiration date for derivatives</param>
        /// <param name="strike">Strike price for options</param>
        /// <param name="optionRight">Option right (Call/Put)</param>
        /// <returns>LEAN symbol</returns>
        public Symbol GetLeanSymbol(
            string brokerageSymbol,
            SecurityType securityType = SecurityType.Equity,
            string market = Market.India,
            DateTime expirationDate = default,
            decimal strike = 0,
            OptionRight optionRight = OptionRight.Call)
        {
            if (string.IsNullOrWhiteSpace(brokerageSymbol))
                throw new ArgumentNullException(nameof(brokerageSymbol));

            // Check cache first
            if (_fyersToLeanCache.TryGetValue(brokerageSymbol, out var cachedSymbol))
                return cachedSymbol;

            var leanSymbol = ConvertFyersToLean(brokerageSymbol, securityType, market, expirationDate, strike, optionRight);
            _fyersToLeanCache[brokerageSymbol] = leanSymbol;
            return leanSymbol;
        }

        /// <summary>
        /// Gets instrument data from symbol master
        /// </summary>
        /// <param name="fyersSymbol">Fyers symbol</param>
        /// <returns>Instrument data or null if not found</returns>
        public FyersInstrument? GetInstrument(string fyersSymbol)
        {
            return _instruments.TryGetValue(fyersSymbol, out var instrument) ? instrument : null;
        }

        /// <summary>
        /// Gets the exchange from a Fyers symbol
        /// </summary>
        /// <param name="fyersSymbol">Fyers symbol (e.g., NSE:SBIN-EQ)</param>
        /// <returns>Exchange code (NSE, BSE, MCX)</returns>
        public static string GetExchangeFromSymbol(string fyersSymbol)
        {
            if (string.IsNullOrEmpty(fyersSymbol))
                return FyersExchange.NSE;

            var colonIndex = fyersSymbol.IndexOf(':');
            return colonIndex > 0 ? fyersSymbol.Substring(0, colonIndex) : FyersExchange.NSE;
        }

        /// <summary>
        /// Determines security type from Fyers symbol format
        /// </summary>
        /// <param name="fyersSymbol">Fyers symbol</param>
        /// <returns>Security type</returns>
        public static SecurityType GetSecurityTypeFromSymbol(string fyersSymbol)
        {
            if (string.IsNullOrEmpty(fyersSymbol))
                return SecurityType.Equity;

            // NSE:SBIN-EQ -> Equity
            if (fyersSymbol.EndsWith("-EQ"))
                return SecurityType.Equity;

            // NSE:NIFTY50-INDEX -> Index
            if (fyersSymbol.EndsWith("-INDEX"))
                return SecurityType.Index;

            // NSE:NIFTY24DECFUT -> Future
            if (fyersSymbol.Contains("FUT"))
                return SecurityType.Future;

            // NSE:NIFTY24D1923500CE or NSE:NIFTY24D1923500PE -> Option
            if (fyersSymbol.EndsWith("CE") || fyersSymbol.EndsWith("PE"))
                return SecurityType.Option;

            return SecurityType.Equity;
        }

        #region Private Methods

        private string ConvertLeanToFyers(Symbol symbol)
        {
            var exchange = GetFyersExchange(symbol);

            switch (symbol.SecurityType)
            {
                case SecurityType.Equity:
                    // SBIN -> NSE:SBIN-EQ
                    return $"{exchange}:{symbol.Value}-EQ";

                case SecurityType.Index:
                    // NIFTY50 -> NSE:NIFTY50-INDEX
                    return $"{exchange}:{symbol.Value}-INDEX";

                case SecurityType.Future:
                    // Build future symbol: NIFTY24DECFUT
                    var futureExpiry = symbol.ID.Date;
                    var futureUnderlying = symbol.ID.Symbol;
                    var futureMonthCode = GetMonthCode(futureExpiry.Month);
                    var futureYear = futureExpiry.ToString("yy");
                    return $"{exchange}:{futureUnderlying}{futureYear}{futureMonthCode}FUT";

                case SecurityType.Option:
                    // Build option symbol: NIFTY24D1923500CE
                    var optionExpiry = symbol.ID.Date;
                    var optionUnderlying = symbol.ID.Symbol;
                    var optionMonthCode = optionExpiry.ToString("MMM").ToUpper().Substring(0, 1);
                    var optionDay = optionExpiry.Day.ToString("D2");
                    var optionYear = optionExpiry.ToString("yy");
                    var strike = (int)symbol.ID.StrikePrice;
                    var optionType = symbol.ID.OptionRight == OptionRight.Call ? "CE" : "PE";
                    return $"{exchange}:{optionUnderlying}{optionYear}{optionMonthCode}{optionDay}{strike}{optionType}";

                default:
                    throw new ArgumentException($"Unsupported security type: {symbol.SecurityType}");
            }
        }

        private Symbol ConvertFyersToLean(
            string fyersSymbol,
            SecurityType securityType,
            string market,
            DateTime expirationDate,
            decimal strike,
            OptionRight optionRight)
        {
            // Extract the trading symbol part (after colon)
            var symbolPart = fyersSymbol;
            var colonIndex = fyersSymbol.IndexOf(':');
            if (colonIndex > 0)
            {
                symbolPart = fyersSymbol.Substring(colonIndex + 1);
            }

            // Auto-detect security type if needed
            if (securityType == SecurityType.Equity)
            {
                securityType = GetSecurityTypeFromSymbol(fyersSymbol);
            }

            switch (securityType)
            {
                case SecurityType.Equity:
                    // NSE:SBIN-EQ -> SBIN
                    // Use mapSymbol: false to avoid requiring MapFileProvider for Indian equities
                    var equityTicker = symbolPart.Replace("-EQ", "");
                    var equitySid = SecurityIdentifier.GenerateEquity(equityTicker, market, mapSymbol: false);
                    return new Symbol(equitySid, equityTicker);

                case SecurityType.Index:
                    // NSE:NIFTY50-INDEX -> NIFTY50
                    var indexTicker = symbolPart.Replace("-INDEX", "");
                    var indexSid = SecurityIdentifier.GenerateIndex(indexTicker, market);
                    return new Symbol(indexSid, indexTicker);

                case SecurityType.Future:
                    // NSE:NIFTY24DECFUT -> parse and create future symbol
                    var futureInfo = ParseFutureSymbol(symbolPart);
                    return Symbol.CreateFuture(futureInfo.Underlying, market, futureInfo.Expiry);

                case SecurityType.Option:
                    // NSE:NIFTY24D1923500CE -> parse and create option symbol
                    var optionInfo = ParseOptionSymbol(symbolPart);
                    return Symbol.CreateOption(
                        optionInfo.Underlying,
                        market,
                        OptionStyle.European,
                        optionInfo.Right,
                        optionInfo.Strike,
                        optionInfo.Expiry
                    );

                default:
                    throw new ArgumentException($"Unsupported security type: {securityType}");
            }
        }

        private static string GetFyersExchange(Symbol symbol)
        {
            // Default to NSE for India market
            if (symbol.ID.Market == Market.India)
            {
                return FyersExchange.NSE;
            }

            return FyersExchange.NSE;
        }

        private static string GetMonthCode(int month)
        {
            var months = new[] { "JAN", "FEB", "MAR", "APR", "MAY", "JUN", "JUL", "AUG", "SEP", "OCT", "NOV", "DEC" };
            return months[month - 1];
        }

        private (string Underlying, DateTime Expiry) ParseFutureSymbol(string symbol)
        {
            // NIFTY24DECFUT
            // Pattern: UNDERLYING + YY + MMM + FUT
            var match = Regex.Match(symbol, @"^(.+?)(\d{2})([A-Z]{3})FUT$");
            if (!match.Success)
            {
                throw new ArgumentException($"Cannot parse future symbol: {symbol}");
            }

            var underlying = match.Groups[1].Value;
            var year = 2000 + int.Parse(match.Groups[2].Value);
            var monthStr = match.Groups[3].Value;
            var month = DateTime.ParseExact(monthStr, "MMM", CultureInfo.InvariantCulture).Month;

            // Get last Thursday of the month (typical expiry)
            var expiry = GetLastThursday(year, month);

            return (underlying, expiry);
        }

        private (string Underlying, DateTime Expiry, decimal Strike, OptionRight Right) ParseOptionSymbol(string symbol)
        {
            // NIFTY24D1923500CE
            // Pattern: UNDERLYING + YY + M + DD + STRIKE + CE/PE
            // where M is single letter month code

            var isCall = symbol.EndsWith("CE");
            var isPut = symbol.EndsWith("PE");

            if (!isCall && !isPut)
            {
                throw new ArgumentException($"Cannot parse option symbol: {symbol}");
            }

            var optionRight = isCall ? OptionRight.Call : OptionRight.Put;
            var symbolWithoutType = symbol.Substring(0, symbol.Length - 2);

            // Extract strike from end
            var strikeMatch = Regex.Match(symbolWithoutType, @"(\d+)$");
            if (!strikeMatch.Success)
            {
                throw new ArgumentException($"Cannot parse strike from option symbol: {symbol}");
            }

            var strike = decimal.Parse(strikeMatch.Groups[1].Value);
            var remainingSymbol = symbolWithoutType.Substring(0, symbolWithoutType.Length - strikeMatch.Groups[1].Length);

            // Extract date part (YYXDD where X is month letter)
            var dateMatch = Regex.Match(remainingSymbol, @"^(.+?)(\d{2})([A-Z])(\d{2})$");
            if (!dateMatch.Success)
            {
                throw new ArgumentException($"Cannot parse date from option symbol: {symbol}");
            }

            var underlying = dateMatch.Groups[1].Value;
            var year = 2000 + int.Parse(dateMatch.Groups[2].Value);
            var monthLetter = dateMatch.Groups[3].Value[0];
            var day = int.Parse(dateMatch.Groups[4].Value);

            // Convert month letter to month number
            var month = GetMonthFromLetter(monthLetter);
            var expiry = new DateTime(year, month, day);

            return (underlying, expiry, strike, optionRight);
        }

        private static int GetMonthFromLetter(char letter)
        {
            // Common month abbreviation first letters: J(an), F(eb), M(ar), A(pr), M(ay), J(un), J(ul), A(ug), S(ep), O(ct), N(ov), D(ec)
            // Fyers uses: J, F, M, A, Y, N, L, G, S, O, N, D for months 1-12
            return letter switch
            {
                'J' => 1,  // January or June/July - need context
                'F' => 2,  // February
                'M' => 3,  // March
                'A' => 4,  // April or August
                'Y' => 5,  // May (using Y to differentiate)
                'N' => 6,  // June (using N) or November
                'L' => 7,  // July (using L)
                'G' => 8,  // August (using G)
                'S' => 9,  // September
                'O' => 10, // October
                'V' => 11, // November (using V)
                'D' => 12, // December
                _ => throw new ArgumentException($"Unknown month letter: {letter}")
            };
        }

        private static DateTime GetLastThursday(int year, int month)
        {
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
            while (lastDay.DayOfWeek != DayOfWeek.Thursday)
            {
                lastDay = lastDay.AddDays(-1);
            }
            return lastDay;
        }

        private string GetSymbolMasterUrl(string segment)
        {
            return segment.ToUpperInvariant() switch
            {
                "NSE_CM" => FyersConstants.SymbolMasterNseCm,
                "BSE_CM" => FyersConstants.SymbolMasterBseCm,
                "NSE_FO" => FyersConstants.SymbolMasterNseFo,
                "BSE_FO" => FyersConstants.SymbolMasterBseFo,
                "MCX_COM" => FyersConstants.SymbolMasterMcxCom,
                "NSE_CD" => FyersConstants.SymbolMasterNseCd,
                _ => string.Empty
            };
        }

        private void LoadSymbolMasterFromUrl(string url, string segment)
        {
            using var httpClient = new HttpClient();
            using var stream = httpClient.GetStreamAsync(url).Result;
            using var reader = new StreamReader(stream);
            using var csv = new CsvReader(reader, new CsvConfiguration(CultureInfo.InvariantCulture)
            {
                HasHeaderRecord = true,
                MissingFieldFound = null,
                HeaderValidated = null
            });

            while (csv.Read())
            {
                try
                {
                    var instrument = new FyersInstrument
                    {
                        FyToken = csv.GetField<string>(0) ?? string.Empty,
                        Symbol = csv.GetField<string>(1) ?? string.Empty,
                        Name = csv.GetField<string>(2) ?? string.Empty,
                        Exchange = csv.GetField<string>(3) ?? string.Empty,
                        Segment = csv.GetField<string>(4) ?? string.Empty,
                        InstrumentType = csv.GetField<string>(5) ?? string.Empty,
                        LotSize = csv.TryGetField<int>(6, out var lotSize) ? lotSize : 1,
                        TickSize = csv.TryGetField<decimal>(7, out var tickSize) ? tickSize : 0.05m
                    };

                    if (!string.IsNullOrEmpty(instrument.Symbol))
                    {
                        _instruments[instrument.Symbol] = instrument;
                    }
                }
                catch (Exception ex)
                {
                    Log.Trace($"FyersSymbolMapper.LoadSymbolMasterFromUrl: Failed to parse row - {ex.Message}");
                }
            }
        }

        #endregion
    }

    /// <summary>
    /// Represents an instrument from Fyers symbol master
    /// </summary>
    public class FyersInstrument
    {
        /// <summary>
        /// Fyers token (unique identifier)
        /// </summary>
        public string FyToken { get; set; } = string.Empty;

        /// <summary>
        /// Full symbol (e.g., NSE:SBIN-EQ)
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Instrument name/description
        /// </summary>
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Exchange code
        /// </summary>
        public string Exchange { get; set; } = string.Empty;

        /// <summary>
        /// Market segment
        /// </summary>
        public string Segment { get; set; } = string.Empty;

        /// <summary>
        /// Instrument type (EQUITY, FUT, CE, PE, etc.)
        /// </summary>
        public string InstrumentType { get; set; } = string.Empty;

        /// <summary>
        /// Lot size
        /// </summary>
        public int LotSize { get; set; } = 1;

        /// <summary>
        /// Tick size (minimum price movement)
        /// </summary>
        public decimal TickSize { get; set; } = 0.05m;

        /// <summary>
        /// Expiry date (for F&O)
        /// </summary>
        public DateTime? Expiry { get; set; }

        /// <summary>
        /// Strike price (for options)
        /// </summary>
        public decimal? Strike { get; set; }

        /// <summary>
        /// Option type (CE/PE)
        /// </summary>
        public string? OptionType { get; set; }

        /// <summary>
        /// ISIN code
        /// </summary>
        public string? Isin { get; set; }
    }
}
