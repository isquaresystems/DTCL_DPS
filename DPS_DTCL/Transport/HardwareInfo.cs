using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Timers;
using System.Linq;
using DTCL.Cartridges;
using IspProtocol;

namespace DTCL.Transport
{
    /// <summary>
    /// Professional hardware manager for standalone DTCL/DPS operations
    /// Implements robust state-driven scanning with proper memory management
    /// Thread-safe singleton pattern with comprehensive event handling
    /// </summary>
    public sealed class HardwareInfo : IHardwareInfo, IDisposable
    {
        #region Singleton Pattern
        static readonly Lazy<HardwareInfo> _lazy = new Lazy<HardwareInfo>(() => new HardwareInfo());
        public static HardwareInfo Instance => _lazy.Value;
        #endregion

        #region Private Fields - Core State
        readonly object _lockObject = new object();
        SlotInfo[] _slotInfo;
        HardwareType _hardwareType = HardwareType.Unknown;
        bool _isConnected;
        string _firmwareVersion = string.Empty;
        string _boardId = string.Empty;
        string _lastError = string.Empty;
        int _activeSlot;
        CartType _detectedCartTypeAtHw = CartType.Unknown;
        #endregion

        #region Private Fields - Communication
        public UartIspTransport _transport;
        IspCmdControl _cmdControl;
        IspSubCommandProcessor _processor;
        #endregion

        #region Private Fields - Cart Management (Memory Leak Prevention)
        Dictionary<CartType, ICart> _cartInstancePool;
        ICart _currentCartObj;
        readonly HashSet<CartType> _detectedCartTypes = new HashSet<CartType>();
        int _totalDetectedCarts;
        #endregion

        #region Private Fields - Professional Timer Management
        System.Timers.Timer _scanTimer;
        bool _isScanInProgress;
        ScanMode _currentScanMode = ScanMode.Hardware;
        bool _scanningEnabled = true;
        DateTime _lastHardwareScanTime = DateTime.MinValue;
        DateTime _lastCartScanTime = DateTime.MinValue;
        #endregion

        #region Public Properties
        public HardwareType HardwareType => _hardwareType;

        // Static path configurations
        public string D3UploadFilePath = "c:\\mps\\DARIN3\\upload\\";
        public string D3DownloadFilePath = "c:\\mps\\DARIN3\\download\\";
        public string D3DownloadTempFilePath = "c:\\mps\\DARIN3\\download\\temp\\";
        public string D3UploadTempFilePath = "c:\\mps\\DARIN3\\upload\\temp\\";
        public string D3CopyFilePath = "c:\\mps\\DARIN3\\copy\\";
        public string D3Compare1FilePath = "c:\\mps\\DARIN3\\compare1\\";
        public string D3Compare2FilePath = "c:\\mps\\DARIN3\\compare2\\";

        public string D2UploadFilePath = "c:\\mps\\DARIN2\\upload\\";
        public string D2DownloadFilePath = "c:\\mps\\DARIN2\\download\\";
        public string D2DownloadTempFilePath = "c:\\mps\\DARIN2\\download\\temp\\";
        public string D2UploadTempFilePath = "c:\\mps\\DARIN2\\upload\\temp\\";
        public string D2CopyFilePath = "c:\\mps\\DARIN2\\copy\\";
        public string D2Compare1FilePath = "c:\\mps\\DARIN2\\compare1\\";
        public string D2Compare2FilePath = "c:\\mps\\DARIN2\\compare2\\";

        public string CartUploadFilePath { get; set; }
        public string CartDownloadFilePath { get; set; }
        public string CartDownloadTempFilePath { get; set; }
        public string CartUploadTempFilePath { get; set; }
        public string CartCopyFilePath { get; set; }
        public string CartCompare1FilePath { get; set; }
        public string CartCompare2FilePath { get; set; }

        public SlotInfo[] SlotInfo
        {
            get
            {
                lock (_lockObject)
                {
                    // Return defensive copy to prevent external modification
                    var copy = new SlotInfo[_slotInfo.Length];
                    Array.Copy(_slotInfo, copy, _slotInfo.Length);
                    return copy;
                }
            }
        }

        public bool IsConnected
        {
            get { lock (_lockObject) return _isConnected; }
        }

        public string FirmwareVersion => _firmwareVersion;
        public string BoardId => _boardId;
        public string LastError => _lastError;

        /// <summary>
        /// Current cart object for operations with memory leak prevention
        /// DTCL: null if no cart, multi-cart, or invalid state
        /// DPS: null if no carts detected in any slot
        /// Non-null: UI can perform operations using this cart instance
        /// </summary>
        public ICart CartObj
        {
            get
            {
                lock (_lockObject)
                    return _currentCartObj;
            }
        }

        /// <summary>
        /// Total number of detected carts across all slots
        /// Updated during cart scanning to indicate cart count for hardware type
        /// </summary>
        public int TotalDetectedCarts
        {
            get
            {
                lock (_lockObject)
                    return _totalDetectedCarts;
            }
        }

        /// <summary>
        /// Detected cart type at hardware level
        /// DTCL: Same as slot cart type if single cart, MultiCart if different types in different slots
        /// DPS: Same as the detected cart type (all slots same type)
        /// Unknown: When no carts detected
        /// </summary>
        public CartType DetectedCartTypeAtHw
        {
            get
            {
                lock (_lockObject)
                    return _detectedCartTypeAtHw;
            }
        }
        #endregion

