using DTCL.Cartridges;
using DTCL.JsonParser;
using DTCL.Log;
using DTCL.Transport;
using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO.Ports;
using System.Runtime.CompilerServices;
using System.Threading.Tasks;
using System.Windows.Media;

namespace DTCL.Mux
{
    public class MuxChannelInfo : INotifyPropertyChanged
    {
        public int Channel { get; set; }
        public SlotInfo[] channel_SlotInfo { get; set; }
        public int cartNo { get; set; }
        public bool isDTCLConnected { get; set; }

        string _pcStatus = "";
        public string PCStatus
        {
            get => _pcStatus;
            set
            {
                if (_pcStatus != value)
                {
                    _pcStatus = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool _isUserSelected { get; set; }

        public bool isUserSelected
        {
            get => _isUserSelected;
            set
            {
                if (_isUserSelected != value)
                {
                    _isUserSelected = value;
                    OnPropertyChanged();
                    // Notify the window to update Select All checkbox state
                    UserSelectionChanged?.Invoke();
                }
            }
        }

        public static event Action UserSelectionChanged;

        public event PropertyChangedEventHandler PropertyChanged;

        protected virtual void OnPropertyChanged([CallerMemberName] string propertyName = null)
        {
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
        }

        public string CartType { get; set; }
        public string UnitSno { get; set; }
        public string DtcSno { get; set; }

        bool _isInProgress;
        public bool isInProgress
        {
            get => _isInProgress;
            set
            {
                if (_isInProgress != value)
                {
                    _isInProgress = value;
                    OnPropertyChanged();
                }
            }
        }

        public bool isPCActive { get; set; } // True when this channel is part of active PC session
        public string PCLogFileName { get; set; }
    }

    public class MuxManager
    {
        public MuxWindow _mainWindow;
        public Dictionary<int, MuxChannelInfo> channels { get; private set; }
        public Dictionary<int, MuxChannelManager> channelManagers { get; private set; }
        public bool isMuxHwConnected;
        public UartTransportSync _muxTransport;
        bool stopPcFlag;
        public bool IstRun;
        int _activeChannelNumber;

        public event EventHandler PortConnected;
        public event EventHandler PortDisconnected;

        // Load popup messages
        JsonParser<PopUpMessagesContainer> PopUpMessagesParserObj;
        PopUpMessagesContainer PopUpMessagesContainerObj;

        public MuxManager(MuxWindow mainWindow)
        {
            _mainWindow = mainWindow;
            InitializeChannels();
            InitializeChannelManagers();
            PopUpMessagesParserObj = new JsonParser<PopUpMessagesContainer>();
            PopUpMessagesContainerObj = PopUpMessagesParserObj.Deserialize("PopUpMessage\\PopUpMessages.json");
        }

        void InitializeChannels()
        {
            channels = new Dictionary<int, MuxChannelInfo>();

            for (int ch = 1; ch <= 8; ch++)
            {
                channels[ch] = new MuxChannelInfo
                {
                    Channel = ch,
                    isDTCLConnected = false,
                    CartType = "",
                    PCStatus = "",
                    isUserSelected = false,
                    channel_SlotInfo = new SlotInfo[5], // [0] unused, [1-4] for slots
                    cartNo = 0,
                    UnitSno = "", // Will be set to 999 when DTCL is detected
                    DtcSno = "", // Will be set to 999 when DTCL is detected
                    isInProgress = false,
                    PCLogFileName = ""
                };
            }
        }

        void InitializeChannelManagers()
        {
            channelManagers = new Dictionary<int, MuxChannelManager>();

            for (int ch = 1; ch <= 8; ch++)
            {
                var channelManager = new MuxChannelManager(ch, channels[ch]);

                // Wire up events
                channelManager.HardwareDetected += OnChannelHardwareDetected;
                channelManager.HardwareDisconnected += OnChannelHardwareDisconnected;
                channelManager.CartDetected += OnChannelCartDetected;

                channelManagers[ch] = channelManager;
            }
        }

        public async Task ScanMuxHw()
        {
            if (isMuxHwConnected) return;

            var availablePorts = SerialPort.GetPortNames();

            foreach (string port in availablePorts)
            {
                try
                {
                    _muxTransport = new UartTransportSync(port, 9600);

                    var res = _muxTransport.Connect();

                    if (!res)
                    {
                        // _muxTransport.Disconnect();
                        // MessageBox.Show("USB Mux Port is corrupted, remove and insert again" + port);
                        continue;
                    }

                    var Txbuff = new byte[1];
                    Txbuff[0] = (byte)'0';
                    _muxTransport.Send(Txbuff, 0, Txbuff.Length);

                    var response = _muxTransport.WaitForResponse(4, 1000);

                    if ((response != null) && ((response[3] == 48) || (response[1] == 'M')))
                    {
                        _muxTransport.PortOpened += OnPortConnected;
                        _muxTransport.PortClosed += OnPortClosed;
                        isMuxHwConnected = true;

                        System.Windows.Application.Current.Dispatcher
                            .Invoke(() =>
                        {
                            PortConnected?.Invoke(this, EventArgs.Empty);
                        });

                        break;
                    }
                    else
                    {
                        // Not a DTCL, disconnect
                        _muxTransport.Disconnect();
                        _mainWindow.UpdateUserStatus("USBMux_NotDetect_Msg");
                    }
                }
                catch (Exception ex)
                {
                    Log.Log.Error($"Error connecting to port {port}: {ex.Message}");
                }
            }
        }

        public void OnPortClosed(object sender, EventArgs e)
        {
            Log.Log.Info($"Mux Hw Disconnected");
            isMuxHwConnected = false;

            System.Windows.Application.Current.Dispatcher
                .Invoke(() =>
            {
                PortDisconnected?.Invoke(this, EventArgs.Empty);
            });
        }

        void OnPortConnected(object sender, EventArgs e)
        {
            Log.Log.Info($"Mux Hw Connected");
            isMuxHwConnected = true;

            System.Windows.Application.Current.Dispatcher
                .Invoke(() =>
            {
                PortConnected?.Invoke(this, EventArgs.Empty);
            });
        }

        public bool switch_Mux(char number, bool preserveChannelData = false)
        {
            var Txbuff = new byte[1];
            var Rxbuff = new byte[4];
            byte temp = 0x30;
            Txbuff[0] = (byte)((byte)(number) + temp);

            if (isMuxHwConnected == true)
            {
                // Deactivate current channel before switching
                if (_activeChannelNumber > 0 && _activeChannelNumber != (int)number)
                {
                    var currentChannelManager = channelManagers[_activeChannelNumber];
                    currentChannelManager?.DeactivateChannel(clearDiscoveredData: !preserveChannelData);
                }

                _muxTransport.Send(Txbuff, 0, 1);
                Rxbuff = _muxTransport.WaitForResponse(4, 500);

                if ((Rxbuff != null) && (Rxbuff[3] == (byte)((byte)(number) + temp)) && (Rxbuff[1] == 65))
                {
                    // Update active channel tracking
                    var newChannelNumber = (int)number;
                    _activeChannelNumber = newChannelNumber;

                    Log.Log.Info($"Mux switched to Channel {number}");
                    return true;
                }
                else if ((Rxbuff != null) && (Rxbuff[3] == (byte)((byte)(number) + temp)) && (Rxbuff[1] == 77))
                {
                    // Update active channel tracking
                    var newChannelNumber = (int)number;
                    _activeChannelNumber = newChannelNumber;

                    Log.Log.Info($"Mux in manual mode switched to Channel {number}");
                    return true;
                }
                else
                {
                    Log.Log.Error($"Failed to switch Mux to Channel {number}");
                    return false;
                }
            }
            else
            {
                Log.Log.Error($"Mux Hardware not connected, Failed to switch to Channel {number}");
                return false;
            }
        }

        public async Task ScanAllChannelsAsync()
        {
            if (isMuxHwConnected)
            {
                _mainWindow.UpdateUserStatus("Scan_DTCL_Msg");
                _mainWindow.MuxChannelGrid.UnselectAll();

                for (int ChNo = 1; ChNo <= 8; ChNo++)
                {
                    var channelInfo = channels[ChNo];
                    var channelManager = channelManagers[ChNo];

                    if (switch_Mux((char)ChNo, preserveChannelData: true))
                    {
                        Log.Log.Info($"Switched to Mux Channel {ChNo}");
                        _activeChannelNumber = ChNo;

                        channelInfo.isInProgress = true;
                        _mainWindow.MuxChannelGrid.Items.Refresh();

                        await Task.Delay(2000); // Allow stabilization after channel switch

                        // Activate channel and scan for hardware/carts
                        var success = await channelManager.ActivateChannelAsync();

                        if (!success)
                        {
                            Log.Log.Info($"Retry: Switch off Mux");
                            switch_Mux((char)0, preserveChannelData: true);
                            await Task.Delay(200);

                            Log.Log.Info($"Retry: Switch on Mux channel {ChNo}");
                            switch_Mux((char)ChNo, preserveChannelData: true);
                            await Task.Delay(500);

                            Log.Log.Info($"Retry: Activate channel {ChNo}");
                            success = await channelManager.ActivateChannelAsync();

                            // Update UI immediately after retry if successful
                            if (success)
                            {
                                System.Windows.Application.Current.Dispatcher
                                    .Invoke(() =>
                                {
                                    _mainWindow.MuxChannelGrid.Items.Refresh();
                                });

                                Log.Log.Info($"Channel {ChNo} retry successful - UI updated");
                            }
                        }

                        if (success)
                        {
                            Log.Log
                                .Info($"Channel {ChNo} activated successfully - DTCL: {channelInfo.isDTCLConnected}, Cart: {channelInfo.CartType}");

                            // Update UI immediately with hardware and cart information
                            System.Windows.Application.Current.Dispatcher
                                .Invoke(() =>
                            {
                                // Refresh the specific channel data in the grid
                                _mainWindow.MuxChannelGrid.Items.Refresh();

                                // Force UI to update by triggering property change notifications if needed
                                _mainWindow.StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                            });

                            Log.Log
                                .Info($"Channel {ChNo} UI updated - DtcSno: {channelInfo.DtcSno}, UnitSno: {channelInfo.UnitSno}, Cart: {channelInfo.CartType}");
                        }
                        else
                        {
                            Log.Log.Info($"Failed to activate channel {ChNo}");
                        }

                        channelInfo.isInProgress = false;

                        // Final UI refresh to show progress completion
                        System.Windows.Application.Current.Dispatcher
                            .Invoke(() =>
                        {
                            _mainWindow.MuxChannelGrid.Items.Refresh();
                        });

                        // Small delay to let user see the results before moving to next channel
                        await Task.Delay(500);

                        // Deactivate channel but PRESERVE discovered data for GUI display
                        channelManager.DeactivateChannel(clearDiscoveredData: false);
                    }
                    else
                    {
                        Log.Log.Error($"Failed to switch to Mux Channel {ChNo}");
                    }

                    switch_Mux((char)0, preserveChannelData: true); // Switch off but preserve data
                    _activeChannelNumber = 0;
                    await Task.Delay(700); // Slight delay after each iteration
                }
            }
            else
            {
                _mainWindow.UpdateUserStatus("USBMux_NotDetect_Msg2");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), _mainWindow);
            }

            _mainWindow.UpdateUserStatus("Idle_Msg");
        }

        public async Task ScanChannelAsync(byte ChNo)
        {
            if (isMuxHwConnected)
            {
                _mainWindow.UpdateUserStatus("Scan_DTCL_Msg");
                _mainWindow.MuxChannelGrid.UnselectAll();

                var channelInfo = channels[ChNo];
                var channelManager = channelManagers[ChNo];

                if (switch_Mux((char)ChNo, preserveChannelData: true))
                {
                    Log.Log.Info($"Switched to Mux Channel {ChNo}");
                    _activeChannelNumber = ChNo;

                    channelInfo.isInProgress = true;
                    _mainWindow.MuxChannelGrid.Items.Refresh();

                    await Task.Delay(2000); // Allow stabilization after channel switch

                    // Activate channel and scan for hardware/carts
                    var success = await channelManager.ActivateChannelAsync();

                    if (!success)
                    {
                        Log.Log.Info($"Retry: Switch off Mux");
                        switch_Mux((char)0, preserveChannelData: true);
                        await Task.Delay(200);

                        Log.Log.Info($"Retry: Switch on Mux channel {ChNo}");
                        switch_Mux((char)ChNo, preserveChannelData: true);
                        await Task.Delay(500);

                        Log.Log.Info($"Retry: Activate channel {ChNo}");
                        success = await channelManager.ActivateChannelAsync();

                        // Update UI immediately after retry if successful
                        if (success)
                        {
                            System.Windows.Application.Current.Dispatcher
                                .Invoke(() =>
                            {
                                _mainWindow.MuxChannelGrid.Items.Refresh();
                            });

                            Log.Log.Info($"Channel {ChNo} retry successful - UI updated");
                        }
                    }

                    if (success)
                    {
                        Log.Log
                            .Info($"Channel {ChNo} activated successfully - DTCL: {channelInfo.isDTCLConnected}, Cart: {channelInfo.CartType}");

                        // Update UI immediately with hardware and cart information
                        System.Windows.Application.Current.Dispatcher
                            .Invoke(() =>
                        {
                            _mainWindow.MuxChannelGrid.Items.Refresh();
                            _mainWindow.StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);
                        });

                        Log.Log
                            .Info($"Channel {ChNo} UI updated - DtcSno: {channelInfo.DtcSno}, UnitSno: {channelInfo.UnitSno}, Cart: {channelInfo.CartType}");
                    }
                    else
                    {
                        Log.Log.Info($"Failed to activate channel {ChNo}");
                    }

                    switch_Mux((char)0, preserveChannelData: true); // Switch off but preserve data
                    channelInfo.isInProgress = false;

                    // Final UI refresh to show progress completion
                    System.Windows.Application.Current.Dispatcher
                        .Invoke(() =>
                    {
                        _mainWindow.MuxChannelGrid.Items.Refresh();
                    });

                    // Don't deactivate - keep channel active for subsequent operations
                }
                else
                {
                    Log.Log.Error($"Failed to switch to Mux Channel {ChNo}");
                }
            }
            else
            {
                _mainWindow.UpdateUserStatus("USBMux_NotDetect_Msg2");
                CustomMessageBox.Show(PopUpMessagesContainerObj.FindMessageById("USBMux_NotDetect_Msg2"), _mainWindow);
            }

            _mainWindow.UpdateUserStatus("Idle_Msg");
        }

