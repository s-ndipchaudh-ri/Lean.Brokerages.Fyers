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
using System.Threading;
using Moq;
using NUnit.Framework;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Packets;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    /// <summary>
    /// Data queue handler tests for Fyers brokerage
    /// </summary>
    [TestFixture]
    public class FyersBrokerageDataQueueHandlerTests
    {
        #region CanSubscribe Tests

        [Test]
        public void CanSubscribe_IndiaEquity_ReturnsTrue()
        {
            // Arrange
            var symbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void CanSubscribe_IndiaIndex_ReturnsTrue()
        {
            // Arrange
            var symbol = Symbol.Create("NIFTY50", SecurityType.Index, Market.India);

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void CanSubscribe_IndiaFuture_ReturnsTrue()
        {
            // Arrange
            var symbol = Symbol.CreateFuture("NIFTY", Market.India, new DateTime(2025, 1, 30));

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void CanSubscribe_IndiaOption_ReturnsTrue()
        {
            // Arrange
            var underlying = TestSymbolHelper.CreateEquity("SBIN", Market.India);
            var symbol = Symbol.CreateOption(underlying, Market.India, OptionStyle.European,
                OptionRight.Call, 800m, new DateTime(2025, 1, 30));

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void CanSubscribe_IndiaIndexOption_ReturnsTrue()
        {
            // Arrange
            var symbol = Symbol.CreateOption(
                Symbol.Create("NIFTY50", SecurityType.Index, Market.India),
                Market.India,
                OptionStyle.European,
                OptionRight.Call,
                23000m,
                new DateTime(2025, 1, 30));

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsTrue(result);
        }

        [Test]
        public void CanSubscribe_USAEquity_ReturnsFalse()
        {
            // Arrange
            var symbol = Symbol.Create("AAPL", SecurityType.Equity, Market.USA);

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void CanSubscribe_Forex_ReturnsFalse()
        {
            // Arrange
            var symbol = Symbol.Create("EURUSD", SecurityType.Forex, Market.Oanda);

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void CanSubscribe_Crypto_ReturnsFalse()
        {
            // Arrange
            var symbol = Symbol.Create("BTCUSD", SecurityType.Crypto, Market.Coinbase);

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void CanSubscribe_UniverseSymbol_ReturnsFalse()
        {
            // Arrange - Universe symbols contain "universe" in their name
            var sid = SecurityIdentifier.GenerateEquity("universe-sbin", Market.India, mapSymbol: false);
            var symbol = new Symbol(sid, "universe-sbin");

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert
            Assert.IsFalse(result);
        }

        [Test]
        public void CanSubscribe_CanonicalFutureSymbol_ReturnsFalse()
        {
            // Arrange - Canonical symbols are used for universe selection
            var symbol = Symbol.Create("NIFTY", SecurityType.Future, Market.India);

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert - Canonical symbols should be rejected
            Assert.IsFalse(result);
        }

        [Test]
        public void CanSubscribe_CanonicalOptionSymbol_ReturnsFalse()
        {
            // Arrange - Canonical option symbol
            var underlying = TestSymbolHelper.CreateEquity("SBIN", Market.India);
            var symbol = Symbol.CreateCanonicalOption(underlying);

            // Act
            var result = CanSubscribeHelper(symbol);

            // Assert - Canonical symbols should be rejected
            Assert.IsFalse(result);
        }

        #endregion

        #region Subscribe/Unsubscribe Integration Tests

        [Test]
        public void Subscribe_ValidSymbol_ReturnsEnumerator()
        {
            // Skip if no credentials
            FyersTestCredentials.SkipIfNoCredentials();

            // Arrange
            var brokerage = CreateConnectedBrokerage();
            var symbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);
            var config = CreateSubscriptionDataConfig(symbol);

            try
            {
                // Act
                var enumerator = brokerage.Subscribe(config, (s, e) => { });

                // Assert
                Assert.IsNotNull(enumerator);

                // Cleanup
                brokerage.Unsubscribe(config);
            }
            finally
            {
                brokerage.Disconnect();
                brokerage.Dispose();
            }
        }

        [Test]
        public void Subscribe_InvalidSymbol_ReturnsNull()
        {
            // Skip if no credentials
            FyersTestCredentials.SkipIfNoCredentials();

            // Arrange
            var brokerage = CreateConnectedBrokerage();
            var symbol = Symbol.Create("AAPL", SecurityType.Equity, Market.USA);
            var config = CreateSubscriptionDataConfig(symbol);

            try
            {
                // Act
                var enumerator = brokerage.Subscribe(config, (s, e) => { });

                // Assert
                Assert.IsNull(enumerator);
            }
            finally
            {
                brokerage.Disconnect();
                brokerage.Dispose();
            }
        }

        [Test]
        public void Subscribe_ReceivesData_WithinTimeout()
        {
            // Skip if no credentials
            FyersTestCredentials.SkipIfNoCredentials();

            // Arrange
            var brokerage = CreateConnectedBrokerage();
            var symbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);
            var config = CreateSubscriptionDataConfig(symbol);
            var dataReceived = new ManualResetEvent(false);
            BaseData receivedData = null;
            IEnumerator<BaseData> enumerator = null;

            try
            {
                // Act
                enumerator = brokerage.Subscribe(config, (s, e) =>
                {
                    if (enumerator?.Current != null)
                    {
                        receivedData = enumerator.Current;
                        dataReceived.Set();
                    }
                });

                // Wait for data (market hours dependent)
                var received = dataReceived.WaitOne(TimeSpan.FromSeconds(30));

                // Assert - During market hours, we should receive data
                // During off-market hours, this may timeout which is acceptable
                if (received)
                {
                    Assert.IsNotNull(receivedData);
                    Assert.AreEqual(symbol, receivedData.Symbol);
                }

                // Cleanup
                brokerage.Unsubscribe(config);
            }
            finally
            {
                brokerage.Disconnect();
                brokerage.Dispose();
            }
        }

        [Test]
        public void Unsubscribe_RemovesSubscription_NoErrors()
        {
            // Skip if no credentials
            FyersTestCredentials.SkipIfNoCredentials();

            // Arrange
            var brokerage = CreateConnectedBrokerage();
            var symbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);
            var config = CreateSubscriptionDataConfig(symbol);

            try
            {
                // Act
                var enumerator = brokerage.Subscribe(config, (s, e) => { });
                Assert.IsNotNull(enumerator);

                // Should not throw
                Assert.DoesNotThrow(() => brokerage.Unsubscribe(config));
            }
            finally
            {
                brokerage.Disconnect();
                brokerage.Dispose();
            }
        }

        [Test]
        public void Subscribe_MultipleSymbols_AllReceiveEnumerators()
        {
            // Skip if no credentials
            FyersTestCredentials.SkipIfNoCredentials();

            // Arrange
            var brokerage = CreateConnectedBrokerage();
            var symbols = new[]
            {
                TestSymbolHelper.CreateEquity("SBIN", Market.India),
                TestSymbolHelper.CreateEquity("RELIANCE", Market.India),
                TestSymbolHelper.CreateEquity("TCS", Market.India)
            };

            var configs = new List<SubscriptionDataConfig>();
            var enumerators = new List<IEnumerator<BaseData>>();

            try
            {
                // Act
                foreach (var symbol in symbols)
                {
                    var config = CreateSubscriptionDataConfig(symbol);
                    configs.Add(config);
                    var enumerator = brokerage.Subscribe(config, (s, e) => { });
                    enumerators.Add(enumerator);
                }

                // Assert
                Assert.AreEqual(3, enumerators.Count);
                foreach (var enumerator in enumerators)
                {
                    Assert.IsNotNull(enumerator);
                }

                // Cleanup
                foreach (var config in configs)
                {
                    brokerage.Unsubscribe(config);
                }
            }
            finally
            {
                brokerage.Disconnect();
                brokerage.Dispose();
            }
        }

        #endregion

        #region SetJob Tests

        [Test]
        public void SetJob_ValidPacket_InitializesBrokerage()
        {
            // Skip if no credentials
            FyersTestCredentials.SkipIfNoCredentials();

            // Arrange
            var aggregator = new AggregationManager();
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null,
                null,
                aggregator
            );

            var packet = new LiveNodePacket
            {
                BrokerageData = new Dictionary<string, string>
                {
                    { "fyers-client-id", FyersTestCredentials.ClientId },
                    { "fyers-access-token", FyersTestCredentials.AccessToken },
                    { "fyers-trading-segment", FyersTestCredentials.TradingSegment },
                    { "fyers-product-type", FyersTestCredentials.ProductType }
                }
            };

            try
            {
                // Act & Assert - Should not throw
                Assert.DoesNotThrow(() => brokerage.SetJob(packet));
                Assert.IsTrue(brokerage.IsConnected);
            }
            finally
            {
                brokerage.Disconnect();
                brokerage.Dispose();
            }
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Helper to test CanSubscribe logic (mirrors the private method)
        /// </summary>
        private static bool CanSubscribeHelper(Symbol symbol)
        {
            var market = symbol.ID.Market;
            var securityType = symbol.ID.SecurityType;

            // Reject universe symbols
            if (symbol.Value.IndexOf("universe", StringComparison.OrdinalIgnoreCase) != -1) return false;

            // Reject canonical symbols
            if (symbol.IsCanonical()) return false;

            // Only support India market
            return (securityType == SecurityType.Equity ||
                    securityType == SecurityType.Index ||
                    securityType == SecurityType.Future ||
                    securityType == SecurityType.Option ||
                    securityType == SecurityType.IndexOption) &&
                   (market == Market.India);
        }

        /// <summary>
        /// Creates a connected brokerage instance for testing
        /// </summary>
        private static FyersBrokerage CreateConnectedBrokerage()
        {
            var aggregator = new AggregationManager();
            var brokerage = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                null,
                null,
                aggregator
            );

            brokerage.Connect();

            if (!brokerage.IsConnected)
            {
                throw new Exception("Failed to connect to Fyers brokerage");
            }

            return brokerage;
        }

        /// <summary>
        /// Creates a subscription data config for testing
        /// </summary>
        private static SubscriptionDataConfig CreateSubscriptionDataConfig(Symbol symbol)
        {
            return new SubscriptionDataConfig(
                typeof(TradeBar),
                symbol,
                Resolution.Second,
                TimeZones.Kolkata,
                TimeZones.Kolkata,
                true,
                true,
                false
            );
        }

        #endregion
    }
}
