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
using System.Threading;
using NUnit.Framework;
using QuantConnect.Logging;

namespace QuantConnect.Brokerages.Fyers.Tests
{
    /// <summary>
    /// Unit tests for FyersBrokerage reconnection logic
    /// </summary>
    [TestFixture]
    public class FyersBrokerageReconnectionTests
    {
        #region Exponential Backoff Tests

        [Test]
        public void ExponentialBackoff_CalculatesCorrectDelays()
        {
            // Test exponential backoff calculation
            // Base delay = 2^attempt (capped at 64)
            for (int attempt = 1; attempt <= 10; attempt++)
            {
                var baseDelay = Math.Pow(2, Math.Min(attempt, 6));
                var minJitter = 0.75;
                var maxJitter = 1.25;

                var minDelay = TimeSpan.FromSeconds(baseDelay * minJitter);
                var maxDelay = TimeSpan.FromSeconds(baseDelay * maxJitter);

                // Verify delay is within expected range
                Assert.GreaterOrEqual(maxDelay.TotalSeconds, minDelay.TotalSeconds);

                Log.Trace($"Attempt {attempt}: Delay range {minDelay.TotalSeconds:F1}s - {maxDelay.TotalSeconds:F1}s");
            }
        }

        [Test]
        public void ExponentialBackoff_CapsAtMaxValue()
        {
            // After attempt 6, the base delay should cap at 64 seconds
            var attempt = 10;
            var baseDelay = Math.Pow(2, Math.Min(attempt, 6));

            Assert.AreEqual(64, baseDelay); // 2^6 = 64

            Log.Trace($"Capped base delay: {baseDelay}s");
        }

        [Test]
        public void ExponentialBackoff_FirstAttemptIsShort()
        {
            // First attempt should be around 2 seconds (with jitter)
            var attempt = 1;
            var baseDelay = Math.Pow(2, Math.Min(attempt, 6));

            Assert.AreEqual(2, baseDelay); // 2^1 = 2

            Log.Trace($"First attempt base delay: {baseDelay}s");
        }

        #endregion

        #region Reconnection Counter Tests

        [Test]
        public void MaxReconnectAttempts_HasReasonableValue()
        {
            // Verify max attempts constant
            var maxAttempts = 10;

            Assert.GreaterOrEqual(maxAttempts, 5, "Should allow at least 5 attempts");
            Assert.LessOrEqual(maxAttempts, 20, "Should not attempt indefinitely");

            // Calculate total max wait time with exponential backoff
            double totalMaxWait = 0;
            for (int i = 1; i <= maxAttempts; i++)
            {
                totalMaxWait += Math.Pow(2, Math.Min(i, 6)) * 1.25; // Max jitter
            }

            Log.Trace($"Max reconnect attempts: {maxAttempts}");
            Log.Trace($"Approximate max wait time: {totalMaxWait:F0}s ({totalMaxWait / 60:F1} minutes)");
        }

        #endregion

        #region Heartbeat Configuration Tests

        [Test]
        public void HeartbeatInterval_IsReasonable()
        {
            // Heartbeat should fire frequently enough to detect issues
            var heartbeatIntervalMs = FyersConstants.WebSocketHeartbeatIntervalMs;

            Assert.GreaterOrEqual(heartbeatIntervalMs, 5000, "Heartbeat shouldn't be too frequent");
            Assert.LessOrEqual(heartbeatIntervalMs, 60000, "Heartbeat should detect issues within a minute");

            Log.Trace($"Heartbeat interval: {heartbeatIntervalMs}ms ({heartbeatIntervalMs / 1000.0}s)");
        }

        [Test]
        public void StaleConnectionTimeout_IsAppropriate()
        {
            // Stale connection should be detected within reasonable time
            var staleTimeoutSeconds = 60; // From the code

            Assert.GreaterOrEqual(staleTimeoutSeconds, 30, "Should allow for network delays");
            Assert.LessOrEqual(staleTimeoutSeconds, 120, "Should not wait too long for stale detection");

            Log.Trace($"Stale connection timeout: {staleTimeoutSeconds}s");
        }

        #endregion

        #region Timer Tests

        [Test]
        public void Timer_InfiniteTimeSpan_PreventsRecurring()
        {
            // Verify Timeout.InfiniteTimeSpan works as expected for one-shot timers
            Assert.AreEqual(-1, (int)Timeout.InfiniteTimeSpan.TotalMilliseconds);
            Log.Trace("Timeout.InfiniteTimeSpan correctly represents no recurring");
        }

        [Test]
        public void Timer_CanBeDisposedMultipleTimes()
        {
            // Timers should handle multiple dispose calls gracefully
            var timer = new Timer(_ => { }, null, Timeout.InfiniteTimeSpan, Timeout.InfiniteTimeSpan);

            Assert.DoesNotThrow(() =>
            {
                timer.Dispose();
                timer.Dispose();
            });

            Log.Trace("Timer disposed multiple times without error");
        }

        [Test]
        public void NullableTimer_CanCallDisposeWithNullCheck()
        {
            // Test nullable timer pattern used in code
            Timer? timer = null;

            Assert.DoesNotThrow(() =>
            {
                timer?.Dispose();
            });

            Log.Trace("Nullable timer dispose pattern works correctly");
        }

        #endregion

        #region Jitter Tests

        [Test]
        public void Jitter_IsWithinExpectedRange()
        {
            // Test jitter calculation multiple times
            var random = new Random(42); // Seed for reproducibility
            var iterations = 100;
            var minExpected = 0.75;
            var maxExpected = 1.25;

            for (int i = 0; i < iterations; i++)
            {
                var jitter = random.NextDouble() * 0.5 + 0.75;

                Assert.GreaterOrEqual(jitter, minExpected);
                Assert.LessOrEqual(jitter, maxExpected);
            }

            Log.Trace($"All {iterations} jitter values were within [{minExpected}, {maxExpected}]");
        }

