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

using System.Collections.Generic;
using Newtonsoft.Json;

namespace QuantConnect.Brokerages.Fyers.Messages
{
    /// <summary>
    /// Base response structure from Fyers API
    /// </summary>
    /// <typeparam name="T">Type of data in response</typeparam>
    public class FyersResponse<T>
    {
        /// <summary>
        /// Response status ("ok" or "error")
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code (200 for success, negative for errors)
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Response message
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Response data
        /// </summary>
        [JsonProperty("data")]
        public T? Data { get; set; }

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// User profile data
    /// </summary>
    public class FyersUserProfile
    {
        /// <summary>
        /// Fyers client ID
        /// </summary>
        [JsonProperty("fy_id")]
        public string FyersId { get; set; } = string.Empty;

        /// <summary>
        /// User email
        /// </summary>
        [JsonProperty("email_id")]
        public string Email { get; set; } = string.Empty;

        /// <summary>
        /// User full name
        /// </summary>
        [JsonProperty("name")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Display name
        /// </summary>
        [JsonProperty("display_name")]
        public string DisplayName { get; set; } = string.Empty;

        /// <summary>
        /// PAN number
        /// </summary>
        [JsonProperty("PAN")]
        public string Pan { get; set; } = string.Empty;
    }

    /// <summary>
    /// Fund/margin limit item
    /// </summary>
    public class FyersFundLimit
    {
        /// <summary>
        /// Fund type ID
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Fund type title
        /// </summary>
        [JsonProperty("title")]
        public string Title { get; set; } = string.Empty;

        /// <summary>
        /// Equity segment amount
        /// </summary>
        [JsonProperty("equityAmount")]
        public decimal EquityAmount { get; set; }

        /// <summary>
        /// Commodity segment amount
        /// </summary>
        [JsonProperty("commodityAmount")]
        public decimal CommodityAmount { get; set; }
    }

