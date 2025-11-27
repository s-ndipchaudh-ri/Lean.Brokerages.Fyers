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

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Base exception class for Fyers API errors
    /// </summary>
    public class FyersException : Exception
    {
        /// <summary>
        /// HTTP status code associated with the error
        /// </summary>
        public HttpStatusCode StatusCode { get; }

        /// <summary>
        /// Fyers error code from API response
        /// </summary>
        public int ErrorCode { get; }

        /// <summary>
        /// Creates a new FyersException
        /// </summary>
        /// <param name="message">Error message</param>
        /// <param name="httpStatus">HTTP status code</param>
        /// <param name="errorCode">Fyers error code</param>
        /// <param name="innerException">Inner exception</param>
        public FyersException(string message, HttpStatusCode httpStatus = HttpStatusCode.InternalServerError, int errorCode = -1, Exception innerException = null)
            : base(message, innerException)
        {
            StatusCode = httpStatus;
            ErrorCode = errorCode;
        }
    }

    /// <summary>
    /// Exception thrown when there's a general/unknown error
    /// </summary>
    public class GeneralException : FyersException
    {
        /// <summary>
        /// Creates a new GeneralException
        /// </summary>
        public GeneralException(string message, HttpStatusCode httpStatus = HttpStatusCode.InternalServerError, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when the access token is invalid or expired
    /// </summary>
    public class TokenException : FyersException
    {
        /// <summary>
        /// Creates a new TokenException
        /// </summary>
        public TokenException(string message, HttpStatusCode httpStatus = HttpStatusCode.Forbidden, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when the user doesn't have permission for an operation
    /// </summary>
    public class PermissionException : FyersException
    {
        /// <summary>
        /// Creates a new PermissionException
        /// </summary>
        public PermissionException(string message, HttpStatusCode httpStatus = HttpStatusCode.Forbidden, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when an order operation fails
    /// </summary>
    public class OrderException : FyersException
    {
        /// <summary>
        /// Creates a new OrderException
        /// </summary>
        public OrderException(string message, HttpStatusCode httpStatus = HttpStatusCode.BadRequest, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when input validation fails
    /// </summary>
    public class InputException : FyersException
    {
        /// <summary>
        /// Creates a new InputException
        /// </summary>
        public InputException(string message, HttpStatusCode httpStatus = HttpStatusCode.BadRequest, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when there's an issue with data retrieval
    /// </summary>
    public class DataException : FyersException
    {
        /// <summary>
        /// Creates a new DataException
        /// </summary>
        public DataException(string message, HttpStatusCode httpStatus = HttpStatusCode.BadGateway, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when there's a network connectivity issue
    /// </summary>
    public class NetworkException : FyersException
    {
        /// <summary>
        /// Creates a new NetworkException
        /// </summary>
        public NetworkException(string message, HttpStatusCode httpStatus = HttpStatusCode.ServiceUnavailable, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when rate limit is exceeded
    /// </summary>
    public class RateLimitException : FyersException
    {
        /// <summary>
        /// Creates a new RateLimitException
        /// </summary>
        public RateLimitException(string message, HttpStatusCode httpStatus = HttpStatusCode.TooManyRequests, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }

    /// <summary>
    /// Exception thrown when market is closed or unavailable
    /// </summary>
    public class MarketException : FyersException
    {
        /// <summary>
        /// Creates a new MarketException
        /// </summary>
        public MarketException(string message, HttpStatusCode httpStatus = HttpStatusCode.ServiceUnavailable, int errorCode = -1, Exception innerException = null)
            : base(message, httpStatus, errorCode, innerException) { }
    }
}