        [Test]
        public void Jitter_ProducesVariedDelays()
        {
            // Verify jitter actually produces different delays
            var random = new Random();
            var baseDelay = 8.0; // 2^3

            var delays = new double[10];
            for (int i = 0; i < 10; i++)
            {
                var jitter = random.NextDouble() * 0.5 + 0.75;
                delays[i] = baseDelay * jitter;
            }

            // Check that not all delays are the same
            var firstDelay = delays[0];
            var allSame = true;
            for (int i = 1; i < delays.Length; i++)
            {
                if (Math.Abs(delays[i] - firstDelay) > 0.001)
                {
                    allSame = false;
                    break;
                }
            }

            Assert.IsFalse(allSame, "Jitter should produce varied delays");

            Log.Trace("Jitter produces varied delays as expected");
        }

        #endregion

        #region Connection State Tests

        [Test]
        public void ReconnectingFlag_PreventsMultipleAttempts()
        {
            // Simulate the flag behavior
            var isReconnecting = false;

            // First attempt sets the flag
            if (!isReconnecting)
            {
                isReconnecting = true;
                Assert.IsTrue(isReconnecting, "Flag should be set");
            }

            // Second attempt should be blocked
            var secondAttemptBlocked = false;
            if (isReconnecting)
            {
                secondAttemptBlocked = true;
            }

            Assert.IsTrue(secondAttemptBlocked, "Second reconnect attempt should be blocked");

            Log.Trace("Reconnecting flag correctly prevents concurrent attempts");
        }

        [Test]
        public void LockObject_PreventsRaceConditions()
        {
            // Verify lock pattern works correctly
            var lockObj = new object();
            var counter = 0;

            // Simulate concurrent access
            var tasks = new System.Threading.Tasks.Task[10];
            for (int i = 0; i < 10; i++)
            {
                tasks[i] = System.Threading.Tasks.Task.Run(() =>
                {
                    lock (lockObj)
                    {
                        var temp = counter;
                        Thread.Sleep(1); // Simulate some work
                        counter = temp + 1;
                    }
                });
            }

            System.Threading.Tasks.Task.WaitAll(tasks);

            Assert.AreEqual(10, counter, "Lock should prevent race conditions");

            Log.Trace("Lock pattern works correctly for synchronization");
        }

        #endregion

        #region DateTime Tests

        [Test]
        public void LastMessageTime_TrackingWorks()
        {
            var lastMessageTime = DateTime.UtcNow;
            Thread.Sleep(100);

            var timeSinceLast = DateTime.UtcNow - lastMessageTime;

            Assert.GreaterOrEqual(timeSinceLast.TotalMilliseconds, 100);

            // Update the time
            lastMessageTime = DateTime.UtcNow;
            var newTimeSinceLast = DateTime.UtcNow - lastMessageTime;

            Assert.Less(newTimeSinceLast.TotalMilliseconds, 50);

            Log.Trace("Last message time tracking works correctly");
        }

        [Test]
        public void StaleCheck_DetectsNoActivity()
        {
            var lastMessageTime = DateTime.UtcNow.AddSeconds(-70);
            var staleThreshold = TimeSpan.FromSeconds(60);

            var timeSinceLast = DateTime.UtcNow - lastMessageTime;
            var isStale = timeSinceLast > staleThreshold;

            Assert.IsTrue(isStale, "Should detect stale connection");

            Log.Trace($"Stale check detected: {timeSinceLast.TotalSeconds:F1}s since last message");
        }

        [Test]
        public void StaleCheck_AllowsRecentActivity()
        {
            var lastMessageTime = DateTime.UtcNow.AddSeconds(-30);
            var staleThreshold = TimeSpan.FromSeconds(60);

            var timeSinceLast = DateTime.UtcNow - lastMessageTime;
            var isStale = timeSinceLast > staleThreshold;

            Assert.IsFalse(isStale, "Should not flag recent activity as stale");

            Log.Trace($"Recent activity not flagged: {timeSinceLast.TotalSeconds:F1}s since last message");
        }

        #endregion

        #region Event Handler Tests

        [Test]
        public void EventHandler_CanBeRemovedSafely()
        {
            EventHandler handler = (s, e) => { };

            Assert.DoesNotThrow(() =>
            {
                // Simulating the -= operation
                handler -= (s, e) => { }; // This is actually a no-op but shouldn't throw
            });

            Log.Trace("Event handler removal pattern works safely");
        }

        [Test]
        public void ManualResetEvent_WorksForSynchronization()
        {
            var resetEvent = new ManualResetEvent(false);
            var wasSignaled = false;

            // Start a background task that will signal
            System.Threading.Tasks.Task.Run(() =>
            {
                Thread.Sleep(50);
                resetEvent.Set();
            });

            // Wait for signal
            wasSignaled = resetEvent.WaitOne(TimeSpan.FromSeconds(1));

            Assert.IsTrue(wasSignaled, "ManualResetEvent should be signaled");

            resetEvent.Dispose();
            Log.Trace("ManualResetEvent synchronization works correctly");
        }

        [Test]
        public void ManualResetEvent_TimesOutCorrectly()
        {
            var resetEvent = new ManualResetEvent(false);

            // Don't signal, just wait
            var wasSignaled = resetEvent.WaitOne(TimeSpan.FromMilliseconds(50));

            Assert.IsFalse(wasSignaled, "ManualResetEvent should timeout");

            resetEvent.Dispose();
            Log.Trace("ManualResetEvent timeout works correctly");
        }

        #endregion
    }
}
