using System;
using System.Collections.Generic;
using System.Collections.ObjectModel;
using System.ComponentModel;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Threading;
using DTCL;
using DTCL.Transport;
using DTCL.Log;
using DTCL.Cartridges;
using DTCL.JsonParser;

namespace DTCL.Mux
{
    /// <summary>
    /// DPS MUX Window for 4-slot hardware (DPS2_4_IN_1 and DPS3_4_IN_1)
    /// Supports 8 channels × 4 slots = 32 total slots
    /// </summary>
    public partial class DPSMuxWindow : Window
    {
        // MUX Manager
        private DPSMuxManager dpsMuxManager;

        // Observable collection for data binding
        private ObservableCollection<DPSMuxChannelInfo> channelDataSource;

        // MUX detection timer
        private System.Timers.Timer _dpsMuxScanTimer;
        private bool usrInfoMsg = false;
        private bool _isScanningMux = false;  // Flag to prevent overlapping scans
        private bool _isUpdatingSelectAllState = false;  // Flag to prevent recursion in SelectAll logic

        // PopUp messages
        private PopUpMessagesContainer PopUpMessagesContainerObj;

        // Log confirmation state
        private bool isLogConfirmed = false;

        // Performance Check state
        private bool isPCRunning = false;
        private CancellationTokenSource pcCancellationTokenSource;
        private System.Windows.Threading.DispatcherTimer progressTimer;
        private DateTime pcStartTime;
        private int totalIterations = 0;
        private int currentIterationCount = 0;

        // Flag to distinguish between Exit button click and X button click
        private bool _isExitingApplication = false;

        // Constructor
        public DPSMuxWindow()
        {
            InitializeComponent();
            InitializeWindow();
        }

        /// <summary>
        /// Initialize window components
        /// </summary>
        private void InitializeWindow()
        {
            try
            {
                // Load popup messages
                var PopUpMessagesParserObj = new JsonParser<PopUpMessagesContainer>();
                PopUpMessagesContainerObj = PopUpMessagesParserObj.Deserialize("PopUpMessage\\PopUpMessages.json");

                // Create DPS MUX Manager
                dpsMuxManager = new DPSMuxManager();

                // Wire up MUX events
                dpsMuxManager.PortConnected += OnDPSMuxHwConnected;
                dpsMuxManager.PortDisconnected += OnDPSMuxHwDisconnected;

                // Setup data source
                channelDataSource = new ObservableCollection<DPSMuxChannelInfo>();
                for (int i = 1; i <= 8; i++)
                {
                    var channel = dpsMuxManager.channels[i];
                    channelDataSource.Add(channel);

                    // Subscribe to PropertyChanged to update SelectAllCheckBox when individual checkboxes change
                    channel.PropertyChanged += Channel_PropertyChanged;
                }

                // Bind to DataGrid
                DPSMuxChannelGrid.ItemsSource = channelDataSource;

                // Initialize progress timer
                progressTimer = new System.Windows.Threading.DispatcherTimer();
                progressTimer.Interval = TimeSpan.FromSeconds(1);
                progressTimer.Tick += ProgressTimer_Tick;

                // Set initial UI state
                UpdateUIState(isPCRunning: false);

                // Initialize command buttons to dark gray and hide them initially
                Erase.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                Write.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                Read.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                LoopBack.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);

                // Hide command buttons initially
                CommandsLabel.Visibility = Visibility.Visible;
                Erase.Visibility = Visibility.Hidden;
                Write.Visibility = Visibility.Hidden;
                Read.Visibility = Visibility.Hidden;
                LoopBack.Visibility = Visibility.Visible;

                // Wire up PerformanceCheckBlock Loaded event
                PerformanceCheckBlock.Loaded += PerformanceCheckBlock_Loaded;
                InitiatePC.IsEnabled = false;
                ConfirmLog.IsEnabled = false;
                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;
                InspectorName.IsEnabled = false;
                TestNumber.IsEnabled = false;
                IterationSel.IsEnabled = false;
                DurationSel.IsEnabled = false;
                IterationCount.IsEnabled = false;
                DurationMin.IsEnabled = false;
                DurationSec.IsEnabled = false;
                TimeElapsed.IsEnabled = false;
                CurrentIteration.IsEnabled = false;

                DTCL.Log.Log.Info("DPS MUX Window initialized successfully");
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error initializing DPS MUX Window: {ex.Message}");
                // Initialization errors - use debug log only, don't show popup at startup
            }
        }

