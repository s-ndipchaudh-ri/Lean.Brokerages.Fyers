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
using System.Buffers.Binary;
using System.Collections.Concurrent;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using QuantConnect.Brokerages;
using QuantConnect.Logging;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Fyers.WebSocket
{
    /// <summary>
    /// Custom WebSocket wrapper for Fyers Data WebSocket with binary authentication protocol
    /// </summary>
    public class FyersDataWebSocketWrapper : IWebSocket, IDisposable
    {
        private readonly string _url;
        private readonly string _hsmKey;
        private readonly string _source;
        private ClientWebSocket? _client;
        private CancellationTokenSource? _cts;
        private Task? _taskConnect;
        private Task? _pingTask;
        private readonly object _locker = new object();
        private readonly object _connectLock = new object();
        private readonly ConcurrentQueue<byte[]> _messageQueue = new ConcurrentQueue<byte[]>();
        private bool _isAuthenticated;
        private int _ackCount;
        private int _updateCount;

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
        /// Returns true if the connection is authenticated
        /// </summary>
        public bool IsAuthenticated => _isAuthenticated;

        /// <summary>
        /// Creates a new instance of FyersDataWebSocketWrapper
        /// </summary>
        /// <param name="accessToken">The Fyers JWT access token</param>
        public FyersDataWebSocketWrapper(string accessToken)
        {
            _url = FyersConstants.DataWebSocketUrl;
            _hsmKey = ExtractHsmKeyFromToken(accessToken);
            _source = "LEAN-Brokerage-1.0";
        }

        /// <summary>
        /// Extract hsm_key from JWT access token
        /// </summary>
        private string ExtractHsmKeyFromToken(string accessToken)
        {
            try
            {
                // Remove client_id prefix if present (format: CLIENT_ID:JWT)
                if (accessToken.Contains(":"))
                {
                    accessToken = accessToken.Split(':')[1];
                }

                // JWT format: header.payload.signature
                var parts = accessToken.Split('.');
                if (parts.Length != 3)
                {
                    throw new ArgumentException("Invalid JWT format");
                }

                // Decode the payload (second part)
                var payload = parts[1];
                // Add padding if needed
                var paddedPayload = payload.PadRight((payload.Length + 3) / 4 * 4, '=');
                // Convert from URL-safe base64 to standard base64
                paddedPayload = paddedPayload.Replace('-', '+').Replace('_', '/');

                var decodedBytes = Convert.FromBase64String(paddedPayload);
                var decodedPayload = Encoding.UTF8.GetString(decodedBytes);

                using var jsonDoc = JsonDocument.Parse(decodedPayload);
                if (jsonDoc.RootElement.TryGetProperty("hsm_key", out var hsmKeyElement))
                {
                    return hsmKeyElement.GetString() ?? throw new ArgumentException("hsm_key is null");
                }

                throw new ArgumentException("hsm_key not found in JWT payload");
            }
            catch (Exception ex)
            {
                Log.Error($"FyersDataWebSocketWrapper: Failed to extract hsm_key from token - {ex.Message}");
                throw new ArgumentException($"Failed to extract hsm_key from access token: {ex.Message}", ex);
            }
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
                        _isAuthenticated = false;
                        _cts = new CancellationTokenSource();
                        _client = null;

                        _taskConnect = Task.Factory.StartNew(
                            () =>
                            {
                                Log.Trace($"FyersDataWebSocketWrapper: Connection task started: {_url}");

                                try
                                {
                                    HandleConnection();
                                }
                                catch (Exception e)
                                {
                                    Log.Error(e, $"FyersDataWebSocketWrapper: Error in connection task: {_url}");
                                }

                                Log.Trace($"FyersDataWebSocketWrapper: Connection task ended: {_url}");
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
                    _isAuthenticated = false;
                }
            }
            catch (Exception e)
            {
                Log.Error($"FyersDataWebSocketWrapper.Close(): Error - {e.Message}");
            }
        }

        /// <summary>
        /// Sends a text message through the WebSocket (used for subscription messages)
        /// Note: Data WebSocket uses binary protocol, but subscriptions may use text
        /// </summary>
        public void Send(string data)
        {
            // For data WebSocket, we need to convert subscription messages to binary format
            Log.Trace($"FyersDataWebSocketWrapper.Send: Received text message (will need binary conversion): {data}");
            // For now, queue the binary message - actual implementation will convert
            var buffer = Encoding.UTF8.GetBytes(data);
            SendBinary(buffer);
        }

        /// <summary>
        /// Sends a binary message through the WebSocket
        /// </summary>
        public void SendBinary(byte[] data)
        {
            lock (_locker)
            {
                if (_client?.State == WebSocketState.Open)
                {
                    var buffer = new ArraySegment<byte>(data);
                    _client.SendAsync(buffer, WebSocketMessageType.Binary, true, _cts?.Token ?? CancellationToken.None)
                        .SynchronouslyAwaitTask();
                }
            }
        }

        private void HandleConnection()
        {
            var receiveBuffer = new byte[65536];

            while (_cts != null && !_cts.IsCancellationRequested)
            {
                Log.Trace($"FyersDataWebSocketWrapper: Connecting to {_url}...");

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
                            _isAuthenticated = false;

                            // Connect without any authentication headers - auth is done via binary message
                            _client.ConnectAsync(new Uri(_url), connectionCts.Token).SynchronouslyAwaitTask();
                        }

                        Log.Trace("FyersDataWebSocketWrapper: WebSocket connected, sending authentication...");

                        // Send binary authentication message
                        SendAuthenticationMessage();

                        // Start ping task
                        _pingTask = Task.Run(() => PingLoop(connectionCts.Token), connectionCts.Token);

                        // Start message sender task
                        var senderTask = Task.Run(() => MessageSenderLoop(connectionCts.Token), connectionCts.Token);

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

        /// <summary>
        /// Sends the binary authentication message as per Fyers protocol
        /// </summary>
        private void SendAuthenticationMessage()
        {
            try
            {
                var mode = "P"; // P for Production
                var channelFlag = (byte)1;

                // Calculate buffer size
                var bufferSize = 18 + _hsmKey.Length + _source.Length;

                var buffer = new byte[bufferSize];
                var offset = 0;

                // Data length (2 bytes, big endian) - excludes the length field itself
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)(bufferSize - 2));
                offset += 2;

                // Request Type = 1 (authentication)
                buffer[offset++] = 1;

                // Field Count = 4
                buffer[offset++] = 4;

                // Field-1: AuthToken (hsm_key)
                buffer[offset++] = 1; // Field ID
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)_hsmKey.Length);
                offset += 2;
                Encoding.UTF8.GetBytes(_hsmKey, 0, _hsmKey.Length, buffer, offset);
                offset += _hsmKey.Length;

                // Field-2: Mode
                buffer[offset++] = 2; // Field ID
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), 1);
                offset += 2;
                buffer[offset++] = (byte)mode[0];

                // Field-3: Channel flag
                buffer[offset++] = 3; // Field ID
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), 1);
                offset += 2;
                buffer[offset++] = channelFlag;

                // Field-4: Source
                buffer[offset++] = 4; // Field ID
                BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)_source.Length);
                offset += 2;
                Encoding.UTF8.GetBytes(_source, 0, _source.Length, buffer, offset);

                Log.Trace($"FyersDataWebSocketWrapper: Sending authentication message ({buffer.Length} bytes)");
                SendBinary(buffer);
            }
            catch (Exception ex)
            {
                Log.Error($"FyersDataWebSocketWrapper: Failed to send authentication message - {ex.Message}");
                throw;
            }
        }

        /// <summary>
        /// Creates binary subscription message for symbols
        /// </summary>
        public byte[] CreateSubscriptionMessage(string[] hsmSymbols, int channelNum = 11)
        {
            // Build scrips data
            var scripsData = new System.IO.MemoryStream();

            // Symbol count (2 bytes, big endian)
            var countBytes = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(countBytes, (ushort)hsmSymbols.Length);
            scripsData.Write(countBytes, 0, 2);

            foreach (var scrip in hsmSymbols)
            {
                var scripBytes = Encoding.ASCII.GetBytes(scrip);
                scripsData.WriteByte((byte)scripBytes.Length);
                scripsData.Write(scripBytes, 0, scripBytes.Length);
            }

            var scripsDataArray = scripsData.ToArray();

            // Build full message
            var dataLen = 6 + scripsDataArray.Length;
            var buffer = new byte[dataLen];
            var offset = 0;

            // Data length
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)dataLen);
            offset += 2;

            // Request type = 4 (subscription)
            buffer[offset++] = 4;

            // Field count = 2
            buffer[offset++] = 2;

            // Field-1: Scrips data
            buffer[offset++] = 1; // Field ID
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), (ushort)scripsDataArray.Length);
            offset += 2;
            Array.Copy(scripsDataArray, 0, buffer, offset, scripsDataArray.Length);
            offset += scripsDataArray.Length;

            // Field-2: Channel number
            buffer[offset++] = 2; // Field ID
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), 1);
            offset += 2;
            buffer[offset++] = (byte)channelNum;

            return buffer;
        }

        /// <summary>
        /// Creates binary full mode message for a channel
        /// </summary>
        public byte[] CreateFullModeMessage(int channelNum = 11)
        {
            var data = new System.IO.MemoryStream();

            // Data length placeholder (will be 0 for now, actual is calculated)
            data.Write(new byte[] { 0, 0 }, 0, 2);

            // Request type = 12 (mode change)
            data.WriteByte(12);

            // Field count = 2
            data.WriteByte(2);

            // Field-1: Channel bits (8 bytes)
            data.WriteByte(1); // Field ID
            var field1SizeBytes = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(field1SizeBytes, 8);
            data.Write(field1SizeBytes, 0, 2);

            // Channel bits - set bit for channelNum
            ulong channelBits = 1UL << channelNum;
            var channelBitsBytes = new byte[8];
            BinaryPrimitives.WriteUInt64BigEndian(channelBitsBytes, channelBits);
            data.Write(channelBitsBytes, 0, 8);

            // Field-2: Mode (F = Full, L = Lite)
            data.WriteByte(2); // Field ID
            var field2SizeBytes = new byte[2];
            BinaryPrimitives.WriteUInt16BigEndian(field2SizeBytes, 1);
            data.Write(field2SizeBytes, 0, 2);
            data.WriteByte(70); // 'F' for Full mode

            return data.ToArray();
        }

        /// <summary>
        /// Creates acknowledgement message
        /// </summary>
        private byte[] CreateAcknowledgementMessage(int messageNumber)
        {
            var buffer = new byte[11];
            var offset = 0;

            // Total size - 2
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), 9);
            offset += 2;

            // Request type = 3
            buffer[offset++] = 3;

            // Field count = 1
            buffer[offset++] = 1;

            // Field ID = 1
            buffer[offset++] = 1;

            // Field size = 4
            BinaryPrimitives.WriteUInt16BigEndian(buffer.AsSpan(offset), 4);
            offset += 2;

            // Field value = message number
            BinaryPrimitives.WriteUInt32BigEndian(buffer.AsSpan(offset), (uint)messageNumber);

            return buffer;
        }

        private void PingLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _client?.State == WebSocketState.Open)
                {
                    // Binary ping: bytes([0, 1, 11])
                    var pingMessage = new byte[] { 0, 1, 11 };
                    _messageQueue.Enqueue(pingMessage);
                    token.WaitHandle.WaitOne(10000); // Ping every 10 seconds
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error($"FyersDataWebSocketWrapper: Ping loop error - {ex.Message}");
            }
        }

        private void MessageSenderLoop(CancellationToken token)
        {
            try
            {
                while (!token.IsCancellationRequested && _client?.State == WebSocketState.Open)
                {
                    if (_messageQueue.TryDequeue(out var message))
                    {
                        SendBinary(message);
                    }
                    else
                    {
                        token.WaitHandle.WaitOne(50); // Small delay when queue is empty
                    }
                }
            }
            catch (OperationCanceledException) { }
            catch (Exception ex)
            {
                Log.Error($"FyersDataWebSocketWrapper: Message sender loop error - {ex.Message}");
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

                var data = ms.ToArray();

                // Process binary response for authentication status
                if (data.Length >= 3)
                {
                    var responseType = data[2];
                    if (responseType == 1) // Authentication response
                    {
                        ProcessAuthResponse(data);
                    }
                    else if (responseType == 6) // Data feed response
                    {
                        ProcessDataFeedAcknowledgement(data);
                    }
                }

                // Always return as binary message for Fyers data WebSocket
                return new WebSocketClientWrapper.BinaryMessage { Data = data };
            }
        }

        private void ProcessAuthResponse(byte[] data)
        {
            try
            {
                if (data.Length >= 7)
                {
                    var offset = 4;
                    offset++; // Skip field ID
                    var fieldLength = BinaryPrimitives.ReadUInt16BigEndian(data.AsSpan(offset));
                    offset += 2;
                    var responseValue = Encoding.UTF8.GetString(data, offset, fieldLength);

                    if (responseValue == "K")
                    {
                        _isAuthenticated = true;
                        Log.Trace("FyersDataWebSocketWrapper: Authentication successful");

                        // Get ack count from second field
                        offset += fieldLength;
                        if (offset < data.Length - 6)
                        {
                            offset++; // Skip field ID
                            offset += 2; // Skip field length
                            _ackCount = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(offset));
                            Log.Trace($"FyersDataWebSocketWrapper: Ack count set to {_ackCount}");
                        }

                        // Send full mode message after authentication
                        var fullModeMsg = CreateFullModeMessage();
                        _messageQueue.Enqueue(fullModeMsg);
                    }
                    else
                    {
                        Log.Error($"FyersDataWebSocketWrapper: Authentication failed - response: {responseValue}");
                        _isAuthenticated = false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersDataWebSocketWrapper: Error processing auth response - {ex.Message}");
            }
        }

        private void ProcessDataFeedAcknowledgement(byte[] data)
        {
            try
            {
                if (_ackCount > 0 && data.Length >= 7)
                {
                    _updateCount++;
                    var messageNum = (int)BinaryPrimitives.ReadUInt32BigEndian(data.AsSpan(3, 4));

                    if (_updateCount >= _ackCount)
                    {
                        var ackMsg = CreateAcknowledgementMessage(messageNum);
                        _messageQueue.Enqueue(ackMsg);
                        _updateCount = 0;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Error($"FyersDataWebSocketWrapper: Error processing data feed ack - {ex.Message}");
            }
        }

        private void OnOpen()
        {
            Log.Trace("FyersDataWebSocketWrapper: Connection opened");
            Open?.Invoke(this, EventArgs.Empty);
        }

        private void OnClose(WebSocketCloseData e)
        {
            Log.Trace($"FyersDataWebSocketWrapper: Connection closed - {e.Reason}");
            _isAuthenticated = false;
            Closed?.Invoke(this, e);
        }

        private void OnMessage(WebSocketMessage e)
        {
            Message?.Invoke(this, e);
        }

        private void OnError(WebSocketError e)
        {
            Log.Error($"FyersDataWebSocketWrapper: Error - {e.Message}");
            Error?.Invoke(this, e);
        }

        /// <summary>
        /// Queue a subscription message
        /// </summary>
        public void QueueSubscription(string[] hsmSymbols, int channelNum = 11)
        {
            if (!_isAuthenticated)
            {
                Log.Trace("FyersDataWebSocketWrapper: Not authenticated yet, queuing subscription for later");
            }
            var msg = CreateSubscriptionMessage(hsmSymbols, channelNum);
            _messageQueue.Enqueue(msg);
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
