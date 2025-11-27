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

using System.Globalization;
using Newtonsoft.Json;

namespace QuantConnect.Brokerages.Fyers.WebSocket
{
    /// <summary>
    /// Helper class for creating Fyers WebSocket URLs with authentication
    /// </summary>
    public static class FyersWebSocketHelper
    {
        /// <summary>
        /// Creates the authenticated WebSocket URL for data streaming
        /// </summary>
        /// <param name="clientId">Fyers client ID (App ID)</param>
        /// <param name="accessToken">OAuth access token</param>
        /// <returns>Full WebSocket URL with authentication</returns>
        public static string GetDataWebSocketUrl(string clientId, string accessToken)
        {
            // Fyers WebSocket URL format: wss://api.fyers.in/socket/v3/dataTicker?access_token=CLIENT_ID:ACCESS_TOKEN
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?access_token={1}:{2}",
                FyersConstants.DataWebSocketUrl,
                clientId,
                accessToken);
        }

        /// <summary>
        /// Creates the authenticated WebSocket URL for order updates
        /// </summary>
        /// <param name="clientId">Fyers client ID (App ID)</param>
        /// <param name="accessToken">OAuth access token</param>
        /// <returns>Full WebSocket URL with authentication</returns>
        public static string GetOrderWebSocketUrl(string clientId, string accessToken)
        {
            // Fyers order WebSocket URL format
            return string.Format(CultureInfo.InvariantCulture,
                "{0}?access_token={1}:{2}",
                FyersConstants.OrderWebSocketUrl,
                clientId,
                accessToken);
        }

        /// <summary>
        /// Creates a subscription message for Fyers WebSocket
        /// </summary>
        /// <param name="symbols">Array of Fyers symbols (e.g., NSE:SBIN-EQ)</param>
        /// <param name="subscriptionType">Type of subscription</param>
        /// <returns>JSON subscription message</returns>
        public static string CreateSubscribeMessage(string[] symbols, FyersWebSocketSubscriptionType subscriptionType = FyersWebSocketSubscriptionType.SymbolUpdate)
        {
            var request = new
            {
                T = FyersWebSocketMessageType.Subscribe,
                L1LIST = symbols,
                SUB_T = (int)subscriptionType
            };
            return JsonConvert.SerializeObject(request);
        }

        /// <summary>
        /// Creates an unsubscription message for Fyers WebSocket
        /// </summary>
        /// <param name="symbols">Array of Fyers symbols to unsubscribe</param>
        /// <returns>JSON unsubscription message</returns>
        public static string CreateUnsubscribeMessage(string[] symbols)
        {
            var request = new
            {
                T = FyersWebSocketMessageType.Unsubscribe,
                L1LIST = symbols
            };
            return JsonConvert.SerializeObject(request);
        }
    }
}
