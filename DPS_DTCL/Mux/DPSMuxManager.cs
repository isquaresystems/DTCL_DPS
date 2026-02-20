using DTCL.Transport;
using IspProtocol;
using System;
using System.Collections.Generic;
using System.IO.Ports;
using System.Linq;
using System.Threading.Tasks;

namespace DTCL.Mux
{
    /// <summary>
    /// Manages 8 DPS MUX channels with 4 slots each
    /// Supports DPS2_4_IN_1 (32 NAND slots) or DPS3_4_IN_1 (32 CF slots)
    /// </summary>
    public class DPSMuxManager : IDisposable
    {
        // Channel data (1-8)
        public Dictionary<int, DPSMuxChannelInfo> channels = new Dictionary<int, DPSMuxChannelInfo>();
        public Dictionary<int, MuxChannelManager> channelManagers = new Dictionary<int, MuxChannelManager>();

        // Internal MuxChannelInfo for MuxChannelManager compatibility
        private Dictionary<int, MuxChannelInfo> internalChannelInfos = new Dictionary<int, MuxChannelInfo>();

        // MUX hardware connection
        private UartTransportSync _muxTransport;
        private int _activeChannelNumber = 0;  // 0=off, 1-8=active
        private string _muxComPort = "";

        // Hardware type
        public HardwareType DPSHardwareType { get; private set; } = HardwareType.Unknown;

        // Events
        public event EventHandler<int> PortConnected;
        public event EventHandler PortDisconnected;

        // Lock for thread safety
        private readonly object _lockObject = new object();
        private bool _isDisposed = false;

        // Constructor
        public DPSMuxManager()
        {
            // Initialize 8 channels
            for (int i = 1; i <= 8; i++)
            {
                // Create DPS channel info for UI binding
                channels.Add(i, new DPSMuxChannelInfo(i));

                // Create internal MuxChannelInfo for MuxChannelManager
                var internalInfo = new MuxChannelInfo
                {
                    Channel = i,
                    isDTCLConnected = false,
                    CartType = "",
                    PCStatus = "",
                    isUserSelected = false,
                    channel_SlotInfo = new SlotInfo[5], // [0] unused, [1-4] for slots
                    cartNo = 0,
                    UnitSno = "",
                    DtcSno = "",
                    isInProgress = false,
                    PCLogFileName = ""
                };
                internalChannelInfos.Add(i, internalInfo);

                // Create channel manager with internal info
                channelManagers.Add(i, new MuxChannelManager(i, internalInfo));
            }

            Log.Log.Info("DPSMuxManager initialized with 8 channels");
        }