        /// <summary>
        /// Window loaded event - Initialize MUX detection timer
        /// </summary>
        private async void PerformanceCheckBlock_Loaded(object sender, RoutedEventArgs e)
        {
            try
            {
                Scan.IsEnabled = false;
                Clear.IsEnabled = false;

                // Create MUX scan timer with 2000ms (2 second) interval
                // This gives enough time for COM port cleanup and prevents overlapping scans
                _dpsMuxScanTimer = new System.Timers.Timer(2000);
                _dpsMuxScanTimer.Elapsed += ScanDPSMuxPorts;

                // Initial scan for DPS MUX hardware
                await dpsMuxManager.ScanMuxHw();

                if (!dpsMuxManager.IsMuxConnected())
                {
                    DTCL.Log.Log.Debug("DPS MUX hardware not connected initially");
                    UpdateUserStatus("USBMux_NotDetect_Msg2");

                    // Defer the popup until UI finishes rendering
                    await Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), this);
                    }), System.Windows.Threading.DispatcherPriority.Background);

                    // Keep Scan and Clear disabled when not connected
                    Scan.IsEnabled = false;
                    Clear.IsEnabled = false;

                    // Start timer to keep scanning
                    _dpsMuxScanTimer.Start();
                }
                else
                {
                    // MUX hardware is connected - enable Scan and Clear buttons
                    Scan.IsEnabled = true;
                    Clear.IsEnabled = true;
                    DTCL.Log.Log.Info("DPS MUX hardware connected initially - Scan and Clear buttons enabled");
                }
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error in PerformanceCheckBlock_Loaded: {ex.Message}");
                // Don't enable buttons on error - let user try to connect hardware manually
                Scan.IsEnabled = false;
                Clear.IsEnabled = false;
            }
        }

        /// <summary>
        /// Timer callback to scan for DPS MUX hardware
        /// </summary>
        private async void ScanDPSMuxPorts(object sender, ElapsedEventArgs e)
        {
            // Prevent overlapping scans
            if (_isScanningMux)
            {
                DTCL.Log.Log.Debug("Previous MUX scan still in progress, skipping...");
                return;
            }

            _isScanningMux = true;
            _dpsMuxScanTimer.Stop();

            try
            {
                DTCL.Log.Log.Debug("Start scanning for DPS MUX hardware...");
                await dpsMuxManager.ScanMuxHw();

                if (!dpsMuxManager.IsMuxConnected())
                {
                    DTCL.Log.Log.Debug("DPS MUX not found, will retry...");
                    _dpsMuxScanTimer.Start();
                }
                else
                {
                    DTCL.Log.Log.Info("DPS MUX hardware reconnected successfully");
                }
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error during MUX scan: {ex.Message}");
                _dpsMuxScanTimer.Start(); // Continue trying
            }
            finally
            {
                _isScanningMux = false;
            }
        }

        /// <summary>
        /// Event handler for DPS MUX hardware disconnection
        /// </summary>
        private async void OnDPSMuxHwDisconnected(object sender, EventArgs e)
        {
            DTCL.Log.Log.Debug("DPS MUX hardware port disconnected");

            // Don't show disconnect popup if application is exiting
            if (_isExitingApplication)
            {
                DTCL.Log.Log.Debug("Application is exiting - skipping disconnect handling");
                return;
            }

            Dispatcher.Invoke(() =>
            {
                // Disable all buttons and controls except Exit
                Scan.IsEnabled = false;
                Clear.IsEnabled = false;
                ConfirmLog.IsEnabled = false;
                InitiatePC.IsEnabled = false;
                StopPc.IsEnabled = false;
                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;
                InspectorName.IsEnabled = false;
                TestNumber.IsEnabled = false;
                IterationSel.IsEnabled = false;
                DurationSel.IsEnabled = false;
                IterationCount.IsEnabled = false;
                DurationMin.IsEnabled = false;
                DurationSec.IsEnabled = false;

                // Keep Exit button enabled
                Exit.IsEnabled = true;

                UpdateUserStatus("USBMux_NotDetect_Msg2");
                usrInfoMsg = false;
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), this);

                DTCL.Log.Log.Info("DPS MUX hardware disconnected - all controls disabled except Exit, starting auto-detection");
            });

            // Give COM port time to fully release (500ms delay)
            await Task.Delay(500);

            _dpsMuxScanTimer.Elapsed += ScanDPSMuxPorts;
            _dpsMuxScanTimer.Start();
        }

        /// <summary>
        /// Event handler for DPS MUX hardware connection
        /// </summary>
        private void OnDPSMuxHwConnected(object sender, int channelNo)
        {
            DTCL.Log.Log.Debug("DPS MUX hardware port connected");

            // Defer the popup until UI finishes rendering
            Dispatcher.BeginInvoke(new Action(() =>
            {
                var diag = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Detect_Msg"), this);

                if ((diag == CustomMessageBox.MessageBoxResult.Ok) && usrInfoMsg == false)
                {
                    Dispatcher.BeginInvoke(new Action(() =>
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Start_PC_Msg"), this);
                    }), System.Windows.Threading.DispatcherPriority.Background);

                    usrInfoMsg = true;
                }
            }), System.Windows.Threading.DispatcherPriority.Background);

            Dispatcher.Invoke(() =>
            {
                // Enable Scan and Clear buttons now that MUX hardware is detected
                Scan.IsEnabled = true;
                Clear.IsEnabled = true;

                // Keep other controls disabled until scan is performed
                ConfirmLog.IsEnabled = false;
                InitiatePC.IsEnabled = false;
                StopPc.IsEnabled = false;
                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;
                InspectorName.IsEnabled = false;
                TestNumber.IsEnabled = false;
                IterationSel.IsEnabled = false;
                DurationSel.IsEnabled = false;
                IterationCount.IsEnabled = false;
                DurationMin.IsEnabled = false;
                DurationSec.IsEnabled = false;

                UpdateUserStatus("USBMux_Detect_Msg", 15);

                DTCL.Log.Log.Info("DPS MUX hardware connected - Scan and Clear buttons enabled");
            });

            _dpsMuxScanTimer.Elapsed -= ScanDPSMuxPorts;
            _dpsMuxScanTimer.Stop();
        }

        /// <summary>
        /// Scan all 8 channels for DPS hardware (4 slots each)
        /// Clean, maintainable implementation
        /// </summary>
        private async void Scan_Click(object sender, RoutedEventArgs e)
        {
            // Verify MUX hardware is connected
            if (!dpsMuxManager.IsMuxConnected())
            {
                UpdateUserStatus("USBMux_NotDetect_Msg2");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), this);
                return;
            }

            // Disable UI during scan
            Scan.IsEnabled = false;
            Clear.IsEnabled = false;
            DPSMuxChannelGrid.IsEnabled = false;

            try
            {
                // Clear previous scan data
                ClearScanData();

                UpdateUserStatus("USBMux_Scan_Msg");
                DTCL.Log.Log.Info("Starting DPS MUX channel scan");

                // Scan all 8 channels for DPS hardware
                await dpsMuxManager.ScanAllChannelsAsync();

                // Process scan results
                int dpsCount = 0;
                int totalCarts = 0;
                bool hasMultiCart = false;

                for (int chNo = 1; chNo <= 8; chNo++)
                {
                    var channelInfo = dpsMuxManager.channels[chNo];

                    if (channelInfo.isDPSConnected)
                    {
                        dpsCount++;

                        // Count detected carts across all 4 slots
                        int channelCarts = 0;
                        for (int slot = 1; slot <= 4; slot++)
                        {
                            if (channelInfo.IsCartDetected[slot])
                            {
                                channelCarts++;
                                totalCarts++;
                            }
                        }

                        DTCL.Log.Log.Info($"Channel {chNo}: {channelInfo.HardwareType} detected with {channelCarts} cart(s)");

                        // Check for Multi-cart (not supported)
                        if (channelInfo.CartType.Contains("Multi"))
                        {
                            hasMultiCart = true;
                            DTCL.Log.Log.Warning($"Channel {chNo}: Multi-cart detected (not supported)");
                        }
                    }
                    else
                    {
                        DTCL.Log.Log.Info($"Channel {chNo}: No DPS hardware detected");
                    }
                }

                // Update UI with scan results
                RefreshUI();
                UpdateUserStatus("USBMux_Scan_finish_Msg");

                // Handle Multi-cart scenario (not supported)
                if (hasMultiCart)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Multi_cart_Msg"), this);
                    withCart.IsEnabled = false;
                    withOutCart.IsEnabled = false;
                    DPSMuxChannelGrid.IsEnabled = false;
                    DTCL.Log.Log.Warning("Multi-cart detected - disabling cart options");
                    return;
                }

                // Display scan results
                if (dpsCount == 0)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Mux_DtclDetect_Fail"), this);
                    DTCL.Log.Log.Info("Scan completed: No DPS hardware detected");
                    // Enable cart selection options
                    withCart.IsEnabled = false;
                    withOutCart.IsEnabled = false;
                    InspectorName.IsEnabled = false;
                    TestNumber.IsEnabled = false;
                    ConfirmLog.IsEnabled = false;
                    InitiatePC.IsEnabled = false;
                    StopPc.IsEnabled = false;
                    Clear.IsEnabled = true;
                    Scan.IsEnabled = true;
                }
                else
                {
                    var message = $"Scan completed: {dpsCount} DPS unit(s) detected, {totalCarts} cart(s) found";
                    DTCL.Log.Log.Info(message);
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Scan_finish_Msg"), this);

                    // Enable cart selection options
                    withCart.IsEnabled = true;
                    withOutCart.IsEnabled = true;
                    InspectorName.IsEnabled = true;
                    TestNumber.IsEnabled = true;
                    ConfirmLog.IsEnabled = true;
                }
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error during DPS MUX channel scan: {ex.Message}");
                UpdateStatus($"Error during scan: {ex.Message}");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Error_Msg"), this);
            }
            finally
            {
                Scan.IsEnabled = true;
                Clear.IsEnabled = true;
                DPSMuxChannelGrid.IsEnabled = true;

                // Update SelectAllCheckBox state after scan
                UpdateSelectAllCheckBoxState();
            }
        }

        /// <summary>
        /// Clear previous scan data for all channels
        /// Clean implementation - resets all channel state before scanning
        /// </summary>
        private void ClearScanData()
        {
            try
            {
                // Deactivate any currently active channels
                foreach (var manager in dpsMuxManager.channelManagers.Values)
                {
                    manager.DeactivateChannel(clearDiscoveredData: true);
                }

                // Reset all channel information
                foreach (var channel in dpsMuxManager.channels.Values)
                {
                    channel.isDPSConnected = false;
                    channel.HardwareType = "";
                    channel.CartType = "";
                    channel.isUserSelected = false;
                    channel.UnitSno = "999";

                    // Reset all 4 slots
                    for (int slot = 1; slot <= 4; slot++)
                    {
                        channel.DTCSerialNumbers[slot] = "999";
                        channel.IsSlotSelected[slot] = false;
                        channel.IsCartDetected[slot] = false;
                        channel.DetectedCartTypes[slot] = CartType.Unknown;
                        channel.PCStatus[slot] = "N/A";
                    }

                    channel.ClearResults();
                    channel.isInProgress = false;
                }

                DTCL.Log.Log.Debug("Previous scan data cleared");
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error clearing scan data: {ex.Message}");
            }
        }

        /// <summary>
        /// Refresh UI after data changes
        /// Clean implementation - single point for UI updates
        /// </summary>
        private void RefreshUI()
        {
            try
            {
                Dispatcher.Invoke(() =>
                {
                    DPSMuxChannelGrid.Items.Refresh();
                });
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error refreshing UI: {ex.Message}");
            }
        }

        /// <summary>
        /// Clear all channel data and results
        /// </summary>
        private void Clear_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Clear all channel data
                foreach (var channel in dpsMuxManager.channels.Values)
                {
                    channel.isDPSConnected = false;
                    channel.HardwareType = "";
                    channel.CartType = "";
                    channel.isUserSelected = false;
                    channel.UnitSno = "999";

                    for (int slot = 1; slot <= 4; slot++)
                    {
                        channel.DTCSerialNumbers[slot] = "999";
                        channel.IsSlotSelected[slot] = false;
                        channel.IsCartDetected[slot] = false;
                        channel.DetectedCartTypes[slot] = CartType.Unknown;
                        channel.PCStatus[slot] = "";
                    }

                    channel.ClearResults();
                    channel.isInProgress = false;
                }

                // Refresh grid
                DPSMuxChannelGrid.Items.Refresh();

                // Reset progress display
                PCProgressBar.Value = 0;
                TimeElapsed.Text = "0";
                CurrentIteration.Text = "0";
                //PCResultDisplay.Text = "N/A";
                InitiatePC.IsEnabled = false;
                StopPc.IsEnabled = false;
                ConfirmLog.IsEnabled = false;

                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;
                InspectorName.IsEnabled = false;
                TestNumber.IsEnabled = false;
                IterationSel.IsEnabled = false;
                DurationSel.IsEnabled = false;
                IterationCount.IsEnabled = false;
                DurationMin.IsEnabled = false;
                DurationSec.IsEnabled = false;
                TimeElapsed.IsEnabled = false;
                CurrentIteration.IsEnabled = false;
                Scan.IsEnabled = true;

                UpdateUserStatus("Idle_Msg");
                DTCL.Log.Log.Info("DPS MUX channel data cleared");
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error clearing MUX data: {ex.Message}");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Error_Msg"), this);
            }
        }

        /// <summary>
        /// Initiate Performance Check on selected channels and slots
        /// </summary>
        private async void InitiatePC_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Validate MUX connection
                if (!dpsMuxManager.IsMuxConnected())
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), this);
                    return;
                }

                // Get test mode
                bool withCartMode = withCart.IsChecked == true;

                // Validate test configuration
                if (IterationSel.IsChecked == true)
                {
                    if (!int.TryParse(IterationCount.Text, out totalIterations) || totalIterations <= 0)
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Iter"), this);
                        return;
                    }
                }
                else if (DurationSel.IsChecked == true)
                {
                    if (!int.TryParse(DurationMin.Text, out int minutes) || !int.TryParse(DurationSec.Text, out int seconds))
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Invalid_Duration"), this);
                        return;
                    }
                    totalIterations = int.MaxValue; // Use max value for duration mode
                }
                else
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Iter_Log"), this);
                    return;
                }

                // Validate test number and inspector name
                if (string.IsNullOrWhiteSpace(TestNumber.Text))
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg3"), this);
                    return;
                }

                if (string.IsNullOrWhiteSpace(InspectorName.Text))
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg3"), this);
                    return;
                }

                // Get selected channels with slots
                var selectedChannelsWithSlots = GetSelectedChannelsWithSlots();

                if (selectedChannelsWithSlots.Count == 0)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("No_Channel_Selected"), this);
                    return;
                }

                // Validate cart presence if "with cart" mode
                if (withCartMode)
                {
                    bool hasInvalidSelection = false;
                    foreach (var (channelNo, slots) in selectedChannelsWithSlots)
                    {
                        var channel = dpsMuxManager.channels[channelNo];
                        foreach (int slot in slots)
                        {
                            if (!channel.IsCartDetected[slot])
                            {
                                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Insert_Cart_Msg"), this);
                                hasInvalidSelection = true;
                                break;
                            }
                        }
                        if (hasInvalidSelection) break;
                    }
                    if (hasInvalidSelection) return;
                }

                // Update status and show confirmation dialog with detailed information
                UpdateUserStatus("USBMux_Exe_Progress_Msg");

                string confirmMessage = $"Start Performance Check?\n\n" +
                                      $"Mode: {(withCartMode ? "With Cart" : "Without Cart")}\n" +
                                      $"Channels: {selectedChannelsWithSlots.Count}\n" +
                                      $"Total Slots: {selectedChannelsWithSlots.Sum(x => x.slots.Count)}";

                //var result = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Start_Confirm_Msg_Detailed"), this);
                var result = MessageBox.Show(confirmMessage, "Info", MessageBoxButton.YesNo);
                if (result != MessageBoxResult.Yes)
                    return;

                // Start performance check
                await ExecutePerformanceCheck(selectedChannelsWithSlots, withCartMode);
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error initiating performance check: {ex.Message}");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Error_Msg"), this);
                UpdateUIState(isPCRunning: false);
            }
        }

        /// <summary>
        /// Execute Performance Check across selected channels and slots
        /// </summary>
        private async Task ExecutePerformanceCheck(List<(int channelNo, List<int> slots)> selectedChannelsWithSlots, bool withCart)
        {
            pcCancellationTokenSource = new CancellationTokenSource();
            isPCRunning = true;
            UpdateUIState(isPCRunning: true);

            pcStartTime = DateTime.Now;
            currentIterationCount = 0;
            progressTimer.Start();

            // Initialize command buttons to dark gray
            Dispatcher.Invoke(() =>
            {
                Erase.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                Write.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                Read.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                LoopBack.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
            });

            try
            {
                bool isIterationMode = IterationSel.IsChecked == true;
                int durationSeconds = 0;

                if (!isIterationMode)
                {
                    int minutes = int.Parse(DurationMin.Text);
                    int seconds = int.Parse(DurationSec.Text);
                    durationSeconds = (minutes * 60) + seconds;
                }

                // Update log headers with expected iteration/duration information at start
                foreach (var (channelNo, slots) in selectedChannelsWithSlots)
                {
                    var channel = dpsMuxManager.channels[channelNo];
                    foreach (int slot in slots)
                    {
                        var slotInfo = channel.channel_SlotInfo[slot];
                        if (!string.IsNullOrEmpty(slotInfo.SlotPCLogName))
                        {
                            try
                            {
                                // Update iteration and duration in log header
                                if (isIterationMode)
                                {
                                    // Iteration mode: show expected iterations, duration = 0
                                    PCLog.Instance.EditIterationDurationType(totalIterations, 0, slotInfo);
                                }
                                else
                                {
                                    // Duration mode: iteration = 0, show total duration
                                    PCLog.Instance.EditIterationDurationType(0, durationSeconds, slotInfo);
                                }
                                DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slot}: Log header updated");
                            }
                            catch (Exception ex)
                            {
                                DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slot}: Failed to update log header - {ex.Message}");
                            }
                        }
                    }
                }

                bool allPassed = true;

                // Execute PC for each iteration
                while (!pcCancellationTokenSource.Token.IsCancellationRequested)
                {
                    // Check iteration limit or duration
                    if (isIterationMode && currentIterationCount >= totalIterations)
                    {
                        DTCL.Log.Log.Info($"Iteration mode: Reached max iterations ({totalIterations}), exiting loop");
                        break;
                    }

                    if (!isIterationMode)
                    {
                        TimeSpan elapsed = DateTime.Now - pcStartTime;
                        if (elapsed.TotalSeconds >= durationSeconds)
                        {
                            DTCL.Log.Log.Info($"Duration mode: Elapsed {elapsed.TotalSeconds}s >= {durationSeconds}s, exiting loop");
                            break;
                        }
                        DTCL.Log.Log.Info($"Duration mode: Elapsed {elapsed.TotalSeconds}s / {durationSeconds}s, continuing...");
                    }

                    currentIterationCount++;
                    DTCL.Log.Log.Info($"==================== ITERATION {currentIterationCount} START ====================");
                    UpdateStatus($"\nRunning iteration {currentIterationCount}...", "USBMux_Exe_Progress_Msg");
                    //UpdateUserStatus("USBMux_Exe_Progress_Msg");

                    // Execute PC for each selected channel and slot
                    foreach (var (channelNo, slots) in selectedChannelsWithSlots)
                    {
                        if (pcCancellationTokenSource.Token.IsCancellationRequested)
                            break;

                        var channel = dpsMuxManager.channels[channelNo];

                        // Set channel in progress
                        Dispatcher.Invoke(() => channel.isInProgress = true);

                        // Reestablish connection to this channel
                        DTCL.Log.Log.Info($"Channel {channelNo}, Iteration {currentIterationCount}: Calling ReestablishChannelConnection...");
                        bool connected = await dpsMuxManager.ReestablishChannelConnection(channelNo, withCart);
                        if (!connected)
                        {
                            UpdateStatus($"Failed to connect to channel {channelNo}");
                            allPassed = false;
                            Dispatcher.Invoke(() =>
                            {
                                channel.OverallPCStatus = "FAIL";
                                channel.isInProgress = false;
                            });
                            continue;
                        }

                        // Restore log file paths from persistent storage (SlotLogPaths)
                        DTCL.Log.Log.Info($"Channel {channelNo}, Iteration {currentIterationCount}: Restoring log paths from persistent storage");
                        foreach (int slot in slots)
                        {
                            if (channel.SlotLogPaths.ContainsKey(slot))
                            {
                                string logPath = channel.SlotLogPaths[slot];
                                var slotInfo = channel.channel_SlotInfo[slot];
                                if (slotInfo != null)
                                {
                                    slotInfo.SlotPCLogName = logPath;
                                    DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slot}, Iteration {currentIterationCount}: RESTORED log path: {logPath}");
                                }
                                else
                                {
                                    DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slot}, Iteration {currentIterationCount}: SlotInfo is NULL after reconnection, cannot restore!");
                                }
                            }
                            else
                            {
                                DTCL.Log.Log.Warning($"Channel {channelNo}, Slot {slot}, Iteration {currentIterationCount}: No persistent log path found!");
                            }
                        }

                        // Execute PC for each slot in this channel
                        foreach (int slot in slots)
                        {
                            if (pcCancellationTokenSource.Token.IsCancellationRequested)
                                break;

                            UpdateStatus($"\nPortNo {channelNo}, Slot {slot}, Iteration {currentIterationCount}...", "USBMux_Exe_Progress_Msg");

                            bool slotPassed = await ExecuteSlotPerformanceCheck(channelNo, slot, withCart);

                            Dispatcher.Invoke(() =>
                            {
                                channel.PCStatus[slot] = slotPassed ? "PASS" : "FAIL";
                                channel.UpdateOverallPCStatus();
                            });

                            if (!slotPassed)
                                allPassed = false;
                        }

                        // Clear channel in progress
                        Dispatcher.Invoke(() => channel.isInProgress = false);

                        // Switch MUX off between channels
                        await dpsMuxManager.switch_Mux((char)0);
                        await Task.Delay(500);
                    }

                    // Note: Progress bar is updated automatically by progressTimer
                    TimeSpan iterElapsed = DateTime.Now - pcStartTime;
                    DTCL.Log.Log.Info($"==================== ITERATION {currentIterationCount} END (Elapsed: {iterElapsed.TotalSeconds}s) ====================");
                }

                // Update final result
                Dispatcher.Invoke(() =>
                {
                    //PCResultDisplay.Text = allPassed ? "PASS" : "FAIL";
                    DPSMuxChannelGrid.Items.Refresh();
                });

                // Calculate final elapsed time
                TimeSpan totalElapsed = DateTime.Now - pcStartTime;
                int totalElapsedSeconds = (int)totalElapsed.TotalSeconds;

                // Add final iteration/duration line to all log files
                foreach (var (channelNo, slots) in selectedChannelsWithSlots)
                {
                    var channel = dpsMuxManager.channels[channelNo];
                    foreach (int slot in slots)
                    {
                        var slotInfo = channel.channel_SlotInfo[slot];
                        if (!string.IsNullOrEmpty(slotInfo.SlotPCLogName))
                        {
                            try
                            {
                                // Add final iteration/duration summary
                                if (isIterationMode)
                                {
                                    // Iteration mode: log final iteration count
                                    PCLog.Instance.AddIterationDuration(currentIterationCount, 0, slotInfo);
                                }
                                else
                                {
                                    // Duration mode: log total duration
                                    PCLog.Instance.AddIterationDuration(0, totalElapsedSeconds, slotInfo);
                                }
                                DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slot}: Final iteration/duration logged");
                            }
                            catch (Exception ex)
                            {
                                DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slot}: Failed to log final iteration/duration - {ex.Message}");
                            }
                        }
                    }
                }

                // Add final summary to all log files
                string summaryHeader = $"\n----------------------------------------------------------------------------------------------------";
                string summaryText = $"Performance Check Completed";
                summaryText += $"\nTotal Iterations Completed: {currentIterationCount}";
                summaryText += $"\nTotal Duration: {totalElapsedSeconds} seconds";
                summaryText += $"\nOverall Result: {(allPassed ? "PASS" : "FAIL")}";
                summaryText += $"\nCompleted at: {DateTime.Now:dd-MM-yyyy HH:mm:ss}";

                foreach (var (channelNo, slots) in selectedChannelsWithSlots)
                {
                    var channel = dpsMuxManager.channels[channelNo];
                    foreach (int slot in slots)
                    {
                        var slotInfo = channel.channel_SlotInfo[slot];
                        if (!string.IsNullOrEmpty(slotInfo.SlotPCLogName))
                        {
                            try
                            {
                                PCLog.Instance.AddEntry(summaryHeader + "\n" + summaryText, slotInfo);
                                DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slot}: Summary added to log");
                            }
                            catch (Exception ex)
                            {
                                DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slot}: Failed to add summary - {ex.Message}");
                            }
                        }
                    }
                }

                UpdateStatus(pcCancellationTokenSource.Token.IsCancellationRequested
                    ? "Performance check stopped by user"
                    : "Performance check completed successfully");

                if (pcCancellationTokenSource.Token.IsCancellationRequested)
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Stopped_Msg2"), this);
                else
                   CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Completed_Msg"), this);

                DTCL.Log.Log.Info($"DPS MUX Performance check completed: {(allPassed ? "PASS" : "FAIL")}");
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error during performance check: {ex.Message}");
                UpdateStatus($"Error during performance check: {ex.Message}");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Error_Msg"), this);
            }
            finally
            {
                progressTimer.Stop();
                isPCRunning = false;
                UpdateUIState(isPCRunning: false);

                // Reset command buttons to dark gray
                Dispatcher.Invoke(() =>
                {
                    Erase.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                    Write.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                    Read.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                    LoopBack.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                });

                // Switch MUX off
                await dpsMuxManager.switch_Mux((char)0);
            }
        }

        /// <summary>
        /// Execute performance check for a single slot and log results
        /// </summary>
        private async Task<bool> ExecuteSlotPerformanceCheck(int channelNo, int slotNo, bool withCart)
        {
            DateTime slotStartTime = DateTime.Now;

            try
            {
                var channel = dpsMuxManager.channels[channelNo];
                var slotInfo = channel.channel_SlotInfo[slotNo];
                var channelManager = dpsMuxManager.channelManagers[channelNo];

                // Debug: Check SlotInfo validity
                if (slotInfo == null)
                {
                    DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: SlotInfo is null");
                    return false;
                }

                DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: SlotPCLogName = {slotInfo.SlotPCLogName}");

                // Get hardware info and set active slot
                var hwInfo = channelManager.HardwareInfo;
                if (hwInfo == null || !hwInfo.IsConnected)
                {
                    DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: Hardware not connected (hwInfo={hwInfo?.GetType().Name}, IsConnected={hwInfo?.IsConnected})");
                    return false;
                }

                // Set the active slot
                if (!hwInfo.SetActiveSlot(slotNo))
                {
                    DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: Failed to set active slot");
                    return false;
                }

                // Get cart instance for the slot
                ICart cart = hwInfo.CartObj;
                if (cart == null)
                {
                    DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: Failed to get cart instance");
                    return false;
                }

                // Subscribe to command events for button highlighting
                cart.CommandInProgress += OnCommandChanged;

                // Execute performance check
                DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slotNo}: Starting PC iteration {currentIterationCount}");
                PCResult result = await cart.ExecutePC(withCart, slotInfo.DetectedCartTypeAtSlot, (byte)slotNo);

                // Unsubscribe from command events
                cart.CommandInProgress -= OnCommandChanged;

                // Calculate duration for this slot
                TimeSpan slotDuration = DateTime.Now - slotStartTime;
                int durationSeconds = (int)slotDuration.TotalSeconds;

                // Check result
                bool passed = result.loopBackResult == "PASS";
                if (withCart)
                {
                    passed = passed &&
                             result.eraseResult == "PASS" &&
                             result.writeResult == "PASS" &&
                             result.readResult == "PASS";
                }

                // Log results to file
                DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: SlotPCLogName check - IsNull={slotInfo == null}, LogPath={slotInfo?.SlotPCLogName ?? "NULL"}");

                if (!string.IsNullOrEmpty(slotInfo.SlotPCLogName))
                {
                    // Add performance response to log file
                    bool logSuccess = PCLog.Instance.AddPerformanceResponse(
                        withCart,
                        result,
                        currentIterationCount,
                        slotInfo
                    );

                    if (logSuccess)
                    {
                        DTCL.Log.Log.Info($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: Logged iteration - {(passed ? "PASS" : "FAIL")}");
                    }
                    else
                    {
                        DTCL.Log.Log.Warning($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: Failed to log results to file");
                    }
                }
                else
                {
                    DTCL.Log.Log.Warning($"Channel {channelNo}, Slot {slotNo}, Iteration {currentIterationCount}: Log file path not set, cannot log results");
                }

                return passed;
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Channel {channelNo}, Slot {slotNo}: Performance check error: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Stop ongoing performance check
        /// </summary>
        private void StopPC_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                if (isPCRunning && pcCancellationTokenSource != null)
                {
                    pcCancellationTokenSource.Cancel();
                    UpdateStatus("Stopping performance check...");
                    DTCL.Log.Log.Info("DPS MUX Performance check stop requested");
                }
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error stopping performance check: {ex.Message}");
            }
        }

        /// <summary>
        /// Exit and close window
        /// </summary>
        private void Exit_Click(object sender, RoutedEventArgs e)
        {
            // Show confirmation popup
            var shouldContinue = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("Exit_Msg"), this);

            if (shouldContinue == CustomMessageBox.MessageBoxResult.No)
            {
                DTCL.Log.Log.Warning("User chose to cancel exit");
                return;
            }

            // Log "Performance Check Exited" for all selected channels/slots
            foreach (var kvp in dpsMuxManager.channels)
            {
                var channelNo = kvp.Key;
                var channel = kvp.Value;

                if (channel.isUserSelected)
                {
                    // Log exit for each selected slot
                    for (int slot = 1; slot <= 4; slot++)
                    {
                        if (channel.IsSlotSelected[slot] && channel.channel_SlotInfo[slot] != null)
                        {
                            if (!string.IsNullOrEmpty(channel.channel_SlotInfo[slot].SlotPCLogName))
                            {
                                PCLog.Instance.AddEntry("Performance Check Exited", channel.channel_SlotInfo[slot]);
                                DTCL.Log.Log.Info($"Logged exit for Channel {channelNo}, Slot {slot}");
                            }
                        }
                    }
                }
            }

            // Set flag to indicate this is an application exit, not just window close
            _isExitingApplication = true;

            // Ensure complete application shutdown including hidden main window
            DTCL.Log.Log.Info("Shutting down application from DPS MUX window");

            // Stop any ongoing PC
            if (isPCRunning && pcCancellationTokenSource != null)
            {
                pcCancellationTokenSource.Cancel();
            }

            // Stop timer
            progressTimer?.Stop();

            // Clean up DPS MUX resources
            dpsMuxManager?.Dispose();

            // Force complete application shutdown
            Close();
            Application.Current.Shutdown();
            Environment.Exit(0);
        }

        /// <summary>
        /// Window closing event
        /// </summary>
        private void Window_Closed(object sender, EventArgs e)
        {
            try
            {
                // Clean up transport if not already disposed (only if not already disposed by Exit button)
                if (!_isExitingApplication)
                {
                    // Stop any ongoing PC
                    if (isPCRunning && pcCancellationTokenSource != null)
                    {
                        pcCancellationTokenSource.Cancel();
                    }

                    // Stop timer
                    progressTimer?.Stop();

                    // Dispose MUX manager
                    dpsMuxManager?.Dispose();
                }

                // Different behavior based on how the window was closed
                if (_isExitingApplication)
                {
                    // Exit button was clicked - application shutdown is already handled in Exit_Click
                    DTCL.Log.Log.Info("DPS MUX Window closed via Exit button - application shutdown in progress");
                }
                else
                {
                    // X button was clicked - return to main window
                    // Search through all windows to find the MainWindow (it might be hidden)
                    MainWindow mainWindow = null;
                    foreach (Window window in Application.Current.Windows)
                    {
                        if (window is MainWindow mw)
                        {
                            mainWindow = mw;
                            break;
                        }
                    }

                    if (mainWindow != null)
                    {
                        DTCL.Log.Log.Info("DPS MUX window closed via X button - showing main window");
                        mainWindow.Show();
                        mainWindow.WindowState = WindowState.Normal;
                        mainWindow.Activate();

                        // Re-activate hardware event handlers that were disabled when MuxWindow opened
                        mainWindow.ReactivateHardwareEventHandlers();
                    }
                    else
                    {
                        // Fallback: if no main window exists, exit the application
                        DTCL.Log.Log.Warning("No main window found - shutting down application");
                        Application.Current.Shutdown();
                    }
                }
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error during window cleanup: {ex.Message}");
            }
        }

        /// <summary>
        /// Control which grid cells are editable
        /// </summary>
        private void DPSMuxChannelGrid_BeginningEdit(object sender, DataGridBeginningEditEventArgs e)
        {
            try
            {
                var channel = e.Row.Item as DPSMuxChannelInfo;
                if (channel == null)
                {
                    e.Cancel = true;
                    return;
                }

                var columnHeader = e.Column.Header.ToString();

                // Allow editing only for specific columns when DPS is connected
                if (columnHeader == "Unit SNo" ||
                    columnHeader == "DTC1 Sno" || columnHeader == "DTC2 Sno" ||
                    columnHeader == "DTC3 Sno" || columnHeader == "DTC4 Sno")
                {
                    if (!channel.isDPSConnected)
                    {
                        e.Cancel = true;
                    }
                }
                else if (columnHeader == "Select")
                {
                    if (!channel.isDPSConnected)
                    {
                        e.Cancel = true;
                    }
                }
                else if (columnHeader == "S1" || columnHeader == "S2" ||
                         columnHeader == "S3" || columnHeader == "S4")
                {
                    // Slot checkboxes handled by XAML triggers
                    // Cancel edit if not user selected or cart not detected
                    int slotNo = int.Parse(columnHeader.Substring(1));
                    if (!channel.isUserSelected || !channel.IsCartDetected[slotNo])
                    {
                        e.Cancel = true;
                    }
                }
                else
                {
                    // All other columns are read-only
                    e.Cancel = true;
                }

                // Cancel all edits if PC is running
                if (isPCRunning)
                {
                    e.Cancel = true;
                }
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error in BeginningEdit: {ex.Message}");
                e.Cancel = true;
            }
        }

        /// <summary>
        /// Progress timer tick - update elapsed time and progress bar
        /// </summary>
        private void ProgressTimer_Tick(object sender, EventArgs e)
        {
            try
            {
                TimeSpan elapsed = DateTime.Now - pcStartTime;
                TimeElapsed.Text = ((int)elapsed.TotalSeconds).ToString();
                CurrentIteration.Text = currentIterationCount.ToString();

                // Update progress bar based on mode
                bool isIterationMode = IterationSel.IsChecked == true;

                if (isIterationMode && totalIterations > 0)
                {
                    // Iteration mode: show progress based on iterations
                    double progress = (double)currentIterationCount / totalIterations * 100;
                    PCProgressBar.Value = progress;
                }
                else if (!isIterationMode)
                {
                    // Duration mode: show progress based on elapsed time
                    int minutes = int.Parse(DurationMin.Text);
                    int seconds = int.Parse(DurationSec.Text);
                    int totalDurationSeconds = (minutes * 60) + seconds;

                    if (totalDurationSeconds > 0)
                    {
                        double progress = (elapsed.TotalSeconds / totalDurationSeconds) * 100;
                        PCProgressBar.Value = Math.Min(progress, 100); // Cap at 100%
                    }
                }
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error updating progress: {ex.Message}");
            }
        }

        /// <summary>
        /// Get list of selected channels with their selected slots
        /// For "without cart" mode: Returns slot 1 for each selected channel
        /// For "with cart" mode: Returns selected slots for each selected channel
        /// </summary>
        private List<(int channelNo, List<int> slots)> GetSelectedChannelsWithSlots()
        {
            var result = new List<(int channelNo, List<int> slots)>();
            bool isWithoutCartMode = withOutCart.IsChecked == true;

            foreach (var channel in dpsMuxManager.channels.Values)
            {
                if (!channel.isUserSelected)
                    continue;

                var selectedSlots = new List<int>();

                // Without cart mode: Use slot 1 as placeholder (no cart operations)
                if (isWithoutCartMode)
                {
                    selectedSlots.Add(1); // Use slot 1 for hardware testing without cart
                }
                // With cart mode: Use selected slots
                else
                {
                    for (int slot = 1; slot <= 4; slot++)
                    {
                        if (channel.IsSlotSelected[slot])
                        {
                            selectedSlots.Add(slot);
                        }
                    }
                }

                if (selectedSlots.Count > 0)
                {
                    result.Add((channel.Channel, selectedSlots));
                }
            }

            return result;
        }

        /// <summary>
        /// Update UI state based on PC running status
        /// </summary>
        private void UpdateUIState(bool isPCRunning)
        {
            Dispatcher.Invoke(() =>
            {
                Scan.IsEnabled = !isPCRunning;
                Clear.IsEnabled = !isPCRunning;
                InitiatePC.IsEnabled = !isPCRunning;
                StopPc.IsEnabled = isPCRunning;
                DPSMuxChannelGrid.IsEnabled = !isPCRunning;

                // Disable test configuration during PC
                withCart.IsEnabled = !isPCRunning;
                withOutCart.IsEnabled = !isPCRunning;
                TestNumber.IsEnabled = !isPCRunning;
                InspectorName.IsEnabled = !isPCRunning;
                IterationSel.IsEnabled = !isPCRunning;
                IterationCount.IsEnabled = !isPCRunning;
                DurationSel.IsEnabled = !isPCRunning;
                DurationMin.IsEnabled = !isPCRunning;
                DurationSec.IsEnabled = !isPCRunning;
            });
        }

        /// <summary>
        /// Update status text
        /// </summary>
        private void UpdateStatus(string message, string messageId=null)
        {
            Dispatcher.Invoke(() =>
            {
                if(messageId!=null)
                    StatusTextBlock.Text = PopUpMessagesContainerObj.FindStatusMsgById(messageId) + " " + message;
                else
                    StatusTextBlock.Text = message;

                DTCL.Log.Log.Info($"DPS MUX: {message}");
            });
        }

        /// <summary>
        /// Update status using message ID from PopUpMessages.json
        /// </summary>
        private void UpdateUserStatus(string messageId, int fontSize = 17)
        {
            Dispatcher.Invoke(() =>
            {
                StatusTextBlock.FontSize = fontSize;
                StatusTextBlock.Text = PopUpMessagesContainerObj.FindStatusMsgById(messageId);
                DTCL.Log.Log.Info($"DPS MUX: {StatusTextBlock.Text}");
            });
        }

        /// <summary>
        /// Handle command execution events to highlight corresponding command button
        /// </summary>
        private void OnCommandChanged(object sender, CommandEventArgs e)
        {
            if (e.commandName != null)
            {
                Dispatcher.Invoke(() =>
                {
                    // Highlight the active command button with the specified color
                    if (e.commandName == "Erase")
                    {
                        Erase.Background = new System.Windows.Media.SolidColorBrush(e.commandColor);
                        Write.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        Read.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        LoopBack.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                    }
                    else if (e.commandName == "Write")
                    {
                        Write.Background = new System.Windows.Media.SolidColorBrush(e.commandColor);
                        Erase.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        Read.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        LoopBack.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                    }
                    else if (e.commandName == "Read")
                    {
                        Read.Background = new System.Windows.Media.SolidColorBrush(e.commandColor);
                        Erase.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        Write.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        LoopBack.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                    }
                    else if (e.commandName == "LoopBack")
                    {
                        Read.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        Erase.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        Write.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        LoopBack.Background = new System.Windows.Media.SolidColorBrush(e.commandColor);
                    }
                    else
                    {
                        // Reset all to dark gray when no specific command
                        Read.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        Erase.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        Write.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                        LoopBack.Background = new System.Windows.Media.SolidColorBrush(System.Windows.Media.Colors.DarkGray);
                    }
                });
            }
        }

        private void Logo_MouseDown(object sender, System.Windows.Input.MouseButtonEventArgs e)
        {

        }

        /// <summary>
        /// Handle "With Cartridge" radio button click
        /// Shows all command buttons (Erase, Write, Read, LoopBack)
        /// </summary>
        private void withCart_Click(object sender, RoutedEventArgs e)
        {
            // Show all command buttons for "with cart" mode
            CommandsLabel.Visibility = Visibility.Visible;
            Erase.Visibility = Visibility.Visible;
            Write.Visibility = Visibility.Visible;
            Read.Visibility = Visibility.Visible;
            LoopBack.Visibility = Visibility.Visible;

            DTCL.Log.Log.Info("DPS MUX: With Cart mode selected - All command buttons visible");
        }

        /// <summary>
        /// Handle "Without Cartridge" radio button click
        /// Hides Erase, Write, Read buttons, shows only LoopBack
        /// </summary>
        private void withOutCart_Click(object sender, RoutedEventArgs e)
        {
            // Hide Erase, Write, Read buttons for "without cart" mode (only LoopBack visible)
            CommandsLabel.Visibility = Visibility.Visible;
            Erase.Visibility = Visibility.Hidden;
            Write.Visibility = Visibility.Hidden;
            Read.Visibility = Visibility.Hidden;
            LoopBack.Visibility = Visibility.Visible;

            DTCL.Log.Log.Info("DPS MUX: Without Cart mode selected - Only LoopBack button visible");
        }

        /// <summary>
        /// Confirm log settings and validate user inputs
        /// Clean implementation with proper validation flow
        /// </summary>
        private void ConfirmLog_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                // Force DataGrid to commit any pending edits (checkbox changes)
                // DO NOT call Items.Refresh() - it causes slot checkboxes to clear!
                try
                {
                    DPSMuxChannelGrid.CommitEdit(DataGridEditingUnit.Cell, true);
                }
                catch { } // Ignore errors if no edit is in progress

                try
                {
                    DPSMuxChannelGrid.CommitEdit(DataGridEditingUnit.Row, true);
                }
                catch { } // Ignore errors if no edit is in progress

                // Debug: Log current selection state from ObservableCollection (which is bound to DataGrid)
                for (int i = 0; i < channelDataSource.Count; i++)
                {
                    var ch = channelDataSource[i];
                    DTCL.Log.Log.Debug($"Channel {ch.Channel}: isUserSelected = {ch.isUserSelected}, isDPSConnected = {ch.isDPSConnected}");

                    // Also log slot selections
                    if (ch.isUserSelected)
                    {
                        for (int slot = 1; slot <= 4; slot++)
                        {
                            DTCL.Log.Log.Debug($"  Slot {slot}: IsSlotSelected = {ch.IsSlotSelected[slot]}, IsCartDetected = {ch.IsCartDetected[slot]}");
                        }
                    }
                }

                // STEP 0: Check if at least one channel is selected (use ObservableCollection, not dictionary)
                bool anySelected = channelDataSource.Any(c => c.isUserSelected);
                if (!anySelected)
                {
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_No_Log"), this);
                    DTCL.Log.Log.Warning("ConfirmLog: No channels selected");
                    return;
                }

                // STEP 1: Validate basic inputs for all selected channels
                foreach (var channel in channelDataSource)
                {
                    if (!channel.isUserSelected)
                        continue;

                    // Check Inspector Name and Test Number
                    if (string.IsNullOrWhiteSpace(InspectorName.Text) || string.IsNullOrWhiteSpace(TestNumber.Text))
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg3"), this);
                        return;
                    }

                    // Check Unit Serial Number
                    if (string.IsNullOrWhiteSpace(channel.UnitSno))
                    {
                        if (withCart.IsChecked == true)
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg"), this);
                        else
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg2"), this);
                        return;
                    }

                    // Check DTC Serial Numbers if "With Cartridge" is selected
                    if (withCart.IsChecked == true)
                    {
                        bool hasSelectedSlot = false;
                        bool hasEmptySerial = false;

                        for (int slot = 1; slot <= 4; slot++)
                        {
                            if (channel.IsSlotSelected[slot])
                            {
                                hasSelectedSlot = true;
                                if (string.IsNullOrWhiteSpace(channel.DTCSerialNumbers[slot]))
                                {
                                    hasEmptySerial = true;
                                    break;
                                }
                            }
                        }

                        if (hasSelectedSlot && hasEmptySerial)
                        {
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg"), this);
                            return;
                        }
                    }
                }

                // STEP 2: Warn about default "999" values
                bool hasDefaultValues = false;
                foreach (var channel in channelDataSource)
                {
                    if (channel.UnitSno == "999" || InspectorName.Text == "999" || TestNumber.Text == "999")
                    {
                        hasDefaultValues = true;
                        break;
                    }

                    if (withCart.IsChecked == true)
                    {
                        for (int slot = 1; slot <= 4; slot++)
                        {
                            if (channel.IsSlotSelected[slot] && channel.DTCSerialNumbers[slot] == "999")
                            {
                                hasDefaultValues = true;
                                break;
                            }
                        }
                    }

                    if (hasDefaultValues)
                        break;
                }

                if (hasDefaultValues)
                {
                    var result = CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Default_Serial_Msg"), this);
                    if (result != CustomMessageBox.MessageBoxResult.Yes)
                    {
                        CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Serial_Msg4"), this);
                        return;
                    }
                }

                // STEP 3: Validate cart presence matches selected mode
                for (int chNo = 1; chNo <= 8; chNo++)
                {
                    var channel = dpsMuxManager.channels[chNo];

                    if (!channel.isUserSelected)
                        continue;

                    if (withCart.IsChecked == true)
                    {
                        // "With Cart" mode: Check if at least one selected slot has a cart
                        bool hasCartInSelectedSlot = false;
                        for (int slot = 1; slot <= 4; slot++)
                        {
                            if (channel.IsSlotSelected[slot] && channel.IsCartDetected[slot])
                            {
                                hasCartInSelectedSlot = true;
                                break;
                            }
                        }

                        if (!hasCartInSelectedSlot)
                        {
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Insert_Cart_Msg"), this);
                            DTCL.Log.Log.Warning($"Channel {chNo}: 'With Cart' selected but no carts detected in selected slots");
                            return;
                        }
                    }
                    else
                    {
                        // "Without Cart" mode: Check if ANY cart is detected in SELECTED slots only
                        bool hasSelectedSlot = false;
                        bool hasCartInSelectedSlot = false;

                        for (int slot = 1; slot <= 4; slot++)
                        {
                            if (channel.IsSlotSelected[slot])
                            {
                                hasSelectedSlot = true;
                                if (channel.IsCartDetected[slot])
                                {
                                    hasCartInSelectedSlot = true;
                                    break;
                                }
                            }
                        }

                        // Only validate if at least one slot is selected
                        if (hasSelectedSlot && hasCartInSelectedSlot)
                        {
                            CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("PC_Remove_Cart_Msg"), this);
                            DTCL.Log.Log.Warning($"Channel {chNo}: 'Without Cart' selected but carts are detected in selected slots - please remove carts");
                            return;
                        }
                    }
                }

                // STEP 4: Create log files for selected channels
                PCLog.Instance.LogType = "New";
                PCLog.Instance.LogFileNameList.Clear();

                bool logsCreated = false;
                int logCount = 0;

                for (int chNo = 1; chNo <= 8; chNo++)
                {
                    var channel = dpsMuxManager.channels[chNo];

                    if (!channel.isUserSelected)
                        continue;

                    // For "With Cartridge" mode - create log for each selected slot
                    if (withCart.IsChecked == true)
                    {
                        for (int slot = 1; slot <= 4; slot++)
                        {
                            if (channel.IsSlotSelected[slot] && channel.IsCartDetected[slot])
                            {
                                PCLog.Instance.CreateNewLog(
                                    TestNumber.Text,
                                    InspectorName.Text,
                                    channel.DTCSerialNumbers[slot],
                                    channel.UnitSno,
                                    true, // withCart
                                    channel.channel_SlotInfo[slot],
                                    chNo,
                                    isDPSMux: true  // DPS MUX mode - creates DPSMux/Channel-X/Slot-Y/ structure
                                );

                                // Store log path in persistent dictionary
                                channel.SlotLogPaths[slot] = channel.channel_SlotInfo[slot].SlotPCLogName;

                                logsCreated = true;
                                logCount++;
                                DTCL.Log.Log.Info($"DPS MUX Log created: DPSMux/Channel-{chNo}/Slot-{slot}/ - Path: {channel.SlotLogPaths[slot]}");
                            }
                        }
                    }
                    // For "Without Cartridge" mode - create one log per channel (use slot 1)
                    else
                    {
                        var dummySlot = channel.channel_SlotInfo[1]; // Use slot 1 as placeholder
                        PCLog.Instance.CreateNewLog(
                            TestNumber.Text,
                            InspectorName.Text,
                            "",
                            channel.UnitSno,
                            false, // withoutCart
                            dummySlot,
                            chNo,
                            isDPSMux: true  // DPS MUX mode - creates DPSMux/Channel-X/Slot-1/ structure
                        );

                        // Store log path in persistent dictionary
                        channel.SlotLogPaths[1] = dummySlot.SlotPCLogName;

                        logsCreated = true;
                        logCount++;
                        DTCL.Log.Log.Info($"DPS MUX Log created: DPSMux/Channel-{chNo}/Slot-1/ (without cart)");
                    }
                }

                // STEP 5: Validate at least one log was created
                if (!logsCreated)
                {
                    InitiatePC.IsEnabled = false;
                    CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_No_Log"), this);
                    return;
                }

                // STEP 6: Success - show confirmation and update UI state
                DTCL.Log.Log.Info($"Log confirmation successful: {logCount} log(s) created");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_New_Log2"), this);

                // Update UI state - clean approach using IsEnabled
                isLogConfirmed = true;
                ConfirmLog.IsEnabled = false;
                Scan.IsEnabled = false;
                InitiatePC.IsEnabled = true;
                IterationSel.IsEnabled = true;
                DurationSel.IsEnabled = true;
                IterationCount.IsEnabled = true;
                DurationMin.IsEnabled = true;
                DurationSec.IsEnabled = true;
                withCart.IsEnabled = false;
                withOutCart.IsEnabled = false;
                DPSMuxChannelGrid.IsEnabled = false;
                InspectorName.IsEnabled = false;
                TestNumber.IsEnabled = false;

                // Show iteration message
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_Enter_Iter_Log"), this);
                UpdateUserStatus("USBMux_Enter_Iter_Log");
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error confirming log: {ex.Message}");
                //CustomMessageBox.Show($"Error: {ex.Message}", "Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void IterationSel_Click_1(object sender, RoutedEventArgs e)
        {

        }

        private void IterationCount_TextChanged(object sender, TextChangedEventArgs e)
        {

        }

        private void DurationSel_Click_1(object sender, RoutedEventArgs e)
        {

        }

        /// <summary>
        /// Handle individual channel property changes to update SelectAllCheckBox state
        /// </summary>
        private void Channel_PropertyChanged(object sender, PropertyChangedEventArgs e)
        {
            // Only respond to isUserSelected property changes
            if (e.PropertyName != nameof(DPSMuxChannelInfo.isUserSelected))
                return;

            // Don't update if we're in the middle of a bulk update
            if (_isUpdatingSelectAllState)
                return;

            try
            {
                // Update SelectAllCheckBox state based on current selection
                UpdateSelectAllCheckBoxState();
            }
            catch (Exception ex)
            {
                DTCL.Log.Log.Error($"Error in Channel_PropertyChanged: {ex.Message}");
            }
        }

        /// <summary>
        /// Update SelectAllCheckBox state based on current channel selections
        /// </summary>
        private void UpdateSelectAllCheckBoxState()
        {
            try
            {
                // Prevent recursion
                if (_isUpdatingSelectAllState)
                    return;

                _isUpdatingSelectAllState = true;

                // Check if all connected channels are selected
                var connectedChannels = channelDataSource.Where(c => c.isDPSConnected).ToList();

                if (connectedChannels.Count == 0)
                {
                    // No connected channels - uncheck SelectAll
                    SelectAllCheckBox.IsChecked = false;
                    _isUpdatingSelectAllState = false;
                    return;
                }

                bool allSelected = connectedChannels.All(c => c.isUserSelected);

                // Two-state checkbox: checked only if ALL connected channels are selected
                SelectAllCheckBox.IsChecked = allSelected;

                _isUpdatingSelectAllState = false;
            }
            catch (Exception ex)
            {
                _isUpdatingSelectAllState = false;
                DTCL.Log.Log.Error($"Error updating SelectAllCheckBox state: {ex.Message}");
            }
        }

        private void SelectAllCheckBox_Checked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ignore if this is being set programmatically during state update
                if (_isUpdatingSelectAllState)
                    return;

                DTCL.Log.Log.Info("SelectAll checkbox checked - selecting all channels with DPS detected");

                // Temporarily disable state updates to prevent recursion
                _isUpdatingSelectAllState = true;

                // Select all channels where DPS is detected
                foreach (var channel in channelDataSource)
                {
                    if (channel.isDPSConnected)
                    {
                        channel.isUserSelected = true;
                    }
                }

                _isUpdatingSelectAllState = false;

                // Force DataGrid to refresh
                DPSMuxChannelGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                _isUpdatingSelectAllState = false;
                DTCL.Log.Log.Error($"Error in SelectAllCheckBox_Checked: {ex.Message}");
            }
        }

        private void SelectAllCheckBox_Unchecked(object sender, RoutedEventArgs e)
        {
            try
            {
                // Ignore if this is being set programmatically during state update
                if (_isUpdatingSelectAllState)
                    return;

                DTCL.Log.Log.Info("SelectAll checkbox unchecked - deselecting all channels");

                // Temporarily disable state updates to prevent recursion
                _isUpdatingSelectAllState = true;

                // Deselect all channels where DPS is detected
                foreach (var channel in channelDataSource)
                {
                    if (channel.isDPSConnected)
                    {
                        channel.isUserSelected = false;
                    }
                }

                _isUpdatingSelectAllState = false;

                // Force DataGrid to refresh
                DPSMuxChannelGrid.Items.Refresh();
            }
            catch (Exception ex)
            {
                _isUpdatingSelectAllState = false;
                DTCL.Log.Log.Error($"Error in SelectAllCheckBox_Unchecked: {ex.Message}");
            }
        }

        private void IterationSel_Click(object sender, RoutedEventArgs e)
        {

        }

        private void DurationSel_Click(object sender, RoutedEventArgs e)
        {

        }
    }
}