    /// <summary>
    /// Funds response
    /// </summary>
    public class FyersFundsResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Fund limits
        /// </summary>
        [JsonProperty("fund_limit")]
        public List<FyersFundLimit> FundLimits { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Holding item
    /// </summary>
    public class FyersHolding
    {
        /// <summary>
        /// Holding ID
        /// </summary>
        [JsonProperty("id")]
        public int Id { get; set; }

        /// <summary>
        /// Symbol (e.g., NSE:SBIN-EQ)
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Total quantity held
        /// </summary>
        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        /// <summary>
        /// Remaining quantity (after pledging etc.)
        /// </summary>
        [JsonProperty("remainingQuantity")]
        public int RemainingQuantity { get; set; }

        /// <summary>
        /// Average cost price
        /// </summary>
        [JsonProperty("costPrice")]
        public decimal CostPrice { get; set; }

        /// <summary>
        /// Current market value
        /// </summary>
        [JsonProperty("marketVal")]
        public decimal MarketValue { get; set; }

        /// <summary>
        /// Profit/Loss
        /// </summary>
        [JsonProperty("pl")]
        public decimal ProfitLoss { get; set; }

        /// <summary>
        /// Last traded price
        /// </summary>
        [JsonProperty("ltp")]
        public decimal LastTradedPrice { get; set; }

        /// <summary>
        /// Holding type (T1, etc.)
        /// </summary>
        [JsonProperty("holdingType")]
        public string HoldingType { get; set; } = string.Empty;
    }

    /// <summary>
    /// Holdings overall summary
    /// </summary>
    public class FyersHoldingsOverall
    {
        /// <summary>
        /// Total number of holdings
        /// </summary>
        [JsonProperty("count_total")]
        public int TotalCount { get; set; }

        /// <summary>
        /// Overall PnL percentage
        /// </summary>
        [JsonProperty("pnl_perc")]
        public decimal PnlPercentage { get; set; }

        /// <summary>
        /// Total current value
        /// </summary>
        [JsonProperty("total_current_value")]
        public decimal TotalCurrentValue { get; set; }

        /// <summary>
        /// Total investment
        /// </summary>
        [JsonProperty("total_investment")]
        public decimal TotalInvestment { get; set; }

        /// <summary>
        /// Total profit/loss
        /// </summary>
        [JsonProperty("total_pl")]
        public decimal TotalProfitLoss { get; set; }
    }

    /// <summary>
    /// Holdings response
    /// </summary>
    public class FyersHoldingsResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Holdings list
        /// </summary>
        [JsonProperty("holdings")]
        public List<FyersHolding> Holdings { get; set; } = new();

        /// <summary>
        /// Overall summary
        /// </summary>
        [JsonProperty("overall")]
        public FyersHoldingsOverall? Overall { get; set; }

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Position item
    /// </summary>
    public class FyersPosition
    {
        /// <summary>
        /// Position ID
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Symbol (e.g., NSE:SBIN-EQ)
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Position side (1=Long, -1=Short)
        /// </summary>
        [JsonProperty("side")]
        public int Side { get; set; }

        /// <summary>
        /// Product type (CNC, INTRADAY, etc.)
        /// </summary>
        [JsonProperty("productType")]
        public string ProductType { get; set; } = string.Empty;

        /// <summary>
        /// Net quantity
        /// </summary>
        [JsonProperty("qty")]
        public int Quantity { get; set; }

        /// <summary>
        /// Quantity multiplier
        /// </summary>
        [JsonProperty("qtyMultiplier")]
        public int QuantityMultiplier { get; set; }

        /// <summary>
        /// Buy quantity
        /// </summary>
        [JsonProperty("buyQty")]
        public int BuyQuantity { get; set; }

        /// <summary>
        /// Average buy price
        /// </summary>
        [JsonProperty("buyAvg")]
        public decimal BuyAverage { get; set; }

        /// <summary>
        /// Buy value
        /// </summary>
        [JsonProperty("buyVal")]
        public decimal BuyValue { get; set; }

        /// <summary>
        /// Sell quantity
        /// </summary>
        [JsonProperty("sellQty")]
        public int SellQuantity { get; set; }

        /// <summary>
        /// Average sell price
        /// </summary>
        [JsonProperty("sellAvg")]
        public decimal SellAverage { get; set; }

        /// <summary>
        /// Sell value
        /// </summary>
        [JsonProperty("sellVal")]
        public decimal SellValue { get; set; }

        /// <summary>
        /// Net quantity
        /// </summary>
        [JsonProperty("netQty")]
        public int NetQuantity { get; set; }

        /// <summary>
        /// Net average price
        /// </summary>
        [JsonProperty("netAvg")]
        public decimal NetAverage { get; set; }

        /// <summary>
        /// Realized profit
        /// </summary>
        [JsonProperty("realized_profit")]
        public decimal RealizedProfit { get; set; }

        /// <summary>
        /// Unrealized profit
        /// </summary>
        [JsonProperty("unrealized_profit")]
        public decimal UnrealizedProfit { get; set; }

        /// <summary>
        /// Total profit/loss
        /// </summary>
        [JsonProperty("pl")]
        public decimal ProfitLoss { get; set; }

        /// <summary>
        /// Last traded price
        /// </summary>
        [JsonProperty("ltp")]
        public decimal LastTradedPrice { get; set; }

        /// <summary>
        /// Market segment
        /// </summary>
        [JsonProperty("segment")]
        public string Segment { get; set; } = string.Empty;
    }

    /// <summary>
    /// Positions overall summary
    /// </summary>
    public class FyersPositionsOverall
    {
        /// <summary>
        /// Total positions count
        /// </summary>
        [JsonProperty("count_total")]
        public int TotalCount { get; set; }

        /// <summary>
        /// Open positions count
        /// </summary>
        [JsonProperty("count_open")]
        public int OpenCount { get; set; }

        /// <summary>
        /// Total profit/loss
        /// </summary>
        [JsonProperty("pl_total")]
        public decimal TotalProfitLoss { get; set; }

        /// <summary>
        /// Realized profit/loss
        /// </summary>
        [JsonProperty("pl_realized")]
        public decimal RealizedProfitLoss { get; set; }

        /// <summary>
        /// Unrealized profit/loss
        /// </summary>
        [JsonProperty("pl_unrealized")]
        public decimal UnrealizedProfitLoss { get; set; }
    }

    /// <summary>
    /// Positions response
    /// </summary>
    public class FyersPositionsResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Net positions list
        /// </summary>
        [JsonProperty("netPositions")]
        public List<FyersPosition> NetPositions { get; set; } = new();

        /// <summary>
        /// Overall summary
        /// </summary>
        [JsonProperty("overall")]
        public FyersPositionsOverall? Overall { get; set; }

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Order item
    /// </summary>
    public class FyersOrder
    {
        /// <summary>
        /// Fyers order ID
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Exchange order ID
        /// </summary>
        [JsonProperty("exchOrdId")]
        public string ExchangeOrderId { get; set; } = string.Empty;

        /// <summary>
        /// Symbol (e.g., NSE:SBIN-EQ)
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Market segment
        /// </summary>
        [JsonProperty("segment")]
        public string Segment { get; set; } = string.Empty;

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
        /// Product type (CNC, INTRADAY, etc.)
        /// </summary>
        [JsonProperty("productType")]
        public string ProductType { get; set; } = string.Empty;

        /// <summary>
        /// Order status code
        /// </summary>
        [JsonProperty("status")]
        public int Status { get; set; }

        /// <summary>
        /// Order quantity
        /// </summary>
        [JsonProperty("qty")]
        public int Quantity { get; set; }

        /// <summary>
        /// Filled quantity
        /// </summary>
        [JsonProperty("filledQty")]
        public int FilledQuantity { get; set; }

        /// <summary>
        /// Remaining quantity
        /// </summary>
        [JsonProperty("remainingQty")]
        public int RemainingQuantity { get; set; }

        /// <summary>
        /// Limit price
        /// </summary>
        [JsonProperty("limitPrice")]
        public decimal LimitPrice { get; set; }

        /// <summary>
        /// Stop/trigger price
        /// </summary>
        [JsonProperty("stopPrice")]
        public decimal StopPrice { get; set; }

        /// <summary>
        /// Traded/executed price
        /// </summary>
        [JsonProperty("tradedPrice")]
        public decimal TradedPrice { get; set; }

        /// <summary>
        /// Order status message
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Order date/time string
        /// </summary>
        [JsonProperty("orderDateTime")]
        public string OrderDateTime { get; set; } = string.Empty;

        /// <summary>
        /// Exchange date/time string
        /// </summary>
        [JsonProperty("exchOrdDateTime")]
        public string ExchangeOrderDateTime { get; set; } = string.Empty;

        /// <summary>
        /// Order validity
        /// </summary>
        [JsonProperty("validity")]
        public string Validity { get; set; } = string.Empty;

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
    }

    /// <summary>
    /// Order book response
    /// </summary>
    public class FyersOrderBookResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Orders list
        /// </summary>
        [JsonProperty("orderBook")]
        public List<FyersOrder> Orders { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Trade item
    /// </summary>
    public class FyersTrade
    {
        /// <summary>
        /// Order ID
        /// </summary>
        [JsonProperty("id")]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Exchange order ID
        /// </summary>
        [JsonProperty("exchOrdId")]
        public string ExchangeOrderId { get; set; } = string.Empty;

        /// <summary>
        /// Symbol
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Market segment
        /// </summary>
        [JsonProperty("segment")]
        public string Segment { get; set; } = string.Empty;

        /// <summary>
        /// Trade side (1=Buy, -1=Sell)
        /// </summary>
        [JsonProperty("side")]
        public int Side { get; set; }

        /// <summary>
        /// Product type
        /// </summary>
        [JsonProperty("productType")]
        public string ProductType { get; set; } = string.Empty;

        /// <summary>
        /// Trade quantity
        /// </summary>
        [JsonProperty("qty")]
        public int Quantity { get; set; }

        /// <summary>
        /// Traded price
        /// </summary>
        [JsonProperty("tradedPrice")]
        public decimal TradedPrice { get; set; }

        /// <summary>
        /// Trade number
        /// </summary>
        [JsonProperty("tradeNumber")]
        public string TradeNumber { get; set; } = string.Empty;

        /// <summary>
        /// Trade date/time string
        /// </summary>
        [JsonProperty("tradeDateTime")]
        public string TradeDateTime { get; set; } = string.Empty;
    }

    /// <summary>
    /// Trade book response
    /// </summary>
    public class FyersTradeBookResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Trades list
        /// </summary>
        [JsonProperty("tradeBook")]
        public List<FyersTrade> Trades { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Order placement response
    /// </summary>
    public class FyersOrderResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Response message
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Order ID (for successful orders)
        /// </summary>
        [JsonProperty("id")]
        public string OrderId { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Historical candle response
    /// </summary>
    public class FyersHistoryResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Candles data: [[timestamp, O, H, L, C, V], ...]
        /// </summary>
        [JsonProperty("candles")]
        public List<List<decimal>> Candles { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Market depth level
    /// </summary>
    public class FyersDepthLevel
    {
        /// <summary>
        /// Price at this level
        /// </summary>
        [JsonProperty("price")]
        public decimal Price { get; set; }

        /// <summary>
        /// Quantity at this level
        /// </summary>
        [JsonProperty("quantity")]
        public int Quantity { get; set; }

        /// <summary>
        /// Number of orders at this level
        /// </summary>
        [JsonProperty("orders")]
        public int Orders { get; set; }
    }

    /// <summary>
    /// Quote data
    /// </summary>
    public class FyersQuoteData
    {
        /// <summary>
        /// Change in price
        /// </summary>
        [JsonProperty("ch")]
        public decimal Change { get; set; }

        /// <summary>
        /// Change percentage
        /// </summary>
        [JsonProperty("chp")]
        public decimal ChangePercentage { get; set; }

        /// <summary>
        /// Last traded price
        /// </summary>
        [JsonProperty("lp")]
        public decimal LastPrice { get; set; }

        /// <summary>
        /// Spread
        /// </summary>
        [JsonProperty("spread")]
        public decimal Spread { get; set; }

        /// <summary>
        /// Best ask price
        /// </summary>
        [JsonProperty("ask")]
        public decimal AskPrice { get; set; }

        /// <summary>
        /// Best bid price
        /// </summary>
        [JsonProperty("bid")]
        public decimal BidPrice { get; set; }

        /// <summary>
        /// Open price
        /// </summary>
        [JsonProperty("open_price")]
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// High price
        /// </summary>
        [JsonProperty("high_price")]
        public decimal HighPrice { get; set; }

        /// <summary>
        /// Low price
        /// </summary>
        [JsonProperty("low_price")]
        public decimal LowPrice { get; set; }

        /// <summary>
        /// Previous close price
        /// </summary>
        [JsonProperty("prev_close_price")]
        public decimal PreviousClosePrice { get; set; }

        /// <summary>
        /// Volume
        /// </summary>
        [JsonProperty("volume")]
        public long Volume { get; set; }

        /// <summary>
        /// Short name
        /// </summary>
        [JsonProperty("short_name")]
        public string ShortName { get; set; } = string.Empty;

        /// <summary>
        /// Exchange
        /// </summary>
        [JsonProperty("exchange")]
        public string Exchange { get; set; } = string.Empty;

        /// <summary>
        /// Description
        /// </summary>
        [JsonProperty("description")]
        public string Description { get; set; } = string.Empty;

        /// <summary>
        /// Full symbol
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Fyers token
        /// </summary>
        [JsonProperty("fyToken")]
        public string FyersToken { get; set; } = string.Empty;

        /// <summary>
        /// Timestamp
        /// </summary>
        [JsonProperty("tt")]
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// Quote item wrapper
    /// </summary>
    public class FyersQuoteItem
    {
        /// <summary>
        /// Symbol name
        /// </summary>
        [JsonProperty("n")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Quote values
        /// </summary>
        [JsonProperty("v")]
        public FyersQuoteData? Values { get; set; }
    }

    /// <summary>
    /// Quotes response
    /// </summary>
    public class FyersQuotesResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Quote data
        /// </summary>
        [JsonProperty("d")]
        public List<FyersQuoteItem> Data { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Token response from authentication
    /// </summary>
    public class FyersTokenResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Access token
        /// </summary>
        [JsonProperty("access_token")]
        public string AccessToken { get; set; } = string.Empty;

        /// <summary>
        /// Refresh token
        /// </summary>
        [JsonProperty("refresh_token")]
        public string RefreshToken { get; set; } = string.Empty;

        /// <summary>
        /// Response message
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// OHLC data item
    /// </summary>
    public class FyersOhlcData
    {
        /// <summary>
        /// Fyers token
        /// </summary>
        [JsonProperty("fyToken")]
        public string FyersToken { get; set; } = string.Empty;

        /// <summary>
        /// Symbol
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Open price
        /// </summary>
        [JsonProperty("open_price")]
        public decimal OpenPrice { get; set; }

        /// <summary>
        /// High price
        /// </summary>
        [JsonProperty("high_price")]
        public decimal HighPrice { get; set; }

        /// <summary>
        /// Low price
        /// </summary>
        [JsonProperty("low_price")]
        public decimal LowPrice { get; set; }

        /// <summary>
        /// Close/LTP price
        /// </summary>
        [JsonProperty("close_price")]
        public decimal ClosePrice { get; set; }

        /// <summary>
        /// Previous close price
        /// </summary>
        [JsonProperty("prev_close_price")]
        public decimal PreviousClosePrice { get; set; }

        /// <summary>
        /// Volume
        /// </summary>
        [JsonProperty("volume")]
        public long Volume { get; set; }

        /// <summary>
        /// Timestamp
        /// </summary>
        [JsonProperty("tt")]
        public long Timestamp { get; set; }
    }

    /// <summary>
    /// OHLC item wrapper
    /// </summary>
    public class FyersOhlcItem
    {
        /// <summary>
        /// Symbol name
        /// </summary>
        [JsonProperty("n")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// OHLC values
        /// </summary>
        [JsonProperty("v")]
        public FyersOhlcData? Values { get; set; }
    }

    /// <summary>
    /// OHLC response
    /// </summary>
    public class FyersOhlcResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// OHLC data
        /// </summary>
        [JsonProperty("d")]
        public List<FyersOhlcItem> Data { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// LTP data item
    /// </summary>
    public class FyersLtpData
    {
        /// <summary>
        /// Fyers token
        /// </summary>
        [JsonProperty("fyToken")]
        public string FyersToken { get; set; } = string.Empty;

        /// <summary>
        /// Symbol
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Exchange
        /// </summary>
        [JsonProperty("exchange")]
        public int Exchange { get; set; }

        /// <summary>
        /// Last traded price
        /// </summary>
        [JsonProperty("lp")]
        public decimal LastPrice { get; set; }

        /// <summary>
        /// Change percentage
        /// </summary>
        [JsonProperty("chp")]
        public decimal ChangePercentage { get; set; }

        /// <summary>
        /// Change value
        /// </summary>
        [JsonProperty("ch")]
        public decimal Change { get; set; }
    }

    /// <summary>
    /// LTP item wrapper
    /// </summary>
    public class FyersLtpItem
    {
        /// <summary>
        /// Symbol name
        /// </summary>
        [JsonProperty("n")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// LTP values
        /// </summary>
        [JsonProperty("v")]
        public FyersLtpData? Values { get; set; }
    }

    /// <summary>
    /// LTP response
    /// </summary>
    public class FyersLtpResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// LTP data
        /// </summary>
        [JsonProperty("d")]
        public List<FyersLtpItem> Data { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Order history item (for tracking order state changes)
    /// </summary>
    public class FyersOrderHistoryItem
    {
        /// <summary>
        /// Fyers order ID
        /// </summary>
        [JsonProperty("id")]
        public string Id { get; set; } = string.Empty;

        /// <summary>
        /// Exchange order ID
        /// </summary>
        [JsonProperty("exchOrdId")]
        public string ExchangeOrderId { get; set; } = string.Empty;

        /// <summary>
        /// Symbol
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Order status
        /// </summary>
        [JsonProperty("status")]
        public int Status { get; set; }

        /// <summary>
        /// Status message
        /// </summary>
        [JsonProperty("message")]
        public string Message { get; set; } = string.Empty;

        /// <summary>
        /// Quantity
        /// </summary>
        [JsonProperty("qty")]
        public int Quantity { get; set; }

        /// <summary>
        /// Filled quantity
        /// </summary>
        [JsonProperty("filledQty")]
        public int FilledQuantity { get; set; }

        /// <summary>
        /// Limit price
        /// </summary>
        [JsonProperty("limitPrice")]
        public decimal LimitPrice { get; set; }

        /// <summary>
        /// Stop price
        /// </summary>
        [JsonProperty("stopPrice")]
        public decimal StopPrice { get; set; }

        /// <summary>
        /// Traded price
        /// </summary>
        [JsonProperty("tradedPrice")]
        public decimal TradedPrice { get; set; }

        /// <summary>
        /// Order date/time
        /// </summary>
        [JsonProperty("orderDateTime")]
        public string OrderDateTime { get; set; } = string.Empty;
    }

    /// <summary>
    /// Order history response
    /// </summary>
    public class FyersOrderHistoryResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Order history list
        /// </summary>
        [JsonProperty("orderBook")]
        public List<FyersOrderHistoryItem> Orders { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }

    /// <summary>
    /// Market depth data
    /// </summary>
    public class FyersMarketDepth
    {
        /// <summary>
        /// Symbol
        /// </summary>
        [JsonProperty("symbol")]
        public string Symbol { get; set; } = string.Empty;

        /// <summary>
        /// Fyers token
        /// </summary>
        [JsonProperty("fyToken")]
        public string FyersToken { get; set; } = string.Empty;

        /// <summary>
        /// LTP
        /// </summary>
        [JsonProperty("ltp")]
        public decimal LastPrice { get; set; }

        /// <summary>
        /// Total buy quantity
        /// </summary>
        [JsonProperty("totBuyQty")]
        public long TotalBuyQuantity { get; set; }

        /// <summary>
        /// Total sell quantity
        /// </summary>
        [JsonProperty("totSellQty")]
        public long TotalSellQuantity { get; set; }

        /// <summary>
        /// Best bid price
        /// </summary>
        [JsonProperty("bidPrice")]
        public decimal BidPrice { get; set; }

        /// <summary>
        /// Best bid quantity
        /// </summary>
        [JsonProperty("bidQty")]
        public int BidQuantity { get; set; }

        /// <summary>
        /// Best ask price
        /// </summary>
        [JsonProperty("askPrice")]
        public decimal AskPrice { get; set; }

        /// <summary>
        /// Best ask quantity
        /// </summary>
        [JsonProperty("askQty")]
        public int AskQuantity { get; set; }

        /// <summary>
        /// Bid levels (5 levels)
        /// </summary>
        [JsonProperty("bids")]
        public List<FyersDepthLevel> Bids { get; set; } = new();

        /// <summary>
        /// Ask levels (5 levels)
        /// </summary>
        [JsonProperty("ask")]
        public List<FyersDepthLevel> Asks { get; set; } = new();
    }

    /// <summary>
    /// Market depth item wrapper
    /// </summary>
    public class FyersDepthItem
    {
        /// <summary>
        /// Symbol name
        /// </summary>
        [JsonProperty("n")]
        public string Name { get; set; } = string.Empty;

        /// <summary>
        /// Status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Depth values
        /// </summary>
        [JsonProperty("v")]
        public FyersMarketDepth? Values { get; set; }
    }

    /// <summary>
    /// Market depth response
    /// </summary>
    public class FyersDepthResponse
    {
        /// <summary>
        /// Response status
        /// </summary>
        [JsonProperty("s")]
        public string Status { get; set; } = string.Empty;

        /// <summary>
        /// Response code
        /// </summary>
        [JsonProperty("code")]
        public int Code { get; set; }

        /// <summary>
        /// Depth data
        /// </summary>
        [JsonProperty("d")]
        public Dictionary<string, FyersMarketDepth> Data { get; set; } = new();

        /// <summary>
        /// Indicates if the response was successful
        /// </summary>
        public bool IsSuccess => Status?.ToLowerInvariant() == "ok" && Code >= 0;
    }
}
