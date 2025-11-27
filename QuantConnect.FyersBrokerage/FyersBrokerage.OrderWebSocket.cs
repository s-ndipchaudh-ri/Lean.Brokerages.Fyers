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
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Brokerages.Fyers.WebSocket;
using QuantConnect.Logging;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Fyers Brokerage - Order WebSocket implementation for real-time order updates
    /// </summary>
    public partial class FyersBrokerage
    {
        #region Order WebSocket Fields

        /// <summary>
        /// Separate WebSocket for order updates (Fyers has separate data and order streams)
        /// Uses custom wrapper with Authorization header authentication
        /// </summary>
        protected FyersOrderWebSocketWrapper? OrderWebSocket;

        /// <summary>
        /// Message handler for order WebSocket
        /// </summary>
        private BrokerageConcurrentMessageHandler<WebSocketClientWrapper.MessageData>? _orderMessageHandler;

        /// <summary>
        /// Flag to track if order WebSocket is enabled
        /// </summary>
        private bool _orderWebSocketEnabled;

        /// <summary>
        /// Reconnection timer for order WebSocket
        /// </summary>
        private Timer? _orderWebSocketReconnectTimer;

        /// <summary>
        /// Maximum reconnection attempts
        /// </summary>
        private const int MaxReconnectAttempts = 5;

        /// <summary>
        /// Current reconnection attempt count
        /// </summary>
        private int _orderWebSocketReconnectAttempts;

        /// <summary>
        /// Lock object for order WebSocket operations
        /// </summary>
        private readonly object _orderWebSocketLock = new object();

        #endregion

        #region Order WebSocket Initialization

        /// <summary>
        /// Initializes the order WebSocket for real-time order updates
        /// Uses FyersOrderWebSocketWrapper which sets the Authorization header properly
        /// </summary>
        private void InitializeOrderWebSocket()
        {
            if (string.IsNullOrEmpty(_clientId) || string.IsNullOrEmpty(_accessToken))
            {
                Log.Error("FyersBrokerage.InitializeOrderWebSocket: Cannot initialize - missing credentials");
                return;
            }

            lock (_orderWebSocketLock)
            {
                if (OrderWebSocket != null)
                {
                    return; // Already initialized
                }

                // Initialize message handler
                _orderMessageHandler = new BrokerageConcurrentMessageHandler<WebSocketClientWrapper.MessageData>(OnOrderWebSocketMessage);

                // Create WebSocket with Authorization header authentication
                // Format: CLIENT_ID:ACCESS_TOKEN
                var authToken = $"{_clientId}:{_accessToken}";
                OrderWebSocket = new FyersOrderWebSocketWrapper(authToken);

                // Wire up events
                OrderWebSocket.Message += OnOrderWebSocketRawMessage;
                OrderWebSocket.Open += OnOrderWebSocketOpen;
                OrderWebSocket.Closed += OnOrderWebSocketClosed;
                OrderWebSocket.Error += OnOrderWebSocketError;

                _orderWebSocketEnabled = true;
                Log.Trace("FyersBrokerage.InitializeOrderWebSocket: Order WebSocket initialized");
            }
        }

        /// <summary>
        /// Connects the order WebSocket
        /// </summary>
        public void ConnectOrderWebSocket()
        {
            if (!_orderWebSocketEnabled || OrderWebSocket == null)
            {
                InitializeOrderWebSocket();
            }

            if (OrderWebSocket == null)
            {
                Log.Error("FyersBrokerage.ConnectOrderWebSocket: Order WebSocket not initialized");
                return;
            }

            if (OrderWebSocket.IsOpen)
            {
                Log.Trace("FyersBrokerage.ConnectOrderWebSocket: Already connected");
                return;
            }

            Log.Trace("FyersBrokerage.ConnectOrderWebSocket: Connecting...");

            var resetEvent = new ManualResetEvent(false);
            EventHandler triggerEvent = (o, args) => resetEvent.Set();
            OrderWebSocket.Open += triggerEvent;

            try
            {
                OrderWebSocket.Connect();

                if (!resetEvent.WaitOne(ConnectionTimeout))
                {
                    Log.Error("FyersBrokerage.ConnectOrderWebSocket: Connection timeout");
                    throw new TimeoutException("Order WebSocket connection timeout");
                }
            }
            finally
            {
                OrderWebSocket.Open -= triggerEvent;
            }
        }

        /// <summary>
        /// Disconnects the order WebSocket
        /// </summary>
        public void DisconnectOrderWebSocket()
        {
            lock (_orderWebSocketLock)
            {
                _orderWebSocketReconnectTimer?.Dispose();
                _orderWebSocketReconnectTimer = null;

                if (OrderWebSocket != null)
                {
                    OrderWebSocket.Close();
                }
            }
        }

        #endregion

        #region Order WebSocket Event Handlers

        /// <summary>
        /// Called when order WebSocket opens
        /// </summary>
        private void OnOrderWebSocketOpen(object? sender, EventArgs e)
        {
            Log.Trace("FyersBrokerage.OnOrderWebSocketOpen: Order WebSocket connected");
            _orderWebSocketReconnectAttempts = 0;

            // Subscribe to order updates
            SubscribeToOrderUpdates();
        }

        /// <summary>
        /// Called when order WebSocket closes
        /// </summary>
        private void OnOrderWebSocketClosed(object? sender, WebSocketCloseData e)
        {
            Log.Trace($"FyersBrokerage.OnOrderWebSocketClosed: Order WebSocket closed - Code: {e.Code}, Reason: {e.Reason}");

            // Attempt reconnection
            ScheduleOrderWebSocketReconnect();
        }

        /// <summary>
        /// Called when order WebSocket error occurs
        /// </summary>
        private void OnOrderWebSocketError(object? sender, WebSocketError e)
        {
            Log.Error($"FyersBrokerage.OnOrderWebSocketError: {e.Message}");
            OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1, $"Order WebSocket error: {e.Message}"));
        }

        /// <summary>
        /// Handles raw WebSocket messages for orders
        /// </summary>
        private void OnOrderWebSocketRawMessage(object? sender, WebSocketMessage message)
        {
            _orderMessageHandler?.HandleNewMessage(message.Data);
        }

        /// <summary>
        /// Processes order WebSocket messages
        /// </summary>
        private void OnOrderWebSocketMessage(WebSocketClientWrapper.MessageData message)
        {
            try
            {
                if (message.MessageType == System.Net.WebSockets.WebSocketMessageType.Text)
                {
                    var textMessage = (WebSocketClientWrapper.TextMessage)message;
                    ProcessOrderWebSocketTextMessage(textMessage.Message);
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.OnOrderWebSocketMessage: Error processing message - {ex.Message}");
            }
        }

        #endregion

        #region Order WebSocket Message Processing

        /// <summary>
        /// Subscribes to order updates via WebSocket
        /// </summary>
        private void SubscribeToOrderUpdates()
        {
            if (OrderWebSocket == null || !OrderWebSocket.IsOpen)
            {
                return;
            }

            // Fyers order WebSocket subscription message
            var subscribeMessage = JsonConvert.SerializeObject(new
            {
                T = "SUB_ORD",
                SLIST = new[] { "orderUpdate", "tradeUpdate", "positionUpdate" },
                SUB_T = 1
            });

            OrderWebSocket.Send(subscribeMessage);
            Log.Trace("FyersBrokerage.SubscribeToOrderUpdates: Subscribed to order updates");
        }

        /// <summary>
        /// Processes text messages from order WebSocket
        /// </summary>
        private void ProcessOrderWebSocketTextMessage(string message)
        {
            try
            {
                var json = JObject.Parse(message);
                var messageType = json["T"]?.ToString() ?? json["type"]?.ToString();

                switch (messageType?.ToLowerInvariant())
                {
                    case "order":
                    case "ord":
                    case "orderupdate":
                        ProcessOrderUpdateMessage(json);
                        break;

                    case "trade":
                    case "trd":
                    case "tradeupdate":
                        ProcessTradeUpdateMessage(json);
                        break;

                    case "position":
                    case "pos":
                    case "positionupdate":
                        ProcessPositionUpdateMessage(json);
                        break;

                    case "error":
                        var errorMsg = json["message"]?.ToString() ?? json["data"]?.ToString() ?? "Unknown error";
                        Log.Error($"FyersBrokerage.ProcessOrderWebSocketTextMessage: Error - {errorMsg}");
                        OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1, $"Order WebSocket error: {errorMsg}"));
                        break;

                    case "connected":
                    case "success":
                        Log.Trace($"FyersBrokerage.ProcessOrderWebSocketTextMessage: {messageType} - {message}");
                        break;

                    default:
                        Log.Trace($"FyersBrokerage.ProcessOrderWebSocketTextMessage: Unknown message type '{messageType}': {message}");
                        break;
                }
            }
            catch (JsonException ex)
            {
                Log.Error($"FyersBrokerage.ProcessOrderWebSocketTextMessage: JSON parse error - {ex.Message}");
            }
        }

        /// <summary>
        /// Processes order update messages from WebSocket
        /// </summary>
        private void ProcessOrderUpdateMessage(JObject json)
        {
            try
            {
                var data = json["data"] ?? json["d"];
                if (data == null)
                {
                    Log.Trace("FyersBrokerage.ProcessOrderUpdateMessage: No data in message");
                    return;
                }

                var orderId = data["id"]?.ToString();
                if (string.IsNullOrEmpty(orderId))
                {
                    return;
                }

                Log.Trace($"FyersBrokerage.ProcessOrderUpdateMessage: Order update for {orderId}");

                // Find the cached order
                var order = CachedOrderIDs
                    .FirstOrDefault(o => o.Value.BrokerId.Contains(orderId))
                    .Value;

                if (order == null)
                {
                    Log.Trace($"FyersBrokerage.ProcessOrderUpdateMessage: Order not found in cache: {orderId}");
                    return;
                }

                var status = data["status"]?.Value<int>() ?? 0;
                var filledQty = data["filledQty"]?.Value<decimal>() ?? data["filled_qty"]?.Value<decimal>() ?? 0;
                var tradedPrice = data["tradedPrice"]?.Value<decimal>() ?? data["traded_price"]?.Value<decimal>() ?? 0;
                var message = data["message"]?.ToString() ?? string.Empty;

                // Handle different order statuses
                switch (status)
                {
                    case (int)FyersOrderStatus.Cancelled:
                        HandleOrderCancelled(order, orderId, message);
                        break;

                    case (int)FyersOrderStatus.Rejected:
                        HandleOrderRejected(order, orderId, message);
                        break;

                    case (int)FyersOrderStatus.Filled:
                    case (int)FyersOrderStatus.PartiallyFilled:
                        HandleOrderFill(order, orderId, filledQty, tradedPrice, status);
                        break;

                    case (int)FyersOrderStatus.Pending:
                        // Order is pending, no action needed
                        Log.Trace($"FyersBrokerage.ProcessOrderUpdateMessage: Order {orderId} is pending");
                        break;

                    default:
                        Log.Trace($"FyersBrokerage.ProcessOrderUpdateMessage: Unknown status {status} for order {orderId}");
                        break;
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.ProcessOrderUpdateMessage: Error - {ex.Message}");
            }
        }

        /// <summary>
        /// Handles order cancellation
        /// </summary>
        private void HandleOrderCancelled(Order order, string brokerId, string message)
        {
            CachedOrderIDs.TryRemove(order.Id, out _);
            _fills.TryRemove(order.Id, out _);

            OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, $"Fyers Order Cancelled: {message}")
            {
                Status = OrderStatus.Canceled
            });

            Log.Trace($"FyersBrokerage.HandleOrderCancelled: Order {order.Id} (Broker: {brokerId}) cancelled");
        }

        /// <summary>
        /// Handles order rejection
        /// </summary>
        private void HandleOrderRejected(Order order, string brokerId, string message)
        {
            CachedOrderIDs.TryRemove(order.Id, out _);
            _fills.TryRemove(order.Id, out _);

            OnOrderEvent(new OrderEvent(order, DateTime.UtcNow, OrderFee.Zero, $"Fyers Order Rejected: {message}")
            {
                Status = OrderStatus.Invalid
            });

            Log.Trace($"FyersBrokerage.HandleOrderRejected: Order {order.Id} (Broker: {brokerId}) rejected: {message}");
        }

        /// <summary>
        /// Handles order fill
        /// </summary>
        private void HandleOrderFill(Order order, string brokerId, decimal filledQty, decimal tradedPrice, int status)
        {
            var direction = order.Direction;
            var cumulativeFillQuantity = direction == OrderDirection.Buy ? filledQty : -filledQty;

            // Get fee
            var security = _securityProvider?.GetSecurity(order.Symbol);
            var orderFee = security?.FeeModel.GetOrderFee(new OrderFeeParameters(security, order)) ?? OrderFee.Zero;

            var orderStatus = status == (int)FyersOrderStatus.Filled
                ? OrderStatus.Filled
                : OrderStatus.PartiallyFilled;

            // Track fills to avoid duplicate events
            _fills.TryGetValue(order.Id, out var previousFillQuantity);

            if (Math.Abs(cumulativeFillQuantity) <= Math.Abs(previousFillQuantity))
            {
                // Already processed this fill
                return;
            }

            _fills[order.Id] = cumulativeFillQuantity;
            var fillQuantityInThisEvent = cumulativeFillQuantity - previousFillQuantity;

            var orderEvent = new OrderEvent(
                order.Id,
                order.Symbol,
                DateTime.UtcNow,
                orderStatus,
                direction,
                tradedPrice,
                fillQuantityInThisEvent,
                orderFee,
                $"Fyers Order Fill"
            );

            if (orderStatus == OrderStatus.Filled)
            {
                CachedOrderIDs.TryRemove(order.Id, out _);
                _fills.TryRemove(order.Id, out _);
            }

            OnOrderEvent(orderEvent);
            Log.Trace($"FyersBrokerage.HandleOrderFill: Order {order.Id} filled {fillQuantityInThisEvent} @ {tradedPrice}");
        }

        /// <summary>
        /// Processes trade update messages from WebSocket
        /// </summary>
        private void ProcessTradeUpdateMessage(JObject json)
        {
            try
            {
                var data = json["data"] ?? json["d"];
                if (data == null)
                {
                    return;
                }

                var orderId = data["id"]?.ToString() ?? data["orderId"]?.ToString();
                var tradeNumber = data["tradeNumber"]?.ToString();
                var tradedPrice = data["tradedPrice"]?.Value<decimal>() ?? 0;
                var quantity = data["qty"]?.Value<int>() ?? 0;

                Log.Trace($"FyersBrokerage.ProcessTradeUpdateMessage: Trade {tradeNumber} for order {orderId} - {quantity} @ {tradedPrice}");

                // Trade updates are typically handled by order updates
                // This is for additional logging/tracking
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.ProcessTradeUpdateMessage: Error - {ex.Message}");
            }
        }

        /// <summary>
        /// Processes position update messages from WebSocket
        /// </summary>
        private void ProcessPositionUpdateMessage(JObject json)
        {
            try
            {
                var data = json["data"] ?? json["d"];
                if (data == null)
                {
                    return;
                }

                var symbol = data["symbol"]?.ToString();
                var netQty = data["netQty"]?.Value<int>() ?? data["qty"]?.Value<int>() ?? 0;
                var netAvg = data["netAvg"]?.Value<decimal>() ?? 0;

                Log.Trace($"FyersBrokerage.ProcessPositionUpdateMessage: Position update - {symbol}: {netQty} @ {netAvg}");

                // Position updates can be used to trigger portfolio reconciliation
                // For now, just log the update
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.ProcessPositionUpdateMessage: Error - {ex.Message}");
            }
        }

        #endregion

        #region Order WebSocket Reconnection

        /// <summary>
        /// Schedules a reconnection attempt for the order WebSocket
        /// </summary>
        private void ScheduleOrderWebSocketReconnect()
        {
            if (!_orderWebSocketEnabled)
            {
                return;
            }

            lock (_orderWebSocketLock)
            {
                if (_orderWebSocketReconnectAttempts >= MaxReconnectAttempts)
                {
                    Log.Error($"FyersBrokerage.ScheduleOrderWebSocketReconnect: Max reconnection attempts ({MaxReconnectAttempts}) reached");
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Error, -1,
                        "Order WebSocket reconnection failed after maximum attempts"));
                    return;
                }

                _orderWebSocketReconnectAttempts++;
                var delay = TimeSpan.FromSeconds(Math.Pow(2, _orderWebSocketReconnectAttempts)); // Exponential backoff

                Log.Trace($"FyersBrokerage.ScheduleOrderWebSocketReconnect: Scheduling reconnect attempt {_orderWebSocketReconnectAttempts} in {delay.TotalSeconds}s");

                _orderWebSocketReconnectTimer?.Dispose();
                _orderWebSocketReconnectTimer = new Timer(
                    _ => AttemptOrderWebSocketReconnect(),
                    null,
                    delay,
                    Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Attempts to reconnect the order WebSocket
        /// </summary>
        private void AttemptOrderWebSocketReconnect()
        {
            try
            {
                Log.Trace($"FyersBrokerage.AttemptOrderWebSocketReconnect: Attempting reconnection (attempt {_orderWebSocketReconnectAttempts})");

                // Dispose old WebSocket and create new one
                lock (_orderWebSocketLock)
                {
                    if (OrderWebSocket != null)
                    {
                        OrderWebSocket.Message -= OnOrderWebSocketRawMessage;
                        OrderWebSocket.Open -= OnOrderWebSocketOpen;
                        OrderWebSocket.Closed -= OnOrderWebSocketClosed;
                        OrderWebSocket.Error -= OnOrderWebSocketError;

                        try { OrderWebSocket.Dispose(); } catch { }
                        OrderWebSocket = null;
                    }
                }

                // Reinitialize and connect
                InitializeOrderWebSocket();
                ConnectOrderWebSocket();

                Log.Trace("FyersBrokerage.AttemptOrderWebSocketReconnect: Reconnection successful");
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.AttemptOrderWebSocketReconnect: Reconnection failed - {ex.Message}");
                ScheduleOrderWebSocketReconnect();
            }
        }

        #endregion
    }
}