        public async Task<(bool isDTCLDetected, bool isCartDetected)> ScanChannelNo(int ChNo)
        {
            if (!isMuxHwConnected)
                return (false, false);

            var channelInfo = channels[ChNo];
            var channelManager = channelManagers[ChNo];

            if (!switch_Mux((char)ChNo, preserveChannelData: true))
            {
                channelInfo.isDTCLConnected = false;
                Log.Log.Error($"Failed to switch to Mux Channel {ChNo}");
                return (false, false);
            }

            _activeChannelNumber = ChNo;
            await Task.Delay(1000); // Allow stabilization after channel switch

            // Activate channel and scan for hardware/carts
            var success = await channelManager.ActivateChannelAsync();

            // Update UI immediately if successful
            if (success)
            {
                System.Windows.Application.Current.Dispatcher
                    .Invoke(() =>
                {
                    _mainWindow.MuxChannelGrid.Items.Refresh();
                });

                Log.Log.Info($"Channel {ChNo} scan completed - UI updated immediately");
            }

            var isDTCLDetected = channelInfo.isDTCLConnected;
            var isCartDetected = !string.IsNullOrEmpty(channelInfo.CartType) && channelInfo.cartNo > 0;

            return (isDTCLDetected, isCartDetected);
        }

        public async void UpdatePCProgress(int iterationCount, int PCDurationTime, int elapsedTime, int counter, PCResult result, int ChNo)
        {
            var channelInfo = channels[ChNo];
            // Set the result display
            if (result.eraseResult.Equals("PASS") && result.writeResult.Equals("PASS") && result.readResult.Equals("PASS") && result.loopBackResult.Equals("PASS"))
            {
                channelInfo.PCStatus = "PASS";
            }
            else
            {
                channelInfo.PCStatus = "FAIL";
            }

            // Update the progress bar
            if (_mainWindow.IterationSel.IsChecked == true)
            {
                _mainWindow.PCProgressBar.Value = _mainWindow.PCProgressBar.Maximum - iterationCount;
                _mainWindow.TimeElapsed.Text = $"{(int)(elapsedTime)}";
            }
            else
            {
                _mainWindow.PCProgressBar.Value = _mainWindow.PCProgressBar.Maximum - PCDurationTime;
                _mainWindow.TimeElapsed.Text = $"{(int)(_mainWindow.PCProgressBar.Maximum - PCDurationTime)}";
            }

            // Update both iteration and elapsed time
            _mainWindow.CurrentIteration.Text = counter.ToString();

            // Refresh the UI
            _mainWindow.StatusTextBlock.Dispatcher.Invoke(() => { }, System.Windows.Threading.DispatcherPriority.Render);

            await Task.Delay(1);
        }

