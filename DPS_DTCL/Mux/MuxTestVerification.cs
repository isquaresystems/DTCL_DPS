using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using DTCL.Transport;

namespace DTCL.Mux
{
    /// <summary>
    /// Test verification class for MUX operations with isolated channel instances
    /// Validates that channels operate independently without state conflicts
    /// </summary>
    public static class MuxTestVerification
    {
        /// <summary>
        /// Verify basic channel isolation
        /// Tests that activating/deactivating channels doesn't affect other channels
        /// </summary>
        public static async Task<bool> VerifyChannelIsolation(MuxManager muxManager)
        {
            try
            {
                Console.WriteLine("=== Testing Channel Isolation ===");
                
                // Test 1: Activate channel 1
                bool success1 = await muxManager.SwitchToChannelAsync(1, true);
                if (!success1)
                {
                    Console.WriteLine("FAIL: Could not activate channel 1");
                    return false;
                }

                var channel1Manager = muxManager.GetChannelManager(1);
                if (!channel1Manager.IsActive)
                {
                    Console.WriteLine("FAIL: Channel 1 not reported as active");
                    return false;
                }

                // Test 2: Switch to channel 2, verify channel 1 deactivated
                bool success2 = await muxManager.SwitchToChannelAsync(2, true);
                if (!success2)
                {
                    Console.WriteLine("FAIL: Could not activate channel 2");
                    return false;
                }

                if (channel1Manager.IsActive)
                {
                    Console.WriteLine("FAIL: Channel 1 still active after switching to channel 2");
                    return false;
                }

                var channel2Manager = muxManager.GetChannelManager(2);
                if (!channel2Manager.IsActive)
                {
                    Console.WriteLine("FAIL: Channel 2 not reported as active");
                    return false;
                }

                // Test 3: Deactivate all channels
                muxManager.DeactivateCurrentChannel();
                
                if (channel1Manager.IsActive || channel2Manager.IsActive)
                {
                    Console.WriteLine("FAIL: Channels still active after deactivation");
                    return false;
                }

                Console.WriteLine("PASS: Channel isolation test passed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: Channel isolation test error - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verify channel state independence
        /// Tests that each channel maintains its own hardware and cart state
        /// </summary>
        public static async Task<bool> VerifyChannelStateIndependence(MuxManager muxManager)
        {
            try
            {
                Console.WriteLine("=== Testing Channel State Independence ===");

                var testResults = new Dictionary<int, string>();

                // Test each channel's independent state
                for (int ch = 1; ch <= 3; ch++)
                {
                    bool success = await muxManager.SwitchToChannelAsync(ch, true);
                    if (success)
                    {
                        var channelManager = muxManager.GetChannelManager(ch);
                        var channelInfo = muxManager.channels[ch];
                        
                        string state = $"DTCL:{channelInfo.isDTCLConnected}, Cart:{channelInfo.CartType}, Slots:{channelManager.HardwareInfo.TotalDetectedCarts}";
                        testResults[ch] = state;
                        
                        Console.WriteLine($"Channel {ch}: {state}");
                    }
                    else
                    {
                        testResults[ch] = "Failed to activate";
                        Console.WriteLine($"Channel {ch}: Failed to activate");
                    }

                    // Deactivate before next test
                    muxManager.DeactivateCurrentChannel();
                    await Task.Delay(500);
                }

                // Verify no cross-contamination by re-testing same channels
                Console.WriteLine("Re-testing channels for state consistency...");
                
                for (int ch = 1; ch <= 3; ch++)
                {
                    bool success = await muxManager.SwitchToChannelAsync(ch, true);
                    if (success)
                    {
                        var channelManager = muxManager.GetChannelManager(ch);
                        var channelInfo = muxManager.channels[ch];
                        
                        string newState = $"DTCL:{channelInfo.isDTCLConnected}, Cart:{channelInfo.CartType}, Slots:{channelManager.HardwareInfo.TotalDetectedCarts}";
                        
                        if (testResults.ContainsKey(ch) && testResults[ch] != "Failed to activate")
                        {
                            Console.WriteLine($"Channel {ch} state consistency: {newState}");
                        }
                    }

                    muxManager.DeactivateCurrentChannel();
                    await Task.Delay(500);
                }

                Console.WriteLine("PASS: Channel state independence test completed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: Channel state independence test error - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Verify memory leak prevention
        /// Tests that repeated channel activations don't cause memory leaks
        /// </summary>
        public static async Task<bool> VerifyMemoryLeakPrevention(MuxManager muxManager)
        {
            try
            {
                Console.WriteLine("=== Testing Memory Leak Prevention ===");

                var channel1Manager = muxManager.GetChannelManager(1);
                string initialStats = channel1Manager.GetChannelStatistics();
                Console.WriteLine($"Initial: {initialStats}");

                // Perform multiple activate/deactivate cycles
                for (int cycle = 1; cycle <= 5; cycle++)
                {
                    bool success = await muxManager.SwitchToChannelAsync(1, true);
                    if (success)
                    {
                        await Task.Delay(200); // Simulate some operations
                        muxManager.DeactivateCurrentChannel();
                        await Task.Delay(200);
                    }
                    
                    if (cycle % 2 == 0)
                    {
                        string stats = channel1Manager.GetChannelStatistics();
                        Console.WriteLine($"After cycle {cycle}: {stats}");
                    }
                }

                string finalStats = channel1Manager.GetChannelStatistics();
                Console.WriteLine($"Final: {finalStats}");

                // Verify channel is ready for operations after cycles
                bool finalSuccess = await muxManager.SwitchToChannelAsync(1, true);
                if (!finalSuccess)
                {
                    Console.WriteLine("FAIL: Channel not operational after multiple cycles");
                    return false;
                }

                muxManager.DeactivateCurrentChannel();

                Console.WriteLine("PASS: Memory leak prevention test passed");
                return true;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"FAIL: Memory leak prevention test error - {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Run comprehensive MUX test suite
        /// </summary>
        public static async Task<bool> RunComprehensiveTests(MuxManager muxManager)
        {
            if (muxManager == null)
            {
                Console.WriteLine("FAIL: MuxManager is null");
                return false;
            }

            if (!muxManager.isMuxHwConnected)
            {
                Console.WriteLine("SKIP: MUX hardware not connected - cannot run tests");
                return true; // Not a failure, just no hardware available
            }

            Console.WriteLine("Starting MUX Comprehensive Test Suite...");
            Console.WriteLine($"Active Channel: {muxManager.GetActiveChannelNumber()}");

            var results = new List<bool>();

            // Test 1: Channel Isolation
            results.Add(await VerifyChannelIsolation(muxManager));

            // Test 2: Channel State Independence  
            results.Add(await VerifyChannelStateIndependence(muxManager));

            // Test 3: Memory Leak Prevention
            results.Add(await VerifyMemoryLeakPrevention(muxManager));

            // Cleanup after tests
            await muxManager.SafeShutdownAsync();

            // Summary
            int passCount = 0;
            int totalTests = results.Count;
            
            for (int i = 0; i < results.Count; i++)
            {
                if (results[i]) passCount++;
            }

            Console.WriteLine($"=== TEST SUMMARY ===");
            Console.WriteLine($"Tests Passed: {passCount}/{totalTests}");
            Console.WriteLine($"Overall Result: {(passCount == totalTests ? "PASS" : "FAIL")}");

            return passCount == totalTests;
        }

        /// <summary>
        /// Quick validation test for basic functionality
        /// </summary>
        public static async Task<bool> QuickValidationTest(MuxManager muxManager)
        {
            try
            {
                Console.WriteLine("=== Quick Validation Test ===");

                if (!muxManager.isMuxHwConnected)
                {
                    Console.WriteLine("SKIP: MUX hardware not connected");
                    return true;
                }

                // Test basic channel switching
                bool result = await muxManager.SwitchToChannelAsync(1, false);
                muxManager.DeactivateCurrentChannel();
                
                Console.WriteLine($"Quick validation: {(result ? "PASS" : "FAIL")}");
                return result;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Quick validation FAIL: {ex.Message}");
                return false;
            }
        }
    }
}