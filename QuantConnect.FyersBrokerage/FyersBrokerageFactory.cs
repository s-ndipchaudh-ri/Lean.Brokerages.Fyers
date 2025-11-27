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
using System.Linq;
using QuantConnect.Configuration;
using QuantConnect.Data;
using QuantConnect.Interfaces;
using QuantConnect.Orders;
using QuantConnect.Orders.Fees;
using QuantConnect.Packets;
using QuantConnect.Securities;
using QuantConnect.Util;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Factory class for creating Fyers brokerage instances
    /// </summary>
    public class FyersBrokerageFactory : BrokerageFactory
    {
        /// <summary>
        /// Configuration keys for Fyers brokerage
        /// </summary>
        public static class ConfigKeys
        {
            /// <summary>
            /// Fyers App ID (Client ID)
            /// </summary>
            public const string ClientId = "fyers-client-id";

            /// <summary>
            /// Fyers Access Token from OAuth
            /// </summary>
            public const string AccessToken = "fyers-access-token";

            /// <summary>
            /// Trading segment (EQUITY or COMMODITY)
            /// </summary>
            public const string TradingSegment = "fyers-trading-segment";

            /// <summary>
            /// Product type (CNC, INTRADAY, MARGIN, CO, BO)
            /// </summary>
            public const string ProductType = "fyers-product-type";
        }

        /// <summary>
        /// Gets the brokerage data required to run the brokerage from configuration/disk
        /// </summary>
        public override Dictionary<string, string> BrokerageData => new()
        {
            { ConfigKeys.ClientId, Config.Get(ConfigKeys.ClientId, "") },
            { ConfigKeys.AccessToken, Config.Get(ConfigKeys.AccessToken, "") },
            { ConfigKeys.TradingSegment, Config.Get(ConfigKeys.TradingSegment, "EQUITY") },
            { ConfigKeys.ProductType, Config.Get(ConfigKeys.ProductType, "INTRADAY") }
        };

        /// <summary>
        /// Initializes a new instance of the <see cref="FyersBrokerageFactory"/> class
        /// </summary>
        public FyersBrokerageFactory() : base(typeof(FyersBrokerage))
        {
        }

        /// <summary>
        /// Gets a brokerage model that can be used to model this brokerage's unique behaviors
        /// </summary>
        /// <param name="orderProvider">The order provider</param>
        /// <returns>The brokerage model for Fyers</returns>
        public override IBrokerageModel GetBrokerageModel(IOrderProvider orderProvider)
        {
            return new FyersBrokerageModel();
        }

        /// <summary>
        /// Creates a new IBrokerage instance
        /// </summary>
        /// <param name="job">The job packet to create the brokerage for</param>
        /// <param name="algorithm">The algorithm instance</param>
        /// <returns>A new brokerage instance</returns>
        public override IBrokerage CreateBrokerage(LiveNodePacket job, IAlgorithm algorithm)
        {
            var errors = new List<string>();

            // Get configuration from job or config file
            var clientId = Read<string>(job.BrokerageData, ConfigKeys.ClientId, errors);
            var accessToken = Read<string>(job.BrokerageData, ConfigKeys.AccessToken, errors);
            var tradingSegment = Read<string>(job.BrokerageData, ConfigKeys.TradingSegment, errors, "EQUITY");
            var productType = Read<string>(job.BrokerageData, ConfigKeys.ProductType, errors, "INTRADAY");

            if (errors.Count > 0)
            {
                throw new ArgumentException($"FyersBrokerageFactory: Missing required configuration: {string.Join(", ", errors)}");
            }

            // Get aggregator
            var aggregator = Composer.Instance.GetPart<IDataAggregator>();
            if (aggregator == null)
            {
                aggregator = Composer.Instance.GetExportedValueByTypeName<IDataAggregator>(
                    Config.Get("data-aggregator", "QuantConnect.Lean.Engine.DataFeeds.AggregationManager"),
                    forceTypeNameOnExisting: false);
            }

            // Create brokerage instance
            var brokerage = new FyersBrokerage(
                tradingSegment,
                productType,
                clientId,
                accessToken,
                algorithm,
                algorithm.Portfolio,
                aggregator
            );

            // Add message handler
            Composer.Instance.AddPart<IDataQueueHandler>(brokerage);

            return brokerage;
        }

        /// <summary>
        /// Performs application-defined tasks associated with freeing, releasing, or resetting unmanaged resources.
        /// </summary>
        public override void Dispose()
        {
            // Nothing to dispose
        }

        /// <summary>
        /// Reads a value from the brokerage data dictionary
        /// </summary>
        private static T Read<T>(IReadOnlyDictionary<string, string> brokerageData, string key, List<string> errors, T? defaultValue = default)
        {
            if (brokerageData.TryGetValue(key, out var value) && !string.IsNullOrWhiteSpace(value))
            {
                try
                {
                    if (typeof(T) == typeof(string))
                    {
                        return (T)(object)value;
                    }
                    return (T)Convert.ChangeType(value, typeof(T));
                }
                catch
                {
                    errors.Add($"Invalid value for {key}: {value}");
                }
            }
            else if (defaultValue == null || (defaultValue is string s && string.IsNullOrEmpty(s)))
            {
                errors.Add(key);
            }

            return defaultValue ?? default!;
        }
    }

    /// <summary>
    /// Fyers brokerage model for order validation and fee calculation
    /// </summary>
    public class FyersBrokerageModel : DefaultBrokerageModel
    {
        /// <summary>
        /// Default markets for Fyers
        /// </summary>
        public override IReadOnlyDictionary<SecurityType, string> DefaultMarkets { get; } = new Dictionary<SecurityType, string>
        {
            { SecurityType.Equity, Market.India },
            { SecurityType.Index, Market.India },
            { SecurityType.Future, Market.India },
            { SecurityType.Option, Market.India }
        };

        /// <summary>
        /// Creates a new instance of the FyersBrokerageModel
        /// </summary>
        public FyersBrokerageModel() : base(AccountType.Margin)
        {
        }

        /// <summary>
        /// Gets the supported order types for Fyers
        /// </summary>
        public virtual IReadOnlyList<OrderType> SupportedOrderTypes => new List<OrderType>
        {
            OrderType.Market,
            OrderType.Limit,
            OrderType.StopMarket,
            OrderType.StopLimit
        };

        /// <summary>
        /// Returns true if the brokerage could accept this order
        /// </summary>
        /// <param name="security">The security being ordered</param>
        /// <param name="order">The order to be processed</param>
        /// <param name="message">If this function returns false, a brokerage message detailing why the order may not be submitted</param>
        /// <returns>True if the brokerage could process the order, false otherwise</returns>
        public override bool CanSubmitOrder(Security security, Order order, out BrokerageMessageEvent message)
        {
            message = null!;

            // Check security type
            if (security.Type != SecurityType.Equity &&
                security.Type != SecurityType.Index &&
                security.Type != SecurityType.Future &&
                security.Type != SecurityType.Option)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidSecurityType",
                    $"Fyers does not support {security.Type} security type");
                return false;
            }

            // Check order type
            if (!SupportedOrderTypes.Contains(order.Type))
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidOrderType",
                    $"Fyers does not support {order.Type} order type. Supported: Market, Limit, StopMarket, StopLimit");
                return false;
            }

            // Check for fractional quantities (Fyers only supports whole numbers)
            if (order.Quantity != Math.Floor(order.Quantity))
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidQuantity",
                    "Fyers does not support fractional quantities");
                return false;
            }

            // Check market
            if (security.Symbol.ID.Market != Market.India)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidMarket",
                    $"Fyers only supports India market. Got: {security.Symbol.ID.Market}");
                return false;
            }

            return base.CanSubmitOrder(security, order, out message);
        }

        /// <summary>
        /// Returns true if the brokerage would allow updating the order
        /// </summary>
        /// <param name="security">The security of the order</param>
        /// <param name="order">The order to be updated</param>
        /// <param name="request">The requested update to be made to the order</param>
        /// <param name="message">If this function returns false, a brokerage message detailing why the order may not be updated</param>
        /// <returns>True if the brokerage would allow updating the order, false otherwise</returns>
        public override bool CanUpdateOrder(Security security, Order order, UpdateOrderRequest request, out BrokerageMessageEvent message)
        {
            message = null!;

            // Cannot update market orders
            if (order.Type == OrderType.Market)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidOrderType",
                    "Fyers does not support updating market orders");
                return false;
            }

            // Cannot update filled orders
            if (order.Status == OrderStatus.Filled)
            {
                message = new BrokerageMessageEvent(BrokerageMessageType.Warning, "InvalidOrderStatus",
                    "Cannot update a filled order");
                return false;
            }

            return base.CanUpdateOrder(security, order, request, out message);
        }

        /// <summary>
        /// Gets the brokerage's fee model
        /// </summary>
        /// <param name="security">The security to get fees for</param>
        /// <returns>The fee model for Fyers</returns>
        public override IFeeModel GetFeeModel(Security security)
        {
            return new FyersFeeModel();
        }
    }

    /// <summary>
    /// Fyers fee model based on actual brokerage charges
    /// </summary>
    public class FyersFeeModel : FeeModel
    {
        /// <summary>
        /// Fyers charges for different segments:
        /// - Equity Delivery: Zero brokerage
        /// - Equity Intraday: Rs 20 per order or 0.03% whichever is lower
        /// - F&O: Rs 20 per order
        /// - Currency: Rs 20 per order
        /// - Commodity: Rs 20 per order
        /// </summary>
        private const decimal FlatFeePerOrder = 20m;
        private const decimal IntradayPercentageFee = 0.0003m; // 0.03%

        /// <summary>
        /// Gets the order fee for the specified order
        /// </summary>
        /// <param name="parameters">Order fee parameters</param>
        /// <returns>The order fee</returns>
        public override OrderFee GetOrderFee(OrderFeeParameters parameters)
        {
            var order = parameters.Order;
            var security = parameters.Security;

            decimal fee = 0m;

            // Calculate order value
            var orderValue = Math.Abs(order.GetValue(security));

            // Determine fee based on product type and security type
            if (order.Properties is IndiaOrderProperties indiaProps)
            {
                if (indiaProps.ProductType == "CNC" && security.Type == SecurityType.Equity)
                {
                    // Zero brokerage for equity delivery
                    fee = 0m;
                }
                else if (indiaProps.ProductType == "INTRADAY" && security.Type == SecurityType.Equity)
                {
                    // Equity intraday: Rs 20 or 0.03% whichever is lower
                    fee = Math.Min(FlatFeePerOrder, orderValue * IntradayPercentageFee);
                }
                else
                {
                    // All other cases: flat Rs 20
                    fee = FlatFeePerOrder;
                }
            }
            else
            {
                // Default: flat Rs 20 per order
                fee = FlatFeePerOrder;
            }

            return new OrderFee(new CashAmount(fee, Currencies.INR));
        }
    }
}
