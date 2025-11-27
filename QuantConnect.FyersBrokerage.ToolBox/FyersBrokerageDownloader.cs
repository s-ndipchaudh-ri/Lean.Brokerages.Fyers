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
using NodaTime;
using QuantConnect.Brokerages.Fyers.Api;
using QuantConnect.Brokerages.Fyers.Messages;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers.ToolBox
{
    /// <summary>
    /// Fyers Brokerage Data Downloader implementation
    /// Downloads historical data from Fyers API and saves to disk in LEAN format
    /// </summary>
    public class FyersBrokerageDownloader : IDataDownloader, IDisposable
    {
        private readonly FyersApiClient _apiClient;
        private readonly FyersSymbolMapper _symbolMapper;
        private static readonly DateTimeZone IndiaTimeZone = DateTimeZoneProviders.Tzdb["Asia/Kolkata"];
        private bool _disposed;

        /// <summary>
        /// Creates a new instance of FyersBrokerageDownloader using config settings
        /// </summary>
        public FyersBrokerageDownloader()
            : this(
                Config.Get("fyers-client-id"),
                Config.Get("fyers-access-token"))
        {
        }

        /// <summary>
        /// Creates a new instance of FyersBrokerageDownloader
        /// </summary>
        /// <param name="clientId">Fyers App ID (Client ID)</param>
        /// <param name="accessToken">Fyers access token from OAuth</param>
        public FyersBrokerageDownloader(string clientId, string accessToken)
        {
            if (string.IsNullOrWhiteSpace(clientId))
                throw new ArgumentNullException(nameof(clientId), "Fyers Client ID is required. Set 'fyers-client-id' in config.");
            if (string.IsNullOrWhiteSpace(accessToken))
                throw new ArgumentNullException(nameof(accessToken), "Fyers access token is required. Set 'fyers-access-token' in config.");

            _apiClient = new FyersApiClient(clientId, accessToken);
            _symbolMapper = new FyersSymbolMapper();
        }

        /// <summary>
        /// Get historical data enumerable for a single symbol, type and resolution given this start and end time (in UTC).
        /// </summary>
        /// <param name="dataDownloaderGetParameters">Model class for passing in parameters for historical data</param>
        /// <returns>Enumerable of base data for this symbol</returns>
        public IEnumerable<BaseData> Get(DataDownloaderGetParameters dataDownloaderGetParameters)
        {
            var symbol = dataDownloaderGetParameters.Symbol;
            var resolution = dataDownloaderGetParameters.Resolution;
            var startUtc = dataDownloaderGetParameters.StartUtc;
            var endUtc = dataDownloaderGetParameters.EndUtc;
            var tickType = dataDownloaderGetParameters.TickType;

            // Validate parameters
            if (!CanDownload(symbol, resolution, tickType))
            {
                yield break;
            }

            // Convert resolution to Fyers format
            var fyersResolution = ConvertResolutionToFyers(resolution);
            if (string.IsNullOrEmpty(fyersResolution))
            {
                Log.Error($"FyersBrokerageDownloader.Get(): Resolution {resolution} is not supported by Fyers");
                yield break;
            }

            // Get Fyers symbol
            var fyersSymbol = _symbolMapper.GetBrokerageSymbol(symbol);
            if (string.IsNullOrEmpty(fyersSymbol))
            {
                Log.Error($"FyersBrokerageDownloader.Get(): Could not map symbol {symbol}");
                yield break;
            }

            Log.Trace($"FyersBrokerageDownloader.Get(): Downloading {resolution} data for {fyersSymbol} " +
                      $"from {startUtc:yyyy-MM-dd} to {endUtc:yyyy-MM-dd}");

            // Fyers has data limits - split into chunks
            var maxDaysPerRequest = GetMaxDaysForResolution(resolution);
            var startDate = startUtc.Date;
            var endDate = endUtc.Date;

            while (startDate < endDate)
            {
                var chunkEndDate = startDate.AddDays(maxDaysPerRequest);
                if (chunkEndDate > endDate)
                    chunkEndDate = endDate;

                var historyRequest = new FyersHistoryRequest
                {
                    Symbol = fyersSymbol,
                    Resolution = fyersResolution,
                    DateFormat = "1", // Epoch format
                    RangeFrom = startDate.ToString("yyyy-MM-dd"),
                    RangeTo = chunkEndDate.ToString("yyyy-MM-dd"),
                    ContinuousFlag = "0"
                };

                FyersHistoryResponse response = null;

                try
                {
                    response = _apiClient.GetHistory(historyRequest);
                }
                catch (Exception ex)
                {
                    Log.Error($"FyersBrokerageDownloader.Get(): API error - {ex.Message}");
                    break;
                }

                if (response == null || !response.IsSuccess || response.Candles == null)
                {
                    Log.Trace($"FyersBrokerageDownloader.Get(): No data returned for {fyersSymbol} " +
                              $"from {startDate:yyyy-MM-dd} to {chunkEndDate:yyyy-MM-dd}");
                    startDate = chunkEndDate;
                    continue;
                }

                // Convert candles to LEAN TradeBar
                foreach (var candle in response.Candles)
                {
                    if (candle.Count < 6)
                        continue;

                    var bar = ConvertCandleToTradeBar(candle, symbol, resolution);

                    // Filter by requested time range
                    if (bar.Time >= startUtc && bar.Time < endUtc)
                    {
                        yield return bar;
                    }
                }

                startDate = chunkEndDate;
            }
        }

        /// <summary>
        /// Checks if this downloader can handle the specified parameters
        /// </summary>
        private static bool CanDownload(Symbol symbol, Resolution resolution, TickType tickType)
        {
            // Check market
            if (symbol.ID.Market != Market.India)
            {
                Log.Error($"FyersBrokerageDownloader: Only India market is supported. Got: {symbol.ID.Market}");
                return false;
            }

            // Check security type
            var supportedTypes = new[] { SecurityType.Equity, SecurityType.Index, SecurityType.Future, SecurityType.Option };
            if (Array.IndexOf(supportedTypes, symbol.SecurityType) < 0)
            {
                Log.Error($"FyersBrokerageDownloader: Security type {symbol.SecurityType} is not supported");
                return false;
            }

            // Check resolution
            var supportedResolutions = new[] { Resolution.Minute, Resolution.Hour, Resolution.Daily };
            if (Array.IndexOf(supportedResolutions, resolution) < 0)
            {
                Log.Error($"FyersBrokerageDownloader: Resolution {resolution} is not supported. " +
                          "Supported: Minute, Hour, Daily");
                return false;
            }

            // Check tick type
            if (tickType != TickType.Trade)
            {
                Log.Error($"FyersBrokerageDownloader: Only Trade tick type is supported. Got: {tickType}");
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts LEAN resolution to Fyers resolution string
        /// </summary>
        private static string ConvertResolutionToFyers(Resolution resolution)
        {
            return resolution switch
            {
                Resolution.Minute => FyersResolution.Minute1,
                Resolution.Hour => FyersResolution.Minute60,
                Resolution.Daily => FyersResolution.Daily,
                _ => null
            };
        }

        /// <summary>
        /// Gets the maximum number of days for a single history request based on resolution
        /// </summary>
        private static int GetMaxDaysForResolution(Resolution resolution)
        {
            return resolution switch
            {
                Resolution.Minute => 30,   // 1-minute data: ~3 months available
                Resolution.Hour => 90,     // Hourly data: ~6 months available
                Resolution.Daily => 365,   // Daily data: 10+ years available
                _ => 30
            };
        }

        /// <summary>
        /// Converts a Fyers candle array to a LEAN TradeBar
        /// Candle format: [timestamp, open, high, low, close, volume]
        /// </summary>
        private static TradeBar ConvertCandleToTradeBar(List<decimal> candle, Symbol symbol, Resolution resolution)
        {
            // Extract candle data
            var timestamp = (long)candle[0];
            var open = candle[1];
            var high = candle[2];
            var low = candle[3];
            var close = candle[4];
            var volume = (long)candle[5];

            // Convert Unix timestamp to DateTime
            var utcTime = DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime;

            // Convert to India time for market hours alignment
            var indiaInstant = Instant.FromDateTimeUtc(utcTime);
            var indiaTime = indiaInstant.InZone(IndiaTimeZone).ToDateTimeUnspecified();

            // Get the period for the bar
            var period = GetResolutionPeriod(resolution);

            return new TradeBar
            {
                Symbol = symbol,
                Time = indiaTime,
                Open = open,
                High = high,
                Low = low,
                Close = close,
                Volume = volume,
                Period = period
            };
        }

        /// <summary>
        /// Gets the time period for a resolution
        /// </summary>
        private static TimeSpan GetResolutionPeriod(Resolution resolution)
        {
            return resolution switch
            {
                Resolution.Minute => TimeSpan.FromMinutes(1),
                Resolution.Hour => TimeSpan.FromHours(1),
                Resolution.Daily => TimeSpan.FromDays(1),
                _ => TimeSpan.FromMinutes(1)
            };
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (!_disposed)
            {
                if (disposing)
                {
                    _apiClient?.Dispose();
                }
                _disposed = true;
            }
        }
    }
}
