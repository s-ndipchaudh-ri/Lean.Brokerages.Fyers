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
using QuantConnect.Brokerages.Fyers.WebSocket;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Fyers Brokerage - WebSocket reconnection logic
    /// </summary>
    public partial class FyersBrokerage
    {
        #region Data WebSocket Reconnection Fields

        /// <summary>
        /// Timer for data WebSocket reconnection
        /// </summary>
        private Timer? _dataWebSocketReconnectTimer;

        /// <summary>
        /// Current reconnection attempt count for data WebSocket
        /// </summary>
        private int _dataWebSocketReconnectAttempts;

        /// <summary>
        /// Maximum reconnection attempts for data WebSocket
        /// </summary>
        private const int MaxDataReconnectAttempts = 10;

        /// <summary>
        /// Lock object for data WebSocket operations
        /// </summary>
        private readonly object _dataWebSocketLock = new object();

        /// <summary>
        /// Flag indicating if reconnection is in progress
        /// </summary>
        private bool _isReconnecting;

        /// <summary>
        /// Heartbeat timer
        /// </summary>
        private Timer? _heartbeatTimer;

        /// <summary>
        /// Last message time for heartbeat tracking
        /// </summary>
        private DateTime _lastDataMessageTime = DateTime.UtcNow;

        #endregion

        #region Heartbeat & Connection Monitoring

        /// <summary>
        /// Starts the heartbeat timer to monitor connection health
        /// </summary>
        private void StartHeartbeatTimer()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = new Timer(
                CheckConnectionHealth,
                null,
                TimeSpan.FromSeconds(FyersConstants.WebSocketHeartbeatIntervalMs / 1000),
                TimeSpan.FromSeconds(FyersConstants.WebSocketHeartbeatIntervalMs / 1000));
        }

        /// <summary>
        /// Stops the heartbeat timer
        /// </summary>
        private void StopHeartbeatTimer()
        {
            _heartbeatTimer?.Dispose();
            _heartbeatTimer = null;
        }

        /// <summary>
        /// Checks connection health and triggers reconnection if needed
        /// </summary>
        private void CheckConnectionHealth(object? state)
        {
            try
            {
                // Check data WebSocket
                if (WebSocket != null && !WebSocket.IsOpen && !_isReconnecting)
                {
                    Log.Trace("FyersBrokerage.CheckConnectionHealth: Data WebSocket disconnected, scheduling reconnect");
                    ScheduleDataWebSocketReconnect();
                }

                // Check order WebSocket
                if (_orderWebSocketEnabled && OrderWebSocket != null && !OrderWebSocket.IsOpen)
                {
                    Log.Trace("FyersBrokerage.CheckConnectionHealth: Order WebSocket disconnected, scheduling reconnect");
                    ScheduleOrderWebSocketReconnect();
                }

                // Check for stale connection (no messages in 60 seconds)
                var timeSinceLastMessage = DateTime.UtcNow - _lastDataMessageTime;
                if (timeSinceLastMessage > TimeSpan.FromSeconds(60) && _subscribedSymbols.Count > 0)
                {
                    Log.Trace($"FyersBrokerage.CheckConnectionHealth: No data received in {timeSinceLastMessage.TotalSeconds}s");

                    // Send a ping or resubscribe to check if connection is alive
                    if (WebSocket?.IsOpen == true)
                    {
                        ResubscribeToSymbols();
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.CheckConnectionHealth: Error - {ex.Message}");
            }
        }

        /// <summary>
        /// Updates the last message time
        /// </summary>
        private void UpdateLastMessageTime()
        {
            _lastDataMessageTime = DateTime.UtcNow;
        }

        #endregion

        #region Data WebSocket Reconnection

        /// <summary>
        /// Schedules a reconnection attempt for the data WebSocket
        /// </summary>
        private void ScheduleDataWebSocketReconnect()
        {
            lock (_dataWebSocketLock)
            {
                if (_isReconnecting)
                {
                    return; // Already reconnecting
                }

                if (_dataWebSocketReconnectAttempts >= MaxDataReconnectAttempts)
                {
                    Log.Error($"FyersBrokerage.ScheduleDataWebSocketReconnect: Max reconnection attempts ({MaxDataReconnectAttempts}) reached");
                    OnMessage(new BrokerageMessageEvent(BrokerageMessageType.Disconnect, -1,
                        "Data WebSocket reconnection failed after maximum attempts. Please restart the algorithm."));
                    return;
                }

                _isReconnecting = true;
                _dataWebSocketReconnectAttempts++;

                // Exponential backoff with jitter
                var baseDelay = Math.Pow(2, Math.Min(_dataWebSocketReconnectAttempts, 6));
                var jitter = new Random().NextDouble() * 0.5 + 0.75; // 0.75 to 1.25
                var delay = TimeSpan.FromSeconds(baseDelay * jitter);

                Log.Trace($"FyersBrokerage.ScheduleDataWebSocketReconnect: Scheduling reconnect attempt {_dataWebSocketReconnectAttempts} in {delay.TotalSeconds:F1}s");

                _dataWebSocketReconnectTimer?.Dispose();
                _dataWebSocketReconnectTimer = new Timer(
                    _ => AttemptDataWebSocketReconnect(),
                    null,
                    delay,
                    Timeout.InfiniteTimeSpan);
            }
        }

        /// <summary>
        /// Attempts to reconnect the data WebSocket
        /// </summary>
        private void AttemptDataWebSocketReconnect()
        {
            try
            {
                Log.Trace($"FyersBrokerage.AttemptDataWebSocketReconnect: Attempting reconnection (attempt {_dataWebSocketReconnectAttempts})");

                // Close existing connection
                if (WebSocket != null)
                {
                    try
                    {
                        WebSocket.Message -= OnMessage;
                        WebSocket.Open -= (s, e) => { };
                        WebSocket.Error -= OnError;
                        WebSocket.Close();
                    }
                    catch { }
                }

                // Create new WebSocket with binary authentication
                WebSocket = new FyersDataWebSocketWrapper(_accessToken);
                _wssUrl = FyersConstants.DataWebSocketUrl;
                WebSocket.Message += OnMessage;
                WebSocket.Open += (sender, args) =>
                {
                    Log.Trace("FyersBrokerage.DataWebSocket: Reconnected, waiting for authentication...");
                    _dataWebSocketReconnectAttempts = 0;
                    _isReconnecting = false;
                    // Note: ResubscribeToSymbols will be called after authentication is confirmed
                };
                WebSocket.Error += OnError;
                WebSocket.Closed += OnDataWebSocketClosed;

                // Connect
                var resetEvent = new ManualResetEvent(false);
                EventHandler triggerEvent = (o, args) => resetEvent.Set();
                WebSocket.Open += triggerEvent;

                WebSocket.Connect();

                if (!resetEvent.WaitOne(ConnectionTimeout))
                {
                    Log.Error("FyersBrokerage.AttemptDataWebSocketReconnect: Connection timeout");
                    throw new TimeoutException("Data WebSocket reconnection timeout");
                }

                WebSocket.Open -= triggerEvent;
                Log.Trace("FyersBrokerage.AttemptDataWebSocketReconnect: Reconnection successful");
            }
            catch (Exception ex)
            {
                Log.Error($"FyersBrokerage.AttemptDataWebSocketReconnect: Reconnection failed - {ex.Message}");
                _isReconnecting = false;
                ScheduleDataWebSocketReconnect();
            }
        }

        /// <summary>
        /// Called when data WebSocket closes
        /// </summary>
        private void OnDataWebSocketClosed(object? sender, WebSocketCloseData e)
        {
            Log.Trace($"FyersBrokerage.OnDataWebSocketClosed: Data WebSocket closed - Code: {e.Code}, Reason: {e.Reason}");

            if (!_isReconnecting)
            {
                ScheduleDataWebSocketReconnect();
            }
        }

        /// <summary>
        /// Resubscribes to all previously subscribed symbols
        /// </summary>
        private void ResubscribeToSymbols()
        {
            if (WebSocket == null || !WebSocket.IsOpen)
            {
                return;
            }

            if (_subscribedSymbols.Count == 0)
            {
                return;
            }

            Log.Trace($"FyersBrokerage.ResubscribeToSymbols: Resubscribing to {_subscribedSymbols.Count} symbols");

            var subscribeMessage = FyersWebSocketHelper.CreateSubscribeMessage(
                _subscribedSymbols.ToArray(),
                FyersWebSocketSubscriptionType.SymbolUpdate);
            WebSocket.Send(subscribeMessage);
        }

        #endregion

        #region Connection Management

        /// <summary>
        /// Enhanced Connect method with reconnection setup
        /// </summary>
        public void ConnectWithReconnection()
        {
            Connect();
            StartHeartbeatTimer();

            // Also connect order WebSocket if initialized
            if (_orderWebSocketEnabled)
            {
                try
                {
                    ConnectOrderWebSocket();
                }
                catch (Exception ex)
                {
                    Log.Error($"FyersBrokerage.ConnectWithReconnection: Failed to connect order WebSocket - {ex.Message}");
                }
            }
        }

        /// <summary>
        /// Disconnects all WebSockets and stops reconnection
        /// </summary>
        public void DisconnectAll()
        {
            StopHeartbeatTimer();

            _dataWebSocketReconnectTimer?.Dispose();
            _dataWebSocketReconnectTimer = null;

            DisconnectOrderWebSocket();
            Disconnect();
        }

        /// <summary>
        /// Resets reconnection counters (call after successful connection)
        /// </summary>
        private void ResetReconnectionCounters()
        {
            _dataWebSocketReconnectAttempts = 0;
            _orderWebSocketReconnectAttempts = 0;
            _isReconnecting = false;
        }

        #endregion
    }
}
