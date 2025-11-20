using DTCL.Cartridges;
using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Messages;
using DTCL.Mux;
using DTCL.Transport;
using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;

namespace DTCL
{
    /// <summary>
    /// Interaction logic for MuxWindow.xaml
    /// </summary>
    public partial class MuxWindow : Window
    {
        // HwInfo hwInfo;
        public bool IsInProgress { get; set; }
        public bool isLogTypeSelected;
        PopUpMessagesContainer PopUpMessagesContainerObj;
        Color defaultColor = Color.FromArgb(255, 39, 174, 96);

        public bool isLogSelected;
        System.Timers.Timer _muxScanTimer;
        public bool stopPcFlag;
        public bool isPcStarted;
        public bool usrInfoMsg;
        public bool isFirstPcRun = true;

        public bool[] IstRun = new bool[9];
        MuxManager muxManager;
        MuxPerformanceCheck muxPerformanceCheck;

        // Flag to distinguish between Exit button click and X button click
        bool _isExitingApplication;

        // ObservableCollection for DataGrid editing support
        ObservableCollection<MuxChannelInfo> _channelCollection;

        // Professional cancellation mechanism
        CancellationTokenSource _pcCancellationTokenSource;

        public MuxWindow()
        {
            InitializeComponent();

            // Load popup messages
            var PopUpMessagesParserObj = new JsonParser<PopUpMessagesContainer>();
            PopUpMessagesContainerObj = PopUpMessagesParserObj.Deserialize("PopUpMessage\\PopUpMessages.json");

            // Initialize Mux components
            muxManager = new MuxManager(this);
            muxPerformanceCheck = new MuxPerformanceCheck(muxManager);

            // Initialize UI
            disableEntries();
            Scan.IsEnabled = true;
            withCart.IsChecked = false;
            withOutCart.IsChecked = false;
            withCart.IsEnabled = false;
            withOutCart.IsEnabled = false;
            IterationSel.IsChecked = false;
            DurationSel.IsChecked = false;

            // Create ObservableCollection for DataGrid editing support
            var channelCollection = new ObservableCollection<MuxChannelInfo>(muxManager.channels.Values);
            MuxChannelGrid.ItemsSource = channelCollection;

            // Store reference for updates
            _channelCollection = channelCollection;

            // Set up event handlers
            MuxChannelInfo.UserSelectionChanged += UpdateSelectAllCheckBoxState;
            muxManager.PortDisconnected += OnMuxHwDisconnected;
            muxManager.PortConnected += OnMuxHwConnected;

            // Hide command buttons initially
            CommandsLabel.Visibility = Visibility.Hidden;
            Erase.Visibility = Visibility.Hidden;
            Write.Visibility = Visibility.Hidden;
            Read.Visibility = Visibility.Hidden;
            LoopBack.Visibility = Visibility.Hidden;
            KeyDown += PerformanceCheckBlock_KeyDown;

            for (int i = 0; i < 9; i++)
                IstRun[i] = false;

            MuxChannelGrid.IsEnabled = false;
        }

        async void ScanMuxPorts(object sender, ElapsedEventArgs e)
        {
            Log.Log.Debug($"start scanning for Mux Port");
            _muxScanTimer.Stop();
            await muxManager.ScanMuxHw();

            if (muxManager.isMuxHwConnected == false)
                _muxScanTimer.Start();
        }

