using System;
using System.Threading;
using System.Threading.Tasks;
using DTCL.Transport;
using DTCL.Log;
using DTCL.Cartridges;

namespace DTCL.Mux
{
    /// <summary>
    /// Manages isolated hardware instance for a single MUX channel
    /// Provides clean separation between channels to prevent state conflicts
    /// </summary>
    public class MuxChannelManager : IDisposable
    {
        #region Private Fields
        readonly int _channelNumber;
        readonly object _lockObject = new object();
        ChannelHardwareInfo _hardwareInfo;
        bool _isActive;
        bool _disposed;
        MuxChannelInfo _channelInfo;
        DateTime _lastActivated = DateTime.MinValue;
        DateTime _lastDeactivated = DateTime.MinValue;
        int _activationCount;
        #endregion

        #region Public Properties
        public int ChannelNumber => _channelNumber;

        public ChannelHardwareInfo HardwareInfo
        {
            get
            {
                lock (_lockObject)
                    return _hardwareInfo;
            }
        }

        public bool IsActive
        {
            get
            {
                lock (_lockObject)
                    return _isActive && !_disposed;
            }
        }

        public MuxChannelInfo ChannelInfo
        {
            get
            {
                lock (_lockObject)
                    return _channelInfo;
            }
        }
        #endregion

        #region Events
        public event EventHandler<HardwareDetectionEventArgs> HardwareDetected;
        public event EventHandler<HardwareDetectionEventArgs> HardwareDisconnected;
        public event EventHandler<CartDetectionEventArgs> CartDetected;
        #endregion

        #region Constructor
        public MuxChannelManager(int channelNumber, MuxChannelInfo channelInfo)
        {
            _channelNumber = channelNumber;
            _channelInfo = channelInfo ?? throw new ArgumentNullException(nameof(channelInfo));

            InitializeHardwareInfo();
        }
        #endregion

        #region Initialization
        void InitializeHardwareInfo()
        {
            _hardwareInfo = new ChannelHardwareInfo(_channelNumber);

            // Wire up events for forwarding
            _hardwareInfo.HardwareDetected += OnHardwareDetected;
            _hardwareInfo.HardwareDisconnected += OnHardwareDisconnected;
            _hardwareInfo.CartDetected += OnCartDetected;
        }

        #endregion

        #region Channel Activation/Deactivation
        /// <summary>
        /// Activate this channel - establish hardware connection and scan for carts
        /// Should be called when MUX switches to this channel
        /// </summary>
        public async Task<bool> ActivateChannelAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed)
                return false;

            lock (_lockObject)
            {
                if (_isActive)
                {
                    Log.Log.Info($"Channel {_channelNumber} already active");
                    return true; // Already active
                }

                _isActive = true;
                _lastActivated = DateTime.Now;
                _activationCount++;
            }

            try
            {
                Log.Log.Info($"Activating channel {_channelNumber} (Activation #{_activationCount})");

                // Step 1: Reset channel state before activation
                ResetChannelState();

                // Step 2: Establish hardware connection
                var hardwareConnected = await _hardwareInfo.OnChannelActivatedAsync(cancellationToken);

                if (!hardwareConnected)
                {
                    lock (_lockObject)
                        _isActive = false;

                    Log.Log.Warning($"Failed to establish hardware connection for channel {_channelNumber}");
                    return false;
                }

                // Step 3: Update channel info with hardware details
                UpdateChannelInfoFromHardware();

                // Step 4: Detect carts
                var cartsDetected = await _hardwareInfo.DetectCartsAsync(cancellationToken);

                // Step 5: Update channel info with cart details
                UpdateChannelInfoFromCartDetection();

                // Step 6: Trigger UI refresh to show updated hardware info
                System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                {
                    // Force UI refresh - this ensures the MuxChannelGrid shows updated data
                    // The UI binding should automatically pick up the changed properties
                });

