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

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Fyers API constants and configuration values
    /// </summary>
    public static class FyersConstants
    {
        #region API Endpoints

        /// <summary>
        /// Base URL for Fyers REST API
        /// </summary>
        public const string ApiBaseUrl = "https://api-t1.fyers.in";

        /// <summary>
        /// API version path
        /// </summary>
        public const string ApiVersion = "/api/v3";

        /// <summary>
        /// WebSocket URL for real-time data streaming (HSM - High Speed Market data)
        /// </summary>
        public const string DataWebSocketUrl = "wss://socket.fyers.in/hsm/v1-5/prod";

        /// <summary>
        /// WebSocket URL for order updates
        /// </summary>
        public const string OrderWebSocketUrl = "wss://socket.fyers.in/trade/v3";

        /// <summary>
        /// WebSocket URL for tick-by-tick data
        /// </summary>
        public const string TbtWebSocketUrl = "wss://rtsocket-api.fyers.in/versova";

        #endregion

        #region REST API Paths

        /// <summary>
        /// User profile endpoint
        /// </summary>
        public const string ProfileEndpoint = "/api/v3/profile";

        /// <summary>
        /// Funds/margins endpoint
        /// </summary>
        public const string FundsEndpoint = "/api/v3/funds";

        /// <summary>
        /// Holdings endpoint
        /// </summary>
        public const string HoldingsEndpoint = "/api/v3/holdings";

        /// <summary>
        /// Positions endpoint
        /// </summary>
        public const string PositionsEndpoint = "/api/v3/positions";

        /// <summary>
        /// Order placement/modification/cancellation endpoint (sync)
        /// </summary>
        public const string OrdersSyncEndpoint = "/api/v3/orders/sync";

        /// <summary>
        /// Order book endpoint
        /// </summary>
        public const string OrderBookEndpoint = "/api/v3/orders";

        /// <summary>
        /// Trade book endpoint
        /// </summary>
        public const string TradeBookEndpoint = "/api/v3/tradebook";

        /// <summary>
        /// Historical data endpoint
        /// </summary>
        public const string HistoryEndpoint = "/api/v3/history";

        /// <summary>
        /// Quotes endpoint
        /// </summary>
        public const string QuotesEndpoint = "/api/v3/quotes";

        /// <summary>
        /// Market depth endpoint
        /// </summary>
        public const string DepthEndpoint = "/api/v3/depth";

        /// <summary>
        /// Auth code generation endpoint
        /// </summary>
        public const string GenerateAuthCodeEndpoint = "/api/v3/generate-authcode";

        /// <summary>
        /// Token validation endpoint
        /// </summary>
        public const string ValidateAuthCodeEndpoint = "/api/v3/validate-authcode";

        #endregion

        #region Symbol Master URLs

        /// <summary>
        /// NSE Capital Market symbols
        /// </summary>
        public const string SymbolMasterNseCm = "https://public.fyers.in/sym_details/NSE_CM.csv";

        /// <summary>
        /// BSE Capital Market symbols
        /// </summary>
        public const string SymbolMasterBseCm = "https://public.fyers.in/sym_details/BSE_CM.csv";

        /// <summary>
        /// NSE Futures & Options symbols
        /// </summary>
        public const string SymbolMasterNseFo = "https://public.fyers.in/sym_details/NSE_FO.csv";

        /// <summary>
        /// BSE Futures & Options symbols
        /// </summary>
        public const string SymbolMasterBseFo = "https://public.fyers.in/sym_details/BSE_FO.csv";

        /// <summary>
        /// MCX Commodity symbols
        /// </summary>
        public const string SymbolMasterMcxCom = "https://public.fyers.in/sym_details/MCX_COM.csv";

        /// <summary>
        /// NSE Currency Derivatives symbols
        /// </summary>
        public const string SymbolMasterNseCd = "https://public.fyers.in/sym_details/NSE_CD.csv";

        #endregion

        #region Rate Limits

        /// <summary>
        /// REST API rate limit (requests per second)
        /// </summary>
        public const int RestApiRateLimit = 10;

        /// <summary>
        /// Order API rate limit (requests per second)
        /// </summary>
        public const int OrderApiRateLimit = 10;

        /// <summary>
        /// Historical data API rate limit (requests per second)
        /// </summary>
        public const int HistoryApiRateLimit = 3;

        /// <summary>
        /// Maximum symbols per WebSocket connection
        /// </summary>
        public const int MaxWebSocketSymbols = 200;

        #endregion

        #region Timeouts

        /// <summary>
        /// Default HTTP request timeout in milliseconds
        /// </summary>
        public const int DefaultHttpTimeoutMs = 30000;

        /// <summary>
        /// WebSocket connection timeout in milliseconds
        /// </summary>
        public const int WebSocketConnectionTimeoutMs = 30000;

        /// <summary>
        /// WebSocket heartbeat interval in milliseconds
        /// </summary>
        public const int WebSocketHeartbeatIntervalMs = 30000;

        #endregion
    }

    /// <summary>
    /// Fyers order types
    /// </summary>
    public enum FyersOrderType
    {
        /// <summary>
        /// Limit order
        /// </summary>
        Limit = 1,

        /// <summary>
        /// Market order
        /// </summary>
        Market = 2,

        /// <summary>
        /// Stop Loss with limit price
        /// </summary>
        StopLoss = 3,

        /// <summary>
        /// Stop Loss at market price
        /// </summary>
        StopLossMarket = 4
    }

    /// <summary>
    /// Fyers order side (direction)
    /// </summary>
    public enum FyersOrderSide
    {
        /// <summary>
        /// Buy order
        /// </summary>
        Buy = 1,

        /// <summary>
        /// Sell order
        /// </summary>
        Sell = -1
    }

    /// <summary>
    /// Fyers product types
    /// </summary>
    public enum FyersProductType
    {
        /// <summary>
        /// Cash and Carry (Delivery)
        /// </summary>
        CNC,

        /// <summary>
        /// Intraday trading
        /// </summary>
        INTRADAY,

        /// <summary>
        /// Margin trading
        /// </summary>
        MARGIN,

        /// <summary>
        /// Cover Order
        /// </summary>
        CO,

        /// <summary>
        /// Bracket Order
        /// </summary>
        BO
    }

    /// <summary>
    /// Fyers order validity types
    /// </summary>
    public enum FyersOrderValidity
    {
        /// <summary>
        /// Day order - valid till end of trading day
        /// </summary>
        DAY,

        /// <summary>
        /// Immediate or Cancel
        /// </summary>
        IOC
    }

    /// <summary>
    /// Fyers order status codes
    /// </summary>
    public enum FyersOrderStatus
    {
        /// <summary>
        /// Order cancelled
        /// </summary>
        Cancelled = 1,

        /// <summary>
        /// Order completely filled/traded
        /// </summary>
        Traded = 2,

        /// <summary>
        /// Order completely filled (alias for Traded)
        /// </summary>
        Filled = 2,

        /// <summary>
        /// Order partially filled
        /// </summary>
        PartiallyFilled = 4,

        /// <summary>
        /// Order rejected
        /// </summary>
        Rejected = 5,

        /// <summary>
        /// Order pending
        /// </summary>
        Pending = 6
    }

    /// <summary>
    /// Fyers exchange codes
    /// </summary>
    public static class FyersExchange
    {
        /// <summary>
        /// National Stock Exchange
        /// </summary>
        public const string NSE = "NSE";

        /// <summary>
        /// Bombay Stock Exchange
        /// </summary>
        public const string BSE = "BSE";

        /// <summary>
        /// Multi Commodity Exchange
        /// </summary>
        public const string MCX = "MCX";
    }

    /// <summary>
    /// Fyers market segments
    /// </summary>
    public static class FyersSegment
    {
        /// <summary>
        /// Capital Market (Cash/Equity)
        /// </summary>
        public const string CM = "CM";

        /// <summary>
        /// Futures & Options
        /// </summary>
        public const string FO = "FO";

        /// <summary>
        /// Currency Derivatives
        /// </summary>
        public const string CD = "CD";

        /// <summary>
        /// Commodities
        /// </summary>
        public const string COM = "COM";
    }

    /// <summary>
    /// Historical data resolution values for Fyers API
    /// </summary>
    public static class FyersResolution
    {
        /// <summary>
        /// 1 minute candles
        /// </summary>
        public const string Minute1 = "1";

        /// <summary>
        /// 5 minute candles
        /// </summary>
        public const string Minute5 = "5";

        /// <summary>
        /// 15 minute candles
        /// </summary>
        public const string Minute15 = "15";

        /// <summary>
        /// 30 minute candles
        /// </summary>
        public const string Minute30 = "30";

        /// <summary>
        /// 60 minute (hourly) candles
        /// </summary>
        public const string Minute60 = "60";

        /// <summary>
        /// Daily candles
        /// </summary>
        public const string Daily = "D";
    }

    /// <summary>
    /// WebSocket subscription types
    /// </summary>
    public enum FyersWebSocketSubscriptionType
    {
        /// <summary>
        /// Symbol updates (LTP, OHLC, volume)
        /// </summary>
        SymbolUpdate = 1,

        /// <summary>
        /// Market depth updates (5 levels)
        /// </summary>
        DepthUpdate = 2
    }

    /// <summary>
    /// WebSocket message types
    /// </summary>
    public static class FyersWebSocketMessageType
    {
        /// <summary>
        /// Subscribe to data
        /// </summary>
        public const string Subscribe = "SUB_DATA";

        /// <summary>
        /// Unsubscribe from data
        /// </summary>
        public const string Unsubscribe = "UNSUB_DATA";

        /// <summary>
        /// Symbol feed message
        /// </summary>
        public const string SymbolFeed = "sf";

        /// <summary>
        /// Depth feed message
        /// </summary>
        public const string DepthFeed = "df";

        /// <summary>
        /// Order update message
        /// </summary>
        public const string Order = "order";

        /// <summary>
        /// Trade update message
        /// </summary>
        public const string Trade = "trade";

        /// <summary>
        /// Position update message
        /// </summary>
        public const string Position = "position";

        /// <summary>
        /// Subscribe to order updates
        /// </summary>
        public const string SubscribeOrder = "SUB_ORD";

        /// <summary>
        /// Unsubscribe from order updates
        /// </summary>
        public const string UnsubscribeOrder = "UNSUB_ORD";

        /// <summary>
        /// Order update (alternate format)
        /// </summary>
        public const string OrderUpdate = "ord";

        /// <summary>
        /// Trade update (alternate format)
        /// </summary>
        public const string TradeUpdate = "trd";

        /// <summary>
        /// Position update (alternate format)
        /// </summary>
        public const string PositionUpdate = "pos";

        /// <summary>
        /// Connection successful
        /// </summary>
        public const string Connected = "connected";

        /// <summary>
        /// Error message
        /// </summary>
        public const string Error = "error";
    }
}