        #region Events
        public event EventHandler<HardwareDetectionEventArgs> HardwareDetected;
        public event EventHandler<HardwareDetectionEventArgs> HardwareDisconnected;
        public event EventHandler<CartDetectionEventArgs> CartDetected;
        #endregion

        #region Constructor
        private HardwareInfo()
        {
            InitializeSlots();
            InitializeCartInstancePool();
            InitializeTimer();
        }
        #endregion

        #region Initialization
        void InitializeSlots()
        {
            _slotInfo = new SlotInfo[5]; // [0] dummy, [1-4] for slots

            for (int i = 0; i <= 4; i++)
                _slotInfo[i] = new SlotInfo(i);
        }

        void InitializeCartInstancePool()
        {
            // Pre-create cart instances to prevent memory leaks during scanning
            _cartInstancePool = new Dictionary<CartType, ICart>
            {
                { CartType.Darin2, new Darin2() },
                { CartType.Darin3, new Darin3() }
                // Note: Darin1 commented out as per original code
            };
        }

        void InitializeTimer()
        {
            // Professional timer configuration
            _scanTimer = new System.Timers.Timer(300); // Start with hardware scan interval
            _scanTimer.Elapsed += OnScanTimerElapsed;
            _scanTimer.AutoReset = true;
            _scanTimer.Start(); // Begin hardware scanning immediately
        }

        #endregion

        #region Timer Management
        /// <summary>
        /// Unified timer event handler with professional state-driven logic
        /// Automatically switches between hardware and cart scanning modes
        /// </summary>
        async void OnScanTimerElapsed(object sender, ElapsedEventArgs e)
        {
            if (!_scanningEnabled) return;

            try
            {
                // Determine and execute appropriate scan based on connection state
                await ExecuteStateDrivenScan();
            }
            catch (Exception ex)
            {
                _lastError = $"Timer scan error: {ex.Message}";
                // Log error but continue scanning
            }
        }

        async Task ExecuteStateDrivenScan()
        {
            lock (_lockObject)
            {
                // Update scan mode based on current connection state (self-healing)
                var newMode = _isConnected ? ScanMode.Cartridge : ScanMode.Hardware;

                if (_currentScanMode != newMode)
                {
                    SwitchScanMode(newMode);
                }
            }

            // Execute scan based on current mode
            switch (_currentScanMode)
            {
                case ScanMode.Hardware:
                    if (!_isConnected)
                    {
                        await PerformHardwareScan();
                    }

                    break;

                case ScanMode.Cartridge:
                    if (_isConnected)
                    {
                        await PerformCartScan();
                    }

                    break;
            }
        }

        void SwitchScanMode(ScanMode newMode)
        {
            _currentScanMode = newMode;

            switch (newMode)
            {
                case ScanMode.Hardware:
                    _scanTimer.Interval = 2000; // 2 seconds for hardware scan
                    break;

                case ScanMode.Cartridge:
                    _scanTimer.Interval = 1000; // 1 second for cart scan
                    break;
            }
        }

        #endregion

        #region Hardware Scanning
        async Task PerformHardwareScan()
        {
            if (_isScanInProgress) return;

            _isScanInProgress = true;
            _lastHardwareScanTime = DateTime.Now;

            try
            {
                var availablePorts = System.IO.Ports.SerialPort.GetPortNames();

                foreach (var port in availablePorts)
                {
                    if (await TryConnectToHardware(port))
                    {
                        DataHandlerIsp.Instance.Initialize(_transport, null);
                        await OnHardwareConnected(port);
                        return; // Successfully connected
                    }
                }

                // No hardware found
                OnHardwareNotFound();
            }
            catch (Exception ex)
            {
                _lastError = $"Hardware scan error: {ex.Message}";
            }
            finally
            {
                _isScanInProgress = false;
            }
        }