        public void OnMuxHwDisconnected(object sender, EventArgs e)
        {
            Log.Log.Debug($"Mux Hw port disconnected");
            UpdateUserStatus("USBMux_NotDetect_Msg2");
            usrInfoMsg = false;
            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), this);
            muxManager.isMuxHwConnected = false;
            _muxScanTimer.Elapsed += ScanMuxPorts;
            _muxScanTimer.Start();
        }

        public void OnMuxHwConnected(object sender, EventArgs e)
        {
            Log.Log.Debug($"Mux Hw port connected");

            // Defer the popup until UI finishes rendering
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var diag = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Detect_Msg"), this);

                if ((diag == CustomMessageBox.MessageBoxResult.Ok) && usrInfoMsg == false)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        // CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Info_Msg"), this);
                    }), System.Windows.Threading.DispatcherPriority.Background);

                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Start_PC_Msg"), this);
                    }), System.Windows.Threading.DispatcherPriority.Background);

                    usrInfoMsg = true;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);

            UpdateUserStatus("USBMux_Detect_Msg", 15);
            muxManager.isMuxHwConnected = true;
            _muxScanTimer.Elapsed -= ScanMuxPorts;
            _muxScanTimer.Stop();
        }

        async void Scan_Click(object sender, RoutedEventArgs e)
        {
            Scan.IsEnabled = false;
            var dtclDetected = false;
            var dtclCount = 0;
            var cartCount = 0;
            Clear.IsEnabled = false;

            // Clear previous scan data
            ClearScanData();

            MuxChannelGrid.IsEnabled = false;
            UpdateUserStatus("USBMux_Scan_Msg");

            try
            {
                // Perform comprehensive scan of all channels
                await muxManager.ScanAllChannelsAsync();

                // Process scan results and update channel information
                for (int chNo = 1; chNo <= muxManager.channels.Count; chNo++)
                {
                    var channelInfo = muxManager.channels[chNo];
                    var channelManager = muxManager.GetChannelManager(chNo);

                    if (channelInfo.isDTCLConnected && channelManager != null)
                    {
                        dtclDetected = true;
                        dtclCount++;

                        // Get hardware information from channel manager
                        var hwInfo = channelManager.HardwareInfo;

                        if (hwInfo != null)
                        {
                            // Check cart detection status
                            if (!string.IsNullOrEmpty(channelInfo.CartType) && channelInfo.cartNo > 0)
                            {
                                cartCount++;

                                Log.Log
                                    .Info($"Channel {chNo}: DTCL connected with {channelInfo.CartType} cart in slot {channelInfo.cartNo}");

                                if (channelInfo.CartType.Contains("Multi"))
                                {
                                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Multi_cart_Msg"), this);
                                    withCart.IsEnabled = false;
                                    withOutCart.IsEnabled = false;
                                    Scan.IsEnabled = true;
                                    MuxChannelGrid.IsEnabled = false;
                                    clearData();
                                    UpdateUI();
                                    Clear.IsEnabled = true;
                                    return;
                                }
                            }
                            else
                            {
                                Log.Log.Info($"Channel {chNo}: DTCL connected but no cart detected");
                            }
                        }
                        else
                        {
                            Log.Log.Warning($"Channel {chNo}: DTCL connected but hardware info unavailable");
                        }
                    }
                    else
                    {
                        Log.Log.Info($"Channel {chNo}: No DTCL detected");
                    }
                }

                // Update UI with scan results
                UpdateUI();
                UpdateUserStatus("USBMux_Scan_finish_Msg");

                // Display scan results to user
                if (!dtclDetected)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Mux_DtclDetect_Fail"), this);
                }
                else
                {
                    var message = $"Scan completed: {dtclCount} DTCL(s) detected, {cartCount} cart(s) found";
                    Log.Log.Info(message);

                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Scan_finish_Msg"), this);

                    // Enable cart selection options
                    withCart.IsEnabled = true;
                    withOutCart.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error during MUX scan: {ex.Message}");
            }
            finally
            {
                Scan.IsEnabled = true;
                MuxChannelGrid.IsEnabled = true;
                Clear.IsEnabled = true;
            }
        }

        void UpdateUI()
        {
            // Refresh the DataGrid
            MuxChannelGrid.Items.Refresh();
        }

        /// <summary>
        /// Clear previous scan data and reset all channel states
        /// </summary>
        void ClearScanData()
        {
            try
            {
                // Deactivate any currently active channels
                muxManager.DeactivateCurrentChannel();

                // Reset all channel information
                foreach (var item in muxManager.channels.Values)
                {
                    item.isDTCLConnected = false;
                    item.isUserSelected = false;
                    item.isInProgress = false;
                    item.DtcSno = ""; // Clear field
                    item.UnitSno = ""; // Clear field
                    item.PCStatus = "";
                    item.CartType = "";
                    item.cartNo = 0;
                }

                // Reset channel managers to clean state
                for (int ch = 1; ch <= 8; ch++)
                {
                    var channelManager = muxManager.GetChannelManager(ch);

                    if (channelManager != null && channelManager.IsActive)
                    {
                        channelManager.DeactivateChannel();
                    }
                }

                // Reset UI elements
                SelectAllCheckBox.IsChecked = false;
                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;

                // ObservableCollection will automatically update UI

                Log.Log.Info("MUX scan data cleared and channels reset");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error clearing scan data: {ex.Message}");
            }
        }

        /// <summary>
        /// Legacy method name for backward compatibility
        /// </summary>
        void clearData() => ClearScanData();

        async void InitiatePC_Click(object sender, RoutedEventArgs e)
        {
            // Clear any selected row to ensure proper yellow highlighting during PC execution
            MuxChannelGrid.SelectedItem = null;
            
            if ((IterationSel.IsChecked == false) && (DurationSel.IsChecked == false))
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Iter_Log"), this);
                return;
            }

            var iterationCount = 0;
            var PCDurationTime = 0;

            // Get selected channels
            var selectedChannels = new List<int>();

            for (int i = 1; i <= muxManager.channels.Count; i++)
            {
                if (muxManager.channels[i].isUserSelected)
                {
                    selectedChannels.Add(i);
                }
            }

            // Determine iteration or duration mode
            if (IterationSel.IsChecked == true)
            {
                if (!int.TryParse(IterationCount.Text, out iterationCount) || iterationCount <= 0)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Iter"), this);
                    return;
                }

                PCProgressBar.Maximum = iterationCount * selectedChannels.Count;
            }
            else
            {
                int minutes, seconds = 0;

                if (!int.TryParse(DurationMin.Text, out minutes) || !int.TryParse(DurationSec.Text, out seconds) ||
                    minutes < 0 || seconds < 0 || (minutes == 0 && seconds == 0))
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Duration"), this);
                    return;
                }

                PCDurationTime = (minutes * 60) + seconds;
                PCProgressBar.Maximum = PCDurationTime; // Duration mode: max is still just the duration in seconds
            }

            // Prepare files
            if (!FileOperations.IsFileExist(HardwareInfo.Instance.D2UploadFilePath + @"DR.bin"))
            {
                FileOperations.Copy("D2\\DR.bin", HardwareInfo.Instance.D2UploadFilePath + @"DR.bin");
            }

            if (!FileOperations.IsFileExist(HardwareInfo.Instance.D3UploadFilePath + @"DR.bin"))
            {
                FileOperations.Copy("D3\\DR.bin", HardwareInfo.Instance.D3UploadFilePath + @"DR.bin");
            }

            if (selectedChannels.Count == 0)
            {
                // Check if this is withOutCart mode - allow loopback functionality
                if (withOutCart.IsChecked == true)
                {
                    // Create a dummy channel for loopback testing when no channels selected
                    selectedChannels.Add(1); // Use channel 1 as default for loopback
                }
                else
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("No_Channel_Selected"), this);
                    return;
                }
            }

            // Initialize UI
            ClearPCResult();
            InitiatePC.IsEnabled = false;
            StopPc.IsEnabled = true;
            ConfirmLog.IsEnabled = false;
            Scan.IsEnabled = false;
            Clear.IsEnabled = false;
            IterationCount.IsEnabled = false;
            UpdateUserStatus("USBMux_Exe_Progress_Msg");

            // Initialize logging for selected channels - only add iteration duration line if this is NOT the first PC run
            if (!isFirstPcRun)
            {
                foreach (int i in selectedChannels)
                {
                    var channelInfo = muxManager.channels[i];

                    // Add safety check for channel_SlotInfo
                    if (channelInfo.channel_SlotInfo != null && channelInfo.cartNo >= 0 &&
                        channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                    {
                        if (IterationSel.IsChecked == true)
                        {
                            PCLog.Instance.AddIterationDuration(iterationCount, 0, channelInfo.channel_SlotInfo[channelInfo.cartNo]);
                        }
                        else
                        {
                            PCLog.Instance.AddIterationDuration(0, PCDurationTime, channelInfo.channel_SlotInfo[channelInfo.cartNo]);
                        }
                    }
                    else
                    {
                        Log.Log.Warning($"Channel {i}: SlotInfo not available for cart {channelInfo.cartNo}");
                        // Skip logging for this channel if slot info not available
                    }
                }
            }
            else
            {
                foreach (int i in selectedChannels)
                {
                    var channelInfo = muxManager.channels[i];

                    // Add safety check for channel_SlotInfo
                    if (channelInfo.channel_SlotInfo != null && channelInfo.cartNo >= 0 &&
                        channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                    {
                        PCLog.Instance
                            .EditIterationDurationType(iterationCount, PCDurationTime, channelInfo.channel_SlotInfo[channelInfo.cartNo]);
                    }
                }
            }

            // Mark that we've completed the first PC run
            isFirstPcRun = false;

            try
            {
                // Create new cancellation token for this PC execution
                _pcCancellationTokenSource = new CancellationTokenSource();

                // Use MuxPerformanceCheck for execution
                var progress = new Progress<MuxPCProgress>(UpdatePCProgressFromMux);
                Dictionary<int, List<PCResult>> results;

                if (IterationSel.IsChecked == true)
                {
                    isPcStarted = true;

                    results = await muxPerformanceCheck.ExecuteIterationsOnChannels(
                        withCart.IsChecked ?? false,
                        selectedChannels,
                        iterationCount,
                        progress,
                        _pcCancellationTokenSource.Token
                    );
                }
                else
                {
                    isPcStarted = true;

                    results = await muxPerformanceCheck.ExecuteDurationOnChannels(
                        withCart.IsChecked ?? false,
                        selectedChannels,
                        PCDurationTime,
                        progress,
                        _pcCancellationTokenSource.Token
                    );
                }

                // Results have already been logged in real-time during execution
                // Log completion for all selected channels
                foreach (int i in selectedChannels)
                {
                    var channelInfo = muxManager.channels[i];

                    // Add safety check for channel_SlotInfo - same as initialization
                    if (channelInfo.channel_SlotInfo != null && channelInfo.cartNo >= 0 &&
                        channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                    {
                        PCLog.Instance.AddEntry("Performance Check Completed", channelInfo.channel_SlotInfo[channelInfo.cartNo]);
                    }
                    else
                    {
                        Log.Log
                            .Warning($"Channel {i}: Cannot log completion - SlotInfo not available for cart {channelInfo.cartNo}");
                        // Create a basic SlotInfo for logging completion
                        var defaultSlot = new SlotInfo(channelInfo.cartNo >= 0 ? channelInfo.cartNo : 1);
                        PCLog.Instance.AddEntry("Performance Check Completed", defaultSlot);
                    }
                }

                // Show completion message
                PCProgressBar.Value = PCProgressBar.Maximum;
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Completed_Msg"), this);

                // Clear the Results column after completion message is shown
                ClearPCResult();
            }
            catch (OperationCanceledException)
            {
                Log.Log.Info("Performance check was cancelled by user");

                // Show stopped message to user
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Stopped_Msg2"), this);

                foreach (int i in selectedChannels)
                {
                    var channelInfo = muxManager.channels[i];

                    // Add safety check for channel_SlotInfo - same as initialization
                    if (channelInfo.channel_SlotInfo != null && channelInfo.cartNo >= 0 &&
                        channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                    {
                        PCLog.Instance.AddEntry("Performance Check Stopped", channelInfo.channel_SlotInfo[channelInfo.cartNo]);
                    }
                    else
                    {
                        Log.Log.Warning($"Channel {i}: Cannot log stop - SlotInfo not available for cart {channelInfo.cartNo}");
                        // Create a basic SlotInfo for logging stop
                        var defaultSlot = new SlotInfo(channelInfo.cartNo >= 0 ? channelInfo.cartNo : 1);
                        PCLog.Instance.AddEntry("Performance Check Stopped", defaultSlot);
                    }
                }

                // Clear the Results column after completion message is shown
                ClearPCResult();
                UpdateUI();
                // clearData();
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Performance check execution failed: {ex.Message}");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Error_Msg"), this);
            }
            finally
            {
                // Clean up cancellation token
                _pcCancellationTokenSource?.Dispose();
                _pcCancellationTokenSource = null;
                // Reset UI
                stopPcFlag = false;
                StopPc.IsEnabled = false;
                InitiatePC.IsEnabled = true;
                ConfirmLog.IsEnabled = false;
                Scan.IsEnabled = false;
                Clear.IsEnabled = true;
                IterationSel.IsEnabled = true;
                DurationSel.IsEnabled = true;
                IterationCount.IsEnabled = true;
                isPcStarted = false;

                // Reset display
                DurationMin.Text = "0";
                DurationSec.Text = "10";
                TimeElapsed.Text = "00000";
                CurrentIteration.Text = "00000";
                PCProgressBar.Value = 0;
                UpdateUserStatus("Idle_Msg");
            }
        }

        void UpdatePCProgressFromMux(MuxPCProgress progress)
        {
            // Ensure UI updates happen on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdatePCProgressFromMux(progress));
                return;
            }

            // Update progress bar and display BOTH iteration and elapsed time for both modes
            if (IterationSel.IsChecked == true)
            {
                // Iteration mode: Use the CompletedOperations and TotalOperations from MuxPCProgress
                if (progress.TotalOperations > 0)
                {
                    PCProgressBar.Value = progress.CompletedOperations;
                    PCProgressBar.Maximum = progress.TotalOperations;
                }

                CurrentIteration.Text = progress.CurrentIteration.ToString();
                TimeElapsed.Text = progress.ElapsedTime.ToString();
            }
            else
            {
                // Duration mode: progress bar shows elapsed time vs total duration, but also track operations
                PCProgressBar.Value = progress.ElapsedTime;
                PCProgressBar.Maximum = progress.TotalDuration;
                TimeElapsed.Text = progress.ElapsedTime.ToString();
                CurrentIteration.Text = progress.CurrentIteration.ToString(); // Show iteration too
            }

            // Update channel status
            if (muxManager.channels.ContainsKey(progress.Channel))
            {
                muxManager.channels[progress.Channel].PCStatus = progress.Status;
            }

            // Refresh UI
            MuxChannelGrid.Items.Refresh();
        }

        public void ClearPCResult()
        {
            foreach (var item in muxManager.channels.Values)
                item.PCStatus = "";

            MuxChannelGrid.Items.Refresh();
        }

        public void ClearPCResultForChannels(List<int> channels)
        {
            // Ensure this runs on UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => ClearPCResultForChannels(channels));
                return;
            }

            foreach (var channelNo in channels)
            {
                if (muxManager.channels.ContainsKey(channelNo))
                {
                    muxManager.channels[channelNo].PCStatus = ""; // Now triggers INotifyPropertyChanged
                }
            }

            Log.Log.Info($"Cleared PCStatus for channels: {string.Join(", ", channels)}");
        }

        void OnCommandChanged(object sender, CommandEventArgs e)
        {
            if (e.commandName != null)
            {
                // Check button names using if-else
                if (e.commandName == "Erase")
                {
                    Erase.Background = new SolidColorBrush(e.commandColor);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "Write")
                {
                    Write.Background = new SolidColorBrush(e.commandColor);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "Read")
                {
                    Read.Background = new SolidColorBrush(e.commandColor);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "LoopBack")
                {
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    LoopBack.Background = new SolidColorBrush(e.commandColor);
                }
                else
                {
                    Read.Background = new SolidColorBrush(Colors.DarkGray);
                    Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    Write.Background = new SolidColorBrush(Colors.DarkGray);
                    LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
            }
        }

        public async void UpdatePCProgress(int iterationCount, int PCDurationTime, int elapsedTime, int counter, PCResult result, int ChNo)
        {
            // Ensure UI updates happen on the UI thread
            if (!Dispatcher.CheckAccess())
            {
                Dispatcher.Invoke(() => UpdatePCProgress(iterationCount, PCDurationTime, elapsedTime, counter, result, ChNo));
                return;
            }

            var channelInfo = muxManager.channels[ChNo];
            // Set the result display
            if (result.eraseResult.Equals("PASS") && result.writeResult.Equals("PASS") && result.readResult.Equals("PASS") && result.loopBackResult.Equals("PASS"))
            {
                channelInfo.PCStatus = "PASS";
            }
            else
            {
                channelInfo.PCStatus = "FAIL";
            }

            // Update the progress bar and BOTH time elapsed and iteration in BOTH modes
            if (IterationSel.IsChecked == true)
            {
                // Iteration mode: progress bar shows iterations, but display both values
                PCProgressBar.Value = PCProgressBar.Maximum - iterationCount;
                TimeElapsed.Text = $"{elapsedTime}"; // Show actual elapsed time
                CurrentIteration.Text = counter.ToString(); // Show current iteration
            }
            else
            {
                // Duration mode: progress bar shows elapsed time, but display both values
                PCProgressBar.Value = elapsedTime; // Use actual elapsed time, not calculated value
                TimeElapsed.Text = $"{elapsedTime}"; // Show actual elapsed time
                CurrentIteration.Text = counter.ToString(); // Show current iteration
            }

            // Refresh the UI
            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            await Task.Delay(1);
        }

        public string UpdateUserStatus(string Msg, int size = 17)
        {
            StatusTextBlock.FontSize = size;
            StatusTextBlock.Text = PopUpMessagesContainerObj.FindStatusMsgById(Msg);
            StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
            return StatusTextBlock.Text;
        }

        void NewLog_Click(object sender, RoutedEventArgs e)
        {
            withCart.IsEnabled = true;
            withOutCart.IsEnabled = true;

            isLogTypeSelected = true;
            PCLog.Instance.LogType = "New";

            TestNumber.IsEnabled = true;
            InspectorName.IsEnabled = true;

            InitiatePC.IsEnabled = false;
            StopPc.IsEnabled = false;
            IterationCount.IsEnabled = false;
            DurationMin.IsEnabled = false;
            DurationSec.IsEnabled = false;
            ConfirmLog.IsEnabled = true;
        }

        void ConfirmLog_Click(object sender, RoutedEventArgs e)
        {
            foreach (var item in muxManager.channels.Values)
            {
                if ((item.isUserSelected == true))
                {
                    if ((InspectorName.Text == "") || (TestNumber.Text == ""))
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg3"), this);
                        return;
                    }

                    if (((item.DtcSno == "") && (withCart.IsChecked == true)))
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg"), this);
                        return;
                    }

                    if (item.UnitSno == "")
                    {
                        if (withCart.IsChecked == true)
                        {
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg"), this);
                        }
                        else
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg2"), this);

                        return;
                    }
                }
            }

            foreach (var item in muxManager.channels.Values)
            {
                if ((item.DtcSno == "999") || (item.UnitSno == "999") || (InspectorName.Text == "999") || (TestNumber.Text == "999"))
                {
                    var res = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Default_Serial_Msg"), this);

                    if (res == CustomMessageBox.MessageBoxResult.Yes)
                    {
                        break;
                    }
                    else
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg4"), this);
                        return;
                    }
                }

                else
                {
                    continue;
                }
            }

            isLogTypeSelected = true;
            PCLog.Instance.LogType = "New";
            ConfirmLog.Background = new SolidColorBrush(Colors.DodgerBlue);

            InitiatePC.IsEnabled = true;
            var tmp = false;

            if (isLogTypeSelected)
                isLogSelected = true;
            else
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Sel_Log_Type_Msg"), this);
                ConfirmLog.Background = new SolidColorBrush(defaultColor);
                return;
            }

            if (PCLog.Instance.LogType == "Old")
            {
                // PCLog.Instance.AppendToOldLog(TestNumber.Text, InspectorName.Text, UnitSINo.Text, DTCSINo.Text, withCart.IsChecked ?? false);
                // CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Old_Log_Sel_Msg"), this);
                // OldLog.Background = new SolidColorBrush(defaultColor);
            }
            else
            {
                PCLog.Instance.LogFileNameList.Clear();

                for (int i = 1; i <= 8; i++)
                {
                    if ((muxManager.channels[i].isUserSelected))
                    {
                        var cartNo = muxManager.channels[i].cartNo;

                        PCLog.Instance
                            .CreateNewLog(TestNumber.Text, InspectorName.Text, muxManager.channels[i].DtcSno, muxManager.channels[i].UnitSno, withCart.IsChecked ?? false, muxManager.channels[i].channel_SlotInfo[cartNo], i);

                        tmp = true;
                    }
                }

                if (tmp == false)
                {
                    // Check if this is withOutCart mode with no selected channels - allow loopback functionality
                    if (withOutCart.IsChecked == true)
                    {
                        // Create dummy log for loopback testing when no channels selected in withOutCart mode
                        var dummySlot = HardwareInfo.Instance.SlotInfo[0]; // Use slot 0 as default for loopback
                        PCLog.Instance.CreateNewLog(TestNumber.Text, InspectorName.Text, "", "999", false, dummySlot, 1);
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_New_Log2"), this);
                        tmp = true; // Allow continuation for loopback mode
                    }
                    else
                    {
                        InitiatePC.IsEnabled = false;
                        ConfirmLog.Background = new SolidColorBrush(defaultColor);
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_No_Log"), this);
                        return;
                    }
                }

                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_New_Log2"), this);
            }

            ConfirmLog.IsEnabled = false;
            Scan.IsEnabled = false;
            InitiatePC.IsEnabled = true;
            IterationSel.IsEnabled = true;
            DurationSel.IsEnabled = true;

            withCart.IsEnabled = false;
            withOutCart.IsEnabled = false;

            for (int i = 0; i < 9; i++)
                IstRun[i] = true;
            IterationCount.IsEnabled = true;

            isFirstPcRun = true;
            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Iter_Log"), this);
            UpdateUserStatus("USBMux_Enter_Iter_Log");
            MuxChannelGrid.IsEnabled = false;
            InspectorName.IsEnabled = false;
            TestNumber.IsEnabled = false;
        }

        public void disableEntries()
        {
            Scan.IsEnabled = false;
            ConfirmLog.IsEnabled = false;
            TestNumber.IsEnabled = false;
            InspectorName.IsEnabled = false;
            InitiatePC.IsEnabled = false;
            StopPc.IsEnabled = false;
            IterationCount.IsEnabled = false;
            DurationMin.IsEnabled = false;
            DurationSec.IsEnabled = false;
            IterationSel.IsEnabled = false;
            DurationSel.IsEnabled = false;
            TimeElapsed.IsEnabled = false;
            CurrentIteration.IsEnabled = false;
            Write.Background = new SolidColorBrush(Colors.DarkGray);
            Erase.Background = new SolidColorBrush(Colors.DarkGray);
            Read.Background = new SolidColorBrush(Colors.DarkGray);
            LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
        }

        async void withCart_Click(object sender, RoutedEventArgs e)
        {
            Scan.IsEnabled = true;
            var tmp = false;
            TestNumber.IsEnabled = false;
            InspectorName.IsEnabled = false;
            ConfirmLog.IsEnabled = false;

            foreach (var item in muxManager.channels.Values)
            {
                if ((item.isUserSelected == true))
                {
                    await muxManager.ScanChannelAsync((byte)item.Channel);
                    tmp = true;

                    if (item.CartType.Contains("Multi"))
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Remove_Cart_Msg"), this);
                        withCart.IsChecked = false;
                        withOutCart.IsChecked = false;
                        return;
                    }

                    if (item.CartType != "")
                    {
                        // Cart detected - default value will be set after scanning completes
                        continue;
                    }
                    else
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Insert_Cart_Msg"), this);
                        withCart.IsChecked = false;
                        withOutCart.IsChecked = false;
                        return;
                    }
                }
                else
                {
                    // disbale editing of DTC and Unit S/N if not selected
                    // We'll handle this after the grid refresh
                }
            }

            if (tmp == false)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Slot_Sel"), this);
                withCart.IsChecked = false;
                withOutCart.IsChecked = false;
                return;
            }

            MuxChannelGrid.Items.Refresh();
            InspectorName.IsEnabled = true;
            TestNumber.IsEnabled = true;
            ConfirmLog.IsEnabled = true;
            CommandsLabel.Visibility = Visibility.Visible;
            Erase.Visibility = Visibility.Visible;
            Write.Visibility = Visibility.Visible;
            Read.Visibility = Visibility.Visible;
            LoopBack.Visibility = Visibility.Visible;
            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg"), this);
        }

        async void withOutCart_Click(object sender, RoutedEventArgs e)
        {
            Scan.IsEnabled = true;
            var tmp = false;
            TestNumber.IsEnabled = false;
            InspectorName.IsEnabled = false;
            ConfirmLog.IsEnabled = false;

            foreach (var item in muxManager.channels.Values)
            {
                if ((item.isUserSelected == true))
                {
                    await muxManager.ScanChannelAsync((byte)item.Channel);
                    tmp = true;

                    if (item.CartType == "")
                    {
                        // No cart detected - default value will be set after scanning completes
                        continue;
                    }
                    else
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Please_Remove_Msg"), this);
                        withCart.IsChecked = false;
                        withOutCart.IsChecked = false;
                        return;
                    }
                }
            }

            if (tmp == false)
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Slot_Sel"), this);
                withCart.IsChecked = false;
                withOutCart.IsChecked = false;
                return;
            }

            MuxChannelGrid.Items.Refresh();
            InspectorName.IsEnabled = true;
            TestNumber.IsEnabled = true;
            ConfirmLog.IsEnabled = true;
            CommandsLabel.Visibility = Visibility.Visible;
            Erase.Visibility = Visibility.Hidden;
            Write.Visibility = Visibility.Hidden;
            Read.Visibility = Visibility.Hidden;
            LoopBack.Visibility = Visibility.Visible;
            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg2"), this);
        }

        void Clear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                disableEntries();

                // Use the comprehensive clear method for proper channel cleanup
                ClearScanData();
                // clearData();
                UpdateUI();

                // Reset UI elements
                Scan.IsEnabled = true;
                withCart.IsChecked = false;
                withOutCart.IsChecked = false;
                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;

                CommandsLabel.Visibility = Visibility.Hidden;
                Erase.Visibility = Visibility.Hidden;
                Write.Visibility = Visibility.Hidden;
                Read.Visibility = Visibility.Hidden;
                LoopBack.Visibility = Visibility.Hidden;

                IterationSel.IsChecked = false;
                DurationSel.IsChecked = false;

                // Reset progress and input fields
                PCProgressBar.Value = 0;
                IterationCount.Text = "1";
                DurationMin.Text = "0";
                DurationSec.Text = "10";
                TimeElapsed.Text = "0";

                // Reset PC run tracking for new session
                isFirstPcRun = true;

                ClearPCResult();

                UpdateUserStatus("Idle_Msg");
                Log.Log.Info("MUX interface cleared and reset");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error during Clear operation: {ex.Message}");
            }
        }

        void IterationSel_Click(object sender, RoutedEventArgs e)
        {
            if (IterationSel.IsChecked == true)
            {
                IterationCount.IsEnabled = true;
                DurationMin.IsEnabled = false;
                DurationSec.IsEnabled = false;
            }
        }

        void DurationSel_Click(object sender, RoutedEventArgs e)
        {
            if (DurationSel.IsChecked == true)
            {
                IterationCount.IsEnabled = false;
                DurationMin.IsEnabled = true;
                DurationSec.IsEnabled = true;
            }
        }

        void StopPc_Click(object sender, RoutedEventArgs e)
        {
            StopPc.Background = new SolidColorBrush(Colors.DodgerBlue);

            if (isPcStarted == true)
            {
                // Professional cancellation - trigger the cancellation token
                if (_pcCancellationTokenSource != null && !_pcCancellationTokenSource.IsCancellationRequested)
                {
                    Log.Log.Info("User requested PC cancellation");
                    _pcCancellationTokenSource.Cancel();
                }

                stopPcFlag = true;
                isPcStarted = false;
                // Message will be shown in the catch block when cancellation is processed
                StopPc.IsEnabled = false;
                InitiatePC.IsEnabled = true;
            }
            else
            {
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Start_Pc_Fail_Msg"), this);
            }

            StopPc.Background = new SolidColorBrush(defaultColor);
        }

        void Logo_MouseDown(object sender, MouseButtonEventArgs e)
        {
            MessageBox.Show("Multi Unit GUI Version 2.3");
        }

        void Exit_Click(object sender, RoutedEventArgs e)
        {
            var shouldContinue = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Exit_Msg"), this);

            if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
            {
                Log.Log.Warning($"User chose to cancel exit");
                return;
            }

            var selectedChannels = new List<int>();

            for (int i = 1; i <= muxManager.channels.Count; i++)
            {
                if (muxManager.channels[i].isUserSelected)
                {
                    selectedChannels.Add(i);
                }
            }
            // Results have already been logged in real-time during execution
            // Log completion for all selected channels
            foreach (int i in selectedChannels)
            {
                var channelInfo = muxManager.channels[i];

                // Add safety check for channel_SlotInfo - same as initialization
                if (channelInfo.channel_SlotInfo != null && channelInfo.cartNo >= 0 &&
                    channelInfo.cartNo < channelInfo.channel_SlotInfo.Length)
                {
                    PCLog.Instance.AddEntry("Performance Check Exited", channelInfo.channel_SlotInfo[channelInfo.cartNo]);
                }
                else
                {
                    var defaultSlot = new SlotInfo(channelInfo.cartNo >= 0 ? channelInfo.cartNo : 1);
                    PCLog.Instance.AddEntry("Performance Check Exited", defaultSlot);
                }
            }

            // Set flag to indicate this is an application exit, not just window close
            _isExitingApplication = true;

            // Ensure complete application shutdown including hidden main window
            Log.Log.Info("Shutting down application from Mux window");

            // Stop any ongoing operations
            if (muxManager.isMuxHwConnected)
            {
                muxManager.switch_Mux((char)0); // Switch off all channels
            }

            // Clean up Mux resources first
            if (muxManager._muxTransport != null)
            {
                muxManager._muxTransport.Dispose();
            }

            // Force complete application shutdown
            Close();
            Application.Current.Shutdown();
            Environment.Exit(0);
        }

        void Window_Closed(object sender, EventArgs e)
        {

            // Clean up transport if not already disposed (only if not already disposed by Exit button)
            if (!_isExitingApplication && muxManager._muxTransport != null)
            {
                muxManager._muxTransport.Dispose();
            }

            // Different behavior based on how the window was closed
            if (_isExitingApplication)
            {
                // Exit button was clicked - application shutdown is already handled in Exit_Click
                Log.Log.Info("Window closed via Exit button - application shutdown in progress");
            }
            else
            {
                // X button was clicked - return to main window
                if (Application.Current.MainWindow != null)
                {
                    if (!Application.Current.MainWindow.IsVisible)
                    {
                        Log.Log.Info("Mux window closed via X button - showing main window");
                        Application.Current.MainWindow.Show();
                        Application.Current.MainWindow.WindowState = WindowState.Normal;
                        Application.Current.MainWindow.Activate();

                        // Re-activate OnHwConnected event handler that was disabled when MuxWindow opened
                        if (Application.Current.MainWindow is MainWindow mainWindow)
                        {
                            mainWindow.ReactivateHardwareEventHandlers();
                        }
                    }
                }
                else
                {
                    // Fallback: if no main window exists, exit the application
                    Log.Log.Warning("No main window found - shutting down application");
                    Application.Current.Shutdown();
                }
            }
        }

        void MuxChannelGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            var rowData = e.Row.Item as MuxChannelInfo;

            // Example: Make DtcSno cell read-only where Channel == 3
            if (e.Column.Header.ToString() == "DTC S.No" && (withOutCart.IsChecked == true))
            {
                e.Cancel = true; // cancel editing this cell
            }
            else if (e.Column.Header.ToString() == "DTC S.No" && (withCart.IsChecked == true))
            {
                e.Cancel = false; // cancel editing this cell
            }
        }

        void LoopBack_Click(object sender, RoutedEventArgs e)
        {
        }

        void PerformanceCheckBlock_KeyDown(object sender, KeyEventArgs e)
        {
            if ((Keyboard.IsKeyDown(Key.LeftCtrl) || (Keyboard.IsKeyDown(Key.RightCtrl))) && e.Key == Key.U)
            {
                if (muxManager.isMuxHwConnected)
                {
                    var mux_SelfTest = new Mux_SelfTest(muxManager._muxTransport, PopUpMessagesContainerObj);
                    mux_SelfTest.Show();
                }
                else
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), this);
                }
            }
        }

        async void PerformanceCheckBlock_Loaded(object sender, RoutedEventArgs e)
        {
            Scan.IsEnabled = false;
            Clear.IsEnabled = false;

            _muxScanTimer = new System.Timers.Timer(100);
            _muxScanTimer.Elapsed += ScanMuxPorts;

            await muxManager.ScanMuxHw();

            if (muxManager.isMuxHwConnected == false)
            {
                Log.Log.Debug($"Mux Hw port not connected initially");
                UpdateUserStatus("USBMux_NotDetect_Msg2");

                // Defer the popup until UI finishes rendering
                await Dispatcher.BeginInvoke(new Action(() =>
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), this);
                }), System.Windows.Threading.DispatcherPriority.Background);

                _muxScanTimer.Start();
            }

            Scan.IsEnabled = true;
            Clear.IsEnabled = true;
        }

        void IterationCount_TextChanged(object sender, TextChangedEventArgs e)
        {
            // if(IterationSel.IsChecked==true)
            //  CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Start_PC_Log"), this);
        }

        void IterationSel_Click_1(object sender, RoutedEventArgs e)
        {
            if (IterationSel.IsChecked == true)
            {
                IterationCount.IsEnabled = true;
                DurationMin.IsEnabled = false;
                DurationSec.IsEnabled = false;
            }
        }

        void DurationSel_Click_1(object sender, RoutedEventArgs e)
        {
            if (DurationSel.IsChecked == true)
            {
                IterationCount.IsEnabled = false;
                DurationMin.IsEnabled = true;
                DurationSec.IsEnabled = true;
            }
        }

        // Add these event handlers to your MuxWindow.xaml.cs file

        void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;

            if (checkBox != null && MuxChannelGrid.ItemsSource != null)
            {
                // Get the collection of MuxChannelInfo items
                var items = muxManager.channels.Values;

                if (items != null)
                {
                    // Only select items where DTCL is connected
                    foreach (var item in items.Where(x => x.isDTCLConnected))
                        item.isUserSelected = true;
                }
            }
        }

        void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            var checkBox = sender as CheckBox;

            if (checkBox != null && MuxChannelGrid.ItemsSource != null)
            {
                // Get the collection of MuxChannelInfo items
                var items = muxManager.channels.Values;

                if (items != null)
                {
                    // Only unselect items where DTCL is connected
                    foreach (var item in items.Where(x => x.isDTCLConnected))
                        item.isUserSelected = false;
                }
            }
        }
        void UpdateSelectAllCheckBoxState()
        {
            if (MuxChannelGrid.ItemsSource != null)
            {
                var items = muxManager.channels.Values;

                if (items != null)
                {
                    var connectedItems = items.Where(x => x.isDTCLConnected).ToList();

                    if (connectedItems.Any())
                    {
                        var selectAllCheckBox = FindVisualChild<CheckBox>(MuxChannelGrid, "SelectAllCheckBox");

                        if (selectAllCheckBox != null)
                        {
                            var selectedCount = connectedItems.Count(x => x.isUserSelected);
                            var totalConnected = connectedItems.Count;

                            if (selectedCount == totalConnected)
                            {
                                selectAllCheckBox.IsChecked = true;
                            }
                            else if (selectedCount == 0)
                            {
                                selectAllCheckBox.IsChecked = false;
                            }
                            else
                            {
                                selectAllCheckBox.IsChecked = null; // Indeterminate state
                            }
                        }
                    }
                }
            }
        }

        // Helper method to find visual children
        static T FindVisualChild<T>(DependencyObject parent, string name) where T : DependencyObject
        {
            for (int i = 0; i < VisualTreeHelper.GetChildrenCount(parent); i++)
            {
                var child = VisualTreeHelper.GetChild(parent, i);

                if (child is T t && (child as FrameworkElement)?.Name == name)
                {
                    return t;
                }

                var result = FindVisualChild<T>(child, name);

                if (result != null)
                {
                    return result;
                }
            }

            return null;
        }

    }
}