        /// <summary>
        /// Scan for MUX hardware on available COM ports
        /// </summary>
        public async Task<bool> ScanMuxHw()
        {
            Log.Log.Info("Starting DPS MUX hardware scan...");

            try
            {
                // Clean up any existing transport before scanning
                if (_muxTransport != null)
                {
                    try
                    {
                        _muxTransport.PortOpened -= OnPortConnected;
                        _muxTransport.PortClosed -= OnPortClosed;
                        _muxTransport.Disconnect();
                        _muxTransport.Dispose();
                        _muxTransport = null;
                        Log.Log.Debug("Cleaned up existing MUX transport before scan");
                    }
                    catch (Exception ex)
                    {
                        Log.Log.Warning($"Error cleaning up existing transport: {ex.Message}");
                        _muxTransport = null;
                    }
                }

                // Get all available COM ports
                string[] ports = SerialPort.GetPortNames();

                if (ports.Length == 0)
                {
                    Log.Log.Warning("No COM ports found for MUX scanning");
                    return false;
                }

                // Try each port
                foreach (string port in ports)
                {
                    Log.Log.Debug($"Trying MUX connection on {port}");

                    try
                    {
                        // Create transport for MUX protocol
                        _muxTransport = new UartTransportSync(port, 9600);

                        var res = _muxTransport.Connect();
                        if (!res)
                        {
                            Log.Log.Debug($"Failed to connect to {port}");
                            _muxTransport?.Dispose();
                            _muxTransport = null;
                            await Task.Delay(100); // Small delay between attempts
                            continue;
                        }

                        // Test MUX by switching to channel 0 (off)
                        var txBuffer = new byte[1];
                        txBuffer[0] = (byte)'0';
                        _muxTransport.Send(txBuffer, 0, 1);

                        var response = _muxTransport.WaitForResponse(4, 1000);

                        if (response != null && (response[3] == 48 || response[1] == 'M'))
                        {
                            _muxComPort = port;
                            _muxTransport.PortOpened += OnPortConnected;
                            _muxTransport.PortClosed += OnPortClosed;
                            Log.Log.Info($"✓ DPS MUX hardware found on {port}");
                            PortConnected?.Invoke(this, 0);
                            return true;
                        }
                        else
                        {
                            Log.Log.Debug($"Invalid response from {port}, not a MUX device");
                            _muxTransport.Disconnect();
                            _muxTransport.Dispose();
                            _muxTransport = null;
                            await Task.Delay(100); // Small delay between attempts
                        }
                    }
                    catch (Exception ex)
                    {
                        Log.Log.Debug($"Error testing MUX on {port}: {ex.Message}");
                        try
                        {
                            _muxTransport?.Disconnect();
                            _muxTransport?.Dispose();
                        }
                        catch { }
                        _muxTransport = null;
                        await Task.Delay(100); // Small delay between attempts
                    }
                }

                Log.Log.Debug($"DPS MUX hardware not found (scanned {ports.Length} ports)");
                return false;
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error during DPS MUX scan: {ex.Message}");
                // Clean up on error
                try
                {
                    _muxTransport?.Disconnect();
                    _muxTransport?.Dispose();
                }
                catch { }
                _muxTransport = null;
                return false;
            }
        }