                Log.Log
                    .Info($"Channel {_channelNumber} hardware info updated - BoardId: {_channelInfo.DtcSno}, FirmwareVersion: {_channelInfo.UnitSno}");

                Log.Log
                    .Info($"Channel {_channelNumber} activated successfully - Hardware: {_channelInfo.isDTCLConnected}, Carts: {_hardwareInfo.TotalDetectedCarts}");

                return true;
            }
            catch (Exception ex)
            {
                lock (_lockObject)
                    _isActive = false;

                Log.Log.Error($"Error activating channel {_channelNumber}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Deactivate this channel - clean up state when MUX switches away
        /// Should be called when MUX switches to different channel
        /// </summary>
        public void DeactivateChannel() => DeactivateChannel(clearDiscoveredData: true);

        /// <summary>
        /// Deactivate this channel with option to preserve discovered hardware/cart data
        /// </summary>
        /// <param name="clearDiscoveredData">If true, clears hardware info. If false, preserves it for GUI display</param>
        public async void DeactivateChannel(bool clearDiscoveredData)
        {
            if (_disposed)
                return;

            lock (_lockObject)
            {
                if (!_isActive)
                    return; // Already inactive

                _isActive = false;
                _lastDeactivated = DateTime.Now;
            }

            try
            {
                Log.Log.Info($"Deactivating channel {_channelNumber} (clearData: {clearDiscoveredData})");

                
                // Notify hardware of deactivation
                _hardwareInfo.OnChannelDeactivated();

                // Only reset channel state if requested (for scan operations, preserve data)
                if (clearDiscoveredData)
                {
                    ResetChannelState();
                }
                else
                {
                    // Preserve hardware/cart info AND progress indicator when not clearing data
                    // isInProgress will be managed by PC execution logic, not channel deactivation
                    lock (_lockObject)
                    {
                        // Don't reset isInProgress here - let PC operations manage highlighting
                    }
                }

                Log.Log.Info($"Channel {_channelNumber} deactivated successfully");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error deactivating channel {_channelNumber}: {ex.Message}");
            }
        }

        #endregion

        #region Hardware Operations
        /// <summary>
        /// Scan for hardware on this channel
        /// </summary>
        public async Task<bool> ScanHardwareAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || !_isActive)
                return false;

            var result = await _hardwareInfo.ScanForHardwareAsync(cancellationToken);

            if (result)
            {
                UpdateChannelInfoFromHardware();
            }

            return result;
        }

        /// <summary>
        /// Detect carts on this channel
        /// </summary>
        public async Task<bool> DetectCartsAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || !_isActive)
                return false;

            var result = await _hardwareInfo.DetectCartsAsync(cancellationToken);

            if (result)
            {
                UpdateChannelInfoFromCartDetection();
            }

            return result;
        }

        #endregion

        #region Channel Info Updates
        void UpdateChannelInfoFromHardware()
        {
            lock (_lockObject)
            {
                if (_channelInfo == null) return;

                _channelInfo.isDTCLConnected = _hardwareInfo.IsConnected;

                // Set default 999 value for UnitSno when DTCL is detected (only if field is empty)
                if (_hardwareInfo.IsConnected && string.IsNullOrEmpty(_channelInfo.UnitSno))
                {
                    _channelInfo.UnitSno = "999";
                }
                // Note: DtcSno will be set to 999 only when cart is detected in UpdateChannelInfoFromCartDetection

                // Clear cart info when hardware changes
                _channelInfo.CartType = "";
                _channelInfo.cartNo = 0;
            }
        }

        void UpdateChannelInfoFromCartDetection()
        {
            lock (_lockObject)
            {
                if (_channelInfo == null || _hardwareInfo.SlotInfo == null) return;

                // Find first detected cart and update channel info
                var cartDetected = false;

                for (int slotIndex = 1; slotIndex <= _hardwareInfo.GetSlotCount(); slotIndex++)
                {
                    var slot = _hardwareInfo.SlotInfo[slotIndex];

                    if (slot != null && slot.IsCartDetectedAtSlot)
                    {
                        _channelInfo.cartNo = slotIndex;
                        _channelInfo.CartType = MapCartTypeToString(slot.DetectedCartTypeAtSlot);
                        cartDetected = true;
                        break;
                    }
                }

                if (!cartDetected)
                {
                    _channelInfo.CartType = "";
                    _channelInfo.cartNo = 0;
                }
                else
                {
                    // Cart detected - set default DtcSno to 999 if field is empty
                    if (string.IsNullOrEmpty(_channelInfo.DtcSno))
                    {
                        _channelInfo.DtcSno = "999";
                    }
                }

                // Update slot info reference
                _channelInfo.channel_SlotInfo = _hardwareInfo.SlotInfo;
            }
        }

        string MapCartTypeToString(CartType cartType)
        {
            switch (cartType)
            {
                case CartType.Darin1:
                    return "Darin-I";
                case CartType.Darin2:
                    return "Darin-II";
                case CartType.Darin3:
                    return "Darin-III";
                case CartType.MultiCart:
                    return "Multi";
                default:
                    return "";
            }
        }

        void ResetChannelState()
        {
            lock (_lockObject)
            {
                if (_channelInfo == null) return;

                // Preserve user-edited serial numbers
                var preservedUnitSno = _channelInfo.UnitSno;
                var preservedDtcSno = _channelInfo.DtcSno;

                _channelInfo.isDTCLConnected = false;
                _channelInfo.CartType = "";
                _channelInfo.cartNo = 0;
                _channelInfo.PCStatus = "";
                _channelInfo.isInProgress = false;

                // Only clear if values are default "999" or empty, preserve user edits
                if (string.IsNullOrEmpty(preservedDtcSno) || preservedDtcSno == "999")
                    _channelInfo.DtcSno = "";
                else
                    _channelInfo.DtcSno = preservedDtcSno;

                if (string.IsNullOrEmpty(preservedUnitSno) || preservedUnitSno == "999")
                    _channelInfo.UnitSno = "";
                else
                    _channelInfo.UnitSno = preservedUnitSno;
            }
        }

        #endregion

        #region Event Handlers
        void OnHardwareDetected(object sender, HardwareDetectionEventArgs e)
        {
            HardwareDetected?.Invoke(this, e);
        }

        void OnHardwareDisconnected(object sender, HardwareDetectionEventArgs e)
        {
            // Update channel state on hardware disconnect
            lock (_lockObject)
                _isActive = false;

            ResetChannelState();
            HardwareDisconnected?.Invoke(this, e);
        }

        void OnCartDetected(object sender, CartDetectionEventArgs e)
        {
            // Update channel info when cart detection changes
            UpdateChannelInfoFromCartDetection();
            CartDetected?.Invoke(this, e);
        }

        #endregion

        #region Slot Management
        /// <summary>
        /// Set slot selection for this channel
        /// </summary>
        public void SetSlotSelection(int slotNumber, bool isSelected)
        {
            if (_disposed || !_isActive)
                return;

            _hardwareInfo.SetSlotSelection(slotNumber, isSelected);
        }

        /// <summary>
        /// Set slot role (master/slave) for this channel
        /// </summary>
        public void SetSlotRole(int slotNumber, SlotRole role)
        {
            if (_disposed || !_isActive)
                return;

            _hardwareInfo.SetSlotRole(slotNumber, role);
        }

        /// <summary>
        /// Get master slot for this channel
        /// </summary>
        public int GetMasterSlot()
        {
            if (_disposed || !_isActive)
                return 0;

            return _hardwareInfo.GetMasterSlot();
        }

        /// <summary>
        /// Get slave slots for this channel
        /// </summary>
        public int[] GetSlaveSlots()
        {
            if (_disposed || !_isActive)
                return new int[0];

            return _hardwareInfo.GetSlaveSlots();
        }

        #endregion

        #region Cart Management
        /// <summary>
        /// Get cart instance for specific cart type
        /// </summary>
        public ICart GetCartInstance(CartType cartType)
        {
            if (_disposed || !_isActive)
                return null;

            return _hardwareInfo.GetCartInstance(cartType);
        }

        /// <summary>
        /// Get currently active cart object
        /// </summary>
        public ICart GetActiveCartObject()
        {
            if (_disposed || !_isActive)
                return null;

            return _hardwareInfo.CartObj;
        }

        #endregion

        #region Status and Diagnostics
        /// <summary>
        /// Get channel status summary
        /// </summary>
        public string GetChannelStatus()
        {
            if (_disposed)
                return "Disposed";

            if (!_isActive)
                return "Inactive";

            return _hardwareInfo.GetChannelState();
        }

        /// <summary>
        /// Get detailed channel statistics
        /// </summary>
        public string GetChannelStatistics()
        {
            lock (_lockObject)
            {
                var status = GetChannelStatus();
                var lastActivatedStr = _lastActivated == DateTime.MinValue ? "Never" : _lastActivated.ToString("HH:mm:ss");
                var lastDeactivatedStr = _lastDeactivated == DateTime.MinValue ? "Never" : _lastDeactivated.ToString("HH:mm:ss");

                return $"Channel {_channelNumber}: {status} | Activations: {_activationCount} | Last Active: {lastActivatedStr} | Last Deactive: {lastDeactivatedStr}";
            }
        }

        /// <summary>
        /// Check if channel is in valid state for operations
        /// </summary>
        public bool IsChannelReady()
        {
            if (_disposed || !_isActive)
                return false;

            return _hardwareInfo.IsConnected && _channelInfo.isDTCLConnected;
        }

        /// <summary>
        /// Force reset channel to clean state
        /// </summary>
        public async Task<bool> ForceResetChannelAsync()
        {
            if (_disposed)
                return false;

            try
            {
                Log.Log.Info($"Force resetting channel {_channelNumber}");

                // Deactivate first
                DeactivateChannel();

                // Wait for stabilization
                await Task.Delay(1000);

                // Reset hardware info
                _hardwareInfo.ResetSlots();

                // Reset channel info
                ResetChannelState();

                Log.Log.Info($"Channel {_channelNumber} force reset completed");
                return true;
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error force resetting channel {_channelNumber}: {ex.Message}");
                return false;
            }
        }

        /// <summary>
        /// Check if channel has any detected carts
        /// </summary>
        public bool HasDetectedCarts()
        {
            if (_disposed || !_isActive)
                return false;

            return _hardwareInfo.TotalDetectedCarts > 0;
        }

        /// <summary>
        /// Get total number of detected carts
        /// </summary>
        public int GetDetectedCartCount()
        {
            if (_disposed || !_isActive)
                return 0;

            return _hardwareInfo.TotalDetectedCarts;
        }

        #endregion

        #region Cleanup
        /// <summary>
        /// Dispose of channel resources
        /// </summary>
        public void Dispose()
        {
            if (_disposed)
                return;

            Log.Log.Info($"Disposing channel {_channelNumber} (Activations: {_activationCount})");
            _disposed = true;

            try
            {
                // Deactivate if currently active
                if (_isActive)
                {
                    DeactivateChannel();
                }

                // Unsubscribe from events
                if (_hardwareInfo != null)
                {
                    _hardwareInfo.HardwareDetected -= OnHardwareDetected;
                    _hardwareInfo.HardwareDisconnected -= OnHardwareDisconnected;
                    _hardwareInfo.CartDetected -= OnCartDetected;

                    _hardwareInfo.Dispose();
                    _hardwareInfo = null;
                }

                // Clear channel info reference
                _channelInfo = null;

                Log.Log.Info($"Channel {_channelNumber} disposed successfully");
            }
            catch (Exception ex)
            {
                Log.Log.Error($"Error disposing channel {_channelNumber}: {ex.Message}");
            }
        }
        #endregion
    }
}