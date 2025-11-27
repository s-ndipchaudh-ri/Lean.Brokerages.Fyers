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
using System.Net;
using System.Threading;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using QuantConnect.Brokerages.Fyers.Messages;
using QuantConnect.Logging;
using QuantConnect.Util;
using RestSharp;

namespace QuantConnect.Brokerages.Fyers.Api
{
    /// <summary>
    /// Production-grade Fyers REST API client with rate limiting, retry logic, and comprehensive error handling
    /// </summary>
    public class FyersApiClient : IDisposable
    {
        private readonly string _clientId;
        private readonly string _accessToken;
        private readonly RestClient _restClient;
        private readonly RateGate _restApiRateGate;
        private readonly RateGate _orderApiRateGate;
        private readonly RateGate _historyApiRateGate;
        private readonly JsonSerializerSettings _jsonSettings;
        private bool _disposed;

        /// <summary>
        /// Maximum retry attempts for failed requests
        /// </summary>
        private const int MaxRetries = 5;

        /// <summary>
        /// Delay between retry attempts in milliseconds
        /// </summary>
        private const int RetryDelayMs = 100;

        /// <summary>
        /// Session expiry callback hook
        /// </summary>
        private Action? _sessionExpiryHook;

        /// <summary>
        /// Creates a new instance of FyersApiClient
        /// </summary>
        /// <param name="clientId">Fyers App ID</param>
        /// <param name="accessToken">Access token from OAuth</param>
        public FyersApiClient(string clientId, string accessToken)
        {
            _clientId = clientId ?? throw new ArgumentNullException(nameof(clientId));
            _accessToken = accessToken ?? throw new ArgumentNullException(nameof(accessToken));

            // Initialize REST client
            _restClient = new RestClient(FyersConstants.ApiBaseUrl)
            {
                Timeout = FyersConstants.DefaultHttpTimeoutMs,
                UserAgent = "QuantConnect.Brokerages.Fyers/1.0"
            };

            // Initialize rate limiters
            _restApiRateGate = new RateGate(FyersConstants.RestApiRateLimit, TimeSpan.FromSeconds(1));
            _orderApiRateGate = new RateGate(FyersConstants.OrderApiRateLimit, TimeSpan.FromSeconds(1));
            _historyApiRateGate = new RateGate(FyersConstants.HistoryApiRateLimit, TimeSpan.FromSeconds(1));

            // JSON settings for decimal precision
            _jsonSettings = new JsonSerializerSettings
            {
                FloatParseHandling = FloatParseHandling.Decimal,
                NullValueHandling = NullValueHandling.Ignore
            };
        }

        /// <summary>
        /// Gets the authorization header value
        /// </summary>
        private string AuthorizationHeader => $"{_clientId}:{_accessToken}";

        /// <summary>
        /// Set a callback hook for session expiry (token timeout, expiry, etc.)
        /// </summary>
        /// <param name="method">Action to invoke when session expires</param>
        public void SetSessionExpiryHook(Action method)
        {
            _sessionExpiryHook = method;
        }

        #region User & Account APIs