        /// <summary>
        /// Re-establishes DTCL connection and cart detection for a specific channel
        /// This is critical after switching MUX channels as the previous connection becomes invalid
        /// </summary>
        /// <param name="channelNo">Channel number to re-establish connection for</param>
        /// <returns>True if DTCL and cart are detected, false otherwise</returns>
        public async Task<bool> ReestablishChannelConnection(int channelNo, bool withCart)
        {
            var channelInfo = channels[channelNo];
            var channelManager = channelManagers[channelNo];

            try
            {
                Log.Log.Info($"Re-establishing connection for channel {channelNo}");

                // Ensure channel is switched to (preserve data during re-establishment)
                if (!switch_Mux((char)channelNo, preserveChannelData: true))
                {
                    Log.Log.Error($"Failed to switch to channel {channelNo}");
                    return false;
                }

                _activeChannelNumber = channelNo;
                await Task.Delay(1000); // Allow stabilization

                // Activate channel with hardware and cart detection
                var success = await channelManager.ActivateChannelAsync();

                if (!success)
                {
                    Log.Log.Info($"Retry: Switch off Mux");
                    switch_Mux((char)0, preserveChannelData: true);
                    await Task.Delay(500);

                    Log.Log.Info($"Retry: Switch on Mux channel {channelNo}");
                    switch_Mux((char)channelNo, preserveChannelData: true);
                    await Task.Delay(500);

                    Log.Log.Info($"Retry: Activate channel {channelNo}");
                    success = await channelManager.ActivateChannelAsync();
                }

                if (success)
                {
                    var isDTCLConnected = channelInfo.isDTCLConnected;
                    var isCartDetected = !string.IsNullOrEmpty(channelInfo.CartType) && channelInfo.cartNo > 0;

                    if (withCart && !isCartDetected)
                    {
                        Log.Log.Warning($"No cart detected on channel {channelNo} during re-establishment");
                        return false;
                    }

                    Log.Log
                        .Info($"Successfully re-established connection for channel {channelNo} - DTCL: {isDTCLConnected}, Cart: {channelInfo.CartType}");

                    return true;
                }
                else
                {
                    Log.Log.Warning($"Failed to re-establish connection for channel {channelNo}");
                    return false;
                }
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error re-establishing connection for channel {channelNo}: {ex.Message}");
                return false;
            }
        }

