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
using System.IO;
using NUnit.Framework;
using System.Collections;
using QuantConnect.Logging;
using QuantConnect.Configuration;
using QuantConnect.Util;
using QuantConnect.Interfaces;
using QuantConnect.Data.Auxiliary;
using QuantConnect.Lean.Engine.DataFeeds;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    /// <summary>
    /// Global test setup that runs before all tests in the assembly
    /// </summary>
    [SetUpFixture]
    public class GlobalTestSetup
    {
        private static bool _initialized;

        /// <summary>
        /// One-time setup for all tests in the assembly
        /// </summary>
        [OneTimeSetUp]
        public void RunBeforeAllTests()
        {
            InitializeProviders();
        }

        /// <summary>
        /// Initializes the required providers for symbol creation and other LEAN functionality
        /// </summary>
        public static void InitializeProviders()
        {
            if (_initialized) return;

            lock (typeof(GlobalTestSetup))
            {
                if (_initialized) return;

                try
                {
                    Log.LogHandler = new CompositeLogHandler();
                    Log.Trace("GlobalTestSetup.InitializeProviders(): Initializing test providers...");

                    // Set up directory
                    var dir = TestContext.CurrentContext.TestDirectory;
                    Environment.CurrentDirectory = dir;
                    Directory.SetCurrentDirectory(dir);

                    // Reload configuration
                    Config.Reset();

                    // Set data folder if not configured
                    if (string.IsNullOrEmpty(Config.Get("data-folder")))
                    {
                        Config.Set("data-folder", Path.Combine(dir, "Data"));
                    }

                    // Load environment variables
                    var environment = Environment.GetEnvironmentVariables();
                    foreach (DictionaryEntry entry in environment)
                    {
                        var envKey = entry.Key?.ToString() ?? "";
                        var value = entry.Value?.ToString() ?? "";

                        if (envKey.StartsWith("QC_"))
                        {
                            var key = envKey.Substring(3).Replace("_", "-").ToLower();
                            Log.Trace($"GlobalTestSetup(): Updating config setting '{key}' from environment var '{envKey}'");
                            Config.Set(key, value);
                        }
                    }

                    Globals.Reset();

                    // Initialize data provider
                    var dataProvider = new DefaultDataProvider();
                    Composer.Instance.AddPart<IDataProvider>(dataProvider);

                    // Initialize map file provider (required for Symbol.Create)
                    var mapFileProvider = new LocalDiskMapFileProvider();
                    mapFileProvider.Initialize(dataProvider);
                    Composer.Instance.AddPart<IMapFileProvider>(mapFileProvider);

                    // Initialize factor file provider
                    var factorFileProvider = new LocalDiskFactorFileProvider();
                    factorFileProvider.Initialize(mapFileProvider, dataProvider);
                    Composer.Instance.AddPart<IFactorFileProvider>(factorFileProvider);

                    Log.Trace("GlobalTestSetup.InitializeProviders(): Providers initialized successfully");

                    _initialized = true;
                }
                catch (Exception ex)
                {
                    Log.Error($"GlobalTestSetup.InitializeProviders(): Failed to initialize - {ex.Message}");
                    // Don't fail completely - some tests might not need the providers
                    _initialized = true;
                }
            }
        }
    }

    [TestFixture]
    public class TestSetup
    {
        [Test, TestCaseSource(nameof(TestParameters))]
        public void TestSetupCase()
        {
        }

        public static void ReloadConfiguration()
        {
            // nunit 3 sets the current folder to a temp folder we need it to be the test bin output folder
            var dir = TestContext.CurrentContext.TestDirectory;
            Environment.CurrentDirectory = dir;
            Directory.SetCurrentDirectory(dir);
            // reload config from current path
            Config.Reset();

            var environment = Environment.GetEnvironmentVariables();
            foreach (DictionaryEntry entry in environment)
            {
                var envKey = entry.Key?.ToString() ?? "";
                var value = entry.Value?.ToString() ?? "";

                if (envKey.StartsWith("QC_"))
                {
                    var key = envKey.Substring(3).Replace("_", "-").ToLower();

                    Log.Trace($"TestSetup(): Updating config setting '{key}' from environment var '{envKey}'");
                    Config.Set(key, value);
                }
            }

            // resets the version among other things
            Globals.Reset();
        }

        private static void SetUp()
        {
            // Ensure providers are initialized
            GlobalTestSetup.InitializeProviders();

            Log.LogHandler = new CompositeLogHandler();
            Log.Trace("TestSetup(): starting...");
            ReloadConfiguration();
            Log.DebuggingEnabled = Config.GetBool("debug-mode");
        }

        private static TestCaseData[] TestParameters
        {
            get
            {
                SetUp();
                return new [] { new TestCaseData() };
            }
        }
    }
}
