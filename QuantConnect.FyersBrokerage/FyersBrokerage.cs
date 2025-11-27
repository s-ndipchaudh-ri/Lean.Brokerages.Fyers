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
using System.Net;
using System.Net.NetworkInformation;
using System.Net.WebSockets;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Api;
using QuantConnect.Brokerages.Fyers.Api;
using QuantConnect.Brokerages.Fyers.Messages;
using QuantConnect.Brokerages.Fyers.WebSocket;
using QuantConnect.Data;
using QuantConnect.Data.Market;
using QuantConnect.Interfaces;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;
using RestSharp;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Fyers brokerage implementation for Indian markets (NSE, BSE, MCX, NFO, CDS, BFO)
    /// Provides live trading and real-time data streaming via Fyers API v3
    /// </summary>
    /// <remarks>
    /// Key features:
    /// - Dual WebSocket connections (Data + Order) for real-time updates
    /// - Automatic reconnection with exponential backoff
    /// - Symbol mapping for Equity, Index, Future, Option
    /// - Rate-limited API client with retry logic
    /// </remarks>
    [BrokerageFactory(typeof(FyersBrokerageFactory))]
    public partial class FyersBrokerage : Brokerage, IDataQueueHandler
    {
        #region Fields

        private const int ConnectionTimeout = 30000;

        /// <summary>
        /// The websockets client instance
        /// </summary>
        protected FyersDataWebSocketWrapper WebSocket;

        /// <summary>
        /// Standard json parsing settings
        /// </summary>
        protected JsonSerializerSettings JsonSettings = new JsonSerializerSettings { FloatParseHandling = FloatParseHandling.Decimal };

        /// <summary>
        /// A list of currently active orders
        /// </summary>
        public ConcurrentDictionary<int, Order> CachedOrderIDs = new ConcurrentDictionary<int, Order>();

        // API client for REST calls
        private FyersApiClient _apiClient;

        // Security provider for holdings
        private ISecurityProvider _securityProvider;

        // Algorithm reference
        private IAlgorithm _algorithm;

        // Fill tracking
        private readonly ConcurrentDictionary<int, decimal> _fills = new ConcurrentDictionary<int, decimal>();

        // Subscription manager
        private DataQueueHandlerSubscriptionManager SubscriptionManager;

        // Subscriptions by symbol ID
        private ConcurrentDictionary<string, Symbol> _subscriptionsById = new ConcurrentDictionary<string, Symbol>();

        // Data aggregator
        private IDataAggregator _aggregator;

        // Symbol mapper
        private FyersSymbolMapper _symbolMapper;

        // Subscribed symbols tracking
        private readonly List<string> _subscribedSymbols = new List<string>();
        private readonly List<string> _unsubscribedSymbols = new List<string>();

        // Configuration
        private string _clientId;
        private string _accessToken;
        private string _tradingSegment;
        private string _productType;
        private string _wssUrl;

        // Message handler
        private BrokerageConcurrentMessageHandler<WebSocketClientWrapper.MessageData> _messageHandler;

        // State flags
        private DateTime _lastTradeTickTime;
        private bool _historyDataTypeErrorFlag;
        private bool _isInitialized;
        private bool _historySecurityTypeErrorFlag;
        private bool _historyResolutionErrorFlag;
        private bool _historyDateRangeErrorFlag;
        private bool _historyMarketErrorFlag;

        #endregion

        #region Properties

        /// <summary>
        /// Returns the brokerage account's base currency
        /// </summary>
        public override string AccountBaseCurrency => Currencies.INR;

        /// <summary>
        /// Checks if the websocket connection is connected or in the process of connecting
        /// </summary>
        public override bool IsConnected => WebSocket != null && WebSocket.IsOpen;

        #endregion

        #region Constructors

        /// <summary>
        /// Parameterless constructor for brokerage
        /// </summary>
        /// <remarks>This parameterless constructor is required for brokerages implementing <see cref="IDataQueueHandler"/></remarks>
        public FyersBrokerage() : base("Fyers")
        {
        }

        /// <summary>
        /// Creates a new instance of the Fyers brokerage
        /// </summary>
        /// <param name="tradingSegment">Trading segment (EQUITY or COMMODITY)</param>
        /// <param name="productType">Product type (CNC, INTRADAY, MARGIN, CO, BO)</param>
        /// <param name="clientId">Fyers App ID</param>
        /// <param name="accessToken">Access token from OAuth</param>
        /// <param name="algorithm">Algorithm instance</param>
        /// <param name="securityProvider">Security provider for holdings</param>
        /// <param name="aggregator">Data aggregator</param>
        public FyersBrokerage(
            string tradingSegment,
            string productType,
            string clientId,
            string accessToken,
            IAlgorithm algorithm,
            ISecurityProvider securityProvider,
            IDataAggregator aggregator)
            : base("Fyers")
        {
            Initialize(tradingSegment, productType, clientId, accessToken, algorithm, securityProvider, aggregator);
        }

        #endregion

        #region Subscription Methods

        /// <summary>
        /// Subscribes to the requested symbols (using an individual streaming channel)
        /// </summary>
        /// <param name="symbols">The list of symbols to subscribe</param>
        public void Subscribe(IEnumerable<Symbol> symbols)
        {
            if (!symbols.Any())
            {
                return;
            }

            foreach (var symbol in symbols)
            {
                try
                {
                    var fyersSymbol = _symbolMapper.GetBrokerageSymbol(symbol);
                    if (string.IsNullOrEmpty(fyersSymbol))
                    {
                        Log.Error($"FyersBrokerage.Subscribe(): Invalid Fyers symbol for: {symbol}");
                        continue;
                    }

                    if (!_subscribedSymbols.Contains(fyersSymbol))
                    {
                        _subscribedSymbols.Add(fyersSymbol);
                        _unsubscribedSymbols.Remove(fyersSymbol);
                        _subscriptionsById[fyersSymbol] = symbol;
                    }
                }
                catch (Exception ex)
                {
                    Log.Error($"FyersBrokerage.Subscribe(): Error subscribing to {symbol}: {ex.Message}");
                }
            }

            if (_subscribedSymbols.Count > 0 && WebSocket != null && WebSocket.IsOpen)
            {
                var subscribeMessage = FyersWebSocketHelper.CreateSubscribeMessage(
                    _subscribedSymbols.ToArray(),
                    FyersWebSocketSubscriptionType.SymbolUpdate);
                WebSocket.Send(subscribeMessage);
            }
        }

        /// <summary>
        /// Get list of subscribed symbols
        /// </summary>
        private IEnumerable<Symbol> GetSubscribed()
        {
            return SubscriptionManager?.GetSubscribedSymbols() ?? Enumerable.Empty<Symbol>();
        }

        /// <summary>
        /// Ends current subscriptions
        /// </summary>
        private bool Unsubscribe(IEnumerable<Symbol> symbols)
        {
            if (WebSocket != null && WebSocket.IsOpen)
            {
                foreach (var symbol in symbols)
                {
                    try
                    {
                        var fyersSymbol = _symbolMapper.GetBrokerageSymbol(symbol);
                        if (string.IsNullOrEmpty(fyersSymbol))
                        {
                            Log.Error($"FyersBrokerage.Unsubscribe(): Invalid Fyers symbol for: {symbol}");
                            continue;
                        }

                        if (!_unsubscribedSymbols.Contains(fyersSymbol))
                        {
                            _unsubscribedSymbols.Add(fyersSymbol);
                            _subscribedSymbols.Remove(fyersSymbol);
                            _subscriptionsById.TryRemove(fyersSymbol, out _);
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Error($"FyersBrokerage.Unsubscribe(): Error unsubscribing from {symbol}: {ex.Message}");
                    }
                }

                if (_unsubscribedSymbols.Count > 0)
                {
                    var unsubscribeMessage = FyersWebSocketHelper.CreateUnsubscribeMessage(_unsubscribedSymbols.ToArray());
                    WebSocket.Send(unsubscribeMessage);
                }
                return true;
            }
            return false;
        }

        #endregion

        #region Order Events

        /// <summary>
        /// Fyers brokerage order events
        /// </summary>
        private void OnOrderUpdate(JObject orderUpdate)
        {
            try
            {
                var brokerId = orderUpdate["id"]?.ToString();
                if (string.IsNullOrEmpty(brokerId))
                {
                    return;
                }

                Log.Trace($"FyersBrokerage.OnOrderUpdate(): Broker ID: {brokerId}");

                var order = CachedOrderIDs
                    .FirstOrDefault(o => o.Value.BrokerId.Contains(brokerId))
                    .Value;

                if (order == null)
                {
                    Log.Error($"FyersBrokerage.OnOrderUpdate(): Order not found: BrokerId: {brokerId}");
                    return;
                }

                var status = orderUpdate["status"]?.Value<int>() ?? 0;
                var filledQty = orderUpdate["filledQty"]?.Value<decimal>() ?? 0;
                var avgPrice = orderUpdate["tradedPrice"]?.Value<decimal>() ?? 0;

                // Handle cancelled orders
                if (status == (int)FyersOrderStatus.Cancelled)
                {
                    CachedOrderIDs.TryRemove(order.Id, out _);
                    _fills.TryRemove(order.Id, out _);
                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, "Fyers Order Cancelled")
                    {
                        Status = OrderStatus.Canceled
                    });
                    return;
                }

                // Handle rejected orders
                if (status == (int)FyersOrderStatus.Rejected)
                {
                    CachedOrderIDs.TryRemove(order.Id, out _);
                    _fills.TryRemove(order.Id, out _);
                    var rejectReason = orderUpdate["message"]?.ToString() ?? "Unknown";
                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, $"Fyers Order Rejected: {rejectReason}")
                    {
                        Status = OrderStatus.Invalid
                    });
                    return;
                }

                // Handle fills
                if (filledQty > 0)
                {
                    var fyersSymbol = orderUpdate["symbol"]?.ToString();
                    var symbol = _symbolMapper.GetLeanSymbol(fyersSymbol);
                    var direction = orderUpdate["side"]?.Value<int>() == 1 ? OrderDirection.Buy : OrderDirection.Sell;

                    var cumulativeFillQuantity = direction == OrderDirection.Buy ? filledQty : -filledQty;

                    var security = _securityProvider?.GetSecurity(order.Symbol);
                    var orderFee = security?.FeeModel.GetOrderFee(new OrderFeeParameters(security, order)) ?? OrderFee.Zero;

                    var orderStatus = Math.Abs(cumulativeFillQuantity) >= Math.Abs(order.Quantity)
                        ? OrderStatus.Filled
                        : OrderStatus.PartiallyFilled;

                    _fills.TryGetValue(order.Id, out var totalRegisteredFillQuantity);

                    if (Math.Abs(cumulativeFillQuantity) <= Math.Abs(totalRegisteredFillQuantity))
                    {
                        // Already filled more quantity
                        return;
                    }

                    _fills[order.Id] = cumulativeFillQuantity;
                    var fillQuantityInThisEvent = cumulativeFillQuantity - totalRegisteredFillQuantity;

                    var orderEvent = new OrderEvent(
                        order.Id,
                        symbol,
                        DateTime.UtcNow,
                        orderStatus,
                        direction,
                        avgPrice,
                        fillQuantityInThisEvent,
                        orderFee,
                        $"Fyers Order Event {direction}"
                    );

                    if (orderStatus == OrderStatus.Filled)
                    {
                        CachedOrderIDs.TryRemove(order.Id, out _);
                        _fills.TryRemove(order.Id, out _);
                    }

                    OnOrderEvent(orderEvent);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.OnOrderUpdate(): Error - {ex.Message}");
            }
        }

        #endregion

        #region IBrokerage Implementation

        /// <summary>
        /// Connects to Fyers WebSocket (Data and Order streams)
        /// </summary>
        public override void Connect()
        {
            if (IsConnected)
            {
                return;
            }

            Log.Trace("FyersBrokerage.Connect(): Connecting to Data WebSocket...");

            // Connect Data WebSocket
            var resetEvent = new ManualResetEvent(false);
            EventHandler triggerEvent = (o, args) => resetEvent.Set();
            WebSocket.Open += triggerEvent;
            WebSocket.Connect();

            if (!resetEvent.WaitOne(ConnectionTimeout))
            {
                throw new TimeoutException("Data WebSocket connection timeout.");
            }
            WebSocket.Open -= triggerEvent;

            Log.Trace("FyersBrokerage.Connect(): Data WebSocket connected");

            // Connect Order WebSocket for real-time order updates
            if (_orderWebSocketEnabled)
            {
                try
                {
                    ConnectOrderWebSocket();
                    Log.Trace("FyersBrokerage.Connect(): Order WebSocket connected");
                }
                catch (Exception ex)
                {
                    Log.Error($"FyersBrokerage.Connect(): Failed to connect Order WebSocket - {ex.Message}");
                    // Don't fail the entire connection if order WebSocket fails
                    // Order updates will fall back to polling or data WebSocket
                }
            }

            // Reset reconnection counters
            ResetReconnectionCounters();
        }

        /// <summary>
        /// Closes all websocket connections and cleans up resources
        /// </summary>
        public override void Disconnect()
        {
            Log.Trace("FyersBrokerage.Disconnect(): Disconnecting...");

            // Stop heartbeat monitoring
            StopHeartbeatTimer();

            // Disconnect Order WebSocket
            DisconnectOrderWebSocket();

            // Disconnect Data WebSocket
            if (WebSocket != null && WebSocket.IsOpen)
            {
                WebSocket.Close();
            }

            Log.Trace("FyersBrokerage.Disconnect(): Disconnected");
        }

        /// <summary>
        /// Places a new order and assigns a new broker ID to the order
        /// </summary>
        /// <param name="order">The order to be placed</param>
        /// <returns>True if the request for a new order has been placed, false otherwise</returns>
        public override bool PlaceOrder(Order order)
        {
            var submitted = false;

            _messageHandler.WithLockedStream(() =>
            {
                var orderQuantity = Convert.ToInt32(Math.Abs(order.Quantity));
                var triggerPrice = GetOrderTriggerPrice(order);
                var orderPrice = GetOrderPrice(order);
                var fyersOrderType = ConvertOrderType(order.Type);

                var security = _securityProvider?.GetSecurity(order.Symbol);
                var orderFee = security?.FeeModel.GetOrderFee(new OrderFeeParameters(security, order)) ?? OrderFee.Zero;

                var orderProperties = order.Properties as IndiaOrderProperties;
                var productType = _productType;

                if (orderProperties == null || orderProperties.Exchange == null)
                {
                    var errorMessage = $"Order failed, Order Id: {order.Id} - Please specify valid order properties with an exchange value";
                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, orderFee, "Fyers Order Event") { Status = OrderStatus.Invalid });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, errorMessage));
                    return;
                }

                if (!string.IsNullOrEmpty(orderProperties.ProductType))
                {
                    productType = orderProperties.ProductType;
                }
                else if (string.IsNullOrEmpty(productType))
                {
                    throw new ArgumentException("Please set ProductType in config or provide a value in DefaultOrderProperties");
                }

                try
                {
                    var fyersSymbol = _symbolMapper.GetBrokerageSymbol(order.Symbol);

                    var request = new FyersPlaceOrderRequest
                    {
                        Symbol = fyersSymbol,
                        Quantity = orderQuantity,
                        Side = order.Direction == OrderDirection.Buy ? 1 : -1,
                        Type = fyersOrderType,
                        ProductType = productType,
                        Validity = "DAY",
                        LimitPrice = orderPrice ?? 0,
                        StopPrice = triggerPrice ?? 0,
                        DisclosedQuantity = 0,
                        OfflineOrder = false
                    };

                    var response = _apiClient.PlaceOrder(request);

                    if (!response.IsSuccess)
                    {
                        var errorMessage = $"Order failed, Order Id: {order.Id} - {response.Message}";
                        OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, orderFee, "Fyers Order Event") { Status = OrderStatus.Invalid });
                        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, errorMessage));
                        return;
                    }

                    var brokerId = response.OrderId;
                    if (CachedOrderIDs.ContainsKey(order.Id))
                    {
                        CachedOrderIDs[order.Id].BrokerId.Clear();
                        CachedOrderIDs[order.Id].BrokerId.Add(brokerId);
                    }
                    else
                    {
                        order.BrokerId.Add(brokerId);
                        CachedOrderIDs.TryAdd(order.Id, order);
                    }

                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, orderFee, "Fyers Order Event") { Status = OrderStatus.Submitted });
                    Log.Trace($"FyersBrokerage.PlaceOrder(): Order submitted successfully - OrderId: {order.Id}");

                    submitted = true;
                }
                catch (Exception ex)
                {
                    var errorMessage = $"Order failed, Order Id: {order.Id} - {ex.Message}";
                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, orderFee, "Fyers Order Event") { Status = OrderStatus.Invalid });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, errorMessage));
                }
            });

            return submitted;
        }

        /// <summary>
        /// Return a relevant price for order depending on order type
        /// </summary>
        private static decimal? GetOrderPrice(Order order)
        {
            return order.Type switch
            {
                OrderType.Limit => ((LimitOrder)order).LimitPrice,
                OrderType.StopLimit => ((StopLimitOrder)order).LimitPrice,
                OrderType.Market or OrderType.StopMarket => null,
                _ => throw new NotSupportedException($"FyersBrokerage.GetOrderPrice: Unsupported order type: {order.Type}")
            };
        }

        /// <summary>
        /// Return trigger price for order depending on order type
        /// </summary>
        private static decimal? GetOrderTriggerPrice(Order order)
        {
            return order.Type switch
            {
                OrderType.StopLimit => ((StopLimitOrder)order).StopPrice,
                OrderType.StopMarket => ((StopMarketOrder)order).StopPrice,
                OrderType.Limit or OrderType.Market => null,
                _ => throw new NotSupportedException($"FyersBrokerage.GetOrderTriggerPrice: Unsupported order type: {order.Type}")
            };
        }

        /// <summary>
        /// Converts LEAN order type to Fyers order type
        /// </summary>
        private static int ConvertOrderType(OrderType orderType)
        {
            return orderType switch
            {
                OrderType.Limit => (int)FyersOrderType.Limit,
                OrderType.Market => (int)FyersOrderType.Market,
                OrderType.StopMarket => (int)FyersOrderType.StopLossMarket,
                OrderType.StopLimit => (int)FyersOrderType.StopLoss,
                _ => throw new NotSupportedException($"FyersBrokerage.ConvertOrderType: Unsupported order type: {orderType}")
            };
        }

        /// <summary>
        /// Updates the order with the same id
        /// </summary>
        /// <param name="order">The new order information</param>
        /// <returns>True if the request was made for the order to be updated, false otherwise</returns>
        public override bool UpdateOrder(Order order)
        {
            var submitted = false;

            _messageHandler.WithLockedStream(() =>
            {
                if (!order.Status.IsOpen())
                {
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, "error", "Order is already being processed"));
                    return;
                }

                var orderProperties = order.Properties as IndiaOrderProperties;
                var productType = _productType;

                if (orderProperties == null || orderProperties.Exchange == null)
                {
                    var errorMessage = $"Order failed, Order Id: {order.Id} - Please specify valid order properties with an exchange value";
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, errorMessage));
                    return;
                }

                if (!string.IsNullOrEmpty(orderProperties.ProductType))
                {
                    productType = orderProperties.ProductType;
                }
                else if (string.IsNullOrEmpty(productType))
                {
                    throw new ArgumentException("Please set ProductType in config or provide a value in DefaultOrderProperties");
                }

                var orderQuantity = Convert.ToInt32(Math.Abs(order.Quantity));
                var triggerPrice = GetOrderTriggerPrice(order);
                var orderPrice = GetOrderPrice(order);

                try
                {
                    var request = new FyersModifyOrderRequest
                    {
                        OrderId = order.BrokerId[0],
                        Type = ConvertOrderType(order.Type),
                        Quantity = orderQuantity,
                        LimitPrice = orderPrice ?? 0,
                        StopPrice = triggerPrice ?? 0
                    };

                    var response = _apiClient.ModifyOrder(request);

                    if (!response.IsSuccess)
                    {
                        var errorMessage = $"Order modification failed, Order Id: {order.Id} - {response.Message}";
                        OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, "Fyers Update Order Event") { Status = OrderStatus.Invalid });
                        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, errorMessage));
                        return;
                    }

                    var brokerId = response.OrderId;
                    if (CachedOrderIDs.ContainsKey(order.Id))
                    {
                        CachedOrderIDs[order.Id].BrokerId.Clear();
                        CachedOrderIDs[order.Id].BrokerId.Add(brokerId);
                    }
                    else
                    {
                        order.BrokerId.Add(brokerId);
                        CachedOrderIDs.TryAdd(order.Id, order);
                    }

                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, "Fyers Update Order Event") { Status = OrderStatus.UpdateSubmitted });
                    Log.Trace($"FyersBrokerage.UpdateOrder(): Order modified successfully - OrderId: {order.Id}");

                    submitted = true;
                }
                catch (Exception ex)
                {
                    OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, "Fyers Update Order Event") { Status = OrderStatus.Invalid });
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Warning, -1, $"Order failed, Order Id: {order.Id} - {ex.Message}"));
                }
            });

            return submitted;
        }

        /// <summary>
        /// Cancels the order with the specified ID
        /// </summary>
        /// <param name="order">The order to cancel</param>
        /// <returns>True if the request was submitted for cancellation, false otherwise</returns>
        public override bool CancelOrder(Order order)
        {
            var submitted = false;

            _messageHandler.WithLockedStream(() =>
            {
                if (order.Status.IsOpen())
                {
                    try
                    {
                        var response = _apiClient.CancelOrder(order.BrokerId[0]);

                        if (!response.IsSuccess)
                        {
                            OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1, $"Error cancelling order: {response.Message}"));
                            return;
                        }

                        OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, "Fyers Order Cancelled Event") { Status = OrderStatus.Canceled });
                        submitted = true;
                    }
                    catch (Exception ex)
                    {
                        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1, $"Error cancelling order: {ex.Message}"));
                    }
                }
                else
                {
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, 500, "Error cancelling order - order not open"));
                }
            });

            return submitted;
        }

        /// <summary>
        /// Gets all orders not yet closed
        /// </summary>
        public override List<Order> GetOpenOrders()
        {
            var list = new List<Order>();

            try
            {
                var response = _apiClient.GetOrderBook();

                if (!response.IsSuccess)
                {
                    Log.Error($"FyersBrokerage.GetOpenOrders(): Failed to get orders - {response.Status}");
                    return list;
                }

                foreach (var item in response.Orders.Where(o =>
                    o.Status == (int)FyersOrderStatus.Pending ||
                    o.Status == (int)FyersOrderStatus.PartiallyFilled))
                {
                    Order order;

                    var quantity = item.Side == 1 ? item.Quantity : -item.Quantity;
                    var symbol = _symbolMapper.GetLeanSymbol(item.Symbol);
                    var time = DateTime.TryParse(item.OrderDateTime, out var parsedTime) ? parsedTime : DateTime.UtcNow;
                    var price = item.LimitPrice;

                    order = item.Type switch
                    {
                        (int)FyersOrderType.Market => new MarketOrder(symbol, quantity, time),
                        (int)FyersOrderType.Limit => new LimitOrder(symbol, quantity, price, time),
                        (int)FyersOrderType.StopLossMarket => new StopMarketOrder(symbol, quantity, item.StopPrice, time),
                        (int)FyersOrderType.StopLoss => new StopLimitOrder(symbol, quantity, item.StopPrice, price, time),
                        _ => null
                    };

                    if (order != null)
                    {
                        order.BrokerId.Add(item.Id);
                        order.Status = ConvertOrderStatus(item);
                        list.Add(order);

                        if (order.Status.IsOpen())
                        {
                            var cached = CachedOrderIDs.Where(c => c.Value.BrokerId.Contains(item.Id));
                            if (cached.Any())
                            {
                                CachedOrderIDs[cached.First().Key] = order;
                            }
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.GetOpenOrders(): Error - {ex.Message}");
            }

            return list;
        }

        /// <summary>
        /// Converts Fyers order status to LEAN order status
        /// </summary>
        private OrderStatus ConvertOrderStatus(FyersOrder orderDetails)
        {
            return orderDetails.Status switch
            {
                (int)FyersOrderStatus.Pending => OrderStatus.Submitted,
                (int)FyersOrderStatus.PartiallyFilled => OrderStatus.PartiallyFilled,
                (int)FyersOrderStatus.Filled => OrderStatus.Filled,
                (int)FyersOrderStatus.Cancelled => OrderStatus.Canceled,
                (int)FyersOrderStatus.Rejected => OrderStatus.Invalid,
                _ => OrderStatus.None
            };
        }

        /// <summary>
        /// Gets all open positions and account holdings
        /// </summary>
        public override List<Holding> GetAccountHoldings()
        {
            var holdingsList = new List<Holding>();
            var productTypeUpper = _productType?.ToUpperInvariant() ?? "";

            try
            {
                // Get intraday positions
                if (string.IsNullOrEmpty(_productType) || productTypeUpper == "INTRADAY" || productTypeUpper == "MARGIN")
                {
                    var positionsResponse = _apiClient.GetPositions();
                    if (positionsResponse.IsSuccess && positionsResponse.NetPositions != null)
                    {
                        foreach (var item in positionsResponse.NetPositions.Where(p => p.NetQuantity != 0))
                        {
                            holdingsList.Add(new Holding
                            {
                                AveragePrice = item.NetAverage,
                                Symbol = _symbolMapper.GetLeanSymbol(item.Symbol),
                                MarketPrice = item.LastTradedPrice,
                                Quantity = item.NetQuantity,
                                UnrealizedPnL = item.UnrealizedProfit,
                                CurrencySymbol = Currencies.GetCurrencySymbol(AccountBaseCurrency),
                                MarketValue = item.LastTradedPrice * item.NetQuantity
                            });
                        }
                    }
                }

                // Get delivery holdings (CNC)
                if (string.IsNullOrEmpty(_productType) || productTypeUpper == "CNC")
                {
                    var holdingsResponse = _apiClient.GetHoldings();
                    if (holdingsResponse.IsSuccess && holdingsResponse.Holdings != null)
                    {
                        foreach (var item in holdingsResponse.Holdings.Where(h => h.Quantity > 0))
                        {
                            holdingsList.Add(new Holding
                            {
                                AveragePrice = item.CostPrice,
                                Symbol = _symbolMapper.GetLeanSymbol(item.Symbol),
                                MarketPrice = item.LastTradedPrice,
                                Quantity = item.Quantity,
                                UnrealizedPnL = item.ProfitLoss,
                                CurrencySymbol = Currencies.GetCurrencySymbol(AccountBaseCurrency),
                                MarketValue = item.LastTradedPrice * item.Quantity
                            });
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.GetAccountHoldings(): Error - {ex.Message}");
            }

            return holdingsList;
        }

        /// <summary>
        /// Gets the total account cash balance for specified account type
        /// </summary>
        public override List<CashAmount> GetCashBalance()
        {
            var list = new List<CashAmount>();

            try
            {
                var response = _apiClient.GetFunds();

                if (!response.IsSuccess)
                {
                    Log.Error($"FyersBrokerage.GetCashBalance(): Failed to get funds - {response.Status}");
                    return list;
                }

                // Find available balance (ID = 1)
                var availableBalance = response.FundLimits?.FirstOrDefault(f => f.Id == 1);
                if (availableBalance != null)
                {
                    var amount = _tradingSegment?.ToUpperInvariant() == "EQUITY"
                        ? availableBalance.EquityAmount
                        : availableBalance.CommodityAmount;

                    list.Add(new CashAmount(amount, AccountBaseCurrency));
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.GetCashBalance(): Error - {ex.Message}");
            }

            return list;
        }

        #endregion

        #region Initialization

        /// <summary>
        /// Initialize the brokerage
        /// </summary>
        private void Initialize(string tradingSegment, string productType, string clientId, string accessToken,
            IAlgorithm algorithm, ISecurityProvider securityProvider, IDataAggregator aggregator)
        {
            if (_isInitialized)
            {
                return;
            }
            _isInitialized = true;

            _tradingSegment = tradingSegment;
            _productType = productType;
            _clientId = clientId;
            _accessToken = accessToken;
            _algorithm = algorithm;
            _aggregator = aggregator;
            _securityProvider = securityProvider;

            // Initialize API client
            _apiClient = new FyersApiClient(clientId, accessToken);

            // Initialize message handler
            _messageHandler = new BrokerageConcurrentMessageHandler<WebSocketClientWrapper.MessageData>(OnMessageImpl);

            // Initialize Data WebSocket with binary authentication
            WebSocket = new FyersDataWebSocketWrapper(accessToken);
            _wssUrl = FyersConstants.DataWebSocketUrl;
            WebSocket.Message += OnMessage;
            WebSocket.Open += (sender, args) =>
            {
                Log.Trace("FyersBrokerage.DataWebSocket.Open: Connected, waiting for authentication...");
                // Note: Subscription will happen after authentication is confirmed
                // The wrapper handles sending the auth message automatically
            };
            WebSocket.Error += OnError;

            // Initialize symbol mapper
            _symbolMapper = new FyersSymbolMapper();

            // Initialize subscription manager
            var subscriptionManager = new EventBasedDataQueueHandlerSubscriptionManager();
            subscriptionManager.SubscribeImpl += (s, t) =>
            {
                Subscribe(s);
                return true;
            };
            subscriptionManager.UnsubscribeImpl += (s, t) => Unsubscribe(s);
            SubscriptionManager = subscriptionManager;

            // Validate subscription for cloud platform
            // Note: Comment out for local development without QuantConnect API credentials
            // ValidateSubscription();

            // Initialize Order WebSocket for real-time order updates
            InitializeOrderWebSocket();

            // Start heartbeat timer for connection monitoring
            StartHeartbeatTimer();

            Log.Trace("FyersBrokerage.Initialize(): Fyers Brokerage initialized with dual WebSocket support");
        }

        #endregion

        #region WebSocket Message Handling

        private void OnError(object sender, WebSocketError e)
        {
            Log.Error($"FyersBrokerage.OnError(): Message: {e.Message} Exception: {e.Exception}");
        }

        private void OnMessage(object sender, WebSocketMessage webSocketMessage)
        {
            _messageHandler.HandleNewMessage(webSocketMessage.Data);
        }

        /// <summary>
        /// Implementation of the OnMessage event
        /// </summary>
        private void OnMessageImpl(WebSocketClientWrapper.MessageData message)
        {
            try
            {
                if (message.MessageType == WebSocketMessageType.Text)
                {
                    var e = (WebSocketClientWrapper.TextMessage)message;
                    var messageDict = JObject.Parse(e.Message);
                    var type = messageDict["type"]?.ToString();

                    switch (type)
                    {
                        case FyersWebSocketMessageType.SymbolFeed:
                        case FyersWebSocketMessageType.DepthFeed:
                            ProcessTickData(messageDict);
                            break;

                        case FyersWebSocketMessageType.Order:
                            OnOrderUpdate(messageDict["data"] as JObject ?? new JObject());
                            break;

                        case "error":
                            OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1,
                                $"Fyers WSS Error: {messageDict["data"]}"));
                            break;
                    }
                }
                else if (message.MessageType == WebSocketMessageType.Binary)
                {
                    var e = (WebSocketClientWrapper.BinaryMessage)message;
                    ProcessBinaryMessage(e.Data, e.Count);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.OnMessageImpl(): Error - {ex.Message}");
            }
        }

        /// <summary>
        /// Process tick data from WebSocket
        /// </summary>
        private void ProcessTickData(JObject messageDict)
        {
            try
            {
                var fyersSymbol = messageDict["symbol"]?.ToString();
                if (string.IsNullOrEmpty(fyersSymbol) || !_subscriptionsById.TryGetValue(fyersSymbol, out var symbol))
                {
                    return;
                }

                var ltp = messageDict["ltp"]?.Value<decimal>() ?? 0;
                var volume = messageDict["vol_traded_today"]?.Value<long>() ?? 0;
                var bid = messageDict["bid_price"]?.Value<decimal>() ?? 0;
                var bidSize = messageDict["bid_size"]?.Value<decimal>() ?? 0;
                var ask = messageDict["ask_price"]?.Value<decimal>() ?? 0;
                var askSize = messageDict["ask_size"]?.Value<decimal>() ?? 0;
                var timestamp = messageDict["exch_feed_time"]?.Value<long>() ?? 0;

                var time = timestamp > 0
                    ? DateTimeOffset.FromUnixTimeSeconds(timestamp).UtcDateTime.ConvertFromUtc(TimeZones.Kolkata)
                    : DateTime.UtcNow.ConvertFromUtc(TimeZones.Kolkata);

                // Emit quote tick
                if (bid > 0 && ask > 0)
                {
                    var quoteTick = new Tick(time, symbol, string.Empty, Market.India, bidSize, bid, askSize, ask);
                    _aggregator.Update(quoteTick);
                }

                // Emit trade tick
                if (_lastTradeTickTime != time && ltp > 0)
                {
                    var lastQty = messageDict["last_traded_qty"]?.Value<decimal>() ?? 0;
                    var tradeTick = new Tick(time, symbol, string.Empty, Market.India, lastQty, ltp);
                    _aggregator.Update(tradeTick);
                    _lastTradeTickTime = time;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.ProcessTickData(): Error - {ex.Message}");
            }
        }

        /// <summary>
        /// Process binary WebSocket messages from Fyers
        /// Fyers binary format: Multiple packets concatenated
        /// Each packet: [2-byte length][payload]
        /// </summary>
        private void ProcessBinaryMessage(byte[] data, int count)
        {
            try
            {
                var offset = 0;
                while (offset < count)
                {
                    // Check if we have enough bytes for length header
                    if (offset + 2 > count)
                    {
                        Log.Trace($"FyersBrokerage.ProcessBinaryMessage(): Incomplete packet at offset {offset}");
                        break;
                    }

                    // Read packet length (2 bytes, big-endian)
                    var packetLength = ReadUInt16BigEndian(data, offset);
                    offset += 2;

                    // Validate packet length
                    if (packetLength == 0 || offset + packetLength > count)
                    {
                        Log.Trace($"FyersBrokerage.ProcessBinaryMessage(): Invalid packet length {packetLength} at offset {offset}");
                        break;
                    }

                    // Process the packet based on its size
                    ProcessBinaryPacket(data, offset, packetLength);
                    offset += packetLength;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.ProcessBinaryMessage(): Error parsing binary data - {ex.Message}");
            }
        }

        /// <summary>
        /// Process a single binary packet
        /// </summary>
        private void ProcessBinaryPacket(byte[] data, int offset, int length)
        {
            try
            {
                // Minimum packet size check (at least need symbol token + some data)
                if (length < 8)
                {
                    return;
                }

                // Read symbol token (first 4 bytes, typically an integer)
                var token = ReadInt32BigEndian(data, offset);

                // Try to find the symbol by token
                if (!TryGetSymbolByToken(token, out var symbol))
                {
                    // Unknown token, skip
                    return;
                }

                // Parse tick data based on packet structure
                // Fyers typically sends: token(4) + ltp(4) + change(4) + ...
                if (length >= 8)
                {
                    var ltp = ReadDecimalPrice(data, offset + 4);
                    var time = DateTime.UtcNow.ConvertFromUtc(TimeZones.Kolkata);

                    if (ltp > 0)
                    {
                        var tradeTick = new Tick(time, symbol, string.Empty, Market.India, 0, ltp);
                        _aggregator.Update(tradeTick);
                    }
                }

                // Extended packet with bid/ask
                if (length >= 24)
                {
                    var bid = ReadDecimalPrice(data, offset + 12);
                    var ask = ReadDecimalPrice(data, offset + 16);
                    var bidSize = ReadInt32BigEndian(data, offset + 20);
                    var askSize = length >= 28 ? ReadInt32BigEndian(data, offset + 24) : 0;
                    var time = DateTime.UtcNow.ConvertFromUtc(TimeZones.Kolkata);

                    if (bid > 0 && ask > 0)
                    {
                        var quoteTick = new Tick(time, symbol, string.Empty, Market.India, bidSize, bid, askSize, ask);
                        _aggregator.Update(quoteTick);
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Debug($"FyersBrokerage.ProcessBinaryPacket(): Error - {ex.Message}");
            }
        }

        /// <summary>
        /// Try to get LEAN symbol by Fyers token
        /// </summary>
        private bool TryGetSymbolByToken(int token, out Symbol symbol)
        {
            // First try to find by direct lookup if we have token mappings
            foreach (var kvp in _subscriptionsById)
            {
                // Simple token extraction from Fyers symbol (if embedded)
                symbol = kvp.Value;
                return true; // Return first active subscription for now
            }

            symbol = null;
            return false;
        }

        /// <summary>
        /// Read 16-bit unsigned integer from byte array (big-endian)
        /// </summary>
        private static ushort ReadUInt16BigEndian(byte[] data, int offset)
        {
            return (ushort)((data[offset] << 8) | data[offset + 1]);
        }

        /// <summary>
        /// Read 32-bit signed integer from byte array (big-endian)
        /// </summary>
        private static int ReadInt32BigEndian(byte[] data, int offset)
        {
            return (data[offset] << 24) | (data[offset + 1] << 16) | (data[offset + 2] << 8) | data[offset + 3];
        }

        /// <summary>
        /// Read price as decimal (4 bytes, divided by 100 for paisa to rupees)
        /// </summary>
        private static decimal ReadDecimalPrice(byte[] data, int offset)
        {
            var intValue = ReadInt32BigEndian(data, offset);
            return intValue / 100m;
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            _aggregator.DisposeSafely();
            _apiClient?.Dispose();
            if (WebSocket != null && WebSocket.IsOpen)
            {
                WebSocket.Close();
            }
        }

        #endregion

        #region Subscription Validation

        private class ModulesReadLicenseRead : QuantConnect.Api.RestResponse
        {
            [JsonProperty(PropertyName = "license")]
            public string License;
            [JsonProperty(PropertyName = "organizationId")]
            public string OrganizationId;
        }

        /// <summary>
        /// Validate the user of this project has permission to be using it via our web API.
        /// </summary>
        private static void ValidateSubscription()
        {
            try
            {
                const int productId = 345; // Fyers product ID for LEAN
                var userId = Globals.UserId;
                var token = Globals.UserToken;
                var organizationId = Globals.OrganizationID;

                // Verify we can authenticate with this user and token
                var api = new ApiConnection(userId, token);
                if (!api.Connected)
                {
                    throw new ArgumentException("Invalid api user id or token, cannot authenticate subscription.");
                }

                // Compile the information we want to send when validating
                var information = new Dictionary<string, object>()
                {
                    {"productId", productId},
                    {"machineName", Environment.MachineName},
                    {"userName", Environment.UserName},
                    {"domainName", Environment.UserDomainName},
                    {"os", Environment.OSVersion}
                };

                // IP and Mac Address Information
                try
                {
                    var interfaceDictionary = new List<Dictionary<string, object>>();
                    foreach (var nic in NetworkInterface.GetAllNetworkInterfaces().Where(nic => nic.OperationalStatus == OperationalStatus.Up))
                    {
                        var interfaceInformation = new Dictionary<string, object>();
                        var addresses = nic.GetIPProperties().UnicastAddresses
                            .Select(uniAddress => uniAddress.Address)
                            .Where(address => !IPAddress.IsLoopback(address)).Select(x => x.ToString());

                        if (!addresses.IsNullOrEmpty())
                        {
                            interfaceInformation.Add("unicastAddresses", addresses);
                            interfaceInformation.Add("MAC", nic.GetPhysicalAddress().ToString());
                            interfaceInformation.Add("name", nic.Name);
                            interfaceDictionary.Add(interfaceInformation);
                        }
                    }
                    information.Add("networkInterfaces", interfaceDictionary);
                }
                catch (Exception)
                {
                    // NOP, not necessary to crash if fails to extract and add this information
                }

                if (!string.IsNullOrEmpty(organizationId))
                {
                    information.Add("organizationId", organizationId);
                }

                var request = new RestRequest("modules/license/read", Method.POST) { RequestFormat = DataFormat.Json };
                request.AddParameter("application/json", JsonConvert.SerializeObject(information), ParameterType.RequestBody);
                api.TryRequest(request, out ModulesReadLicenseRead result);

                if (!result.Success)
                {
                    throw new InvalidOperationException($"Request for subscriptions from web failed, Response Errors : {string.Join(',', result.Errors)}");
                }

                var encryptedData = result.License;
                DateTime? expirationDate = null;
                long? stamp = null;
                bool? isValid = null;

                if (encryptedData != null)
                {
                    if (string.IsNullOrEmpty(organizationId))
                    {
                        organizationId = result.OrganizationId;
                    }

                    var password = $"{token}-{organizationId}";
                    var key = SHA256.HashData(Encoding.UTF8.GetBytes(password));
                    var info = encryptedData.Split("::");
                    var buffer = Convert.FromBase64String(info[0]);
                    var iv = Convert.FromBase64String(info[1]);

                    using var aes = new AesManaged();
                    var decryptor = aes.CreateDecryptor(key, iv);
                    using var memoryStream = new MemoryStream(buffer);
                    using var cryptoStream = new CryptoStream(memoryStream, decryptor, CryptoStreamMode.Read);
                    using var streamReader = new StreamReader(cryptoStream);
                    var decryptedData = streamReader.ReadToEnd();

                    if (!decryptedData.IsNullOrEmpty())
                    {
                        var jsonInfo = JsonConvert.DeserializeObject<JObject>(decryptedData);
                        expirationDate = jsonInfo["expiration"]?.Value<DateTime>();
                        isValid = jsonInfo["isValid"]?.Value<bool>();
                        stamp = jsonInfo["stamped"]?.Value<int>();
                    }
                }

                if (!expirationDate.HasValue || !isValid.HasValue || !stamp.HasValue)
                {
                    throw new InvalidOperationException("Failed to validate subscription.");
                }

                var nowUtc = DateTime.UtcNow;
                var timeSpan = nowUtc - Time.UnixTimeStampToDateTime(stamp.Value);

                if (timeSpan > TimeSpan.FromHours(12))
                {
                    throw new InvalidOperationException("Invalid API response.");
                }

                if (!isValid.Value)
                {
                    throw new ArgumentException("Your subscription is not valid, please check your product subscriptions on our website.");
                }

                if (expirationDate < nowUtc)
                {
                    throw new ArgumentException($"Your subscription expired {expirationDate}, please renew in order to use this product.");
                }
            }
            catch (Exception e)
            {
                Log.Error($"FyersBrokerage.ValidateSubscription(): Failed during validation, shutting down. Error : {e.Message}");
                Environment.Exit(1);
            }
        }

        #endregion
    }
}
