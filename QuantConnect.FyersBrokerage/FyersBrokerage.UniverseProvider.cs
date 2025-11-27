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
using System.Collections.Generic;
using System.Linq;
using QuantConnect.Logging;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Fyers Brokerage - Symbol lookup and universe provider functionality
    /// Provides symbol lookup capabilities for universe selection
    /// </summary>
    /// <remarks>
    /// Note: IDataQueueUniverseProvider interface is not available in QuantConnect.Brokerages 2.5.*
    /// This implementation provides the same functionality via direct methods
    /// </remarks>
    public partial class FyersBrokerage
    {
        #region Universe Provider Implementation

        /// <summary>
        /// Method returns a collection of Symbols that are available at the data source
        /// </summary>
        /// <param name="symbol">Symbol to lookup</param>
        /// <param name="includeExpired">Include expired contracts</param>
        /// <param name="securityCurrency">Security currency filter</param>
        /// <returns>Enumerable of matching symbols</returns>
        public IEnumerable<Symbol> LookupSymbols(Symbol symbol, bool includeExpired, string? securityCurrency = null)
        {
            Log.Trace($"FyersBrokerage.LookupSymbols: Looking up {symbol.Value} ({symbol.SecurityType})");

            if (_symbolMapper == null)
            {
                Log.Error("FyersBrokerage.LookupSymbols: Symbol mapper not initialized");
                yield break;
            }

            // Ensure symbol master is loaded
            _symbolMapper.LoadSymbolMaster();

            var underlying = symbol.HasUnderlying ? symbol.Underlying.Value : symbol.Value;

            switch (symbol.SecurityType)
            {
                case SecurityType.Option:
                case SecurityType.IndexOption:
                    foreach (var optionSymbol in LookupOptionSymbols(underlying, symbol, includeExpired))
                    {
                        yield return optionSymbol;
                    }
                    break;

                case SecurityType.Future:
                    foreach (var futureSymbol in LookupFutureSymbols(underlying, symbol, includeExpired))
                    {
                        yield return futureSymbol;
                    }
                    break;

                case SecurityType.Equity:
                case SecurityType.Index:
                    // For equity/index, return the symbol itself if it exists
                    var fyersSymbol = _symbolMapper.GetBrokerageSymbol(symbol);
                    var instrument = _symbolMapper.GetInstrument(fyersSymbol);
                    if (instrument != null)
                    {
                        yield return symbol;
                    }
                    break;

                default:
                    Log.Trace($"FyersBrokerage.LookupSymbols: Unsupported security type {symbol.SecurityType}");
                    break;
            }
        }

        /// <summary>
        /// Returns whether selection can take place or not
        /// </summary>
        /// <returns>True if selection can be performed</returns>
        public bool CanPerformSelection()
        {
            // Selection can be performed if we're connected and have the API client
            return _apiClient != null && _symbolMapper != null;
        }

        #endregion

        #region Symbol Lookup Helpers

        /// <summary>
        /// Looks up option symbols for the given underlying
        /// </summary>
        private IEnumerable<Symbol> LookupOptionSymbols(string underlying, Symbol canonicalSymbol, bool includeExpired)
        {
            // Get all instruments from symbol master
            var instruments = GetOptionInstruments(underlying);

            foreach (var instrument in instruments)
            {
                // Skip expired options if not requested
                if (!includeExpired && instrument.Expiry.HasValue && instrument.Expiry.Value < DateTime.UtcNow.Date)
                {
                    continue;
                }

                // Parse option details from symbol
                var optionDetails = ParseOptionInstrument(instrument);
                if (optionDetails == null)
                {
                    continue;
                }

                // Create the option symbol
                var optionSymbol = Symbol.CreateOption(
                    underlying,
                    Market.India,
                    OptionStyle.European, // Indian options are European style
                    optionDetails.Value.Right,
                    optionDetails.Value.Strike,
                    optionDetails.Value.Expiry
                );

                yield return optionSymbol;
            }
        }

        /// <summary>
        /// Looks up future symbols for the given underlying
        /// </summary>
        private IEnumerable<Symbol> LookupFutureSymbols(string underlying, Symbol canonicalSymbol, bool includeExpired)
        {
            // Get all future instruments from symbol master
            var instruments = GetFutureInstruments(underlying);

            foreach (var instrument in instruments)
            {
                // Skip expired futures if not requested
                if (!includeExpired && instrument.Expiry.HasValue && instrument.Expiry.Value < DateTime.UtcNow.Date)
                {
                    continue;
                }

                if (!instrument.Expiry.HasValue)
                {
                    continue;
                }

                // Create the future symbol
                var futureSymbol = Symbol.CreateFuture(
                    underlying,
                    Market.India,
                    instrument.Expiry.Value
                );

                yield return futureSymbol;
            }
        }

        /// <summary>
        /// Gets option instruments for the given underlying
        /// </summary>
        private IEnumerable<FyersInstrument> GetOptionInstruments(string underlying)
        {
            // This would query the symbol master for all options with this underlying
            // For now, return empty - full implementation would search the instruments dictionary
            var allKnownSymbols = _symbolMapper?.KnownSymbols ?? new List<Symbol>();

            // Filter to options matching the underlying
            return Enumerable.Empty<FyersInstrument>();
        }

        /// <summary>
        /// Gets future instruments for the given underlying
        /// </summary>
        private IEnumerable<FyersInstrument> GetFutureInstruments(string underlying)
        {
            // This would query the symbol master for all futures with this underlying
            // For now, return empty - full implementation would search the instruments dictionary
            return Enumerable.Empty<FyersInstrument>();
        }

        /// <summary>
        /// Parses option details from a Fyers instrument
        /// </summary>
        private (OptionRight Right, decimal Strike, DateTime Expiry)? ParseOptionInstrument(FyersInstrument instrument)
        {
            try
            {
                if (string.IsNullOrEmpty(instrument.OptionType))
                {
                    return null;
                }

                var right = instrument.OptionType.ToUpperInvariant() == "CE" ? OptionRight.Call : OptionRight.Put;
                var strike = instrument.Strike ?? 0;
                var expiry = instrument.Expiry ?? DateTime.MinValue;

                if (strike == 0 || expiry == DateTime.MinValue)
                {
                    return null;
                }

                return (right, strike, expiry);
            }
            catch
            {
                return null;
            }
        }

        #endregion
    }
}