        public void OnCommandChanged(object sender, CommandEventArgs e)
        {
            if (e.commandName != null)
            {
                // Check button names using if-else
                if (e.commandName == "Erase")
                {
                    _mainWindow.Erase.Background = new SolidColorBrush(e.commandColor);
                    _mainWindow.Write.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.Read.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "Write")
                {
                    _mainWindow.Write.Background = new SolidColorBrush(e.commandColor);
                    _mainWindow.Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.Read.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "Read")
                {
                    _mainWindow.Read.Background = new SolidColorBrush(e.commandColor);
                    _mainWindow.Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.Write.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
                else if (e.commandName == "LoopBack")
                {
                    _mainWindow.Read.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.Write.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.LoopBack.Background = new SolidColorBrush(e.commandColor);
                }
                else
                {
                    _mainWindow.Read.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.Erase.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.Write.Background = new SolidColorBrush(Colors.DarkGray);
                    _mainWindow.LoopBack.Background = new SolidColorBrush(Colors.DarkGray);
                }
            }
        }

        #region Channel Event Handlers
        void OnChannelHardwareDetected(object sender, HardwareDetectionEventArgs e)
        {
            Log.Log.Info($"Channel hardware detected: {e.Message}");
        }

        void OnChannelHardwareDisconnected(object sender, HardwareDetectionEventArgs e)
        {
            Log.Log.Warning($"Channel hardware disconnected: {e.Message}");

            // Update UI to reflect disconnection
            System.Windows.Application.Current.Dispatcher
                .Invoke(() =>
            {
                _mainWindow.MuxChannelGrid.Items.Refresh();
            });
        }

        void OnChannelCartDetected(object sender, CartDetectionEventArgs e)
        {
            Log.Log.Info($"Channel cart detection: {e.Message}");

            // Update UI to reflect cart changes
            System.Windows.Application.Current.Dispatcher
                .Invoke(() =>
            {
                _mainWindow.MuxChannelGrid.Items.Refresh();
            });
        }

        #endregion

        #region Helper Methods
        /// <summary>
        /// Get the channel manager for a specific channel
        /// </summary>
        public MuxChannelManager GetChannelManager(int channelNumber)
        {
            return channelManagers.TryGetValue(channelNumber, out var manager) ? manager : null;
        }

        /// <summary>
        /// Clear all channel data (for Clear button or fresh start)
        /// </summary>
        public void ClearAllChannelData()
        {
            Log.Log.Info("Clearing all channel data");

            for (int ch = 1; ch <= 8; ch++)
            {
                var channelManager = channelManagers[ch];

                if (channelManager != null)
                {
                    // Force clear the data
                    channelManager.DeactivateChannel(clearDiscoveredData: true);
                }
            }

            // Refresh UI
            System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
            {
                _mainWindow.MuxChannelGrid.Items.Refresh();
            });
        }

