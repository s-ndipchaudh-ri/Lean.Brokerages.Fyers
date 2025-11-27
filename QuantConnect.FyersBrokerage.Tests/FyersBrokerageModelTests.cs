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
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Orders;
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    [TestFixture]
    public class FyersBrokerageModelTests
    {
        private FyersBrokerageModel _model;
        private Security _indianEquity;
        private Security _usEquity;
        private Security _forex;

        [SetUp]
        public void SetUp()
        {
            _model = new FyersBrokerageModel();

            // Create test securities using helper that doesn't require map file provider
            _indianEquity = CreateSecurity(TestSymbolHelper.CreateEquity("SBIN", Market.India));
            _usEquity = CreateSecurity(TestSymbolHelper.CreateEquity("SPY", Market.USA));
            _forex = CreateSecurity(TestSymbolHelper.CreateForex("EURUSD", Market.Oanda));
        }

        #region Default Markets Tests

        [Test]
        public void DefaultMarketsContainsIndiaForEquity()
        {
            Assert.AreEqual(Market.India, _model.DefaultMarkets[SecurityType.Equity]);
        }

        [Test]
        public void DefaultMarketsContainsIndiaForIndex()
        {
            Assert.AreEqual(Market.India, _model.DefaultMarkets[SecurityType.Index]);
        }

        [Test]
        public void DefaultMarketsContainsIndiaForFuture()
        {
            Assert.AreEqual(Market.India, _model.DefaultMarkets[SecurityType.Future]);
        }

        [Test]
        public void DefaultMarketsContainsIndiaForOption()
        {
            Assert.AreEqual(Market.India, _model.DefaultMarkets[SecurityType.Option]);
        }

        [Test]
        public void DefaultMarketsReturnsIndiaForEquity()
        {
            Assert.IsTrue(_model.DefaultMarkets.TryGetValue(SecurityType.Equity, out var market));
            Assert.AreEqual(Market.India, market);
        }

        [Test]
        public void DefaultMarketsReturnsIndiaForFuture()
        {
            Assert.IsTrue(_model.DefaultMarkets.TryGetValue(SecurityType.Future, out var market));
            Assert.AreEqual(Market.India, market);
        }

        #endregion

        #region Supported Order Types Tests

        [Test]
        public void SupportedOrderTypesContainsMarket()
        {
            Assert.IsTrue(_model.SupportedOrderTypes.Contains(OrderType.Market));
        }

        [Test]
        public void SupportedOrderTypesContainsLimit()
        {
            Assert.IsTrue(_model.SupportedOrderTypes.Contains(OrderType.Limit));
        }

        [Test]
        public void SupportedOrderTypesContainsStopMarket()
        {
            Assert.IsTrue(_model.SupportedOrderTypes.Contains(OrderType.StopMarket));
        }

        [Test]
        public void SupportedOrderTypesContainsStopLimit()
        {
            Assert.IsTrue(_model.SupportedOrderTypes.Contains(OrderType.StopLimit));
        }

        [Test]
        public void SupportedOrderTypesCount()
        {
            Assert.AreEqual(4, _model.SupportedOrderTypes.Count);
        }

        #endregion

        #region CanSubmitOrder Tests - Valid Orders

        [Test]
        public void CanSubmitOrder_MarketOrder_IndianEquity_ReturnsTrue()
        {
            var order = new MarketOrder(_indianEquity.Symbol, 1, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_indianEquity, order, out var message);

            Assert.IsTrue(result);
            Assert.IsNull(message);
        }

        [Test]
        public void CanSubmitOrder_LimitOrder_IndianEquity_ReturnsTrue()
        {
            var order = new LimitOrder(_indianEquity.Symbol, 1, 850m, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_indianEquity, order, out var message);

            Assert.IsTrue(result);
            Assert.IsNull(message);
        }

        [Test]
        public void CanSubmitOrder_StopMarketOrder_IndianEquity_ReturnsTrue()
        {
            var order = new StopMarketOrder(_indianEquity.Symbol, 1, 900m, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_indianEquity, order, out var message);

            Assert.IsTrue(result);
            Assert.IsNull(message);
        }

        [Test]
        public void CanSubmitOrder_StopLimitOrder_IndianEquity_ReturnsTrue()
        {
            var order = new StopLimitOrder(_indianEquity.Symbol, 1, 900m, 905m, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_indianEquity, order, out var message);

            Assert.IsTrue(result);
            Assert.IsNull(message);
        }

        #endregion

        #region CanSubmitOrder Tests - Invalid Security Type

        [Test]
        public void CanSubmitOrder_ForexSecurity_ReturnsFalse()
        {
            var order = new MarketOrder(_forex.Symbol, 1000, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_forex, order, out var message);

            Assert.IsFalse(result);
            Assert.IsNotNull(message);
            Assert.IsTrue(message.Message.Contains("security type"));
        }

        #endregion

        #region CanSubmitOrder Tests - Invalid Market

        [Test]
        public void CanSubmitOrder_USMarket_ReturnsFalse()
        {
            var order = new MarketOrder(_usEquity.Symbol, 1, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_usEquity, order, out var message);

            Assert.IsFalse(result);
            Assert.IsNotNull(message);
            Assert.IsTrue(message.Message.Contains("India market"));
        }

        #endregion

        #region CanSubmitOrder Tests - Fractional Quantities

        [Test]
        public void CanSubmitOrder_FractionalQuantity_ReturnsFalse()
        {
            var order = new MarketOrder(_indianEquity.Symbol, 1.5m, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_indianEquity, order, out var message);

            Assert.IsFalse(result);
            Assert.IsNotNull(message);
            Assert.IsTrue(message.Message.Contains("fractional"));
        }

        [Test]
        public void CanSubmitOrder_WholeQuantity_ReturnsTrue()
        {
            var order = new MarketOrder(_indianEquity.Symbol, 10, DateTime.UtcNow);

            var result = _model.CanSubmitOrder(_indianEquity, order, out var message);

            Assert.IsTrue(result);
            Assert.IsNull(message);
        }

        #endregion

        #region CanUpdateOrder Tests

        [Test]
        public void CanUpdateOrder_LimitOrder_ReturnsTrue()
        {
            var order = new LimitOrder(_indianEquity.Symbol, 1, 850m, DateTime.UtcNow);
            var request = new UpdateOrderRequest(DateTime.UtcNow, 1, new UpdateOrderFields { LimitPrice = 860m });

            var result = _model.CanUpdateOrder(_indianEquity, order, request, out var message);

            Assert.IsTrue(result);
            Assert.IsNull(message);
        }

        [Test]
        public void CanUpdateOrder_MarketOrder_ReturnsFalse()
        {
            var order = new MarketOrder(_indianEquity.Symbol, 1, DateTime.UtcNow);
            var request = new UpdateOrderRequest(DateTime.UtcNow, 1, new UpdateOrderFields());

            var result = _model.CanUpdateOrder(_indianEquity, order, request, out var message);

            Assert.IsFalse(result);
            Assert.IsNotNull(message);
            Assert.IsTrue(message.Message.Contains("market orders"));
        }

        [Test]
        public void CanUpdateOrder_FilledOrder_ReturnsFalse()
        {
            var order = new LimitOrder(_indianEquity.Symbol, 1, 850m, DateTime.UtcNow);
            order.Status = OrderStatus.Filled;
            var request = new UpdateOrderRequest(DateTime.UtcNow, 1, new UpdateOrderFields { LimitPrice = 860m });

            var result = _model.CanUpdateOrder(_indianEquity, order, request, out var message);

            Assert.IsFalse(result);
            Assert.IsNotNull(message);
            Assert.IsTrue(message.Message.Contains("filled"));
        }

        #endregion

        #region Fee Model Tests

        [Test]
        public void GetFeeModel_ReturnsFyersFeeModel()
        {
            var feeModel = _model.GetFeeModel(_indianEquity);

            Assert.IsNotNull(feeModel);
            Assert.IsInstanceOf<FyersFeeModel>(feeModel);
        }

        [Test]
        public void FeeModel_EquityDelivery_ZeroFee()
        {
            var feeModel = new FyersFeeModel();
            var properties = new IndiaOrderProperties(Exchange.NSE, IndiaOrderProperties.IndiaProductType.CNC);
            var order = new MarketOrder(_indianEquity.Symbol, 1, DateTime.UtcNow, "", properties);

            _indianEquity.SetMarketPrice(new Tick(DateTime.UtcNow, _indianEquity.Symbol, 850m, 850m));

            var fee = feeModel.GetOrderFee(new Orders.Fees.OrderFeeParameters(_indianEquity, order));

            Assert.AreEqual(0m, fee.Value.Amount);
            Assert.AreEqual(Currencies.INR, fee.Value.Currency);
        }

        [Test]
        public void FeeModel_EquityIntraday_MinFee()
        {
            var feeModel = new FyersFeeModel();
            var properties = new IndiaOrderProperties(Exchange.NSE, IndiaOrderProperties.IndiaProductType.MIS);
            var order = new MarketOrder(_indianEquity.Symbol, 1, DateTime.UtcNow, "", properties);

            _indianEquity.SetMarketPrice(new Tick(DateTime.UtcNow, _indianEquity.Symbol, 850m, 850m));

            var fee = feeModel.GetOrderFee(new Orders.Fees.OrderFeeParameters(_indianEquity, order));

            // For small orders, fee should be 0.03% of order value or Rs 20 whichever is lower
            // Order value = 850 * 1 = 850
            // 0.03% of 850 = 0.255
            // Min(20, 0.255) = 0.255
            Assert.IsTrue(fee.Value.Amount <= 20m);
            Assert.AreEqual(Currencies.INR, fee.Value.Currency);
        }

        [Test]
        public void FeeModel_FuturesAndOptions_FlatFee()
        {
            var feeModel = new FyersFeeModel();

            var futureSymbol = Symbol.CreateFuture("NIFTY", Market.India, DateTime.UtcNow.AddMonths(1));
            var futureSecurity = CreateSecurity(futureSymbol);
            var order = new MarketOrder(futureSymbol, 75, DateTime.UtcNow);

            futureSecurity.SetMarketPrice(new Tick(DateTime.UtcNow, futureSymbol, 23500m, 23500m));

            var fee = feeModel.GetOrderFee(new Orders.Fees.OrderFeeParameters(futureSecurity, order));

            // F&O is flat Rs 20 per order
            Assert.AreEqual(20m, fee.Value.Amount);
            Assert.AreEqual(Currencies.INR, fee.Value.Currency);
        }

        [Test]
        public void FeeModel_DefaultFlatFee()
        {
            var feeModel = new FyersFeeModel();
            var order = new MarketOrder(_indianEquity.Symbol, 1, DateTime.UtcNow);
            // No properties set - should use default flat fee

            _indianEquity.SetMarketPrice(new Tick(DateTime.UtcNow, _indianEquity.Symbol, 850m, 850m));

            var fee = feeModel.GetOrderFee(new Orders.Fees.OrderFeeParameters(_indianEquity, order));

            Assert.AreEqual(20m, fee.Value.Amount);
            Assert.AreEqual(Currencies.INR, fee.Value.Currency);
        }

        #endregion

        #region Account Type Tests

        [Test]
        public void AccountTypeIsMargin()
        {
            Assert.AreEqual(AccountType.Margin, _model.AccountType);
        }

        #endregion

        #region Helper Methods

        private static Security CreateSecurity(Symbol symbol)
        {
            return new Security(
                SecurityExchangeHours.AlwaysOpen(TimeZones.Kolkata),
                new SubscriptionDataConfig(
                    typeof(TradeBar),
                    symbol,
                    Resolution.Minute,
                    TimeZones.Kolkata,
                    TimeZones.Kolkata,
                    false,
                    false,
                    false
                ),
                new Cash(Currencies.INR, 0, 1m),
                SymbolProperties.GetDefault(Currencies.INR),
                ErrorCurrencyConverter.Instance,
                RegisteredSecurityDataTypesProvider.Null,
                new SecurityCache()
            );
        }

        #endregion
    }
}
