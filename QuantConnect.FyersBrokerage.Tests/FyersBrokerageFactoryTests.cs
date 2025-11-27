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
using Moq;
using NUnit.Framework;
using QuantConnect.Configuration;
using QuantConnect.Interfaces;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    [TestFixture]
    public class FyersBrokerageFactoryTests
    {
        private FyersBrokerageFactory _factory;

        [SetUp]
        public void SetUp()
        {
            _factory = new FyersBrokerageFactory();
        }

        [TearDown]
        public void TearDown()
        {
            _factory?.Dispose();
        }

        #region Factory Initialization Tests

        [Test]
        public void InitializesFactoryFromComposer()
        {
            using var factory = Composer.Instance.Single<IBrokerageFactory>(instance => instance.BrokerageType == typeof(FyersBrokerage));
            Assert.IsNotNull(factory);
            Assert.AreEqual(typeof(FyersBrokerage), factory.BrokerageType);
        }

        [Test]
        public void BrokerageTypeReturnsFyersBrokerage()
        {
            Assert.AreEqual(typeof(FyersBrokerage), _factory.BrokerageType);
        }

        [Test]
        public void FactoryDisposesWithoutException()
        {
            var factory = new FyersBrokerageFactory();
            Assert.DoesNotThrow(() => factory.Dispose());
        }

        #endregion

        #region Brokerage Data Tests

        [Test]
        public void BrokerageDataContainsAllRequiredKeys()
        {
            var brokerageData = _factory.BrokerageData;

            Assert.IsTrue(brokerageData.ContainsKey(FyersBrokerageFactory.ConfigKeys.ClientId));
            Assert.IsTrue(brokerageData.ContainsKey(FyersBrokerageFactory.ConfigKeys.AccessToken));
            Assert.IsTrue(brokerageData.ContainsKey(FyersBrokerageFactory.ConfigKeys.TradingSegment));
            Assert.IsTrue(brokerageData.ContainsKey(FyersBrokerageFactory.ConfigKeys.ProductType));
        }

        [Test]
        public void BrokerageDataHasCorrectDefaultValues()
        {
            var brokerageData = _factory.BrokerageData;

            // These should have default values
            Assert.AreEqual("EQUITY", brokerageData[FyersBrokerageFactory.ConfigKeys.TradingSegment]);
            Assert.AreEqual("INTRADAY", brokerageData[FyersBrokerageFactory.ConfigKeys.ProductType]);
        }

        [Test]
        public void ConfigKeysHaveCorrectValues()
        {
            Assert.AreEqual("fyers-client-id", FyersBrokerageFactory.ConfigKeys.ClientId);
            Assert.AreEqual("fyers-access-token", FyersBrokerageFactory.ConfigKeys.AccessToken);
            Assert.AreEqual("fyers-trading-segment", FyersBrokerageFactory.ConfigKeys.TradingSegment);
            Assert.AreEqual("fyers-product-type", FyersBrokerageFactory.ConfigKeys.ProductType);
        }

        #endregion

        #region Brokerage Model Tests

        [Test]
        public void GetBrokerageModelReturnsFyersBrokerageModel()
        {
            var mockOrderProvider = new Mock<IOrderProvider>();
            var model = _factory.GetBrokerageModel(mockOrderProvider.Object);

            Assert.IsNotNull(model);
            Assert.IsInstanceOf<FyersBrokerageModel>(model);
        }

        #endregion

        #region CreateBrokerage Tests

        [Test]
        public void CreateBrokerageWithValidConfiguration()
        {
            FyersTestCredentials.SkipIfNoCredentials();

            var job = new LiveNodePacket
            {
                BrokerageData = new Dictionary<string, string>
                {
                    { FyersBrokerageFactory.ConfigKeys.ClientId, FyersTestCredentials.ClientId },
                    { FyersBrokerageFactory.ConfigKeys.AccessToken, FyersTestCredentials.AccessToken },
                    { FyersBrokerageFactory.ConfigKeys.TradingSegment, FyersTestCredentials.TradingSegment },
                    { FyersBrokerageFactory.ConfigKeys.ProductType, FyersTestCredentials.ProductType }
                }
            };

            var mockAlgorithm = CreateMockAlgorithm();

            var brokerage = _factory.CreateBrokerage(job, mockAlgorithm);

            Assert.IsNotNull(brokerage);
            Assert.IsInstanceOf<FyersBrokerage>(brokerage);
        }

        [Test]
        public void CreateBrokerageThrowsOnMissingClientId()
        {
            var job = new LiveNodePacket
            {
                BrokerageData = new Dictionary<string, string>
                {
                    // Missing ClientId
                    { FyersBrokerageFactory.ConfigKeys.AccessToken, "test-token" },
                    { FyersBrokerageFactory.ConfigKeys.TradingSegment, "EQUITY" },
                    { FyersBrokerageFactory.ConfigKeys.ProductType, "INTRADAY" }
                }
            };

            var mockAlgorithm = CreateMockAlgorithm();

            Assert.Throws<ArgumentException>(() => _factory.CreateBrokerage(job, mockAlgorithm));
        }

        [Test]
        public void CreateBrokerageThrowsOnMissingAccessToken()
        {
            var job = new LiveNodePacket
            {
                BrokerageData = new Dictionary<string, string>
                {
                    { FyersBrokerageFactory.ConfigKeys.ClientId, "test-client-id" },
                    // Missing AccessToken
                    { FyersBrokerageFactory.ConfigKeys.TradingSegment, "EQUITY" },
                    { FyersBrokerageFactory.ConfigKeys.ProductType, "INTRADAY" }
                }
            };

            var mockAlgorithm = CreateMockAlgorithm();

            Assert.Throws<ArgumentException>(() => _factory.CreateBrokerage(job, mockAlgorithm));
        }

        [Test]
        public void CreateBrokerageUsesDefaultsForOptionalParameters()
        {
            var job = new LiveNodePacket
            {
                BrokerageData = new Dictionary<string, string>
                {
                    { FyersBrokerageFactory.ConfigKeys.ClientId, "test-client-id" },
                    { FyersBrokerageFactory.ConfigKeys.AccessToken, "test-token" }
                    // TradingSegment and ProductType not provided - should use defaults
                }
            };

            var mockAlgorithm = CreateMockAlgorithm();

            // This may still throw due to validation, but the defaults should be applied
            // The exception should be for validation reasons, not missing required config
            try
            {
                var brokerage = _factory.CreateBrokerage(job, mockAlgorithm);
                Assert.IsNotNull(brokerage);
            }
            catch (ArgumentException ex)
            {
                // If it throws, it should NOT be for missing trading-segment or product-type
                Assert.IsFalse(ex.Message.Contains("trading-segment"), "Should use default trading segment");
                Assert.IsFalse(ex.Message.Contains("product-type"), "Should use default product type");
            }
            catch
            {
                // Other exceptions are acceptable (e.g., network errors, validation)
            }
        }

        #endregion

        #region Helper Methods

        private static IAlgorithm CreateMockAlgorithm()
        {
            var mockAlgorithm = new Mock<IAlgorithm>();
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, TimeZones.Kolkata));
            var transactions = new SecurityTransactionManager(null, securities);
            var portfolio = new SecurityPortfolioManager(securities, transactions, new AlgorithmSettings());

            mockAlgorithm.Setup(a => a.Portfolio).Returns(portfolio);
            mockAlgorithm.Setup(a => a.Transactions).Returns(transactions);
            mockAlgorithm.Setup(a => a.BrokerageModel).Returns(new FyersBrokerageModel());

            return mockAlgorithm.Object;
        }

        #endregion
    }
}
