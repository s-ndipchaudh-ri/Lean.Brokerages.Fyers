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
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    /// <summary>
    /// Helper class for creating test symbols without requiring map file provider
    /// </summary>
    public static class TestSymbolHelper
    {
        /// <summary>
        /// Creates an equity symbol without map file lookup
        /// </summary>
        public static Symbol CreateEquity(string ticker, string market = Market.India)
        {
            // Create SecurityIdentifier directly without map file lookup
            var sid = SecurityIdentifier.GenerateEquity(ticker, market, mapSymbol: false);
            return new Symbol(sid, ticker);
        }

        /// <summary>
        /// Creates an index symbol without map file lookup
        /// </summary>
        public static Symbol CreateIndex(string ticker, string market = Market.India)
        {
            var sid = SecurityIdentifier.GenerateIndex(ticker, market);
            return new Symbol(sid, ticker);
        }

        /// <summary>
        /// Creates a future symbol
        /// </summary>
        public static Symbol CreateFuture(string ticker, string market, DateTime expiry)
        {
            return Symbol.CreateFuture(ticker, market, expiry);
        }

        /// <summary>
        /// Creates an option symbol without map file lookup
        /// </summary>
        public static Symbol CreateOption(string underlying, string market, OptionStyle style, OptionRight right, decimal strike, DateTime expiry)
        {
            // Use mapSymbol: false to avoid requiring MapFileProvider
            return Symbol.CreateOption(underlying, market, style, right, strike, expiry, mapSymbol: false);
        }

        /// <summary>
        /// Creates a forex symbol
        /// </summary>
        public static Symbol CreateForex(string ticker, string market = Market.Oanda)
        {
            var sid = SecurityIdentifier.GenerateForex(ticker, market);
            return new Symbol(sid, ticker);
        }
    }
}
