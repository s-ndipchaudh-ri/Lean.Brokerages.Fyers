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

using Newtonsoft.Json;

namespace QuantConnect.Brokerages.Fyers.Messages
{
    /// <summary>
    /// Request to place a new order
    /// </summary>
    public class FyersPlaceOrderRequest
    {
        /// <summary>
        /// Symbol (e.g., NSE:SBIN-EQ)
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Order quantity
        /// </summary>
        [JsonProperty("qty")]
        public int Quantity { get; set; }

        /// <summary>
        /// Order type (1=Limit, 2=Market, 3=SL, 4=SL-M)
        /// </summary>
        [JsonProperty("type")]
        public int Type { get; set; }

        /// <summary>
        /// Order side (1=Buy, -1=Sell)
        /// </summary>
        [JsonProperty("side")]
        public int Side { get; set; }

        /// <summary>
        /// Product type (CNC, INTRADAY, MARGIN, CO, BO)
        /// </summary>
        [JsonProperty("productType")]
        public string ProductType { get; set; } = string.Empty;

        /// <summary>
        /// Limit price (for Limit and SL orders)
        /// </summary>
        [JsonProperty("limitPrice")]
        public decimal LimitPrice { get; set; }

        /// <summary>
        /// Stop/trigger price (for SL and SL-M orders)
        /// </summary>
        [JsonProperty("stopPrice")]
        public decimal StopPrice { get; set; }

        /// <summary>
        /// Order validity (DAY, IOC)
        /// </summary>
        [JsonProperty("validity")]
        public string Validity { get; set; } = "DAY";

        /// <summary>
        /// Disclosed quantity
        /// </summary>
        [JsonProperty("disclosedQty")]
        public int DisclosedQuantity { get; set; }

        /// <summary>
        /// Offline order flag
        /// </summary>
        [JsonProperty("offlineOrder")]
        public bool OfflineOrder { get; set; }

        /// <summary>
        /// Stop loss points (for BO orders)
        /// </summary>
        [JsonProperty("stopLoss")]
        public decimal StopLoss { get; set; }

        /// <summary>
        /// Take profit points (for BO orders)
        /// </summary>
        [JsonProperty("takeProfit")]
        public decimal TakeProfit { get; set; }
    }

    /// <summary>
    /// Request to modify an existing order
    /// </summary>
    public class FyersModifyOrderRequest
    {
        /// <summary>
        /// Order ID to modify
        /// </summary>
        [JsonProperty("id")]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// New order type (1=Limit, 2=Market, 3=SL, 4=SL-M)
        /// </summary>
        [JsonProperty("type")]
        public int Type { get; set; }

        /// <summary>
        /// New quantity
        /// </summary>
        [JsonProperty("qty")]
        public int Quantity { get; set; }

        /// <summary>
        /// New limit price
        /// </summary>
        [JsonProperty("limitPrice")]
        public decimal LimitPrice { get; set; }

        /// <summary>
        /// New stop/trigger price
        /// </summary>
        [JsonProperty("stopPrice")]
        public decimal StopPrice { get; set; }
    }

    /// <summary>
    /// Request to cancel an order
    /// </summary>
    public class FyersCancelOrderRequest
    {
        /// <summary>
        /// Order ID to cancel
        /// </summary>
        [JsonProperty("id")]
        public string OrderId { get; set; } = string.Empty;
    }

    /// <summary>
    /// Request for historical data
    /// </summary>
    public class FyersHistoryRequest
    {
        /// <summary>
        /// Symbol (e.g., NSE:SBIN-EQ)
        /// </summary>
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Resolution (1, 5, 15, 30, 60, D)
        /// </summary>
        public string Resolution { get; set; } = string.Empty;

        /// <summary>
        /// Date format (1=epoch, 0=yyyy-mm-dd)
        /// </summary>
        public string DateFormat { get; set; } = "1";

        /// <summary>
        /// Start date (yyyy-mm-dd format)
        /// </summary>
        public string RangeFrom { get; set; } = string.Empty;

        /// <summary>
        /// End date (yyyy-mm-dd format)
        /// </summary>
        public string RangeTo { get; set; } = string.Empty;

        /// <summary>
        /// Continuous data flag (0=no, 1=yes)
        /// </summary>
        public string ContinuousFlag { get; set; } = "0";
    }

    /// <summary>
    /// WebSocket subscription request
    /// </summary>
    public class FyersWebSocketSubscribeRequest
    {
        /// <summary>
        /// Message type (SUB_DATA, UNSUB_DATA)
        /// </summary>
        [JsonProperty("T")]
        public string MessageType { get; set; } = string.Empty;

        /// <summary>
        /// Symbol list to subscribe/unsubscribe
        /// </summary>
        [JsonProperty("SLIST")]
        public string[] Symbols { get; set; } = System.Array.Empty<string>();

        /// <summary>
        /// Subscription type (1=SymbolUpdate, 2=DepthUpdate)
        /// </summary>
        [JsonProperty("SUB_T")]
        public int SubscriptionType { get; set; } = 1;
    }

    /// <summary>
    /// WebSocket connection request
    /// </summary>
    public class FyersWebSocketConnectionRequest
    {
        /// <summary>
        /// Connection type (DATA, ORDER)
        /// </summary>
        [JsonProperty("type")]
        public string Type { get; set; } = string.Empty;

        /// <summary>
        /// Authentication key (client_id:access_token)
        /// </summary>
        [JsonProperty("key")]
        public string Key { get; set; } = string.Empty;
    }

    /// <summary>
    /// Token validation request for OAuth
    /// </summary>
    public class FyersValidateAuthCodeRequest
    {
        /// <summary>
        /// Grant type (always "authorization_code")
        /// </summary>
        [JsonProperty("grant_type")]
        public string GrantType { get; set; } = "authorization_code";

        /// <summary>
        /// SHA-256 hash of app_id:app_secret
        /// </summary>
        [JsonProperty("appIdHash")]
        public string AppIdHash { get; set; } = string.Empty;

        /// <summary>
        /// Authorization code from redirect
        /// </summary>
        [JsonProperty("code")]
        public string Code { get; set; } = string.Empty;
    }
}