        /// <summary>
        /// Get the currently active channel number
        /// </summary>
        public int GetActiveChannelNumber() => _activeChannelNumber;

        /// <summary>
        /// Deactivate current channel and switch off MUX
        /// </summary>
        public void DeactivateCurrentChannel()
        {
            if (_activeChannelNumber > 0 && _activeChannelNumber <= 8)
            {
                var channelManager = channelManagers[_activeChannelNumber];
                channelManager?.DeactivateChannel();
            }

            // Switch MUX to off state (channel 0)
            _activeChannelNumber = 0;
            switch_Mux((char)0);
        }

        #endregion

        #region Cleanup
        /// <summary>
        /// Safely switch off all channels and deactivate MUX
        /// </summary>
        public async Task SafeShutdownAsync()
        {
            try
            {
                Log.Log.Info("Starting MUX safe shutdown...");

                // Deactivate all channels
                for (int ch = 1; ch <= 8; ch++)
                {
                    var channelManager = channelManagers[ch];

                    if (channelManager != null && channelManager.IsActive)
                    {
                        Log.Log.Info($"Deactivating channel {ch}");
                        channelManager.DeactivateChannel();
                    }
                }

                // Switch MUX to off state
                switch_Mux((char)0);
                _activeChannelNumber = 0;

                // Allow final settling time
                await Task.Delay(200);

                Log.Log.Info("MUX safe shutdown completed");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error during MUX safe shutdown: {ex.Message}");
            }
        }

        /// <summary>
        /// Dispose all channel managers and cleanup resources
        /// </summary>
        public void Dispose()
        {
            try
            {
                // Perform safe shutdown first
                SafeShutdownAsync().Wait(TimeSpan.FromSeconds(5));

                // Dispose all channel managers
                if (channelManagers != null)
                {
                    foreach (var manager in channelManagers.Values)
                    {
                        try
                        {
                            manager?.Dispose();
                        }
                        catch (Exception ex)
                        {
                            Log.Log.Error($"Error disposing channel manager: {ex.Message}");
                        }
                    }

                    channelManagers.Clear();
                }

                // Disconnect MUX transport
                if (_muxTransport != null)
                {
                    try
                    {
                        _muxTransport.Disconnect();
                    }
                    catch (Exception ex)
                    {
                        Log.Log.Error($"Error disconnecting MUX transport: {ex.Message}");
                    }

                    _muxTransport = null;
                }

                isMuxHwConnected = false;
                Log.Log.Info("MuxManager disposed successfully");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error disposing MuxManager: {ex.Message}");
            }
        }
        #endregion
    }
}