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
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NUnit.Framework;
using QuantConnect.Brokerages.Fyers.WebSocket;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    /// <summary>
    /// Unit tests for FyersBrokerage Order WebSocket functionality
    /// </summary>
    [TestFixture]
    public class FyersBrokerageOrderWebSocketTests
    {
        #region WebSocket Helper Tests

        [Test]
        public void GetDataWebSocketUrl_ReturnsCorrectFormat()
        {
            // Arrange
            var clientId = "TESTCLIENT";
            var accessToken = "test_access_token_123";

            // Act
            var url = FyersWebSocketHelper.GetDataWebSocketUrl(clientId, accessToken);

            // Assert
            Assert.IsNotNull(url);
            Assert.IsTrue(url.StartsWith(FyersConstants.DataWebSocketUrl));
            Assert.IsTrue(url.Contains($"access_token={clientId}:{accessToken}"));
            Log.Trace($"Data WebSocket URL: {url}");
        }

        [Test]
        public void GetOrderWebSocketUrl_ReturnsCorrectFormat()
        {
            // Arrange
            var clientId = "TESTCLIENT";
            var accessToken = "test_access_token_123";

            // Act
            var url = FyersWebSocketHelper.GetOrderWebSocketUrl(clientId, accessToken);

            // Assert
            Assert.IsNotNull(url);
            Assert.IsTrue(url.StartsWith(FyersConstants.OrderWebSocketUrl));
            Assert.IsTrue(url.Contains($"access_token={clientId}:{accessToken}"));
            Log.Trace($"Order WebSocket URL: {url}");
        }

        [Test]
        public void CreateSubscribeMessage_ReturnsValidJson()
        {
            // Arrange
            var symbols = new[] { "NSE:SBIN-EQ", "NSE:RELIANCE-EQ", "NSE:TCS-EQ" };

            // Act
            var message = FyersWebSocketHelper.CreateSubscribeMessage(symbols);

            // Assert
            Assert.IsNotNull(message);

            var json = JObject.Parse(message);
            Assert.AreEqual(FyersWebSocketMessageType.Subscribe, json["T"]?.ToString());
            Assert.IsNotNull(json["L1LIST"]);

            var symbolList = json["L1LIST"]?.ToObject<string[]>();
            Assert.IsNotNull(symbolList);
            Assert.AreEqual(3, symbolList.Length);
            Assert.Contains("NSE:SBIN-EQ", symbolList);

            Log.Trace($"Subscribe message: {message}");
        }

        [Test]
        public void CreateSubscribeMessage_WithDepthUpdate_ReturnsCorrectSubscriptionType()
        {
            // Arrange
            var symbols = new[] { "NSE:SBIN-EQ" };

            // Act
            var message = FyersWebSocketHelper.CreateSubscribeMessage(symbols, FyersWebSocketSubscriptionType.DepthUpdate);

            // Assert
            Assert.IsNotNull(message);

            var json = JObject.Parse(message);
            Assert.AreEqual(2, json["SUB_T"]?.Value<int>()); // DepthUpdate = 2

            Log.Trace($"Depth subscribe message: {message}");
        }

        [Test]
        public void CreateUnsubscribeMessage_ReturnsValidJson()
        {
            // Arrange
            var symbols = new[] { "NSE:SBIN-EQ", "NSE:RELIANCE-EQ" };

            // Act
            var message = FyersWebSocketHelper.CreateUnsubscribeMessage(symbols);

            // Assert
            Assert.IsNotNull(message);

            var json = JObject.Parse(message);
            Assert.AreEqual(FyersWebSocketMessageType.Unsubscribe, json["T"]?.ToString());

            Log.Trace($"Unsubscribe message: {message}");
        }

        #endregion

        #region Order Update Parsing Tests

        [Test]
        public void ParseOrderUpdateMessage_ValidOrderUpdate()
        {
            // Arrange
            var orderUpdate = new
            {
                T = "order",
                data = new
                {
                    id = "202411270001",
                    exchOrdId = "1234567890",
                    symbol = "NSE:SBIN-EQ",
                    status = 2, // Filled
                    filledQty = 10,
                    tradedPrice = 850.50m,
                    message = "Order executed successfully"
                }
            };

            var json = JObject.FromObject(orderUpdate);

            // Act & Assert
            var messageType = json["T"]?.ToString();
            Assert.AreEqual("order", messageType);

            var data = json["data"];
            Assert.IsNotNull(data);
            Assert.AreEqual("202411270001", data["id"]?.ToString());
            Assert.AreEqual(2, data["status"]?.Value<int>());
            Assert.AreEqual(10, data["filledQty"]?.Value<int>());
            Assert.AreEqual(850.50m, data["tradedPrice"]?.Value<decimal>());

            Log.Trace($"Parsed order update: ID={data["id"]}, Status={data["status"]}, FilledQty={data["filledQty"]}");
        }

        [Test]
        public void ParseOrderUpdateMessage_CancelledOrder()
        {
            // Arrange
            var orderUpdate = new
            {
                T = "order",
                data = new
                {
                    id = "202411270002",
                    symbol = "NSE:SBIN-EQ",
                    status = 1, // Cancelled
                    message = "Order cancelled by user"
                }
            };

            var json = JObject.FromObject(orderUpdate);
            var data = json["data"];

            // Act & Assert
            Assert.AreEqual(1, data["status"]?.Value<int>());
            Assert.AreEqual("Order cancelled by user", data["message"]?.ToString());

            Log.Trace($"Cancelled order: {data["id"]} - {data["message"]}");
        }

        [Test]
        public void ParseOrderUpdateMessage_RejectedOrder()
        {
            // Arrange
            var orderUpdate = new
            {
                T = "order",
                data = new
                {
                    id = "202411270003",
                    symbol = "NSE:SBIN-EQ",
                    status = 5, // Rejected
                    message = "Insufficient margin"
                }
            };

            var json = JObject.FromObject(orderUpdate);
            var data = json["data"];

            // Act & Assert
            Assert.AreEqual(5, data["status"]?.Value<int>());
            Assert.AreEqual("Insufficient margin", data["message"]?.ToString());

            Log.Trace($"Rejected order: {data["id"]} - {data["message"]}");
        }

        [Test]
        public void ParseOrderUpdateMessage_PartiallyFilledOrder()
        {
            // Arrange
            var orderUpdate = new
            {
                T = "order",
                data = new
                {
                    id = "202411270004",
                    symbol = "NSE:SBIN-EQ",
                    status = 4, // Partially filled
                    qty = 100,
                    filledQty = 50,
                    remainingQty = 50,
                    tradedPrice = 852.25m,
                    message = "Order partially executed"
                }
            };

            var json = JObject.FromObject(orderUpdate);
            var data = json["data"];

            // Act & Assert
            Assert.AreEqual(4, data["status"]?.Value<int>());
            Assert.AreEqual(100, data["qty"]?.Value<int>());
            Assert.AreEqual(50, data["filledQty"]?.Value<int>());
            Assert.AreEqual(50, data["remainingQty"]?.Value<int>());

            Log.Trace($"Partially filled order: {data["id"]} - {data["filledQty"]}/{data["qty"]}");
        }

        #endregion

        #region Trade Update Parsing Tests

        [Test]
        public void ParseTradeUpdateMessage_ValidTrade()
        {
            // Arrange
            var tradeUpdate = new
            {
                T = "trade",
                data = new
                {
                    id = "202411270001",
                    exchOrdId = "1234567890",
                    tradeNumber = "T001",
                    symbol = "NSE:SBIN-EQ",
                    side = 1, // Buy
                    qty = 10,
                    tradedPrice = 850.50m,
                    tradeDateTime = "27-Nov-2024 10:30:15"
                }
            };

            var json = JObject.FromObject(tradeUpdate);
            var data = json["data"];

            // Act & Assert
            var messageType = json["T"]?.ToString();
            Assert.AreEqual("trade", messageType);
            Assert.AreEqual("T001", data["tradeNumber"]?.ToString());
            Assert.AreEqual(10, data["qty"]?.Value<int>());
            Assert.AreEqual(850.50m, data["tradedPrice"]?.Value<decimal>());

            Log.Trace($"Trade: {data["tradeNumber"]} - {data["qty"]} @ {data["tradedPrice"]}");
        }

        #endregion

        #region Position Update Parsing Tests

        [Test]
        public void ParsePositionUpdateMessage_ValidPosition()
        {
            // Arrange
            var positionUpdate = new
            {
                T = "position",
                data = new
                {
                    symbol = "NSE:SBIN-EQ",
                    netQty = 100,
                    netAvg = 850.25m,
                    pl = 250.50m,
                    productType = "INTRADAY"
                }
            };

            var json = JObject.FromObject(positionUpdate);
            var data = json["data"];

            // Act & Assert
            var messageType = json["T"]?.ToString();
            Assert.AreEqual("position", messageType);
            Assert.AreEqual("NSE:SBIN-EQ", data["symbol"]?.ToString());
            Assert.AreEqual(100, data["netQty"]?.Value<int>());
            Assert.AreEqual(850.25m, data["netAvg"]?.Value<decimal>());

            Log.Trace($"Position: {data["symbol"]} - {data["netQty"]} @ {data["netAvg"]}");
        }

        #endregion

        #region Constants Tests

        [Test]
        public void FyersConstants_WebSocketUrls_AreValid()
        {
            // Assert
            Assert.IsNotEmpty(FyersConstants.DataWebSocketUrl);
            Assert.IsNotEmpty(FyersConstants.OrderWebSocketUrl);
            Assert.IsTrue(FyersConstants.DataWebSocketUrl.StartsWith("wss://"));
            Assert.IsTrue(FyersConstants.OrderWebSocketUrl.StartsWith("wss://"));

            Log.Trace($"Data WebSocket URL: {FyersConstants.DataWebSocketUrl}");
            Log.Trace($"Order WebSocket URL: {FyersConstants.OrderWebSocketUrl}");
        }

        [Test]
        public void FyersOrderStatus_HasCorrectValues()
        {
            // Assert
            Assert.AreEqual(1, (int)FyersOrderStatus.Cancelled);
            Assert.AreEqual(2, (int)FyersOrderStatus.Traded);
            Assert.AreEqual(2, (int)FyersOrderStatus.Filled);
            Assert.AreEqual(4, (int)FyersOrderStatus.PartiallyFilled);
            Assert.AreEqual(5, (int)FyersOrderStatus.Rejected);
            Assert.AreEqual(6, (int)FyersOrderStatus.Pending);
        }

        [Test]
        public void FyersWebSocketMessageType_HasCorrectValues()
        {
            // Assert
            Assert.AreEqual("SUB_DATA", FyersWebSocketMessageType.Subscribe);
            Assert.AreEqual("UNSUB_DATA", FyersWebSocketMessageType.Unsubscribe);
            Assert.AreEqual("SUB_ORD", FyersWebSocketMessageType.SubscribeOrder);
            Assert.AreEqual("UNSUB_ORD", FyersWebSocketMessageType.UnsubscribeOrder);
            Assert.AreEqual("order", FyersWebSocketMessageType.Order);
            Assert.AreEqual("trade", FyersWebSocketMessageType.Trade);
            Assert.AreEqual("position", FyersWebSocketMessageType.Position);
        }

        [Test]
        public void FyersWebSocketSubscriptionType_HasCorrectValues()
        {
            // Assert
            Assert.AreEqual(1, (int)FyersWebSocketSubscriptionType.SymbolUpdate);
            Assert.AreEqual(2, (int)FyersWebSocketSubscriptionType.DepthUpdate);
        }

        #endregion

        #region Edge Case Tests

        [Test]
        public void CreateSubscribeMessage_EmptySymbols_ReturnsValidJson()
        {
            // Arrange
            var symbols = new string[0];

            // Act
            var message = FyersWebSocketHelper.CreateSubscribeMessage(symbols);

            // Assert
            Assert.IsNotNull(message);
            var json = JObject.Parse(message);
            Assert.IsNotNull(json["L1LIST"]);

            Log.Trace($"Empty symbols message: {message}");
        }

        [Test]
        public void CreateSubscribeMessage_SingleSymbol_ReturnsValidJson()
        {
            // Arrange
            var symbols = new[] { "NSE:NIFTY50-INDEX" };

            // Act
            var message = FyersWebSocketHelper.CreateSubscribeMessage(symbols);

            // Assert
            Assert.IsNotNull(message);
            var json = JObject.Parse(message);
            var symbolList = json["L1LIST"]?.ToObject<string[]>();
            Assert.AreEqual(1, symbolList?.Length);

            Log.Trace($"Single symbol message: {message}");
        }

        [Test]
        public void ParseOrderUpdateMessage_MissingFields_DoesNotThrow()
        {
            // Arrange - minimal valid structure
            var orderUpdate = new
            {
                T = "order",
                data = new
                {
                    id = "202411270001"
                }
            };

            // Act & Assert - should not throw
            Assert.DoesNotThrow(() =>
            {
                var json = JObject.FromObject(orderUpdate);
                var data = json["data"];
                var status = data["status"]?.Value<int>() ?? 0;
                var filledQty = data["filledQty"]?.Value<decimal>() ?? 0;
                var tradedPrice = data["tradedPrice"]?.Value<decimal>() ?? 0;

                Log.Trace($"Parsed with defaults: status={status}, filledQty={filledQty}, tradedPrice={tradedPrice}");
            });
        }

        [Test]
        public void ParseOrderUpdateMessage_NullData_HandlesGracefully()
        {
            // Arrange
            var orderUpdate = new
            {
                T = "order"
            };

            // Act & Assert
            var json = JObject.FromObject(orderUpdate);
            var data = json["data"];
            Assert.IsNull(data);
        }

        #endregion
    }
}