        /// <summary>
        /// Gets user profile information
        /// </summary>
        /// <returns>User profile data</returns>
        public FyersResponse<FyersUserProfile> GetProfile()
        {
            return ExecuteRequest<FyersResponse<FyersUserProfile>>(
                FyersConstants.ProfileEndpoint,
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets fund/margin information
        /// </summary>
        /// <returns>Funds response with limits</returns>
        public FyersFundsResponse GetFunds()
        {
            return ExecuteRequest<FyersFundsResponse>(
                FyersConstants.FundsEndpoint,
                Method.GET,
                _restApiRateGate
            );
        }

        #endregion

        #region Holdings & Positions APIs

        /// <summary>
        /// Gets all holdings (delivery positions)
        /// </summary>
        /// <returns>Holdings response</returns>
        public FyersHoldingsResponse GetHoldings()
        {
            return ExecuteRequest<FyersHoldingsResponse>(
                FyersConstants.HoldingsEndpoint,
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets all open positions
        /// </summary>
        /// <returns>Positions response</returns>
        public FyersPositionsResponse GetPositions()
        {
            return ExecuteRequest<FyersPositionsResponse>(
                FyersConstants.PositionsEndpoint,
                Method.GET,
                _restApiRateGate
            );
        }

        #endregion

        #region Order APIs

        /// <summary>
        /// Places a new order
        /// </summary>
        /// <param name="request">Order request parameters</param>
        /// <returns>Order response with order ID</returns>
        public FyersOrderResponse PlaceOrder(FyersPlaceOrderRequest request)
        {
            return ExecuteRequest<FyersOrderResponse>(
                FyersConstants.OrdersSyncEndpoint,
                Method.POST,
                _orderApiRateGate,
                request
            );
        }

        /// <summary>
        /// Modifies an existing order
        /// </summary>
        /// <param name="request">Modify request parameters</param>
        /// <returns>Order response</returns>
        public FyersOrderResponse ModifyOrder(FyersModifyOrderRequest request)
        {
            return ExecuteRequest<FyersOrderResponse>(
                FyersConstants.OrdersSyncEndpoint,
                Method.PATCH,
                _orderApiRateGate,
                request
            );
        }

        /// <summary>
        /// Cancels an existing order
        /// </summary>
        /// <param name="orderId">Order ID to cancel</param>
        /// <returns>Order response</returns>
        public FyersOrderResponse CancelOrder(string orderId)
        {
            var request = new FyersCancelOrderRequest { OrderId = orderId };
            return ExecuteRequest<FyersOrderResponse>(
                FyersConstants.OrdersSyncEndpoint,
                Method.DELETE,
                _orderApiRateGate,
                request
            );
        }

        /// <summary>
        /// Gets order book (all orders for the day)
        /// </summary>
        /// <returns>Order book response</returns>
        public FyersOrderBookResponse GetOrderBook()
        {
            return ExecuteRequest<FyersOrderBookResponse>(
                FyersConstants.OrderBookEndpoint,
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets order history for a specific order
        /// </summary>
        /// <param name="orderId">Order ID to get history for</param>
        /// <returns>Order history response</returns>
        public FyersOrderHistoryResponse GetOrderHistory(string orderId)
        {
            return ExecuteRequest<FyersOrderHistoryResponse>(
                $"{FyersConstants.OrderBookEndpoint}?id={Uri.EscapeDataString(orderId)}",
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets trade book (all executed trades for the day)
        /// </summary>
        /// <returns>Trade book response</returns>
        public FyersTradeBookResponse GetTradeBook()
        {
            return ExecuteRequest<FyersTradeBookResponse>(
                FyersConstants.TradeBookEndpoint,
                Method.GET,
                _restApiRateGate
            );
        }

        #endregion

        #region Market Data APIs

        /// <summary>
        /// Gets quotes for multiple symbols (full quote with OHLC, volume, etc.)
        /// </summary>
        /// <param name="symbols">Array of symbols (e.g., NSE:SBIN-EQ)</param>
        /// <returns>Quotes response</returns>
        public FyersQuotesResponse GetQuotes(string[] symbols)
        {
            var symbolsParam = string.Join(",", symbols);
            return ExecuteRequest<FyersQuotesResponse>(
                $"{FyersConstants.QuotesEndpoint}?symbols={Uri.EscapeDataString(symbolsParam)}",
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets OHLC data for multiple symbols
        /// </summary>
        /// <param name="symbols">Array of symbols (e.g., NSE:SBIN-EQ)</param>
        /// <returns>OHLC response</returns>
        public FyersOhlcResponse GetOHLC(string[] symbols)
        {
            var symbolsParam = string.Join(",", symbols);
            return ExecuteRequest<FyersOhlcResponse>(
                $"{FyersConstants.QuotesEndpoint}?symbols={Uri.EscapeDataString(symbolsParam)}",
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets LTP (Last Traded Price) for multiple symbols
        /// </summary>
        /// <param name="symbols">Array of symbols (e.g., NSE:SBIN-EQ)</param>
        /// <returns>LTP response</returns>
        public FyersLtpResponse GetLTP(string[] symbols)
        {
            var symbolsParam = string.Join(",", symbols);
            return ExecuteRequest<FyersLtpResponse>(
                $"{FyersConstants.QuotesEndpoint}?symbols={Uri.EscapeDataString(symbolsParam)}",
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets market depth (Level 2 data) for a symbol
        /// </summary>
        /// <param name="symbol">Symbol (e.g., NSE:SBIN-EQ)</param>
        /// <returns>Market depth response</returns>
        public FyersDepthResponse GetMarketDepth(string symbol)
        {
            return ExecuteRequest<FyersDepthResponse>(
                $"{FyersConstants.DepthEndpoint}?symbol={Uri.EscapeDataString(symbol)}&ohlcv_flag=1",
                Method.GET,
                _restApiRateGate
            );
        }

        /// <summary>
        /// Gets historical candle data
        /// </summary>
        /// <param name="request">History request parameters</param>
        /// <returns>History response with candles</returns>
        public FyersHistoryResponse GetHistory(FyersHistoryRequest request)
        {
            var queryParams = $"symbol={Uri.EscapeDataString(request.Symbol)}" +
                              $"&resolution={request.Resolution}" +
                              $"&date_format={request.DateFormat}" +
                              $"&range_from={request.RangeFrom}" +
                              $"&range_to={request.RangeTo}" +
                              $"&cont_flag={request.ContinuousFlag}";

            return ExecuteRequest<FyersHistoryResponse>(
                $"{FyersConstants.HistoryEndpoint}?{queryParams}",
                Method.GET,
                _historyApiRateGate
            );
        }

        #endregion

        #region Private Methods

        /// <summary>
        /// Executes a REST API request with rate limiting, retry logic, and comprehensive error handling
        /// </summary>
        private T ExecuteRequest<T>(string endpoint, Method method, RateGate rateGate, object? body = null)
            where T : class, new()
        {
            // Wait for rate limiter
            rateGate.WaitToProceed();

            var request = new RestRequest(endpoint, method);
            request.AddHeader("Authorization", AuthorizationHeader);
            request.AddHeader("Content-Type", "application/json");
            request.AddHeader("version", "3");

            if (body != null)
            {
                var json = JsonConvert.SerializeObject(body, _jsonSettings);
                request.AddParameter("application/json", json, ParameterType.RequestBody);
            }

            var retryCount = 0;
            IRestResponse response = null;

            while (retryCount <= MaxRetries)
            {
                try
                {
                    response = _restClient.Execute(request);

                    // Check for network/connection errors that warrant retry
                    if (response.ErrorException != null)
                    {
                        if (retryCount < MaxRetries)
                        {
                            retryCount++;
                            Log.Trace($"FyersApiClient.ExecuteRequest: Retry {retryCount}/{MaxRetries} - {response.ErrorMessage}");
                            Thread.Sleep(RetryDelayMs * retryCount);
                            continue;
                        }

                        Log.Error($"FyersApiClient.ExecuteRequest: Request failed after {MaxRetries} retries - {response.ErrorMessage}");
                        throw new NetworkException($"Request failed: {response.ErrorMessage}", HttpStatusCode.ServiceUnavailable, -1, response.ErrorException);
                    }

                    // Check for rate limiting (429)
                    if (response.StatusCode == HttpStatusCode.TooManyRequests)
                    {
                        if (retryCount < MaxRetries)
                        {
                            retryCount++;
                            Log.Trace($"FyersApiClient.ExecuteRequest: Rate limited, retry {retryCount}/{MaxRetries}");
                            Thread.Sleep(RetryDelayMs * retryCount * 2); // Double delay for rate limit
                            continue;
                        }

                        throw new RateLimitException("Rate limit exceeded", HttpStatusCode.TooManyRequests);
                    }

                    // Success or non-retryable error
                    break;
                }
                catch (FyersException)
                {
                    throw;
                }
                catch (Exception ex) when (retryCount < MaxRetries)
                {
                    retryCount++;
                    Log.Trace($"FyersApiClient.ExecuteRequest: Exception retry {retryCount}/{MaxRetries} - {ex.Message}");
                    Thread.Sleep(RetryDelayMs * retryCount);
                }
            }

            if (response == null)
            {
                throw new NetworkException("No response received from Fyers API", HttpStatusCode.ServiceUnavailable);
            }

            // Handle HTTP errors
            if (response.StatusCode != HttpStatusCode.OK)
            {
                HandleHttpError(response);
            }

            if (string.IsNullOrEmpty(response.Content))
            {
                Log.Error("FyersApiClient.ExecuteRequest: Empty response received");
                throw new DataException("Empty response received from Fyers API", HttpStatusCode.NoContent);
            }

            // Parse and validate response
            var result = JsonConvert.DeserializeObject<T>(response.Content, _jsonSettings);

            if (result == null)
            {
                Log.Error($"FyersApiClient.ExecuteRequest: Failed to deserialize response - {response.Content}");
                throw new DataException("Failed to deserialize API response", HttpStatusCode.InternalServerError);
            }

            return result;
        }

        /// <summary>
        /// Handles HTTP error responses and throws appropriate exceptions
        /// </summary>
        private void HandleHttpError(IRestResponse response)
        {
            var errorCode = -1;
            var errorType = "GeneralException";
            var message = $"HTTP {response.StatusCode}";

            // Try to parse error details from response
            if (!string.IsNullOrEmpty(response.Content))
            {
                try
                {
                    var errorObj = JObject.Parse(response.Content);
                    message = errorObj["message"]?.ToString() ?? message;
                    errorCode = errorObj["code"]?.Value<int>() ?? errorCode;
                    errorType = errorObj["error_type"]?.ToString() ?? errorType;
                }
                catch
                {
                    message = response.Content;
                }
            }

            Log.Error($"FyersApiClient.HandleHttpError: {errorType} - {message} (Code: {errorCode})");

            // Map error types to specific exceptions
            switch (errorType.ToLowerInvariant())
            {
                case "tokenexception":
                case "token_expired":
                case "invalid_token":
                    _sessionExpiryHook?.Invoke();
                    throw new TokenException(message, response.StatusCode, errorCode);

                case "permissionexception":
                case "unauthorized":
                    throw new PermissionException(message, response.StatusCode, errorCode);

                case "orderexception":
                case "order_error":
                    throw new OrderException(message, response.StatusCode, errorCode);

                case "inputexception":
                case "validation_error":
                case "invalid_input":
                    throw new InputException(message, response.StatusCode, errorCode);

                case "dataexception":
                case "data_error":
                    throw new DataException(message, response.StatusCode, errorCode);

                case "networkexception":
                case "network_error":
                    throw new NetworkException(message, response.StatusCode, errorCode);

                case "marketexception":
                case "market_closed":
                    throw new MarketException(message, response.StatusCode, errorCode);

                default:
                    // Map HTTP status codes to exception types
                    switch (response.StatusCode)
                    {
                        case HttpStatusCode.Unauthorized:
                        case HttpStatusCode.Forbidden:
                            _sessionExpiryHook?.Invoke();
                            throw new TokenException(message, response.StatusCode, errorCode);

                        case HttpStatusCode.BadRequest:
                            throw new InputException(message, response.StatusCode, errorCode);

                        case HttpStatusCode.TooManyRequests:
                            throw new RateLimitException(message, response.StatusCode, errorCode);

                        case HttpStatusCode.ServiceUnavailable:
                        case HttpStatusCode.GatewayTimeout:
                            throw new NetworkException(message, response.StatusCode, errorCode);

                        default:
                            throw new GeneralException(message, response.StatusCode, errorCode);
                    }
            }
        }

        #endregion

        #region IDisposable

        /// <summary>
        /// Disposes resources
        /// </summary>
        public void Dispose()
        {
            Dispose(true);
            GC.SuppressFinalize(this);
        }

        /// <summary>
        /// Disposes resources
        /// </summary>
        protected virtual void Dispose(bool disposing)
        {
            if (_disposed)
                return;

            if (disposing)
            {
                // RestClient v106 doesn't implement IDisposable
                _restApiRateGate?.Dispose();
                _orderApiRateGate?.Dispose();
                _historyApiRateGate?.Dispose();
            }

            _disposed = true;
        }

        #endregion
    }
}
