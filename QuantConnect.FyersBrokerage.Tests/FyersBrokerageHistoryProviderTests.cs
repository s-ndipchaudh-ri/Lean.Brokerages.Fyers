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
using System.Linq;
using NUnit.Framework;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Logging;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    [TestFixture]
    public class FyersBrokerageHistoryProviderTests
    {
        /// <summary>
        /// Indian equity symbol for testing - State Bank of India
        /// </summary>
        private static Symbol SBIN => TestSymbolHelper.CreateEquity("SBIN", Market.India);

        /// <summary>
        /// Indian equity symbol for testing - Reliance Industries
        /// </summary>
        private static Symbol RELIANCE => TestSymbolHelper.CreateEquity("RELIANCE", Market.India);

        /// <summary>
        /// US equity symbol - for invalid market tests
        /// </summary>
        private static Symbol SPY => TestSymbolHelper.CreateEquity("SPY", Market.USA);

        /// <summary>
        /// Forex symbol - for invalid security type tests
        /// </summary>
        private static Symbol EURUSD => TestSymbolHelper.CreateForex("EURUSD", Market.Oanda);

        [SetUp]
        public void SetUp()
        {
            FyersTestCredentials.SkipIfNoCredentials();
        }

        private static TestCaseData[] ValidTestParameters
        {
            get
            {
                TestSetup.ReloadConfiguration();

                return new[]
                {
                    // Valid parameters - Minute resolution
                    new TestCaseData(SBIN, Resolution.Minute, Time.OneHour, typeof(TradeBar), false)
                        .SetName("SBIN_Minute_1Hour"),

                    // Valid parameters - Hour resolution
                    new TestCaseData(SBIN, Resolution.Hour, Time.OneDay, typeof(TradeBar), false)
                        .SetName("SBIN_Hour_1Day"),

                    // Valid parameters - Daily resolution
                    new TestCaseData(SBIN, Resolution.Daily, TimeSpan.FromDays(15), typeof(TradeBar), false)
                        .SetName("SBIN_Daily_15Days"),

                    // Valid parameters - Different symbol
                    new TestCaseData(RELIANCE, Resolution.Minute, Time.OneHour, typeof(TradeBar), false)
                        .SetName("RELIANCE_Minute_1Hour"),

                    // Valid parameters - Longer period
                    new TestCaseData(SBIN, Resolution.Daily, TimeSpan.FromDays(30), typeof(TradeBar), false)
                        .SetName("SBIN_Daily_30Days"),
                };
            }
        }

        private static TestCaseData[] InvalidTestParameters
        {
            get
            {
                TestSetup.ReloadConfiguration();

                return new[]
                {
                    // Invalid period - negative
                    new TestCaseData(SBIN, Resolution.Daily, TimeSpan.FromDays(-15), typeof(TradeBar), true)
                        .SetName("SBIN_Invalid_NegativePeriod"),

                    // Invalid security type - Forex
                    new TestCaseData(EURUSD, Resolution.Daily, TimeSpan.FromDays(15), typeof(TradeBar), true)
                        .SetName("EURUSD_Invalid_SecurityType"),

                    // Invalid resolution - Tick (not supported by Fyers history API)
                    new TestCaseData(SBIN, Resolution.Tick, Time.OneMinute, typeof(TradeBar), true)
                        .SetName("SBIN_Invalid_TickResolution"),

                    // Invalid resolution - Second (not supported by Fyers history API)
                    new TestCaseData(SBIN, Resolution.Second, Time.OneMinute, typeof(TradeBar), true)
                        .SetName("SBIN_Invalid_SecondResolution"),

                    // Invalid data type - QuoteBar (Fyers primarily provides TradeBar)
                    new TestCaseData(SBIN, Resolution.Minute, Time.OneHour, typeof(QuoteBar), true)
                        .SetName("SBIN_Invalid_QuoteBarDataType"),

                    // Invalid market - US market instead of India
                    new TestCaseData(SPY, Resolution.Minute, Time.OneHour, typeof(TradeBar), true)
                        .SetName("SPY_Invalid_USMarket"),
                };
            }
        }

        [Test, TestCaseSource(nameof(ValidTestParameters))]
        public void GetsHistory_ValidParameters(Symbol symbol, Resolution resolution, TimeSpan period, Type dataType, bool unsupported)
        {
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null, null, null);

            var now = DateTime.UtcNow;

            var request = new HistoryRequest(
                now.Add(-period),
                now,
                dataType,
                symbol,
                resolution,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Kolkata),
                TimeZones.Kolkata,
                Resolution.Minute,
                false,
                false,
                DataNormalizationMode.Adjusted,
                TickType.Trade);

            var history = brokerage.GetHistory(request);

            Assert.IsNotNull(history, "History should not be null for valid parameters");

            var historyList = history.ToList();
            Assert.IsTrue(historyList.Count > 0, "History should contain data points");

            foreach (var bar in historyList)
            {
                Log.Trace($"{bar.Time}: {bar.Symbol} - O:{((TradeBar)bar).Open} H:{((TradeBar)bar).High} L:{((TradeBar)bar).Low} C:{((TradeBar)bar).Close} V:{((TradeBar)bar).Volume}");
            }

            // Verify data is ordered by time
            var times = historyList.Select(x => x.Time).ToList();
            var orderedTimes = times.OrderBy(x => x).ToList();
            Assert.That(times, Is.EqualTo(orderedTimes), "History data should be ordered by time");

            // Verify no duplicate timestamps
            var distinctTimes = times.Distinct().Count();
            Assert.AreEqual(times.Count, distinctTimes, "History should not contain duplicate timestamps");

            Log.Trace($"Total data points retrieved: {historyList.Count}");
            Log.Trace($"Base currency: {brokerage.AccountBaseCurrency}");
        }

        [Test, TestCaseSource(nameof(InvalidTestParameters))]
        public void GetsHistory_InvalidParameters(Symbol symbol, Resolution resolution, TimeSpan period, Type dataType, bool unsupported)
        {
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null, null, null);

            var now = DateTime.UtcNow;

            var request = new HistoryRequest(
                now.Add(-period),
                now,
                dataType,
                symbol,
                resolution,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Kolkata),
                TimeZones.Kolkata,
                Resolution.Minute,
                false,
                false,
                DataNormalizationMode.Adjusted,
                TickType.Trade);

            var history = brokerage.GetHistory(request);

            Assert.IsNull(history, "History should be null for unsupported parameters");
        }

        [Test]
        public void ReturnsCorrectBaseCurrency()
        {
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null, null, null);

            Assert.AreEqual(Currencies.INR, brokerage.AccountBaseCurrency, "Fyers brokerage should use INR as base currency");
        }

        [Test]
        public void GetHistory_MultipleSymbolsSequentially()
        {
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null, null, null);

            var symbols = new[] { SBIN, RELIANCE };
            var now = DateTime.UtcNow;

            foreach (var symbol in symbols)
            {
                var request = new HistoryRequest(
                    now.Add(-Time.OneHour),
                    now,
                    typeof(TradeBar),
                    symbol,
                    Resolution.Minute,
                    SecurityExchangeHours.AlwaysOpen(TimeZones.Kolkata),
                    TimeZones.Kolkata,
                    Resolution.Minute,
                    false,
                    false,
                    DataNormalizationMode.Adjusted,
                    TickType.Trade);

                var history = brokerage.GetHistory(request);

                Assert.IsNotNull(history, $"History should not be null for {symbol}");

                var historyList = history.ToList();
                Assert.IsTrue(historyList.Count > 0, $"History should contain data points for {symbol}");

                Log.Trace($"{symbol}: Retrieved {historyList.Count} data points");
            }
        }

        [Test]
        public void GetHistory_LargeDateRange()
        {
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null, null, null);

            var now = DateTime.UtcNow;

            // Request 90 days of daily data
            var request = new HistoryRequest(
                now.AddDays(-90),
                now,
                typeof(TradeBar),
                SBIN,
                Resolution.Daily,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Kolkata),
                TimeZones.Kolkata,
                Resolution.Daily,
                false,
                false,
                DataNormalizationMode.Adjusted,
                TickType.Trade);

            var history = brokerage.GetHistory(request);

            Assert.IsNotNull(history, "History should not be null for large date range");

            var historyList = history.ToList();
            Log.Trace($"Retrieved {historyList.Count} daily bars for 90-day period");

            // Should have approximately 60-65 trading days in 90 calendar days
            Assert.IsTrue(historyList.Count > 40, "Should have at least 40 trading days in 90 calendar days");
        }

        [Test]
        public void GetHistory_FutureSymbol()
        {
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null, null, null);

            // Create a future symbol for NIFTY
            var expiry = GetNextMonthlyExpiry();
            var futureSymbol = Symbol.CreateFuture("NIFTY", Market.India, expiry);

            var now = DateTime.UtcNow;

            var request = new HistoryRequest(
                now.Add(-Time.OneDay),
                now,
                typeof(TradeBar),
                futureSymbol,
                Resolution.Minute,
                SecurityExchangeHours.AlwaysOpen(TimeZones.Kolkata),
                TimeZones.Kolkata,
                Resolution.Minute,
                false,
                false,
                DataNormalizationMode.Adjusted,
                TickType.Trade);

            var history = brokerage.GetHistory(request);

            // Future history may or may not be available depending on contract status
            if (history != null)
            {
                var historyList = history.ToList();
                Log.Trace($"Future {futureSymbol}: Retrieved {historyList.Count} data points");
            }
            else
            {
                Log.Trace($"Future {futureSymbol}: No history available (contract may not be active)");
            }
        }

        /// <summary>
        /// Gets the next monthly expiry date (last Thursday of the month)
        /// </summary>
        private static DateTime GetNextMonthlyExpiry()
        {
            var now = DateTime.UtcNow;
            var year = now.Year;
            var month = now.Month;

            // Get last day of current month
            var lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));

            // Find last Thursday
            while (lastDay.DayOfWeek != DayOfWeek.Thursday)
            {
                lastDay = lastDay.AddDays(-1);
            }

            // If we've passed it, go to next month
            if (now > lastDay)
            {
                month++;
                if (month > 12)
                {
                    month = 1;
                    year++;
                }
                lastDay = new DateTime(year, month, DateTime.DaysInMonth(year, month));
                while (lastDay.DayOfWeek != DayOfWeek.Thursday)
                {
                    lastDay = lastDay.AddDays(-1);
                }
            }

            return lastDay;
        }
    }
}
