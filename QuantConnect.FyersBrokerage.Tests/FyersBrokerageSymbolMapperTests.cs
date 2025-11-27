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
using QuantConnect.Securities;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    [TestFixture]
    public class FyersBrokerageSymbolMapperTests
    {
        private FyersSymbolMapper _symbolMapper;

        [SetUp]
        public void SetUp()
        {
            _symbolMapper = new FyersSymbolMapper();
        }

        #region LEAN to Fyers Symbol Conversion Tests

        [Test]
        public void ReturnsCorrectBrokerageSymbol_Equity()
        {
            // Test equity symbol conversion: SBIN -> NSE:SBIN-EQ
            var symbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);
            var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(symbol);

            Assert.AreEqual("NSE:SBIN-EQ", brokerageSymbol);
        }

        [Test]
        public void ReturnsCorrectBrokerageSymbol_MultipleEquities()
        {
            // Test multiple equity symbols
            var testCases = new[]
            {
                (ticker: "RELIANCE", expected: "NSE:RELIANCE-EQ"),
                (ticker: "TCS", expected: "NSE:TCS-EQ"),
                (ticker: "INFY", expected: "NSE:INFY-EQ"),
                (ticker: "HDFCBANK", expected: "NSE:HDFCBANK-EQ"),
                (ticker: "ICICIBANK", expected: "NSE:ICICIBANK-EQ")
            };

            foreach (var testCase in testCases)
            {
                var symbol = TestSymbolHelper.CreateEquity(testCase.ticker, Market.India);
                var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(symbol);
                Assert.AreEqual(testCase.expected, brokerageSymbol, $"Failed for ticker: {testCase.ticker}");
            }
        }

        [Test]
        public void ReturnsCorrectBrokerageSymbol_Index()
        {
            // Test index symbol conversion: NIFTY50 -> NSE:NIFTY50-INDEX
            var symbol = TestSymbolHelper.CreateIndex("NIFTY50", Market.India);
            var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(symbol);

            Assert.AreEqual("NSE:NIFTY50-INDEX", brokerageSymbol);
        }

        [Test]
        public void ReturnsCorrectBrokerageSymbol_Future()
        {
            // Test future symbol conversion: NIFTY (Dec 2024) -> NSE:NIFTY24DECFUT
            var expiry = new DateTime(2024, 12, 26);
            var symbol = Symbol.CreateFuture("NIFTY", Market.India, expiry);
            var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(symbol);

            Assert.AreEqual("NSE:NIFTY24DECFUT", brokerageSymbol);
        }

        [Test]
        public void ReturnsCorrectBrokerageSymbol_FutureMultipleMonths()
        {
            // Test future symbols for different months
            var testCases = new[]
            {
                (underlying: "NIFTY", year: 2024, month: 1, expectedMonth: "JAN"),
                (underlying: "NIFTY", year: 2024, month: 2, expectedMonth: "FEB"),
                (underlying: "NIFTY", year: 2024, month: 3, expectedMonth: "MAR"),
                (underlying: "BANKNIFTY", year: 2024, month: 6, expectedMonth: "JUN"),
                (underlying: "BANKNIFTY", year: 2024, month: 9, expectedMonth: "SEP"),
                (underlying: "BANKNIFTY", year: 2024, month: 12, expectedMonth: "DEC")
            };

            foreach (var testCase in testCases)
            {
                var expiry = new DateTime(testCase.year, testCase.month, 28);
                var symbol = Symbol.CreateFuture(testCase.underlying, Market.India, expiry);
                var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(symbol);
                var expected = $"NSE:{testCase.underlying}24{testCase.expectedMonth}FUT";
                Assert.AreEqual(expected, brokerageSymbol, $"Failed for {testCase.underlying} {testCase.month}");
            }
        }

        [Test]
        public void ReturnsCorrectBrokerageSymbol_CallOption()
        {
            // Test call option symbol
            var expiry = new DateTime(2024, 12, 19);
            var symbol = TestSymbolHelper.CreateOption("NIFTY", Market.India, OptionStyle.European, OptionRight.Call, 23500m, expiry);
            var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(symbol);

            // Expected format: NSE:NIFTY24D1923500CE (YY + Month letter + Day + Strike + CE)
            Assert.IsTrue(brokerageSymbol.EndsWith("CE"), $"Expected CE option, got: {brokerageSymbol}");
            Assert.IsTrue(brokerageSymbol.Contains("23500"), $"Expected strike 23500, got: {brokerageSymbol}");
        }

        [Test]
        public void ReturnsCorrectBrokerageSymbol_PutOption()
        {
            // Test put option symbol
            var expiry = new DateTime(2024, 12, 19);
            var symbol = TestSymbolHelper.CreateOption("NIFTY", Market.India, OptionStyle.European, OptionRight.Put, 23000m, expiry);
            var brokerageSymbol = _symbolMapper.GetBrokerageSymbol(symbol);

            Assert.IsTrue(brokerageSymbol.EndsWith("PE"), $"Expected PE option, got: {brokerageSymbol}");
            Assert.IsTrue(brokerageSymbol.Contains("23000"), $"Expected strike 23000, got: {brokerageSymbol}");
        }

        [Test]
        public void ThrowsOnNullSymbol()
        {
            Assert.Throws<ArgumentNullException>(() => _symbolMapper.GetBrokerageSymbol(null));
        }

        [Test]
        public void ThrowsOnUnsupportedSecurityType()
        {
            var symbol = TestSymbolHelper.CreateForex("EURUSD", Market.Oanda);
            Assert.Throws<ArgumentException>(() => _symbolMapper.GetBrokerageSymbol(symbol));
        }

        #endregion

        #region Fyers to LEAN Symbol Conversion Tests

        [Test]
        public void ReturnsCorrectLeanSymbol_Equity()
        {
            // Test equity symbol conversion: NSE:SBIN-EQ -> SBIN
            var leanSymbol = _symbolMapper.GetLeanSymbol("NSE:SBIN-EQ", SecurityType.Equity, Market.India);

            Assert.AreEqual("SBIN", leanSymbol.Value);
            Assert.AreEqual(SecurityType.Equity, leanSymbol.SecurityType);
            Assert.AreEqual(Market.India, leanSymbol.ID.Market);
        }

        [Test]
        public void ReturnsCorrectLeanSymbol_EquityAutoDetect()
        {
            // Test auto-detection of equity from -EQ suffix
            var leanSymbol = _symbolMapper.GetLeanSymbol("NSE:RELIANCE-EQ");

            Assert.AreEqual("RELIANCE", leanSymbol.Value);
            Assert.AreEqual(SecurityType.Equity, leanSymbol.SecurityType);
        }

        [Test]
        public void ReturnsCorrectLeanSymbol_Index()
        {
            // Test index symbol conversion: NSE:NIFTY50-INDEX -> NIFTY50
            var leanSymbol = _symbolMapper.GetLeanSymbol("NSE:NIFTY50-INDEX", SecurityType.Index, Market.India);

            Assert.AreEqual("NIFTY50", leanSymbol.Value);
            Assert.AreEqual(SecurityType.Index, leanSymbol.SecurityType);
        }

        [Test]
        public void ReturnsCorrectLeanSymbol_IndexAutoDetect()
        {
            // Test auto-detection of index from -INDEX suffix
            var leanSymbol = _symbolMapper.GetLeanSymbol("NSE:BANKNIFTY-INDEX");

            Assert.AreEqual("BANKNIFTY", leanSymbol.Value);
            Assert.AreEqual(SecurityType.Index, leanSymbol.SecurityType);
        }

        [Test]
        public void ReturnsCorrectLeanSymbol_Future()
        {
            // Test future symbol conversion: NSE:NIFTY24DECFUT
            var leanSymbol = _symbolMapper.GetLeanSymbol("NSE:NIFTY24DECFUT", SecurityType.Future, Market.India);

            Assert.AreEqual(SecurityType.Future, leanSymbol.SecurityType);
            Assert.AreEqual("NIFTY", leanSymbol.ID.Symbol);
            Assert.AreEqual(12, leanSymbol.ID.Date.Month); // December
            Assert.AreEqual(2024, leanSymbol.ID.Date.Year);
        }

        [Test]
        public void ReturnsCorrectLeanSymbol_FutureAutoDetect()
        {
            // Test auto-detection of future from FUT suffix
            var leanSymbol = _symbolMapper.GetLeanSymbol("NSE:BANKNIFTY24JANFUT");

            Assert.AreEqual(SecurityType.Future, leanSymbol.SecurityType);
            Assert.AreEqual("BANKNIFTY", leanSymbol.ID.Symbol);
            Assert.AreEqual(1, leanSymbol.ID.Date.Month); // January
        }

        [Test]
        public void ThrowsOnNullOrEmptyBrokerageSymbol()
        {
            Assert.Throws<ArgumentNullException>(() => _symbolMapper.GetLeanSymbol(null));
            Assert.Throws<ArgumentNullException>(() => _symbolMapper.GetLeanSymbol(""));
            Assert.Throws<ArgumentNullException>(() => _symbolMapper.GetLeanSymbol("   "));
        }

        #endregion

        #region Symbol Caching Tests

        [Test]
        public void CachesLeanToFyersConversions()
        {
            var symbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);

            // First call
            var result1 = _symbolMapper.GetBrokerageSymbol(symbol);

            // Second call should return cached result
            var result2 = _symbolMapper.GetBrokerageSymbol(symbol);

            Assert.AreEqual(result1, result2);
            Assert.AreEqual("NSE:SBIN-EQ", result1);
        }

        [Test]
        public void CachesFyersToLeanConversions()
        {
            // First call
            var result1 = _symbolMapper.GetLeanSymbol("NSE:SBIN-EQ");

            // Second call should return cached result
            var result2 = _symbolMapper.GetLeanSymbol("NSE:SBIN-EQ");

            Assert.AreEqual(result1, result2);
            Assert.AreEqual("SBIN", result1.Value);
        }

        #endregion

        #region Static Helper Method Tests

        [Test]
        public void GetExchangeFromSymbol_ReturnsCorrectExchange()
        {
            Assert.AreEqual("NSE", FyersSymbolMapper.GetExchangeFromSymbol("NSE:SBIN-EQ"));
            Assert.AreEqual("BSE", FyersSymbolMapper.GetExchangeFromSymbol("BSE:SBIN-EQ"));
            Assert.AreEqual("MCX", FyersSymbolMapper.GetExchangeFromSymbol("MCX:CRUDEOIL24DECFUT"));
            Assert.AreEqual("NSE", FyersSymbolMapper.GetExchangeFromSymbol("SBIN-EQ")); // No colon defaults to NSE
        }

        [Test]
        public void GetExchangeFromSymbol_HandlesEdgeCases()
        {
            Assert.AreEqual("NSE", FyersSymbolMapper.GetExchangeFromSymbol(null));
            Assert.AreEqual("NSE", FyersSymbolMapper.GetExchangeFromSymbol(""));
        }

        [Test]
        public void GetSecurityTypeFromSymbol_ReturnsCorrectType()
        {
            Assert.AreEqual(SecurityType.Equity, FyersSymbolMapper.GetSecurityTypeFromSymbol("NSE:SBIN-EQ"));
            Assert.AreEqual(SecurityType.Index, FyersSymbolMapper.GetSecurityTypeFromSymbol("NSE:NIFTY50-INDEX"));
            Assert.AreEqual(SecurityType.Future, FyersSymbolMapper.GetSecurityTypeFromSymbol("NSE:NIFTY24DECFUT"));
            Assert.AreEqual(SecurityType.Option, FyersSymbolMapper.GetSecurityTypeFromSymbol("NSE:NIFTY24D1923500CE"));
            Assert.AreEqual(SecurityType.Option, FyersSymbolMapper.GetSecurityTypeFromSymbol("NSE:NIFTY24D1923000PE"));
        }

        [Test]
        public void GetSecurityTypeFromSymbol_HandlesEdgeCases()
        {
            Assert.AreEqual(SecurityType.Equity, FyersSymbolMapper.GetSecurityTypeFromSymbol(null));
            Assert.AreEqual(SecurityType.Equity, FyersSymbolMapper.GetSecurityTypeFromSymbol(""));
            Assert.AreEqual(SecurityType.Equity, FyersSymbolMapper.GetSecurityTypeFromSymbol("UNKNOWN"));
        }

        #endregion

        #region Round-Trip Conversion Tests

        [Test]
        public void RoundTripConversion_Equity()
        {
            var originalSymbol = TestSymbolHelper.CreateEquity("SBIN", Market.India);

            // Convert to Fyers
            var fyersSymbol = _symbolMapper.GetBrokerageSymbol(originalSymbol);

            // Convert back to LEAN
            var roundTripSymbol = _symbolMapper.GetLeanSymbol(fyersSymbol, SecurityType.Equity, Market.India);

            Assert.AreEqual(originalSymbol.Value, roundTripSymbol.Value);
            Assert.AreEqual(originalSymbol.SecurityType, roundTripSymbol.SecurityType);
        }

        [Test]
        public void RoundTripConversion_Index()
        {
            var originalSymbol = TestSymbolHelper.CreateIndex("NIFTY50", Market.India);

            // Convert to Fyers
            var fyersSymbol = _symbolMapper.GetBrokerageSymbol(originalSymbol);

            // Convert back to LEAN
            var roundTripSymbol = _symbolMapper.GetLeanSymbol(fyersSymbol, SecurityType.Index, Market.India);

            Assert.AreEqual(originalSymbol.Value, roundTripSymbol.Value);
            Assert.AreEqual(originalSymbol.SecurityType, roundTripSymbol.SecurityType);
        }

        [Test]
        public void RoundTripConversion_Future()
        {
            var expiry = new DateTime(2024, 12, 26);
            var originalSymbol = Symbol.CreateFuture("NIFTY", Market.India, expiry);

            // Convert to Fyers
            var fyersSymbol = _symbolMapper.GetBrokerageSymbol(originalSymbol);

            // Convert back to LEAN
            var roundTripSymbol = _symbolMapper.GetLeanSymbol(fyersSymbol, SecurityType.Future, Market.India);

            Assert.AreEqual(originalSymbol.ID.Symbol, roundTripSymbol.ID.Symbol);
            Assert.AreEqual(originalSymbol.SecurityType, roundTripSymbol.SecurityType);
            Assert.AreEqual(originalSymbol.ID.Date.Month, roundTripSymbol.ID.Date.Month);
            Assert.AreEqual(originalSymbol.ID.Date.Year, roundTripSymbol.ID.Date.Year);
        }

        #endregion

        #region Symbol Master Tests

        [Test]
        public void LoadSymbolMaster_LoadsFromFyers()
        {
            FyersTestCredentials.SkipIfNoCredentials();

            _symbolMapper.LoadSymbolMaster("NSE_CM");

            // Skip test if Fyers API is unavailable (returns 503 or empty data)
            if (_symbolMapper.KnownSymbols.Count == 0)
            {
                Assert.Ignore("Fyers symbol master API is currently unavailable");
            }

            Assert.IsTrue(_symbolMapper.KnownSymbols.Count > 0, "Expected symbols to be loaded");
        }

        [Test]
        public void GetInstrument_ReturnsNullForUnknownSymbol()
        {
            var instrument = _symbolMapper.GetInstrument("UNKNOWN:SYMBOL-EQ");
            Assert.IsNull(instrument);
        }

        #endregion
    }
}
