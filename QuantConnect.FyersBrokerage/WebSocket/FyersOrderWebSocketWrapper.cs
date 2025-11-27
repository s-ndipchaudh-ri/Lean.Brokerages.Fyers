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
using System.Net.WebSockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Brokerages;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Fyers.WebSocket
{
    /// <summary>
    /// Custom WebSocket wrapper for Fyers Order WebSocket that supports Authorization header
    /// </summary>
    public class FyersOrderWebSocketWrapper : IWebSocket, IDisposable
    {
        private readonly string _url;
        private readonly string _accessToken;
        private ClientWebSocket? _client;
        private CancellationTokenSource? _cts;
        private Task? _taskConnect;
        private readonly object _locker = new object();
        private readonly object _connectLock = new object();

        /// <summary>
        /// Event fired when WebSocket is opened
        /// </summary>
        public event EventHandler? Open;

        /// <summary>
        /// Event fired when WebSocket is closed
        /// </summary>
        public event EventHandler<WebSocketCloseData>? Closed;

        /// <summary>
        /// Event fired when a message is received
        /// </summary>
        public event EventHandler<WebSocketMessage>? Message;

        /// <summary>
        /// Event fired when an error occurs
        /// </summary>
        public event EventHandler<WebSocketError>? Error;

        /// <summary>
        /// Returns true if the WebSocket is open
        /// </summary>
        public bool IsOpen => _client?.State == WebSocketState.Open;

        /// <summary>
        /// Creates a new instance of FyersOrderWebSocketWrapper
        /// </summary>
        /// <param name="accessToken">The Fyers access token</param>
        public FyersOrderWebSocketWrapper(string accessToken)
        {
            _url = FyersConstants.OrderWebSocketUrl;
            _accessToken = accessToken;
        }

        /// <summary>
        /// Initialize the WebSocket (required by IWebSocket interface but not used for this wrapper)
        /// </summary>
        public void Initialize(string url, string sessionToken = null)
        {
            // This wrapper uses constructor initialization instead
            // URL and token are already set in constructor
        }

        /// <summary>
        /// Connects to the WebSocket
        /// </summary>
        public void Connect()
        {
            lock (_connectLock)
            {
                lock (_locker)
                {
                    if (_cts == null)
                    {
                        _cts = new CancellationTokenSource();
                        _client = null;

                        _taskConnect = Task.Factory.StartNew(
                            () =>
                            {
                                Log.Trace($"FyersOrderWebSocketWrapper: Connection task started: {_url}");

                                try
                                {
                                    HandleConnection();
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, $"FyersOrderWebSocketWrapper: Error in connection task: {_url}");
                                }

                                Log.Trace($"FyersOrderWebSocketWrapper: Connection task ended: {_url}");
                            },
                            _cts.Token, TaskCreationOptions.LongRunning, TaskScheduler.Default);
                    }
                }
            }
        }

        /// <summary>
        /// Closes the WebSocket connection
        /// </summary>
        public void Close()
        {
            try
            {
                lock (_locker)
                {
                    _cts?.Cancel();

                    if (_taskConnect != null)
                    {
                        var task = _taskConnect;
                        task.Wait(TimeSpan.FromSeconds(5));
                        _taskConnect = null;
                    }

                    _client.DisposeSafely();
                    _client = null;
                    _cts.DisposeSafely();
                    _cts = null;
                }
            }
            catch (Exception e)
            {
                Log.Error($"FyersOrderWebSocketWrapper.Close(): Error - {e.Message}");
            }
        }

        /// <summary>
        /// Sends a message through the WebSocket
        /// </summary>
        public void Send(string data)
        {
            lock (_locker)
            {
                if (_client?.State == WebSocketState.Open)
                {
                    var buffer = new ArraySegment<byte>(Encoding.UTF8.GetBytes(data));
                    _client.SendAsync(buffer, WebSocketMessageType.Text, true, _cts?.Token ?? CancellationToken.None)
                        .SynchronouslyAwaitTask();
                }
            }
        }

        private void HandleConnection()
        {
            var receiveBuffer = new byte[65536];

            while (_cts != null && !_cts.IsCancellationRequested)
            {
                Log.Trace($"FyersOrderWebSocketWrapper: Connecting to {_url}...");

                const int maximumWaitTimeOnError = 120 * 1000;
                const int minimumWaitTimeOnError = 2 * 1000;
                var waitTimeOnError = minimumWaitTimeOnError;

                using (var connectionCts = CancellationTokenSource.CreateLinkedTokenSource(_cts.Token))
                {
                    try
                    {
                        lock (_locker)
                        {
                            _client.DisposeSafely();
                            _client = new ClientWebSocket();

                            // Set the Authorization header as required by Fyers
                            _client.Options.SetRequestHeader("Authorization", _accessToken);

                            _client.ConnectAsync(new Uri(_url), connectionCts.Token).SynchronouslyAwaitTask();
                        }

                        OnOpen();

                        while ((_client.State == WebSocketState.Open || _client.State == WebSocketState.CloseSent) &&
                            !connectionCts.IsCancellationRequested)
                        {
                            var messageData = ReceiveMessage(_client, connectionCts.Token, receiveBuffer);

                            if (messageData == null)
                            {
                                break;
                            }

                            waitTimeOnError = minimumWaitTimeOnError;
                            OnMessage(new WebSocketMessage((IWebSocket)this, messageData));
                        }
                    }
                    catch (OperationCanceledException) { }
                    catch (ObjectDisposedException) { }
                    catch (WebSocketException ex)
                    {
                        if (!connectionCts.IsCancellationRequested)
                        {
                            OnError(new WebSocketError(ex.Message, ex));
                            connectionCts.Token.WaitHandle.WaitOne(waitTimeOnError);
                            waitTimeOnError += Math.Min(maximumWaitTimeOnError, waitTimeOnError);
                        }
                    }
                    catch (Exception ex)
                    {
                        if (!connectionCts.IsCancellationRequested)
                        {
                            OnError(new WebSocketError(ex.Message, ex));
                        }
                    }

                    if (!connectionCts.IsCancellationRequested)
                    {
                        connectionCts.Cancel();
                        OnClose(new WebSocketCloseData(0, string.Empty, true));
                    }
                }
            }
        }

        private WebSocketClientWrapper.MessageData? ReceiveMessage(
            ClientWebSocket client,
            CancellationToken token,
            byte[] receiveBuffer)
        {
            var buffer = new ArraySegment<byte>(receiveBuffer);
            WebSocketReceiveResult result;

            using (var ms = new System.IO.MemoryStream())
            {
                do
                {
                    result = client.ReceiveAsync(buffer, token).SynchronouslyAwaitTask();
                    ms.Write(buffer.Array!, buffer.Offset, result.Count);
                }
                while (!result.EndOfMessage);

                if (result.MessageType == WebSocketMessageType.Close)
                {
                    return null;
                }

                ms.Seek(0, System.IO.SeekOrigin.Begin);

                if (result.MessageType == WebSocketMessageType.Text)
                {
                    using (var reader = new System.IO.StreamReader(ms, Encoding.UTF8))
                    {
                        return new WebSocketClientWrapper.TextMessage { Message = reader.ReadToEnd() };
                    }
                }
                else
                {
                    return new WebSocketClientWrapper.BinaryMessage { Data = ms.ToArray() };
                }
            }
        }

        private void OnOpen()
        {
            Log.Trace("FyersOrderWebSocketWrapper: Connection opened");
            Open?.Invoke(this, EventArgs.Empty);
        }

        private void OnClose(WebSocketCloseData e)
        {
            Log.Trace($"FyersOrderWebSocketWrapper: Connection closed - {e.Reason}");
            Closed?.Invoke(this, e);
        }

        private void OnMessage(WebSocketMessage e)
        {
            Message?.Invoke(this, e);
        }

        private void OnError(WebSocketError e)
        {
            Log.Error($"FyersOrderWebSocketWrapper: Error - {e.Message}");
            Error?.Invoke(this, e);
        }

        /// <summary>
        /// Disposes the WebSocket wrapper
        /// </summary>
        public void Dispose()
        {
            Close();
        }
    }
}