        /// <summary>
        /// Switch MUX to specified channel
        /// </summary>
        /// <param name="channelNumber">'0' for off, '1'-'8' for channels</param>
        public async Task<bool> switch_Mux(char channelNumber)
        {
            if (_muxTransport == null)
            {
                Log.Log.Error("Cannot switch MUX: No MUX transport available");
                return false;
            }

            try
            {

                lock (_lockObject)
                {
                    // Prepare MUX switch command
                    byte[] txBuffer = new byte[1];
                    byte temp = 0x30;
                    txBuffer[0] = (byte)((byte)channelNumber + temp);

                    // Send switch command
                    _muxTransport.Send(txBuffer, 0, 1);

                    // Wait for response
                    byte[] rxBuffer = _muxTransport.WaitForResponse(4, 500);

                    if (rxBuffer != null && rxBuffer[3] == (byte)((byte)channelNumber + temp) && (rxBuffer[1] == 65 || rxBuffer[1] == 77))
                    {
                        _activeChannelNumber = channelNumber;
                        Log.Log.Info($"MUX switched to channel {_activeChannelNumber}");
                        return true;
                    }
                    else
                    {
                        Log.Log.Error($"MUX switch failed: Expected {channelNumber}, invalid response");
                        return false;
                    }
                }
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error switching MUX to channel {channelNumber}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Scan all 8 channels for DPS hardware and cart detection
        /// Clean implementation: OFF → Channel 1 ON → Scan → OFF → Channel 2 ON → etc.
        /// </summary>
        public async Task ScanAllChannelsAsync()
        {
            Log.Log.Info("Starting scan of all 8 DPS MUX channels...");

            // STEP 0: Reset all channel progress indicators
            for (int i = 1; i <= 8; i++)
            {
                channels[i].isInProgress = false;
            }

            // STEP 1: Switch all channels OFF before scanning
            Log.Log.Info("Switching all MUX channels OFF before scan");
            await switch_Mux((char)0);
            await Task.Delay(500);

            // STEP 2: Scan each channel one by one
            for (int channelNo = 1; channelNo <= 8; channelNo++)
            {
                try
                {
                    Log.Log.Info($"Scanning DPS MUX channel {channelNo}...");

                    // Get channel info
                    var channelInfo = channels[channelNo];
                    var internalInfo = internalChannelInfos[channelNo];

                    // Highlight this channel during scan
                    channelInfo.isInProgress = true;

                    // Switch MUX to this channel (ON)
                    if (!await switch_Mux((char)(channelNo)))
                    {
                        Log.Log.Warning($"Failed to switch to channel {channelNo}");
                        channelInfo.isInProgress = false;
                        continue;
                    }

                    // Wait for stabilization after switching ON
                    await Task.Delay(2000);

                    // Activate channel manager and scan for hardware
                    var channelManager = channelManagers[channelNo];
                    await channelManager.ActivateChannelAsync();

                    // Get hardware info from channel
                    var hwInfo = channelManager.HardwareInfo;

                    if (hwInfo.IsConnected)
                    {
                        // Update DPS channel info
                        channelInfo.isDPSConnected = true;
                        channelInfo.HardwareType = hwInfo.HardwareType.ToString();

                        // Detect hardware type
                        if (hwInfo.HardwareType == HardwareType.DPS2_4_IN_1)
                        {
                            DPSHardwareType = HardwareType.DPS2_4_IN_1;
                            channelInfo.CartType = "Darin2";
                        }
                        else if (hwInfo.HardwareType == HardwareType.DPS3_4_IN_1)
                        {
                            DPSHardwareType = HardwareType.DPS3_4_IN_1;
                            channelInfo.CartType = "Darin3";
                        }

                        // Update internal info for MuxChannelManager
                        internalInfo.isDTCLConnected = true;
                        internalInfo.CartType = channelInfo.CartType;

                        // Detect carts in all 4 slots
                        for (int slot = 1; slot <= 4; slot++)
                        {
                            if (hwInfo.SlotInfo[slot].IsCartDetectedAtSlot)
                            {
                                channelInfo.IsCartDetected[slot] = true;
                                channelInfo.DetectedCartTypes[slot] = hwInfo.SlotInfo[slot].DetectedCartTypeAtSlot;
                                channelInfo.channel_SlotInfo[slot] = hwInfo.SlotInfo[slot];

                                // Update internal info
                                internalInfo.channel_SlotInfo[slot] = hwInfo.SlotInfo[slot];
                            }
                            else
                            {
                                channelInfo.IsCartDetected[slot] = false;
                                channelInfo.DetectedCartTypes[slot] = CartType.Unknown;
                            }
                        }

                        Log.Log.Info($"Channel {channelNo}: {channelInfo.HardwareType} detected with {channelInfo.CartType} slots");
                    }
                    else
                    {
                        channelInfo.isDPSConnected = false;
                        channelInfo.HardwareType = "";
                        channelInfo.CartType = "";
                        internalInfo.isDTCLConnected = false;
                        Log.Log.Info($"Channel {channelNo}: No DPS hardware detected");
                    }

                    // Deactivate channel (preserve data for UI)
                    channelManager.DeactivateChannel(clearDiscoveredData: false);

                    // Clear highlight after scan complete
                    channelInfo.isInProgress = false;

                    // Switch this channel OFF before moving to next
                    Log.Log.Info($"Switching channel {channelNo} OFF");
                    await switch_Mux((char)0);
                    await Task.Delay(700);
                }
                catch (Exception ex)
                {
                    Log.Log.Error($"Error scanning channel {channelNo}: {ex.Message}");

                    // Clear highlight on error
                    var channelInfo = channels[channelNo];
                    channelInfo.isInProgress = false;

                    // Ensure channel is OFF even on error
                    try
                    {
                        await switch_Mux((char)0);
                        await Task.Delay(500);
                    }
                    catch { }
                }
            }

            Log.Log.Info("DPS MUX channel scan completed");
        }

        /// <summary>
        /// Reestablish connection to specific channel after MUX switch
        /// Includes retry mechanism: if first attempt fails, switch OFF then back ON and retry
        /// </summary>
        public async Task<bool> ReestablishChannelConnection(int channelNo, bool withCart)
        {
            try
            {
                Log.Log.Info($"Reestablishing connection to DPS MUX channel {channelNo}...");

                // Switch MUX to channel
                if (!await switch_Mux((char)channelNo))
                {
                    Log.Log.Error($"Failed to switch MUX to channel {channelNo}");
                    return false;
                }

                // Wait for stabilization
                await Task.Delay(2000);

                // Activate channel manager (first attempt)
                var channelManager = channelManagers[channelNo];
                bool success = await channelManager.ActivateChannelAsync();

                // Get hardware info
                var hwInfo = channelManager.HardwareInfo;

                // RETRY MECHANISM: If activation failed, try switching OFF then ON again
                if (!hwInfo.IsConnected || !success)
                {
                    Log.Log.Info($"Retry: Switch off MUX channel {channelNo}");
                    await switch_Mux((char)0);
                    await Task.Delay(500);

                    Log.Log.Info($"Retry: Switch on MUX channel {channelNo}");
                    await switch_Mux((char)channelNo);
                    await Task.Delay(1000);

                    Log.Log.Info($"Retry: Activate channel {channelNo} again");
                    success = await channelManager.ActivateChannelAsync();
                    hwInfo = channelManager.HardwareInfo;

                    if (!hwInfo.IsConnected)
                    {
                        Log.Log.Error($"DPS hardware not detected on channel {channelNo} after retry");
                        return false;
                    }
                }

                // Validate cart presence if required
                if (withCart)
                {
                    var channelInfo = channels[channelNo];
                    bool hasSelectedCart = false;

                    for (int slot = 1; slot <= 4; slot++)
                    {
                        if (channelInfo.IsSlotSelected[slot] && channelInfo.IsCartDetected[slot])
                        {
                            hasSelectedCart = true;
                            break;
                        }
                    }

                    if (!hasSelectedCart)
                    {
                        Log.Log.Warning($"No cart detected in selected slots on channel {channelNo}");
                        return false;
                    }
                }

                Log.Log.Info($"Connection reestablished to channel {channelNo}");
                return true;
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error reestablishing connection to channel {channelNo}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Get active channel number
        /// </summary>
        public int GetActiveChannelNumber()
        {
            return _activeChannelNumber;
        }

        /// <summary>
        /// Check if MUX is connected
        /// </summary>
        public bool IsMuxConnected()
        {
            return _muxTransport != null && !string.IsNullOrEmpty(_muxComPort);
        }

        /// <summary>
        /// Port connected event handler
        /// </summary>
        private void OnPortConnected(object sender, UartTransportSync.PortEventArgs e)
        {
            Log.Log.Info($"DPS MUX Port connected: {e.PortName}");
        }

        /// <summary>
        /// Port closed event handler
        /// </summary>
        private void OnPortClosed(object sender, UartTransportSync.PortEventArgs e)
        {
            Log.Log.Info($"DPS MUX Port closed: {e.PortName}");

            // Clean up the transport to allow reconnection
            try
            {
                if (_muxTransport != null)
                {
                    _muxTransport.PortOpened -= OnPortConnected;
                    _muxTransport.PortClosed -= OnPortClosed;
                    _muxTransport.Dispose();
                    _muxTransport = null;
                }
                _muxComPort = "";
                _activeChannelNumber = 0;
                Log.Log.Info("DPS MUX transport cleaned up after disconnect");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error cleaning up MUX transport: {ex.Message}");
            }

            PortDisconnected?.Invoke(this, EventArgs.Empty);
        }

        /// <summary>
        /// Dispose resources
        /// </summary>
        public void Dispose()
        {
            if (_isDisposed)
                return;

            try
            {
                // Switch MUX off
                if (_muxTransport != null)
                {
                    switch_Mux((char)0).Wait();
                }

                // Dispose all channel managers
                foreach (var manager in channelManagers.Values)
                {
                    manager.DeactivateChannel(clearDiscoveredData: true);
                }

                // Dispose MUX transport
                if (_muxTransport != null)
                {
                    _muxTransport.Disconnect();
                    _muxTransport = null;
                }

                Log.Log.Info("DPSMuxManager disposed");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error disposing DPSMuxManager: {ex.Message}");
            }

            _isDisposed = true;
        }
    }
}
