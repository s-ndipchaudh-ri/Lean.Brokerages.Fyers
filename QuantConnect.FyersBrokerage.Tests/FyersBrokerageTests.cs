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

using Moq;
using NUnit.Framework;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Lean.Engine.DataFeeds;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Securities;
using QuantConnect.Tests;
using QuantConnect.Tests.Brokerages;
using QuantConnect.Tests.Common.Securities;
using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Threading;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    [TestFixture]
    public partial class FyersBrokerageTests
    {
        private static IOrderProperties orderProperties = new IndiaOrderProperties(exchange: Exchange.NSE);

        private IBrokerage _brokerage;
        private OrderProvider _orderProvider;
        private SecurityProvider _securityProvider;

        /// <summary>
        /// Test symbol for Fyers - SBIN (State Bank of India)
        /// </summary>
        protected Symbol Symbol => TestSymbolHelper.CreateEquity("SBIN", Market.India);

        /// <summary>
        /// Gets the security type associated with the Symbol
        /// </summary>
        protected SecurityType SecurityType => SecurityType.Equity;

        [SetUp]
        public void Setup()
        {
            // Skip tests if credentials are not configured
            FyersTestCredentials.SkipIfNoCredentials();

            Log.Trace("");
            Log.Trace("");
            Log.Trace("--- SETUP ---");
            Log.Trace("");
            Log.Trace("");
            // we want to regenerate these for each test
            _brokerage = null;
            _orderProvider = null;
            _securityProvider = null;
            Thread.Sleep(1000);
            CancelOpenOrders();
            LiquidateFyersHoldings();
            Thread.Sleep(1000);
        }

        [TearDown]
        public void Teardown()
        {
            try
            {
                Log.Trace("");
                Log.Trace("");
                Log.Trace("--- TEARDOWN ---");
                Log.Trace("");
                Log.Trace("");
                Thread.Sleep(1000);
                CancelOpenOrders();
                LiquidateFyersHoldings();
                Thread.Sleep(1000);
            }
            finally
            {
                if (_brokerage != null)
                {
                    DisposeBrokerage(_brokerage);
                }
            }
        }

        /// <summary>
        /// Disposes of the brokerage and any external resources started in order to create it
        /// </summary>
        /// <param name="brokerage">The brokerage instance to be disposed of</param>
        protected virtual void DisposeBrokerage(IBrokerage brokerage)
        {
            brokerage.Disconnect();
        }

        public IBrokerage Brokerage
        {
            get
            {
                if (_brokerage == null)
                {
                    _brokerage = InitializeBrokerage();
                }
                return _brokerage;
            }
        }

        private IBrokerage InitializeBrokerage()
        {
            Log.Trace("");
            Log.Trace("- INITIALIZING BROKERAGE -");
            Log.Trace("");

            var brokerage = CreateBrokerage(OrderProvider, SecurityProvider);
            brokerage.Connect();

            if (!brokerage.IsConnected)
            {
                Assert.Fail("Failed to connect to brokerage");
            }

            Log.Trace("");
            Log.Trace("GET OPEN ORDERS");
            Log.Trace("");
            foreach (var openOrder in brokerage.GetOpenOrders())
            {
                OrderProvider.Add(openOrder);
            }

            Log.Trace("");
            Log.Trace("GET ACCOUNT HOLDINGS");
            Log.Trace("");
            foreach (var accountHolding in brokerage.GetAccountHoldings())
            {
                // these securities don't need to be real, just used for the ISecurityProvider impl, required
                // by brokerages to track holdings
                SecurityProvider[accountHolding.Symbol] = CreateSecurity(accountHolding.Symbol);
            }

            brokerage.OrdersStatusChanged += (sender, argss) =>
            {
                var args = argss.Single();
                Log.Trace("");
                Log.Trace("ORDER STATUS CHANGED: " + args);
                Log.Trace("");

                // we need to keep this maintained properly
                if (args.Status == OrderStatus.Filled || args.Status == OrderStatus.PartiallyFilled)
                {
                    Log.Trace("FILL EVENT: " + args.FillQuantity + " units of " + args.Symbol.ToString());

                    Security security;
                    if (_securityProvider.TryGetValue(args.Symbol, out security))
                    {
                        var holding = _securityProvider[args.Symbol].Holdings;
                        holding.SetHoldings(args.FillPrice, holding.Quantity + args.FillQuantity);
                    }
                    else
                    {
                        _securityProvider[args.Symbol] = CreateSecurity(args.Symbol);
                        _securityProvider[args.Symbol].Holdings.SetHoldings(args.FillPrice, args.FillQuantity);
                    }

                    Log.Trace("--HOLDINGS: " + _securityProvider[args.Symbol]);

                    // update order mapping
                    var order = _orderProvider.GetOrderById(args.OrderId);
                    order.Status = args.Status;
                }
            };
            return brokerage;
        }

        internal static Security CreateSecurity(Symbol symbol)
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

        public OrderProvider OrderProvider
        {
            get { return _orderProvider ?? (_orderProvider = new OrderProvider()); }
        }

        public SecurityProvider SecurityProvider
        {
            get { return _securityProvider ?? (_securityProvider = new SecurityProvider()); }
        }

        /// <summary>
        /// This is used to ensure each test starts with a clean, known state.
        /// </summary>
        protected void LiquidateFyersHoldings()
        {
            Log.Trace("");
            Log.Trace("LIQUIDATE HOLDINGS");
            Log.Trace("");

            var holdings = Brokerage.GetAccountHoldings();

            foreach (var holding in holdings)
            {
                if (holding.Quantity == 0) continue;
                Log.Trace("Liquidating: " + holding);
                var order = new MarketOrder(holding.Symbol, -holding.Quantity, DateTime.UtcNow, properties: orderProperties);
                _orderProvider.Add(order);
                PlaceFyersOrderWaitForStatus(order, OrderStatus.Filled);
            }
        }

        /// <summary>
        /// Provides the data required to test each order type in various cases
        /// </summary>
        private static TestCaseData[] OrderParameters()
        {
            var symbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);
            return new[]
            {
                new TestCaseData(new MarketOrderTestParameters(symbol, orderProperties)).SetName("MarketOrder"),
                new TestCaseData(new LimitOrderTestParameters(symbol, 850m, 800m, orderProperties)).SetName("LimitOrder"),
                new TestCaseData(new StopMarketOrderTestParameters(symbol, 900m, 850m, orderProperties)).SetName("StopMarketOrder"),
            };
        }

        /// <summary>
        /// Creates the brokerage under test
        /// </summary>
        /// <returns>A connected brokerage instance</returns>
        protected IBrokerage CreateBrokerage(IOrderProvider orderProvider, ISecurityProvider securityProvider)
        {
            var securities = new SecurityManager(new TimeKeeper(DateTime.UtcNow, TimeZones.Kolkata))
            {
                { Symbol, CreateSecurity(Symbol) }
            };

            var transactions = new SecurityTransactionManager(null, securities);
            transactions.SetOrderProcessor(new FakeOrderProcessor());

            var algorithm = new Mock<IAlgorithm>();
            algorithm.Setup(a => a.Transactions).Returns(transactions);
            algorithm.Setup(a => a.BrokerageModel).Returns(new FyersBrokerageModel());
            algorithm.Setup(a => a.Portfolio).Returns(new SecurityPortfolioManager(securities, transactions, new AlgorithmSettings()));

            var fyers = new FyersBrokerage(
                FyersTestCredentials.TradingSegment,
                FyersTestCredentials.ProductType,
                FyersTestCredentials.ClientId,
                FyersTestCredentials.AccessToken,
                algorithm.Object,
                algorithm.Object.Portfolio,
                new AggregationManager());

            return fyers;
        }

        /// <summary>
        /// Returns whether or not the brokers order methods implementation are async
        /// </summary>
        protected bool IsAsync()
        {
            return false;
        }

        /// <summary>
        /// Returns whether or not the brokers order cancel method implementation is async
        /// </summary>
        protected bool IsCancelAsync()
        {
            return false;
        }

        /// <summary>
        /// Gets the default order quantity
        /// </summary>
        protected virtual decimal GetDefaultQuantity()
        {
            return 1;
        }

        /// <summary>
        /// Gets the current market price of the specified security
        /// </summary>
        protected decimal GetAskPrice(Symbol symbol)
        {
            var fyers = (FyersBrokerage)Brokerage;
            // For now return a default price - would need API client to get live quotes
            return 850m;
        }

        #region Test Methods

        [Test]
        public void IsConnected()
        {
            Assert.IsTrue(Brokerage.IsConnected);
        }

        [Test]
        public void GetCashBalanceContainsSomething()
        {
            Log.Trace("");
            Log.Trace("GET CASH BALANCE");
            Log.Trace("");
            var balance = Brokerage.GetCashBalance();
            Assert.IsTrue(balance.Any());

            foreach (var cash in balance)
            {
                Log.Trace($"Currency: {cash.Currency}, Amount: {cash.Amount}");
            }
        }

        [Test]
        public void GetAccountHoldings()
        {
            Log.Trace("");
            Log.Trace("GET ACCOUNT HOLDINGS");
            Log.Trace("");
            var before = Brokerage.GetAccountHoldings();

            PlaceFyersOrderWaitForStatus(new MarketOrder(Symbol, GetDefaultQuantity(), DateTime.UtcNow, properties: orderProperties));

            Thread.Sleep(3000);

            var after = Brokerage.GetAccountHoldings();

            var beforeHoldings = before.FirstOrDefault(x => x.Symbol == Symbol);
            var afterHoldings = after.FirstOrDefault(x => x.Symbol == Symbol);

            var beforeQuantity = beforeHoldings == null ? 0 : beforeHoldings.Quantity;
            var afterQuantity = afterHoldings == null ? 0 : afterHoldings.Quantity;

            Assert.AreEqual(GetDefaultQuantity(), afterQuantity - beforeQuantity);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public void CancelOrders(OrderTestParameters parameters)
        {
            const int secondsTimeout = 20;
            Log.Trace("");
            Log.Trace("CANCEL ORDERS");
            Log.Trace("");

            var order = PlaceFyersOrderWaitForStatus(parameters.CreateLongOrder(GetDefaultQuantity()), parameters.ExpectedStatus);

            var canceledOrderStatusEvent = new ManualResetEvent(false);
            EventHandler<List<OrderEvent>> orderStatusCallback = (sender, fill) =>
            {
                if (fill.Single().Status == OrderStatus.Canceled)
                {
                    canceledOrderStatusEvent.Set();
                }
            };
            Brokerage.OrdersStatusChanged += orderStatusCallback;
            var cancelResult = false;
            try
            {
                cancelResult = Brokerage.CancelOrder(order);
            }
            catch (Exception exception)
            {
                Log.Error(exception);
            }

            Assert.AreEqual(IsCancelAsync() || parameters.ExpectedCancellationResult, cancelResult);

            if (parameters.ExpectedCancellationResult)
            {
                // We expect the OrderStatus.Canceled event
                canceledOrderStatusEvent.WaitOneAssertFail(1000 * secondsTimeout, "Order timed out to cancel");
            }

            var openOrders = Brokerage.GetOpenOrders();
            var cancelledOrder = openOrders.FirstOrDefault(x => x.Id == order.Id);
            Assert.IsNull(cancelledOrder);

            Brokerage.OrdersStatusChanged -= orderStatusCallback;
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public void LongFromZero(OrderTestParameters parameters)
        {
            Log.Trace("");
            Log.Trace("LONG FROM ZERO");
            Log.Trace("");
            PlaceFyersOrderWaitForStatus(parameters.CreateLongOrder(GetDefaultQuantity()), parameters.ExpectedStatus);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public void CloseFromLong(OrderTestParameters parameters)
        {
            Log.Trace("");
            Log.Trace("CLOSE FROM LONG");
            Log.Trace("");
            // first go long
            PlaceFyersOrderWaitForStatus(parameters.CreateLongMarketOrder(GetDefaultQuantity()), OrderStatus.Filled);

            // now close it
            PlaceFyersOrderWaitForStatus(parameters.CreateShortOrder(GetDefaultQuantity()), parameters.ExpectedStatus);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public void ShortFromZero(OrderTestParameters parameters)
        {
            Log.Trace("");
            Log.Trace("SHORT FROM ZERO");
            Log.Trace("");
            PlaceFyersOrderWaitForStatus(parameters.CreateShortOrder(GetDefaultQuantity()), parameters.ExpectedStatus);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public virtual void CloseFromShort(OrderTestParameters parameters)
        {
            Log.Trace("");
            Log.Trace("CLOSE FROM SHORT");
            Log.Trace("");
            // first go short
            PlaceFyersOrderWaitForStatus(parameters.CreateShortMarketOrder(GetDefaultQuantity()), OrderStatus.Filled);

            // now close it
            PlaceFyersOrderWaitForStatus(parameters.CreateLongOrder(GetDefaultQuantity()), parameters.ExpectedStatus);
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public virtual void ShortFromLong(OrderTestParameters parameters)
        {
            Log.Trace("");
            Log.Trace("SHORT FROM LONG");
            Log.Trace("");
            // first go long
            PlaceFyersOrderWaitForStatus(parameters.CreateLongMarketOrder(GetDefaultQuantity()));

            // now go net short
            var order = PlaceFyersOrderWaitForStatus(parameters.CreateShortOrder(2 * GetDefaultQuantity()), parameters.ExpectedStatus);

            if (parameters.ModifyUntilFilled)
            {
                ModifyOrderUntilFilled(order, parameters);
            }
        }

        [Test, TestCaseSource(nameof(OrderParameters))]
        public virtual void LongFromShort(OrderTestParameters parameters)
        {
            Log.Trace("");
            Log.Trace("LONG FROM SHORT");
            Log.Trace("");
            // first go short
            PlaceFyersOrderWaitForStatus(parameters.CreateShortMarketOrder(-GetDefaultQuantity()), OrderStatus.Filled);

            // now go long
            var order = PlaceFyersOrderWaitForStatus(parameters.CreateLongOrder(2 * GetDefaultQuantity()), parameters.ExpectedStatus);

            if (parameters.ModifyUntilFilled)
            {
                ModifyOrderUntilFilled(order, parameters);
            }
        }

        [Test]
        public void ValidateStopLimitOrders()
        {
            var fyers = (FyersBrokerage)Brokerage;
            var symbol = Symbol;
            var lastPrice = GetAskPrice(symbol);

            // Buy StopLimit order above market
            var stopPrice = lastPrice + 5m;
            var limitPrice = stopPrice + 2m;
            var order = new StopLimitOrder(symbol, 1, stopPrice, limitPrice, DateTime.UtcNow, properties: orderProperties);
            Assert.IsTrue(fyers.PlaceOrder(order));

            // Build a position for sell orders
            var marketOrder = new MarketOrder(symbol, 2, DateTime.UtcNow, properties: orderProperties);
            Assert.IsTrue(fyers.PlaceOrder(marketOrder));

            Thread.Sleep(20000);

            // Sell StopLimit order below market
            stopPrice = lastPrice - 5m;
            limitPrice = stopPrice - 2m;
            order = new StopLimitOrder(symbol, -1, stopPrice, limitPrice, DateTime.UtcNow, properties: orderProperties);
            Assert.IsTrue(fyers.PlaceOrder(order));
        }

        [Test]
        public void GetOpenOrders()
        {
            Log.Trace("");
            Log.Trace("GET OPEN ORDERS");
            Log.Trace("");

            // Place a limit order that won't be filled immediately
            var limitPrice = GetAskPrice(Symbol) - 50m; // Well below market
            var order = new LimitOrder(Symbol, GetDefaultQuantity(), limitPrice, DateTime.UtcNow, properties: orderProperties);
            OrderProvider.Add(order);
            Brokerage.PlaceOrder(order);

            Thread.Sleep(2000);

            var openOrders = Brokerage.GetOpenOrders();
            Assert.IsTrue(openOrders.Any());

            foreach (var openOrder in openOrders)
            {
                Log.Trace($"Open Order: {openOrder.Id} - {openOrder.Symbol} - {openOrder.Type} - Qty: {openOrder.Quantity}");
            }

            // Cancel the order
            Brokerage.CancelOrder(order);
        }

        [Test, Ignore("This test requires reading the output and selection of a low volume security for the Brokerage")]
        public void PartialFills()
        {
            var manualResetEvent = new ManualResetEvent(false);

            var qty = 1000000m;
            var remaining = qty;
            var sync = new object();
            Brokerage.OrdersStatusChanged += (sender, orderEvents) =>
            {
                var orderEvent = orderEvents.Single();
                lock (sync)
                {
                    remaining -= orderEvent.FillQuantity;
                    Console.WriteLine("Remaining: " + remaining + " FillQuantity: " + orderEvent.FillQuantity);
                    if (orderEvent.Status == OrderStatus.Filled)
                    {
                        manualResetEvent.Set();
                    }
                }
            };

            var symbol = Symbol;
            var order = new MarketOrder(symbol, qty, DateTime.UtcNow, properties: orderProperties);
            OrderProvider.Add(order);
            Brokerage.PlaceOrder(order);

            // pause for a while to wait for fills to come in
            manualResetEvent.WaitOne(2500);
            manualResetEvent.WaitOne(2500);
            manualResetEvent.WaitOne(2500);

            Console.WriteLine("Remaining: " + remaining);
            Assert.AreEqual(0, remaining);
        }

        #endregion

        #region Helper Methods

        /// <summary>
        /// Places the specified order with the brokerage and wait until we get the <paramref name="expectedStatus"/> back via an OrderStatusChanged event.
        /// This function handles adding the order to the <see cref="IOrderProvider"/> instance as well as incrementing the order ID.
        /// </summary>
        /// <param name="order">The order to be submitted</param>
        /// <param name="expectedStatus">The status to wait for</param>
        /// <param name="secondsTimeout">Maximum amount of time to wait for <paramref name="expectedStatus"/></param>
        /// <param name="allowFailedSubmission">Allow failed order submission</param>
        /// <returns>The same order that was submitted.</returns>
        protected Order PlaceFyersOrderWaitForStatus(Order order, OrderStatus expectedStatus = OrderStatus.Filled,
                                                double secondsTimeout = 10.0, bool allowFailedSubmission = false)
        {
            var requiredStatusEvent = new ManualResetEvent(false);
            var desiredStatusEvent = new ManualResetEvent(false);
            EventHandler<List<OrderEvent>> brokerageOnOrderStatusChanged = (sender, argss) =>
            {
                var args = argss.Single();
                // no matter what, every order should fire at least one of these
                if (args.Status == OrderStatus.Submitted || args.Status == OrderStatus.Invalid)
                {
                    Log.Trace("");
                    Log.Trace("SUBMITTED: " + args);
                    Log.Trace("");
                    requiredStatusEvent.Set();
                }
                // make sure we fire the status we're expecting
                if (args.Status == expectedStatus)
                {
                    Log.Trace("");
                    Log.Trace("EXPECTED: " + args);
                    Log.Trace("");
                    desiredStatusEvent.Set();
                }
            };

            Brokerage.OrdersStatusChanged += brokerageOnOrderStatusChanged;

            OrderProvider.Add(order);
            if (!Brokerage.PlaceOrder(order) && !allowFailedSubmission)
            {
                Assert.Fail("Brokerage failed to place the order: " + order);
            }

            requiredStatusEvent.WaitOneAssertFail((int)(1000 * secondsTimeout), "Expected every order to fire a submitted or invalid status event");
            desiredStatusEvent.WaitOneAssertFail((int)(1000 * secondsTimeout), "OrderStatus " + expectedStatus + " was not encountered within the timeout. Order Id:" + order.Id);

            Brokerage.OrdersStatusChanged -= brokerageOnOrderStatusChanged;

            return order;
        }

        protected void CancelOpenOrders()
        {
            Log.Trace("");
            Log.Trace("CANCEL OPEN ORDERS");
            Log.Trace("");
            var openOrders = Brokerage.GetOpenOrders();
            foreach (var openOrder in openOrders)
            {
                Log.Trace("Canceling: " + openOrder);
                Brokerage.CancelOrder(openOrder);
            }
        }

        /// <summary>
        /// Updates the specified order in the brokerage until it fills or reaches a timeout
        /// </summary>
        /// <param name="order">The order to be modified</param>
        /// <param name="parameters">The order test parameters that define how to modify the order</param>
        /// <param name="secondsTimeout">Maximum amount of time to wait until the order fills</param>
        protected void ModifyOrderUntilFilled(Order order, OrderTestParameters parameters, double secondsTimeout = 90)
        {
            if (order.Status == OrderStatus.Filled)
            {
                return;
            }

            var filledResetEvent = new ManualResetEvent(false);
            EventHandler<List<OrderEvent>> brokerageOnOrderStatusChanged = (sender, argss) =>
            {
                var args = argss.Single();
                if (args.Status == OrderStatus.Filled)
                {
                    filledResetEvent.Set();
                }
                if (args.Status == OrderStatus.Canceled || args.Status == OrderStatus.Invalid)
                {
                    Log.Trace("ModifyOrderUntilFilled(): " + order);
                    Assert.Fail("Unexpected order status: " + args.Status);
                }
            };

            Brokerage.OrdersStatusChanged += brokerageOnOrderStatusChanged;

            Log.Trace("");
            Log.Trace("MODIFY UNTIL FILLED: " + order);
            Log.Trace("");
            var stopwatch = Stopwatch.StartNew();
            while (!filledResetEvent.WaitOne(3000) && stopwatch.Elapsed.TotalSeconds < secondsTimeout)
            {
                filledResetEvent.Reset();
                if (order.Status == OrderStatus.PartiallyFilled) continue;

                var marketPrice = GetAskPrice(order.Symbol);
                Log.Trace("BrokerageTests.ModifyOrderUntilFilled(): Ask: " + marketPrice);

                var updateOrder = parameters.ModifyOrderToFill(Brokerage, order, marketPrice);
                if (updateOrder)
                {
                    if (order.Status == OrderStatus.Filled) break;

                    Log.Trace("BrokerageTests.ModifyOrderUntilFilled(): " + order);
                    if (!Brokerage.UpdateOrder(order))
                    {
                        Assert.Fail("Brokerage failed to update the order");
                    }
                }
            }

            Brokerage.OrdersStatusChanged -= brokerageOnOrderStatusChanged;
        }

        #endregion
    }
}