        async Task<bool> TryConnectToHardware(string portName)
        {
            try
            {
                _transport = new UartIspTransport(portName);
                _transport.Open();

                if (!_transport.isPortOpen) return false;

                // Subscribe to port events for professional disconnect handling
                _transport.PortClosed += OnTransportDisconnected;

                // Initialize ISP communication
                _processor = new IspSubCommandProcessor();
                _cmdControl = new IspCmdControl(_transport, _processor);

                // Identify hardware type
                if (await IdentifyHardwareType())
                {
                    await GetFirmwareVersion();
                    ConfigureHardwareSpecificSettings();
                    return true;
                }
                else
                {
                    CleanupTransport();
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Port connection error: {ex.Message}";
                CleanupTransport();
            }

            return false;
        }

        async Task<bool> IdentifyHardwareType()
        {
            // Robust hardware identification with multiple attempts and wake-up sequence
            var maxAttempts = 3;
            var delayBetweenAttempts = 1000; // 1 second

            for (int attempt = 1; attempt <= maxAttempts; attempt++)
            {
                try
                {
                    // Send wake-up sequence first (helps with units that powered on without cartridge)
                    if (attempt == 1)
                    {
                        await SendWakeUpSequence();
                    }

                    var boardIdCmd = CreateIspCommand(IspSubCommand.BOARD_ID, new byte[0]);
                    var response = await _cmdControl.ExecuteCmd(boardIdCmd, (int)IspSubCmdRespLen.BOARD_ID, 3000); // Increased timeout

                    if (response?.Length > 0)
                    {
                        var boardId = (IspBoardId)response[0];
                        _boardId = boardId.ToString();

                        switch (boardId)
                        {
                            case IspBoardId.DTCL:
                                _hardwareType = HardwareType.DTCL;
                                break;
                            case IspBoardId.DPS2_4_IN_1:
                                _hardwareType = HardwareType.DPS2_4_IN_1;
                                break;
                            case IspBoardId.DPS3_4_IN_1:
                                _hardwareType = HardwareType.DPS3_4_IN_1;
                                break;
                            default:
                                _hardwareType = HardwareType.Unknown;
                                break;
                        }

                        if (_hardwareType != HardwareType.Unknown)
                        {
                            return true; // Success
                        }
                    }
                }
                catch (Exception ex)
                {
                    _lastError = $"Hardware identification attempt {attempt} failed: {ex.Message}";
                }

                // Wait before next attempt (except on last attempt)
                if (attempt < maxAttempts)
                {
                    await Task.Delay(delayBetweenAttempts);
                }
            }

            return false;
        }

        /// <summary>
        /// Send wake-up sequence to help units that powered on without cartridge
        /// This helps trigger proper USB initialization
        /// </summary>
        async Task SendWakeUpSequence()
        {
            try
            {
                // Send multiple short commands to wake up the firmware
                for (int i = 0; i < 3; i++)
                {
                    // Try to send a simple command with short timeout
                    var wakeUpCmd = CreateIspCommand(IspSubCommand.FIRMWARE_VERSION, new byte[0]);
                    await _cmdControl.ExecuteCmd(wakeUpCmd, (int)IspSubCmdRespLen.FIRMWARE_VERSION, 500); // Short timeout, ignore response

                    await Task.Delay(200); // Small delay between wake-up attempts
                }

                // Give the firmware time to fully initialize after wake-up
                await Task.Delay(1000);
            }
            catch
            {
                // Ignore wake-up sequence errors - this is just to trigger initialization
            }
        }

        async Task GetFirmwareVersion()
        {
            var versionCmd = CreateIspCommand(IspSubCommand.FIRMWARE_VERSION, new byte[0]);
            var response = await _cmdControl.ExecuteCmd(versionCmd, (int)IspSubCmdRespLen.FIRMWARE_VERSION, 2000);

            if (response?.Length >= 2)
            {
                _firmwareVersion = $"{response[0]}.{response[1]}";
            }
        }

        async Task OnHardwareConnected(string portName)
        {
            lock (_lockObject)
                _isConnected = true;

            // Fire hardware detected event
            FireHardwareDetectedEvent(new HardwareDetectionEventArgs
            {
                HardwareType = _hardwareType,
                IsConnected = true,
                Message = $"Connected to {_hardwareType} on {portName}"
            });
        }

        void OnHardwareNotFound()
        {
            lock (_lockObject)
            {
                _isConnected = false;
                _lastError = "No compatible hardware found";
            }
        }

        #endregion

        #region Cart Scanning
        async Task PerformCartScan()
        {
            if (_isScanInProgress) return;

            _isScanInProgress = true;
            _lastCartScanTime = DateTime.Now;

            try
            {
                var cartStatusCmd = CreateIspCommand(IspSubCommand.CART_STATUS, new byte[0]);
                var response = await _cmdControl.ExecuteCmd(cartStatusCmd, (int)IspSubCmdRespLen.CART_STATUS, 2000);

                if (response?.Length >= GetSlotCount())
                {
                    await ProcessCartDetectionResponse(response);
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Cart detection error: {ex.Message}";
            }
            finally
            {
                _isScanInProgress = false;
            }
        }

        async Task ProcessCartDetectionResponse(byte[] data)
        {
            var slotCount = GetSlotCount();
            var detectedCount = 0;
            var currentDetectedTypes = new HashSet<CartType>();

            // Process each slot's detection data
            for (int i = 0; i < slotCount; i++)
            {
                var slotIndex = i + 1;
                var detectedType = MapResponseToCartType(data[i]);

                await UpdateSlotStatus(slotIndex, detectedType);

                if (detectedType != CartType.Unknown)
                {
                    detectedCount++;
                    currentDetectedTypes.Add(detectedType);
                }
            }

            // Update total detected carts count
            _totalDetectedCarts = detectedCount;

            // Handle hardware-specific selection logic
            HandleHardwareSpecificSelection(detectedCount);

            // Update hardware-level cart type
            UpdateDetectedCartTypeAtHw(detectedCount, currentDetectedTypes);

            // Update cart object with memory leak prevention
            UpdateCartObjectSafely(detectedCount, currentDetectedTypes);

            UpdateCartPath(_detectedCartTypeAtHw);

            // Handle multi-cart scenarios for DTCL and fire cart detection event
            HandleCartDetectionEvent(detectedCount, currentDetectedTypes);
        }

        CartType MapResponseToCartType(byte responseValue)
        {
            switch (responseValue)
            {
                case 1: return CartType.Darin1;
                case 2: return CartType.Darin2;
                case 3: return CartType.Darin3;
                default: return CartType.Unknown;
            }
        }

        async Task UpdateSlotStatus(int slotIndex, CartType detectedType)
        {
            var slot = _slotInfo[slotIndex];
            var previousStatus = slot.Status;
            var previousType = slot.DetectedCartTypeAtSlot;

            // Update slot information
            if (detectedType != CartType.Unknown)
            {
                slot.SetDetected(detectedType);
            }
            else
            {
                slot.SetNotPresent();
            }

            // Fire cart detection event only if status changed
            if (previousStatus != slot.Status || previousType != detectedType)
            {
                FireCartDetectedEvent(new CartDetectionEventArgs
                {
                    SlotNumber = slotIndex,
                    CartType = detectedType,
                    Status = slot.Status,
                    Message = detectedType != CartType.Unknown
                        ? $"Cart {detectedType} detected in slot {slotIndex}"
                        : $"No cart in slot {slotIndex}"
                });
            }
        }

        void HandleHardwareSpecificSelection(int detectedCount)
        {
            if (_hardwareType == HardwareType.DTCL)
            {
                // Clear all selections first for DTCL
                var slotCount = GetSlotCount();

                for (int i = 1; i <= slotCount; i++)
                    _slotInfo[i].IsSlotSelected_ByUser = false;

                // Auto-select single cart for DTCL (if exactly one detected)
                if (detectedCount == 1)
                {
                    for (int i = 1; i <= slotCount; i++)
                    {
                        if (_slotInfo[i].Status == DetectionStatus.Detected)
                        {
                            _slotInfo[i].IsSlotSelected_ByUser = true;
                            _activeSlot = i;
                            break;
                        }
                    }
                }
                else
                {
                    _activeSlot = 0; // No selection for multi-cart or no-cart scenarios
                }
            }
            // For DPS: IsSlotSelected_ByUser remains UI-controlled, not modified by detection
        }

        void UpdateDetectedCartTypeAtHw(int detectedCount, HashSet<CartType> currentDetectedTypes)
        {
            var newHwCartType = CartType.Unknown;

            if (detectedCount == 0)
            {
                newHwCartType = CartType.Unknown;
            }
            else if (_hardwareType == HardwareType.DTCL)
            {
                // DTCL logic: Check if different cart types in different slots
                var detectedSlotTypes = new HashSet<CartType>();
                var slotCount = GetSlotCount();

                for (int i = 1; i <= slotCount; i++)
                {
                    if (_slotInfo[i].Status == DetectionStatus.Detected)
                    {
                        detectedSlotTypes.Add(_slotInfo[i].DetectedCartTypeAtSlot);
                    }
                }

                if (detectedSlotTypes.Count == 1)
                {
                    // Single cart type across all detected slots
                    newHwCartType = detectedSlotTypes.First();
                }
                else if (detectedSlotTypes.Count > 1)
                {
                    // Multiple different cart types detected in DTCL
                    newHwCartType = CartType.MultiCart;
                }
            }
            else if (_hardwareType == HardwareType.DPS2_4_IN_1 || _hardwareType == HardwareType.DPS3_4_IN_1)
            {
                // DPS logic: All slots same type, use first detected type
                newHwCartType = currentDetectedTypes.FirstOrDefault();
            }

            _detectedCartTypeAtHw = newHwCartType;
        }

        void UpdateCartPath(CartType type)
        {
            if (type == CartType.Darin3)
            {
                CartUploadFilePath = D3UploadFilePath;
                CartDownloadFilePath = D3DownloadFilePath;
                CartDownloadTempFilePath = D3DownloadTempFilePath;
                CartUploadTempFilePath = D3UploadTempFilePath;
                CartCopyFilePath = D3CopyFilePath;
                CartCompare1FilePath = D3Compare1FilePath;
                CartCompare2FilePath = D3Compare2FilePath;
            }
            else if (type == CartType.Darin2)
            {
                CartUploadFilePath = D2UploadFilePath;
                CartDownloadFilePath = D2DownloadFilePath;
                CartDownloadTempFilePath = D2DownloadTempFilePath;
                CartUploadTempFilePath = D2UploadTempFilePath;
                CartCopyFilePath = D2CopyFilePath;
                CartCompare1FilePath = D2Compare1FilePath;
                CartCompare2FilePath = D2Compare2FilePath;
            }
            else if (type == CartType.Darin1)
            {
                // TODO: Add D1 paths when needed
            }
            else
            {
                // Unknown or MultiCart - paths remain as is
            }
        }

        void UpdateCartObjectSafely(int detectedCount, HashSet<CartType> currentDetectedTypes)
        {
            lock (_lockObject)
            {
                ICart newCartObj = null;

                switch (_hardwareType)
                {
                    case HardwareType.DTCL:
                        // DTCL: CartObj only if exactly 1 cart detected and selected
                        if (detectedCount == 1)
                        {
                            for (int i = 1; i <= GetSlotCount(); i++)
                            {
                                if (_slotInfo[i].Status == DetectionStatus.Detected && _slotInfo[i].IsSlotSelected_ByUser)
                                {
                                    newCartObj = GetCartInstanceSafely(_slotInfo[i].DetectedCartTypeAtSlot);
                                    DataHandlerIsp.Instance.RegisterSubCommandHandlers(newCartObj);
                                    break;
                                }
                            }
                        }

                        // For multi-cart or no cart scenarios in DTCL, CartObj remains null
                        break;

                    case HardwareType.DPS2_4_IN_1:
                    case HardwareType.DPS3_4_IN_1:
                        // DPS: CartObj if any cart detected (at least one slot should have cart)
                        if (detectedCount > 0)
                        {
                            // Use first detected cart type for operations (all DPS slots are same type)
                            foreach (var cartType in currentDetectedTypes)
                            {
                                newCartObj = GetCartInstanceSafely(cartType);
                                DataHandlerIsp.Instance.RegisterSubCommandHandlers(newCartObj);
                                break;
                            }
                        }

                        // For no cart in DPS, CartObj is null
                        break;
                }

                // Update current cart object (memory safe - reusing pre-created instances)
                _currentCartObj = newCartObj;

                // Update detected types tracking for future reference
                _detectedCartTypes.Clear();

                foreach (var type in currentDetectedTypes)
                    _detectedCartTypes.Add(type);
            }
        }

        void HandleCartDetectionEvent(int detectedCount, HashSet<CartType> currentDetectedTypes)
        {
            if (_hardwareType == HardwareType.DTCL && detectedCount > 1)
            {
                // Multi-cart detected in DTCL - fire special event for UI handling
                FireCartDetectedEvent(new CartDetectionEventArgs
                {
                    SlotNumber = 0, // Indicates multi-cart scenario
                    CartType = CartType.MultiCart,
                    Status = DetectionStatus.Error,
                    Message = $"Multiple carts detected in DTCL ({detectedCount} carts)"
                });
            }
            else
            {
                // Fire cart detection summary event for all hardware types
                var cartTypes = string.Join(", ", currentDetectedTypes);

                var message = detectedCount == 0
                    ? "No carts detected"
                    : $"{detectedCount} cart(s) detected: {cartTypes}";

                FireCartDetectedEvent(new CartDetectionEventArgs
                {
                    SlotNumber = _activeSlot,
                    CartType = detectedCount == 1 ? currentDetectedTypes.First() : CartType.Unknown,
                    Status = detectedCount > 0 ? DetectionStatus.Detected : DetectionStatus.NotDetected,
                    Message = message
                });
            }
        }

        #endregion

        #region Memory-Safe Cart Management
        /// <summary>
        /// Get cart instance safely without memory leaks
        /// Reuses pre-created instances from pool
        /// </summary>
        ICart GetCartInstanceSafely(CartType cartType)
        {
            return _cartInstancePool.TryGetValue(cartType, out var cart) ? cart : null;
        }

        public ICart GetCartInstance(CartType cartType) => GetCartInstanceSafely(cartType);

        #endregion

        #region Utility Methods
        byte[] CreateIspCommand(IspSubCommand subCommand, byte[] data)
        {
            var payload = new byte[4 + data.Length];
            payload[0] = (byte)IspCommand.COMMAND_REQUEST;
            payload[1] = (byte)subCommand;
            payload[2] = (byte)((data.Length >> 8) & 0xFF);
            payload[3] = (byte)(data.Length & 0xFF);

            if (data.Length > 0)
            {
                Array.Copy(data, 0, payload, 4, data.Length);
            }

            return payload;
        }

        void ConfigureHardwareSpecificSettings()
        {
            switch (_hardwareType)
            {
                case HardwareType.DPS2_4_IN_1:
                    // Pre-configure slot types for DPS2
                    for (int i = 1; i <= 4; i++)
                        _slotInfo[i].DetectedCartTypeAtSlot = CartType.Darin2;

                    break;

                case HardwareType.DPS3_4_IN_1:
                    // Pre-configure slot types for DPS3
                    for (int i = 1; i <= 4; i++)
                        _slotInfo[i].DetectedCartTypeAtSlot = CartType.Darin3;

                    break;
            }
        }

        public int GetSlotCount()
        {
            switch (_hardwareType)
            {
                case HardwareType.DTCL:
                    return 3;
                case HardwareType.DPS2_4_IN_1:
                case HardwareType.DPS3_4_IN_1:
                    return 4;
                default:
                    return 0;
            }
        }

        #endregion

        #region Professional UI Control Interface
        /// <summary>
        /// Start scanning with professional state management
        /// </summary>
        public void StartScanning()
        {
            lock (_lockObject)
            {
                _scanningEnabled = true;

                if (_scanTimer != null && !_scanTimer.Enabled)
                {
                    _scanTimer.Start();
                }
            }
        }

        /// <summary>
        /// Stop scanning and wait for completion with timeout protection
        /// </summary>
        public async Task StopScanningAsync(int timeoutMs = 5000)
        {
            lock (_lockObject)
            {
                _scanningEnabled = false;
                _scanTimer?.Stop();
            }

            // Wait for current scan to complete with timeout
            var startTime = DateTime.Now;

            while (_isScanInProgress)
            {
                if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                {
                    break; // Timeout protection
                }

                await Task.Delay(50);
            }
        }

        /// <summary>
        /// Set slot selection with professional validation
        /// </summary>
        public void SetSlotSelection(int slotNumber, bool isSelected)
        {
            if (slotNumber < 1 || slotNumber > GetSlotCount()) return;

            lock (_lockObject)
            {
                _slotInfo[slotNumber].IsSlotSelected_ByUser = isSelected;

                // Handle DTCL single-selection constraint
                if (_hardwareType == HardwareType.DTCL && isSelected)
                {
                    for (int i = 1; i <= GetSlotCount(); i++)
                    {
                        if (i != slotNumber)
                        {
                            _slotInfo[i].IsSlotSelected_ByUser = false;
                        }
                    }

                    _activeSlot = slotNumber;
                }
                else if (_hardwareType == HardwareType.DTCL && !isSelected)
                {
                    _activeSlot = 0;
                }

                // Update cart object when selection changes
                UpdateCartObjectSafely(_detectedCartTypes.Count, _detectedCartTypes);
            }
        }

        public bool SetActiveSlot(int slotNumber)
        {
            if (slotNumber < 1 || slotNumber > GetSlotCount()) return false;
            if (!_slotInfo[slotNumber].IsCartDetectedAtSlot) return false;

            SetSlotSelection(slotNumber, true);
            return true;
        }

        public int GetActiveSlot() => _activeSlot;

        public ScanningState GetScanningState()
        {
            return new ScanningState
            {
                IsScannerActive = _scanTimer?.Enabled ?? false,
                CurrentMode = _currentScanMode,
                IsScanInProgress = _isScanInProgress
            };
        }

        public ScanMode GetCurrentScanMode() => _currentScanMode;

        public bool IsScannerActive() => _scanTimer?.Enabled ?? false;

        /// <summary>
        /// Set slot role for DPS master/slave operations
        /// </summary>
        /// <param name="slotNumber">Slot number (1-based)</param>
        /// <param name="role">Master, Slave, or None</param>
        public void SetSlotRole(int slotNumber, SlotRole role)
        {
            if (slotNumber < 1 || slotNumber > GetSlotCount()) return;

            lock (_lockObject)
                _slotInfo[slotNumber].IsSlotRole_ByUser = role;
        }

        /// <summary>
        /// Get master slot number for DPS operations
        /// </summary>
        /// <returns>Master slot number or 0 if no master set</returns>
        public int GetMasterSlot()
        {
            lock (_lockObject)
            {
                for (int i = 1; i <= GetSlotCount(); i++)
                {
                    if (_slotInfo[i].IsSlotRole_ByUser == SlotRole.Master &&
                        _slotInfo[i].IsSlotSelected_ByUser &&
                        _slotInfo[i].IsCartDetectedAtSlot)
                    {
                        return i;
                    }
                }

                return 0;
            }
        }

        /// <summary>
        /// Get slave slot numbers for DPS operations
        /// </summary>
        /// <returns>Array of slave slot numbers</returns>
        public int[] GetSlaveSlots()
        {
            lock (_lockObject)
            {
                var slaveSlots = new List<int>();

                for (int i = 1; i <= GetSlotCount(); i++)
                {
                    if (_slotInfo[i].IsSlotRole_ByUser == SlotRole.Slave &&
                        _slotInfo[i].IsSlotSelected_ByUser &&
                        _slotInfo[i].IsCartDetectedAtSlot)
                    {
                        slaveSlots.Add(i);
                    }
                }

                return slaveSlots.ToArray();
            }
        }

        #endregion

        #region Professional Event Handling
        void OnTransportDisconnected()
        {
            lock (_lockObject)
            {
                if (!_isConnected) return;

                _isConnected = false;
                ResetSlotsInternal();

                // Fire disconnection event
                FireHardwareDisconnectedEvent(new HardwareDetectionEventArgs
                {
                    HardwareType = _hardwareType,
                    IsConnected = false,
                    Message = $"Hardware {_hardwareType} disconnected"
                });

                CleanupTransport();
            }
        }

        void CleanupTransport()
        {
            if (_transport != null)
            {
                _transport.PortClosed -= OnTransportDisconnected;
                _transport.Dispose();
                _transport = null;
            }

            _cmdControl = null;
            _processor = null;
        }

        #endregion

        #region Professional Cleanup
        void ResetSlotsInternal()
        {
            for (int i = 1; i <= 4; i++)
                _slotInfo[i]?.Reset();

            _activeSlot = 0;
            _currentCartObj = null;
            _detectedCartTypes.Clear();
            _detectedCartTypeAtHw = CartType.Unknown;
            _totalDetectedCarts = 0;
        }

        public void ResetSlots()
        {
            lock (_lockObject)
                ResetSlotsInternal();
        }

        public async Task DisconnectAsync()
        {
            await StopScanningAsync();

            lock (_lockObject)
            {
                _isConnected = false;
                CleanupTransport();
                ResetSlotsInternal();
            }
        }

        public void Dispose()
        {
            DisconnectAsync().Wait();

            _scanTimer?.Dispose();

            // Dispose cart instances (memory leak prevention)
            foreach (var cart in _cartInstancePool.Values)
            {
                if (cart is IDisposable disposable)
                {
                    disposable.Dispose();
                }
            }

            _cartInstancePool.Clear();
            _detectedCartTypes.Clear();
        }

        #endregion

        #region Professional Legacy Support Methods
        public async Task<bool> ScanForHardwareAsync(CancellationToken cancellationToken = default)
        {
            await PerformHardwareScan();
            return _isConnected;
        }

        public async Task<bool> DetectCartsAsync(CancellationToken cancellationToken = default)
        {
            await PerformCartScan();
            return _isConnected;
        }

        public async Task<bool> DetectCartAsync(int slotNumber, CancellationToken cancellationToken = default)
        {
            await DetectCartsAsync(cancellationToken);
            return _slotInfo[slotNumber]?.IsCartDetectedAtSlot ?? false;
        }

        public async Task<bool> TriggerHardwareScanAsync() => await ScanForHardwareAsync();

        public async Task<bool> TriggerCartScanAsync() => await DetectCartsAsync();

        #endregion

        #region Cart Change Monitoring for DTCL Copy/Compare
        /// <summary>
        /// Monitor for cart change in a specific slot (DTCL only)
        /// Used during copy/compare operations when user needs to swap carts
        /// Ensures the SAME cart type is reinserted after removal
        /// </summary>
        /// <param name="slotNumber">Slot to monitor (1-3 for DTCL)</param>
        /// <param name="cancellationToken">Cancellation token to stop monitoring</param>
        /// <returns>True if same cart was successfully reinserted, false if wrong cart, cancelled or error</returns>
        public async Task<bool> WaitForCartChangeAsync(int slotNumber, CancellationToken cancellationToken)
        {
            // This function is only for DTCL hardware
            if (_hardwareType != HardwareType.DTCL)
            {
                _lastError = "Cart change monitoring is only supported for DTCL hardware";
                return false;
            }

            // Validate slot number
            if (slotNumber < 1 || slotNumber > GetSlotCount())
            {
                _lastError = $"Invalid slot number: {slotNumber}";
                return false;
            }

            try
            {
                // Save the original cart type that we expect to be reinserted
                var expectedCartType = _slotInfo[slotNumber].DetectedCartTypeAtSlot;

                if (expectedCartType == CartType.Unknown)
                {
                    _lastError = "No cart currently detected in the specified slot";
                    return false;
                }

                // Save current scanning state and stop scanning temporarily
                var wasScanning = _scanTimer?.Enabled ?? false;

                if (wasScanning)
                {
                    await StopScanningAsync();
                }

                // Monitor for cart change with polling
                var startTime = DateTime.Now;
                var timeoutMs = 300000; // 5 minute timeout for cart swap
                var pollIntervalMs = 500; // Check every 500ms
                var waitingForRemoval = true; // Start by waiting for cart removal

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check for timeout
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                    {
                        _lastError = "Cart change timeout - no cart change detected within 5 minutes";
                        return false;
                    }

                    // Perform single cart detection
                    var cartStatusCmd = CreateIspCommand(IspSubCommand.CART_STATUS, new byte[0]);
                    var response = await _cmdControl.ExecuteCmd(cartStatusCmd, (int)IspSubCmdRespLen.CART_STATUS, 2000);

                    if (response?.Length >= slotNumber)
                    {
                        var detectedType = MapResponseToCartType(response[slotNumber - 1]);

                        // Check if cart was removed (waiting for removal)
                        if (waitingForRemoval && detectedType == CartType.Unknown)
                        {
                            // Cart removed, now wait for reinsertion
                            waitingForRemoval = false;

                            // Fire event for cart removal
                            FireCartDetectedEvent(new CartDetectionEventArgs
                            {
                                SlotNumber = slotNumber,
                                CartType = CartType.Unknown,
                                Status = DetectionStatus.NotDetected,
                                Message = $"Cart removed from slot {slotNumber}, please reinsert the same {expectedCartType} cart"
                            });
                        }
                        // Check if cart was reinserted after removal
                        else if (!waitingForRemoval && detectedType != CartType.Unknown)
                        {
                            // Check if it's the SAME cart type as expected
                            if (detectedType != expectedCartType)
                            {
                                // Wrong cart type inserted!
                                _lastError = $"Wrong cart inserted! Expected {expectedCartType} but detected {detectedType} in slot {slotNumber}";

                                // Fire error event
                                FireCartDetectedEvent(new CartDetectionEventArgs
                                {
                                    SlotNumber = slotNumber,
                                    CartType = detectedType,
                                    Status = DetectionStatus.Error,
                                    Message = _lastError
                                });

                                // Resume scanning if it was active before
                                if (wasScanning)
                                {
                                    StartScanning();
                                }

                                return false; // Wrong cart - operation failed
                            }

                            // Correct cart type detected - update status
                            await UpdateSlotStatus(slotNumber, detectedType);

                            // Update hardware-level cart type
                            var detectedCount = 0;
                            var currentDetectedTypes = new HashSet<CartType>();

                            for (int i = 1; i <= GetSlotCount(); i++)
                            {
                                if (_slotInfo[i].Status == DetectionStatus.Detected)
                                {
                                    detectedCount++;
                                    currentDetectedTypes.Add(_slotInfo[i].DetectedCartTypeAtSlot);
                                }
                            }

                            UpdateDetectedCartTypeAtHw(detectedCount, currentDetectedTypes);
                            UpdateCartObjectSafely(detectedCount, currentDetectedTypes);
                            UpdateCartPath(_detectedCartTypeAtHw);

                            // Fire success event
                            FireCartDetectedEvent(new CartDetectionEventArgs
                            {
                                SlotNumber = slotNumber,
                                CartType = detectedType,
                                Status = DetectionStatus.Detected,
                                Message = $"Same cart {detectedType} successfully reinserted in slot {slotNumber}"
                            });

                            // Resume scanning if it was active before
                            if (wasScanning)
                            {
                                StartScanning();
                            }

                            return true; // Cart successfully changed
                        }
                    }

                    // Wait before next poll
                    await Task.Delay(pollIntervalMs, cancellationToken);
                }

                // Cancelled by user
                _lastError = "Cart change monitoring cancelled by user";

                // Resume scanning if it was active before
                if (wasScanning)
                {
                    StartScanning();
                }

                return false;
            }
            catch (Exception ex)
            {
                _lastError = $"Cart change monitoring error: {ex.Message}";
                return false;
            }
        }

        /// <summary>
        /// Monitor for cart presence in a specific slot
        /// Used to ensure cart is present before operations
        /// </summary>
        /// <param name="slotNumber">Slot to monitor</param>
        /// <param name="requiredCartType">Expected cart type (or Unknown to accept any)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if required cart detected, false otherwise</returns>
        public async Task<bool> WaitForCartPresenceAsync(int slotNumber, CartType requiredCartType, CancellationToken cancellationToken)
        {
            // Validate slot number
            if (slotNumber < 1 || slotNumber > GetSlotCount())
            {
                _lastError = $"Invalid slot number: {slotNumber}";
                return false;
            }

            try
            {
                var startTime = DateTime.Now;
                var timeoutMs = 60000; // 1 minute timeout
                var pollIntervalMs = 500;

                while (!cancellationToken.IsCancellationRequested)
                {
                    // Check for timeout
                    if ((DateTime.Now - startTime).TotalMilliseconds > timeoutMs)
                    {
                        _lastError = "Cart presence timeout";
                        return false;
                    }

                    // Check current slot status
                    await DetectCartsAsync(cancellationToken);

                    var slot = _slotInfo[slotNumber];

                    if (slot.Status == DetectionStatus.Detected)
                    {
                        // Check if specific cart type is required
                        if (requiredCartType == CartType.Unknown || slot.DetectedCartTypeAtSlot == requiredCartType)
                        {
                            return true;
                        }
                    }

                    await Task.Delay(pollIntervalMs, cancellationToken);
                }

                return false;
            }
            catch
            {
                return false;
            }
        }

        #region Thread-Safe Event Helpers
        /// <summary>
        /// Fire hardware detection event safely on UI thread
        /// </summary>
        void FireHardwareDetectedEvent(HardwareDetectionEventArgs eventArgs)
        {
            if (HardwareDetected != null)
            {
                try
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        HardwareDetected?.Invoke(this, eventArgs);
                    });
                }
                catch
                {
                    // Fallback if no dispatcher available (non-UI context)
                    HardwareDetected?.Invoke(this, eventArgs);
                }
            }
        }

        /// <summary>
        /// Fire hardware disconnected event safely on UI thread
        /// </summary>
        void FireHardwareDisconnectedEvent(HardwareDetectionEventArgs eventArgs)
        {
            if (HardwareDisconnected != null)
            {
                try
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        HardwareDisconnected?.Invoke(this, eventArgs);
                    });
                }
                catch
                {
                    // Fallback if no dispatcher available (non-UI context)
                    HardwareDisconnected?.Invoke(this, eventArgs);
                }
            }
        }

        /// <summary>
        /// Fire cart detection event safely on UI thread
        /// </summary>
        void FireCartDetectedEvent(CartDetectionEventArgs eventArgs)
        {
            if (CartDetected != null)
            {
                try
                {
                    System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                    {
                        CartDetected?.Invoke(this, eventArgs);
                    });
                }
                catch
                {
                    // Fallback if no dispatcher available (non-UI context)
                    CartDetected?.Invoke(this, eventArgs);
                }
            }
        }
        #endregion
    }

    public class returnCodes
    {
        public const int DTCL_OPER_NOTSTARTED = -2;
        public const int DTCL_DEVICE_NOT_FOUND = -1;
        public const int DTCL_SUCCESS = 0;
        public const int DTCL_FAILED_TO_COMMUNICATE = 1;
        public const int DTCL_NO_RESPONSE = 2;
        public const int DTCL_DOOR_OPENED = 3;
        public const int DTCL_NO_CARTRIDGE = 4;
        public const int DTCL_NOT_FORMATTED = 5;
        public const int DTCL_BAD_BLOCK = 6;
        public const int DTCL_WRONG_PARAMETERS = 7;
        public const int DTCL_BLANK_CARTRIDGE = 8;
        public const int DTCL_BLANK_CARTRIDGE2 = 9;
        public const int DTCL_CARTRIDGE_EQUAL = 10;
        public const int DTCL_CARTRIDGE_NOT_EQUAL = 11;
        public const int DTCL_FILE_NOT_FOUND = 12;
        public const int DTCL_CMD_ABORT = 13;
        public const int DTCL_MISSING_HEADER = 14;
    }
}
#endregion