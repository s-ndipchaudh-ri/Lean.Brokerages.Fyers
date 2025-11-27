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
using System.Collections;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;
using System.Text.RegularExpressions;
using System.Web;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace QuantConnect.Brokerages.Fyers
{
    /// <summary>
    /// Fyers utility helper class
    /// </summary>
    public static class FyersUtils
    {
        /// <summary>
        /// Convert string to Date object
        /// </summary>
        /// <param name="dateString">Date string in format yyyy-MM-dd or yyyy-MM-dd HH:mm:ss</param>
        /// <returns>Date object or null if parsing fails</returns>
        public static DateTime? StringToDate(string dateString)
        {
            if (string.IsNullOrEmpty(dateString))
            {
                return null;
            }

            try
            {
                var format = dateString.Length == 10 ? "yyyy-MM-dd" : "yyyy-MM-dd HH:mm:ss";
                return DateTime.ParseExact(dateString, format, CultureInfo.InvariantCulture);
            }
            catch (Exception)
            {
                return null;
            }
        }

        /// <summary>
        /// Serialize C# object to JSON string
        /// </summary>
        /// <param name="obj">C# object to serialize</param>
        /// <returns>JSON string</returns>
        public static string JsonSerialize(object obj)
        {
            var json = JsonConvert.SerializeObject(obj);

            // Handle .NET DateTime serialization format
            var mc = Regex.Matches(json, @"\\/Date\((\d*?)\)\\/");
            foreach (Match m in mc)
            {
                var unix = Convert.ToInt64(m.Groups[1].Value, CultureInfo.InvariantCulture) / 1000;
                json = json.Replace(m.Groups[0].Value, UnixToDateTime(unix).ToStringInvariant());
            }

            return json;
        }

        /// <summary>
        /// Deserialize JSON string to JObject
        /// </summary>
        /// <param name="json">JSON string to deserialize</param>
        /// <returns>JObject representation</returns>
        public static JObject JsonDeserialize(string json)
        {
            return JObject.Parse(json);
        }

        /// <summary>
        /// Recursively traverses an object and converts double fields to decimal
        /// </summary>
        /// <param name="obj">Input object</param>
        /// <returns>Object with decimals instead of doubles</returns>
        public static dynamic DoubleToDecimal(dynamic obj)
        {
            if (obj is double)
            {
                return Convert.ToDecimal(obj);
            }

            if (obj is IDictionary dict)
            {
                var keys = new List<string>(dict.Keys.Cast<string>());
                foreach (var key in keys)
                {
                    dict[key] = DoubleToDecimal(dict[key]);
                }
            }
            else if (obj is ICollection collection)
            {
                var list = new ArrayList(collection);
                for (var i = 0; i < list.Count; i++)
                {
                    list[i] = DoubleToDecimal(list[i]);
                }
                return list;
            }

            return obj;
        }

        /// <summary>
        /// Wraps a string inside a memory stream
        /// </summary>
        /// <param name="value">String data</param>
        /// <returns>MemoryStream containing the string</returns>
        public static MemoryStream StreamFromString(string value)
        {
            return new MemoryStream(Encoding.UTF8.GetBytes(value ?? ""));
        }

        /// <summary>
        /// Helper function to add parameter to dictionary only if value is not null or empty
        /// </summary>
        /// <param name="parameters">Dictionary to add the key-value pair</param>
        /// <param name="key">Key of the parameter</param>
        /// <param name="value">Value of the parameter</param>
        public static void AddIfNotNull(Dictionary<string, dynamic> parameters, string key, string value)
        {
            if (!string.IsNullOrEmpty(value))
            {
                parameters.Add(key, value);
            }
        }

        /// <summary>
        /// Helper function to add parameter to dictionary only if value is not null
        /// </summary>
        /// <param name="parameters">Dictionary to add the key-value pair</param>
        /// <param name="key">Key of the parameter</param>
        /// <param name="value">Value of the parameter</param>
        public static void AddIfNotNull<T>(Dictionary<string, dynamic> parameters, string key, T? value) where T : struct
        {
            if (value.HasValue)
            {
                parameters.Add(key, value.Value.ToString());
            }
        }

        /// <summary>
        /// Creates key=value with URL encoded value
        /// </summary>
        /// <param name="key">Key</param>
        /// <param name="value">Value (string or string array)</param>
        /// <returns>URL encoded key=value string</returns>
        public static string BuildParam(string key, dynamic value)
        {
            if (value is string strValue)
            {
                return HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(strValue);
            }

            var values = (string[])value;
            return string.Join("&", values.Select(x => HttpUtility.UrlEncode(key) + "=" + HttpUtility.UrlEncode(x)));
        }

        /// <summary>
        /// Convert Unix timestamp to DateTime (IST timezone)
        /// </summary>
        /// <param name="unixTimeStamp">Unix timestamp in seconds</param>
        /// <returns>DateTime object in IST</returns>
        public static DateTime UnixToDateTime(long unixTimeStamp)
        {
            // IST is UTC+5:30
            var dateTime = new DateTime(1970, 1, 1, 5, 30, 0, 0, DateTimeKind.Unspecified);
            return dateTime.AddSeconds(unixTimeStamp);
        }

        /// <summary>
        /// Convert DateTime to Unix timestamp
        /// </summary>
        /// <param name="dateTime">DateTime to convert</param>
        /// <returns>Unix timestamp in seconds</returns>
        public static long DateTimeToUnix(DateTime dateTime)
        {
            var epoch = new DateTime(1970, 1, 1, 0, 0, 0, DateTimeKind.Utc);
            return (long)(dateTime.ToUniversalTime() - epoch).TotalSeconds;
        }

        /// <summary>
        /// Convert ArrayList to list of decimals
        /// </summary>
        /// <param name="arrayList">ArrayList to convert</param>
        /// <returns>List of decimals</returns>
        public static List<decimal> ToDecimalList(ArrayList arrayList)
        {
            var result = new List<decimal>();
            foreach (var item in arrayList)
            {
                result.Add(Convert.ToDecimal(item, CultureInfo.InvariantCulture));
            }
            return result;
        }

        /// <summary>
        /// Parse Fyers symbol to extract exchange and trading symbol
        /// Example: "NSE:SBIN-EQ" -> ("NSE", "SBIN-EQ")
        /// </summary>
        /// <param name="fyersSymbol">Fyers formatted symbol</param>
        /// <returns>Tuple of (exchange, symbol)</returns>
        public static (string exchange, string symbol) ParseFyersSymbol(string fyersSymbol)
        {
            if (string.IsNullOrEmpty(fyersSymbol))
            {
                return (null, null);
            }

            var parts = fyersSymbol.Split(':');
            if (parts.Length != 2)
            {
                return (null, fyersSymbol);
            }

            return (parts[0], parts[1]);
        }

        /// <summary>
        /// Build Fyers symbol from exchange and trading symbol
        /// </summary>
        /// <param name="exchange">Exchange code (NSE, BSE, MCX)</param>
        /// <param name="tradingSymbol">Trading symbol (e.g., SBIN-EQ)</param>
        /// <returns>Fyers formatted symbol (e.g., NSE:SBIN-EQ)</returns>
        public static string BuildFyersSymbol(string exchange, string tradingSymbol)
        {
            return $"{exchange}:{tradingSymbol}";
        }

        /// <summary>
        /// Extract instrument type from Fyers symbol suffix
        /// </summary>
        /// <param name="fyersSymbol">Fyers symbol (e.g., NSE:SBIN-EQ)</param>
        /// <returns>Instrument type (EQ, FUT, CE, PE, etc.)</returns>
        public static string GetInstrumentType(string fyersSymbol)
        {
            var (_, symbol) = ParseFyersSymbol(fyersSymbol);
            if (string.IsNullOrEmpty(symbol))
            {
                return null;
            }

            var lastDash = symbol.LastIndexOf('-');
            if (lastDash < 0 || lastDash >= symbol.Length - 1)
            {
                return null;
            }

            return symbol.Substring(lastDash + 1);
        }

        /// <summary>
        /// Round decimal to specified precision
        /// </summary>
        /// <param name="value">Value to round</param>
        /// <param name="precision">Number of decimal places</param>
        /// <returns>Rounded value</returns>
        public static decimal RoundPrice(decimal value, int precision = 2)
        {
            return Math.Round(value, precision, MidpointRounding.AwayFromZero);
        }

        /// <summary>
        /// Round to tick size (typically 0.05 for Indian markets)
        /// </summary>
        /// <param name="price">Price to round</param>
        /// <param name="tickSize">Tick size (default 0.05)</param>
        /// <returns>Rounded price</returns>
        public static decimal RoundToTickSize(decimal price, decimal tickSize = 0.05m)
        {
            return Math.Round(price / tickSize) * tickSize;
        }

        /// <summary>
        /// Check if market is open (basic check for Indian markets)
        /// </summary>
        /// <param name="time">Time to check (IST)</param>
        /// <returns>True if market is likely open</returns>
        public static bool IsMarketHours(DateTime time)
        {
            // Indian market hours: 9:15 AM to 3:30 PM IST, Monday to Friday
            if (time.DayOfWeek == DayOfWeek.Saturday || time.DayOfWeek == DayOfWeek.Sunday)
            {
                return false;
            }

            var marketOpen = new TimeSpan(9, 15, 0);
            var marketClose = new TimeSpan(15, 30, 0);
            var currentTime = time.TimeOfDay;

            return currentTime >= marketOpen && currentTime <= marketClose;
        }

        /// <summary>
        /// Safely get value from JObject with default
        /// </summary>
        public static T GetValue<T>(JObject obj, string key, T defaultValue = default)
        {
            if (obj == null || !obj.ContainsKey(key))
            {
                return defaultValue;
            }

            try
            {
                return obj[key].Value<T>();
            }
            catch
            {
                return defaultValue;
            }
        }
    }
}
