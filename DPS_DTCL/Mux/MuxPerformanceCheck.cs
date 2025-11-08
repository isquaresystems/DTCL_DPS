using DTCL.Cartridges;
using DTCL.Log;
using DTCL.Transport;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.Remoting;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DTCL.Mux
{
    /*public class MuxPCProgress
    {
        public int Channel { get; set; }
        public string Status { get; set; }
        public PCResult Result { get; set; }
        public int CurrentIteration { get; set; }
        public int TotalIterations { get; set; }
        public int ElapsedTime { get; set; }
    }*/
    public class MuxPCProgress
    {
        public int Channel { get; set; }
        public string Status { get; set; }
        public PCResult Result { get; set; }
        public int CurrentIteration { get; set; }
        public int TotalIterations { get; set; }
        public int ElapsedTime { get; set; }
        public int TotalDuration { get; set; }
        public int CompletedOperations { get; set; } // Total completed channel-iterations
        public int TotalOperations { get; set; } // Total channel-iterations to complete
    }

    public class MuxPerformanceCheck
    {
        MuxManager _muxManager;
        PerformanceCheck _pcExecutor;

        public event EventHandler<CommandEventArgs> CommandInProgress;

        public MuxPerformanceCheck(MuxManager muxManager)
        {
            _muxManager = muxManager;
            _pcExecutor = new PerformanceCheck();

            // Wire up command progress events
            _pcExecutor.CommandInProgress2 += (sender, e) => CommandInProgress?.Invoke(sender, e);
        }

        public async Task<Dictionary<int, PCResult>> ExecuteSelectedChannels(
            bool withCart,
            List<int> selectedChannels,
            int iterations,
            IProgress<MuxPCProgress> progress,
            int currentIteration = 0,
            DateTime? startTime = null,
            CancellationToken cancellationToken = default)
        {
            var results = new Dictionary<int, PCResult>();
            var executionStartTime = startTime ?? DateTime.Now;

            try
            {
                foreach (int channelNo in selectedChannels)
                {
                    try
                    {
                        var channelInfo = _muxManager.channels[channelNo];

                        // Clear PCStatus before starting this channel (only for iterations > 1)
                        if (currentIteration > 1)
                        {
                            Log.Log.Info($"Clearing PCStatus for channel {channelNo} before iteration {currentIteration}");
                            channelInfo.PCStatus = "";
                            _muxManager._mainWindow.MuxChannelGrid.Items.Refresh();
                            await Task.Delay(200); // Brief delay to show clearing
                        }

                        var channelManager = _muxManager.GetChannelManager(channelNo);

                        if (channelManager == null)
                        {
                            Log.Log.Error($"No channel manager found for channel {channelNo}");
                            channelInfo.PCStatus = "FAIL";
                            continue;
                        }

                        // Switch to the channel and establish connection (preserve channel data)
                        if (!_muxManager.switch_Mux((char)channelNo, preserveChannelData: true))
                        {
                            Log.Log.Error($"Failed to switch to channel {channelNo}");
                            channelInfo.PCStatus = "FAIL";
                            channelInfo.isInProgress = false;
                            continue;
                        }

                        await Task.Delay(1000); // Allow stabilization after channel switch

                        // Re-establish connection for this channel
                        var connectionReestablished = await _muxManager.ReestablishChannelConnection(channelNo, withCart);

                        // Update channel status - mark as in progress AFTER successful re-establishment
                        if (connectionReestablished)
                        {
                            channelInfo.isInProgress = true;
                            _muxManager._mainWindow.MuxChannelGrid.Items.Refresh();
                        }

                        if (!connectionReestablished)
                        {
                            Log.Log.Warning($"Failed to re-establish DTCL/Cart connection on channel {channelNo}");
                            channelInfo.PCStatus = "N/A";
                            channelInfo.isInProgress = false;
                            continue;
                        }

                        // Now execute PC with fresh connection
                        if (channelInfo.isDTCLConnected && (channelInfo.cartNo > 0 || !withCart))
                        {
                            var result = new PCResult();
                            Log.Log.Info($"Executing PC on channel {channelNo} with fresh connection");

                            try
                            {
                                if (withCart && channelInfo.cartNo > 0)
                                {
                                    var cartType = ParseCartType(channelInfo.CartType);

                                    // Get the cart object from channel manager (memory safe)
                                    var cartObj = channelManager.GetCartInstance(cartType);

                                    if (cartObj == null)
                                    {
                                        Log.Log.Error($"No cart object for type {cartType} on channel {channelNo}");
                                        channelInfo.PCStatus = "FAIL";
                                        channelInfo.isInProgress = false;
                                        continue;
                                    }

                                    cartObj.CommandInProgress += _muxManager.OnCommandChanged;
                                    // Execute performance check using existing logic
                                    result = await cartObj.ExecutePC(
                                        withCart,
                                        cartType,
                                        (byte)channelInfo.cartNo
                                    );
                                }
                                else
                                {
                                    // Execute loopback test without cart
                                    var pc = new PerformanceCheck();
                                    pc.CommandInProgress2 += _muxManager.OnCommandChanged;
                                    result = await pc.doLoopBackTest(1);
                                }
                                
                                // Always save the result if we got one
                                results[channelNo] = result;
                            }
                            catch (OperationCanceledException)
                            {
                                // If the PC execution was cancelled but we have a result, save it
                                if (result.loopBackResult != null || result.eraseResult != null || 
                                    result.writeResult != null || result.readResult != null)
                                {
                                    results[channelNo] = result;
                                    Log.Log.Info($"Saved partial result for channel {channelNo} despite cancellation");
                                }
                                throw; // Re-throw to be caught by outer catch
                            }

                            // Update channel status based on result
                            if (result.eraseResult == "PASS" &&
                                result.writeResult == "PASS" &&
                                result.readResult == "PASS" &&
                                result.loopBackResult == "PASS")
                            {
                                channelInfo.PCStatus = "PASS";
                                Log.Log.Info($"Set PCStatus=PASS for channel {channelNo} iteration {currentIteration}");
                            }
                            else
                            {
                                channelInfo.PCStatus = "FAIL";
                                Log.Log.Info($"Set PCStatus=FAIL for channel {channelNo} iteration {currentIteration}");
                            }
                        }
                        else
                        {
                            Log.Log.Warning($"DTCL/Cart not properly connected on channel {channelNo} after re-establishment");
                            channelInfo.PCStatus = "N/A";
                        }

                        // Reset isInProgress for this channel now that it's complete
                        channelInfo.isInProgress = false;
                        _muxManager._mainWindow.MuxChannelGrid.Items.Refresh();

                        // Report progress - include iteration and real-time elapsed time
                        var realTimeElapsed = (int)(DateTime.Now - executionStartTime).TotalSeconds;

                        progress?.Report(new MuxPCProgress
                        {
                            Channel = channelNo,
                            Status = channelInfo.PCStatus,
                            Result = results.ContainsKey(channelNo) ? results[channelNo] : null,
                            CurrentIteration = currentIteration,
                            ElapsedTime = realTimeElapsed
                        });
                        
                        // Check for cancellation after processing this channel
                        if (cancellationToken.IsCancellationRequested)
                        {
                            Log.Log.Info($"Cancellation requested after channel {channelNo}, stopping further processing");
                            break; // Exit the loop but keep results
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        // Channel processing was cancelled, but preserve any results already obtained
                        Log.Log.Info($"Channel {channelNo} processing cancelled, preserving completed results");
                        // Don't process remaining channels, but return results we have so far
                        break;
                    }
                }
            }
            finally
            {
                // Switch off all channels when done (preserve channel data)
                _muxManager.switch_Mux((char)0, preserveChannelData: true);
            }

            return results;
        }

        public async Task<Dictionary<int, List<PCResult>>> ExecuteIterationsOnChannels(
            bool withCart,
            List<int> selectedChannels,
            int iterationCount,
            IProgress<MuxPCProgress> progress,
            CancellationToken cancellationToken = default)
        {
            var allResults = new Dictionary<int, List<PCResult>>();
            var startTime = DateTime.Now;
            var totalOperations = iterationCount * selectedChannels.Count;
            var completedOperations = 0;
            
            // Track which iterations have been logged for each channel
            var loggedIterations = new Dictionary<int, int>();

            // Initialize result lists for each channel
            foreach (var channel in selectedChannels)
            {
                allResults[channel] = new List<PCResult>();
                loggedIterations[channel] = 0;
                _muxManager.channels[channel].PCStatus = "";
                // Note: isInProgress will be set to true for each channel when it starts executing
            }

            var iteration = 1;
            // Execute iterations
            try
            {
                for (iteration = 1; iteration <= iterationCount; iteration++)
                {
                    // Check for cancellation before each iteration
                    cancellationToken.ThrowIfCancellationRequested();

                    // Note: PCStatus clearing is now handled per-channel in ExecuteSelectedChannels method

                    var realTimeElapsed = (int)(DateTime.Now - startTime).TotalSeconds;
                    
                    var iterationResults = await ExecuteSelectedChannels(
                        withCart,
                        selectedChannels,
                        1,
                        new Progress<MuxPCProgress>(p =>
                        {
                            p.CurrentIteration = iteration;
                            p.TotalIterations = iterationCount;
                            p.CompletedOperations = completedOperations;
                            p.TotalOperations = totalOperations;
                            progress?.Report(p);
                        }),
                        iteration,
                        startTime,
                        cancellationToken
                    );

                    // Add results to the collection and log immediately
                    foreach (var kvp in iterationResults)
                    {
                        var channelNo = kvp.Key;
                        var result = kvp.Value;

                        allResults[channelNo].Add(result);

                        // Increment completed operations after each channel completes
                        completedOperations++;

                        // Log each iteration immediately after completion
                        try
                        {
                            if (_muxManager.channels.ContainsKey(channelNo))
                            {
                                var channelInfo = _muxManager.channels[channelNo];
                                var channelManager = _muxManager.GetChannelManager(channelNo);

                                if (channelManager != null && channelInfo.cartNo >= 0 && channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                                {
                                    PCLog.Instance
                                        .AddPerformanceResponse(
                                        withCart,
                                        result,
                                        iteration,
                                        channelInfo.channel_SlotInfo[channelInfo.cartNo]
                                    );

                                    Log.Log.Info($"Logged iteration {iteration} for channel {channelNo}");
                                    loggedIterations[channelNo] = allResults[channelNo].Count; // Track number of results logged for this channel
                                }
                                else
                                {
                                    Log.Log.Warning($"Cannot log iteration {iteration} for channel {channelNo} - invalid slot info");
                                }

                                _muxManager._mainWindow.MuxChannelGrid.Items.Refresh();
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Log.Error($"Failed to log iteration {iteration} for channel {channelNo}: {ex.Message}");
                            // Continue processing other channels even if logging fails for one
                        }
                    }
                    
                    // Check if cancellation was requested during ExecuteSelectedChannels
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log.Log.Info($"Cancellation detected after processing iteration {iteration} results");
                        break; // Exit the iteration loop
                    }
                }
            }
            catch (OperationCanceledException)
            {
                // PC was stopped - but we still want to preserve completed results
                Log.Log
                    .Info($"PC execution cancelled, but preserving completed results for {allResults.Sum(kvp => kvp.Value.Count)} operations");
                
                // Log any unlogged results before exiting
                LogUnloggedResults(allResults, loggedIterations, withCart);
                
                // Re-throw to ensure the main method's catch block handles the stop message and logging
                throw;
            }

            // Reset isInProgress for all channels when iterations complete
            foreach (var channel in selectedChannels)
            {
                if (_muxManager.channels.ContainsKey(channel))
                {
                    _muxManager.channels[channel].isInProgress = false; // No longer executing
                    // Don't clear PCStatus here - preserve final results
                }
            }

            // Final UI refresh
            _muxManager._mainWindow.MuxChannelGrid.Items.Refresh();
            
            // If cancellation was requested at any point, throw after we've saved all results
            if (cancellationToken.IsCancellationRequested)
            {
                Log.Log.Info($"Throwing cancellation exception after preserving {allResults.Sum(kvp => kvp.Value.Count)} completed operations");
                throw new OperationCanceledException(cancellationToken);
            }

            return allResults;
        }

        public async Task<Dictionary<int, List<PCResult>>> ExecuteDurationOnChannels(
            bool withCart,
            List<int> selectedChannels,
            int durationSeconds,
            IProgress<MuxPCProgress> progress,
            CancellationToken cancellationToken = default)
        {
            var allResults = new Dictionary<int, List<PCResult>>();
            var startTime = DateTime.Now;
            var endTime = startTime.AddSeconds(durationSeconds);
            var iteration = 0;
            var totalOperations = selectedChannels.Count; // For duration mode, we don't know total iterations in advance
            var completedOperations = 0;
            
            // Track which iterations have been logged for each channel
            var loggedIterations = new Dictionary<int, int>();

            // Initialize result lists for each channel
            foreach (var channel in selectedChannels)
            {
                allResults[channel] = new List<PCResult>();
                loggedIterations[channel] = 0;
            }
            // Note: isInProgress will be set to true for each channel when it starts executing

            // Execute until duration expires
            try
            {
                while (DateTime.Now < endTime)
                {
                    // Check for cancellation before each iteration
                    cancellationToken.ThrowIfCancellationRequested();

                    iteration++;

                    // Report progress at start of iteration with current elapsed time
                    var currentElapsedTime = (int)(DateTime.Now - startTime).TotalSeconds;
                    // Update total operations for duration mode (grows dynamically)
                    totalOperations = iteration * selectedChannels.Count;

                    progress?.Report(new MuxPCProgress
                    {
                        Channel = selectedChannels.FirstOrDefault(),
                        CurrentIteration = iteration,
                        ElapsedTime = currentElapsedTime,
                        TotalDuration = durationSeconds,
                        CompletedOperations = completedOperations,
                        TotalOperations = totalOperations,
                        Status = ""
                    });

                    var iterationResults = await ExecuteSelectedChannels(
                        withCart,
                        selectedChannels,
                        1,
                        new Progress<MuxPCProgress>(p =>
                        {
                            p.CurrentIteration = iteration;
                            p.TotalDuration = durationSeconds;
                            p.CompletedOperations = completedOperations;
                            p.TotalOperations = totalOperations;
                            progress?.Report(p);
                        }),
                        iteration,
                        startTime,
                        cancellationToken
                    );

                    // Add results to the collection and log immediately
                    foreach (var kvp in iterationResults)
                    {
                        var channelNo = kvp.Key;
                        var result = kvp.Value;

                        allResults[channelNo].Add(result);

                        // Increment completed operations after each channel completes
                        completedOperations++;

                        // Log each iteration immediately after completion
                        try
                        {
                            if (_muxManager.channels.ContainsKey(channelNo))
                            {
                                var channelInfo = _muxManager.channels[channelNo];
                                var channelManager = _muxManager.GetChannelManager(channelNo);

                                if (channelManager != null && channelInfo.cartNo >= 0 && channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                                {
                                    PCLog.Instance
                                        .AddPerformanceResponse(
                                        withCart,
                                        result,
                                        iteration,
                                        channelInfo.channel_SlotInfo[channelInfo.cartNo]
                                    );

                                    Log.Log.Info($"Logged iteration {iteration} for channel {channelNo}");
                                    loggedIterations[channelNo] = allResults[channelNo].Count; // Track number of results logged for this channel
                                }
                                else
                                {
                                    Log.Log.Warning($"Cannot log iteration {iteration} for channel {channelNo} - invalid slot info");
                                }
                            }
                        }
                        catch (Exception ex)
                        {
                            Log.Log.Error($"Failed to log iteration {iteration} for channel {channelNo}: {ex.Message}");
                            // Continue processing other channels even if logging fails for one
                        }
                    }
                    
                    // Check if cancellation was requested during ExecuteSelectedChannels
                    if (cancellationToken.IsCancellationRequested)
                    {
                        Log.Log.Info($"Cancellation detected after processing iteration {iteration} results");
                        break; // Exit the iteration loop
                    }
                }

                // Clear Results column after completing one iteration of all selected channels (except when time is up)
                if (DateTime.Now < endTime)
                {
                    foreach (var channel in selectedChannels)
                    {
                        if (_muxManager.channels.ContainsKey(channel))
                        {
                            _muxManager.channels[channel].PCStatus = "";
                        }
                    }

                    _muxManager._mainWindow.MuxChannelGrid.Items.Refresh();
                    Log.Log.Info($"Cleared Results column after iteration {iteration} in duration mode");
                }
            }
            catch (OperationCanceledException)
            {
                // PC was stopped - but we still want to preserve completed results
                Log.Log
                    .Info($"PC duration execution cancelled, but preserving completed results for {allResults.Sum(kvp => kvp.Value.Count)} operations");
                
                // Log any unlogged results before exiting
                LogUnloggedResults(allResults, loggedIterations, withCart);
                
                // Re-throw to ensure the main method's catch block handles the stop message and logging
                throw;
            }

            // Reset isInProgress for all channels when duration complete
            foreach (var channel in selectedChannels)
            {
                if (_muxManager.channels.ContainsKey(channel))
                {
                    _muxManager.channels[channel].isInProgress = false; // No longer executing
                }
            }

            // Final UI refresh
            _muxManager._mainWindow.MuxChannelGrid.Items.Refresh();
            
            // If cancellation was requested at any point, throw after we've saved all results
            if (cancellationToken.IsCancellationRequested)
            {
                Log.Log.Info($"Throwing cancellation exception after preserving {allResults.Sum(kvp => kvp.Value.Count)} completed operations");
                throw new OperationCanceledException(cancellationToken);
            }

            return allResults;
        }

        CartType ParseCartType(string cartTypeString)
        {
            switch (cartTypeString)
            {
                case "Darin1":
                case "Darin-I":
                    return CartType.Darin1;
                case "Darin2":
                case "Darin-II":
                    return CartType.Darin2;
                case "Darin3":
                case "Darin-III":
                    return CartType.Darin3;
                default:
                    return CartType.Unknown;
            }
        }

        void OnCommandProgress(string commandName, Color color)
        {
            CommandInProgress?.Invoke(this, new CommandEventArgs(commandName, color));
        }
        
        void LogUnloggedResults(Dictionary<int, List<PCResult>> allResults, Dictionary<int, int> loggedIterations, bool withCart)
        {
            foreach (var channelNo in allResults.Keys)
            {
                var results = allResults[channelNo];
                var lastLogged = loggedIterations.ContainsKey(channelNo) ? loggedIterations[channelNo] : 0;
                
                // Log any iterations that haven't been logged yet
                for (int i = lastLogged; i < results.Count; i++)
                {
                    var iterationNumber = i + 1;
                    var result = results[i];
                    
                    if (_muxManager.channels.ContainsKey(channelNo))
                    {
                        var channelInfo = _muxManager.channels[channelNo];
                        var channelManager = _muxManager.GetChannelManager(channelNo);
                        
                        if (channelManager != null && channelInfo.cartNo >= 0 && channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                        {
                            try
                            {
                                PCLog.Instance
                                    .AddPerformanceResponse(
                                    withCart,
                                    result,
                                    iterationNumber,
                                    channelInfo.channel_SlotInfo[channelInfo.cartNo]
                                );
                                
                                Log.Log.Info($"Logged unlogged iteration {iterationNumber} for channel {channelNo} after cancellation");
                            }
                            catch (Exception ex)
                            {
                                Log.Log.Error($"Failed to log iteration {iterationNumber} for channel {channelNo} after cancellation: {ex.Message}");
                            }
                        }
                    }
                }
            }
        }
    }
}