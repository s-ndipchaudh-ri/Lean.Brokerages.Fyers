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
using NUnit.Framework;
using QuantConnect.Configuration;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    /// <summary>
    /// Helper class for managing Fyers test credentials and conditional test skipping
    /// </summary>
    public static class FyersTestCredentials
    {
        private static bool? _hasCredentials;
        private static string _clientId;
        private static string _accessToken;
        private static string _tradingSegment;
        private static string _productType;

        /// <summary>
        /// Gets the Fyers client ID from configuration
        /// </summary>
        public static string ClientId
        {
            get
            {
                EnsureCredentialsLoaded();
                return _clientId;
            }
        }

        /// <summary>
        /// Gets the Fyers access token from configuration
        /// </summary>
        public static string AccessToken
        {
            get
            {
                EnsureCredentialsLoaded();
                return _accessToken;
            }
        }

        /// <summary>
        /// Gets the trading segment from configuration
        /// </summary>
        public static string TradingSegment
        {
            get
            {
                EnsureCredentialsLoaded();
                return _tradingSegment;
            }
        }

        /// <summary>
        /// Gets the product type from configuration
        /// </summary>
        public static string ProductType
        {
            get
            {
                EnsureCredentialsLoaded();
                return _productType;
            }
        }

        /// <summary>
        /// Gets whether valid credentials are available
        /// </summary>
        public static bool HasCredentials
        {
            get
            {
                EnsureCredentialsLoaded();
                return _hasCredentials ?? false;
            }
        }

        /// <summary>
        /// Skips the test if credentials are not available
        /// </summary>
        public static void SkipIfNoCredentials()
        {
            if (!HasCredentials)
            {
                Assert.Ignore("Test skipped: Fyers credentials not configured. " +
                    "Set 'fyers-client-id' and 'fyers-access-token' in config or environment variables to run this test.");
            }
        }

        /// <summary>
        /// Gets a skip reason message for tests that require credentials
        /// </summary>
        public static string SkipReason =>
            "Fyers credentials not configured. Set 'fyers-client-id' and 'fyers-access-token' to run integration tests.";

        /// <summary>
        /// Loads credentials from configuration
        /// </summary>
        private static void EnsureCredentialsLoaded()
        {
            if (_hasCredentials.HasValue)
            {
                return;
            }

            try
            {
                // Try to reload configuration
                TestSetup.ReloadConfiguration();
            }
            catch
            {
                // Ignore errors during configuration reload
            }

            // Load from config
            _clientId = Config.Get("fyers-client-id", "");
            _accessToken = Config.Get("fyers-access-token", "");
            _tradingSegment = Config.Get("fyers-trading-segment", "EQUITY");
            _productType = Config.Get("fyers-product-type", "INTRADAY");

            // Also check environment variables
            if (string.IsNullOrEmpty(_clientId))
            {
                _clientId = Environment.GetEnvironmentVariable("QC_FYERS_CLIENT_ID") ?? "";
            }
            if (string.IsNullOrEmpty(_accessToken))
            {
                _accessToken = Environment.GetEnvironmentVariable("QC_FYERS_ACCESS_TOKEN") ?? "";
            }

            _hasCredentials = !string.IsNullOrEmpty(_clientId) && !string.IsNullOrEmpty(_accessToken);
        }

        /// <summary>
        /// Resets the cached credentials (useful for testing)
        /// </summary>
        public static void Reset()
        {
            _hasCredentials = null;
            _clientId = null;
            _accessToken = null;
            _tradingSegment = null;
            _productType = null;
        }
    }
}
