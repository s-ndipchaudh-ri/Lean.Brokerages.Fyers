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
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Fyers brokerage - Historical data provider implementation
    /// </summary>
    public partial class FyersBrokerage
    {
        // India Standard Time zone
        private static readonly DateTimeZone IndiaTimeZone = DateTimeZoneProviders.Tzdb["Asia/Kolkata"];

        /// <summary>
        /// Gets the history for the requested symbols
        /// </summary>
        /// <param name="request">The historical data request</param>
        /// <returns>An enumerable of bars covering the span specified in the request</returns>
        public override IEnumerable<BaseData>? GetHistory(HistoryRequest request)
        {
            if (_apiClient == null)
            {
                OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, "Not connected to Fyers"));
                return null;
            }

            // Validate request
            if (!ValidateHistoryRequest(request))
            {
                return null;
            }

            // Convert resolution to Fyers format
            var fyersResolution = ConvertResolutionToFyers(request.Resolution);
            if (string.IsNullOrEmpty(fyersResolution))
            {
                if (!_historyResolutionErrorFlag)
                {
                    _historyResolutionErrorFlag = true;
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1,
                        $"Resolution {request.Resolution} is not supported by Fyers. Supported: Second, Minute, Hour, Daily"));
                }
                return null;
            }

            return GetHistoryInternal(request, fyersResolution);
        }

        /// <summary>
        /// Internal method to fetch and convert historical data
        /// </summary>
        private IEnumerable<BaseData> GetHistoryInternal(HistoryRequest request, string fyersResolution)
        {
            var fyersSymbol = _symbolMapper.GetBrokerageSymbol(request.Symbol);

            Log.Trace($"FyersBrokerage.GetHistory(): Fetching {request.Resolution} data for {fyersSymbol} " +
                      $"from {request.StartTimeUtc:yyyy-MM-dd} to {request.EndTimeUtc:yyyy-MM-dd}");

            // Fyers has data limits - split into chunks if needed
            var maxDaysPerRequest = GetMaxDaysForResolution(request.Resolution);
            var startDate = request.StartTimeUtc.Date;
            var endDate = request.EndTimeUtc.Date;

            while (startDate < endDate)
            {
                var chunkEndDate = startDate.AddDays(maxDaysPerRequest);
                if (chunkEndDate > endDate)
                    chunkEndDate = endDate;

                var historyRequest = new Messages.FyersHistoryRequest
                {
                    Symbol = fyersSymbol,
                    Resolution = fyersResolution,
                    DateFormat = "1", // Epoch format
                    RangeFrom = startDate.ToString("yyyy-MM-dd"),
                    RangeTo = chunkEndDate.ToString("yyyy-MM-dd"),
                    ContinuousFlag = "0"
                };

                Messages.FyersHistoryResponse? response = null;

                try
                {
                    response = _apiClient!.GetHistory(historyRequest);
                }
                catch (Exception ex)
                {
                    Log.Error($"FyersBrokerage.GetHistory(): API error - {ex.Message}");
                    break;
                }

                if (response == null || !response.IsSuccess || response.Candles == null)
                {
                    Log.Trace($"FyersBrokerage.GetHistory(): No data returned for {fyersSymbol} " +
                              $"from {startDate:yyyy-MM-dd} to {chunkEndDate:yyyy-MM-dd}");
                    startDate = chunkEndDate;
                    continue;
                }

                // Convert candles to LEAN TradeBar
                foreach (var candle in response.Candles)
                {
                    if (candle.Count < 6)
                        continue;

                    var bar = ConvertCandleToTradeBar(candle, request.Symbol, request.Resolution);

                    // Filter by requested time range
                    if (bar.Time >= request.StartTimeUtc && bar.Time < request.EndTimeUtc)
                    {
                        yield return bar;
                    }
                }

                startDate = chunkEndDate;
            }
        }

        /// <summary>
        /// Validates the history request
        /// </summary>
        private bool ValidateHistoryRequest(HistoryRequest request)
        {
            // Check data type
            if (request.DataType != typeof(TradeBar) && request.DataType != typeof(QuoteBar))
            {
                if (!_historyDataTypeErrorFlag)
                {
                    _historyDataTypeErrorFlag = true;
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1,
                        $"Data type {request.DataType.Name} is not supported. Only TradeBar and QuoteBar are supported."));
                }
                return false;
            }

            // Check security type
            var supportedTypes = new[] { SecurityType.Equity, SecurityType.Index, SecurityType.Future, SecurityType.Option };
            if (Array.IndexOf(supportedTypes, request.Symbol.SecurityType) < 0)
            {
                if (!_historySecurityTypeErrorFlag)
                {
                    _historySecurityTypeErrorFlag = true;
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1,
                        $"Security type {request.Symbol.SecurityType} is not supported."));
                }
                return false;
            }

            // Check resolution
            var supportedResolutions = new[] { Resolution.Second, Resolution.Minute, Resolution.Hour, Resolution.Daily };
            if (Array.IndexOf(supportedResolutions, request.Resolution) < 0)
            {
                if (!_historyResolutionErrorFlag)
                {
                    _historyResolutionErrorFlag = true;
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1,
                        $"Resolution {request.Resolution} is not supported."));
                }
                return false;
            }

            return true;
        }

        /// <summary>
        /// Converts LEAN resolution to Fyers resolution string
        /// </summary>
        private static string? ConvertResolutionToFyers(Resolution resolution)
        {
            return resolution switch
            {
                Resolution.Second => FyersResolution.Minute1, // Fyers doesn't support second, use minute
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
                Resolution.Second => 30,   // Use small chunks for high-frequency
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
        private TradeBar ConvertCandleToTradeBar(List<decimal> candle, Symbol symbol, Resolution resolution)
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
                Resolution.Second => TimeSpan.FromSeconds(1),
                Resolution.Minute => TimeSpan.FromMinutes(1),
                Resolution.Hour => TimeSpan.FromHours(1),
                Resolution.Daily => TimeSpan.FromDays(1),
                _ => TimeSpan.FromMinutes(1)
            };
        }
    }
}
