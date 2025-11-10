using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using System.Linq;
using DTCL.Cartridges;
using IspProtocol;

namespace DTCL.Transport
{
    /// <summary>
    /// Non-singleton hardware info for MUX channel operations
    /// Each MUX channel gets its own isolated instance
    /// No shared state between channels
    /// </summary>
    public class ChannelHardwareInfo : IHardwareInfo, IDisposable
    {
        #region Private Fields
        readonly object _lockObject = new object();
        readonly int _channelNumber;
        SlotInfo[] _slotInfo;
        HardwareType _hardwareType = HardwareType.Unknown;
        bool _isConnected;
        string _firmwareVersion = string.Empty;
        string _boardId = string.Empty;
        string _lastError = string.Empty;
        int _activeSlot;
        CartType _detectedCartTypeAtHw = CartType.Unknown;
        int _totalDetectedCarts;

        UartIspTransport _transport;
        IspCmdControl _cmdControl;
        IspSubCommandProcessor _processor;
        Dictionary<CartType, ICart> _cartPool;
        ICart _currentCartObj;
        bool _isHwScanInProgress;
        bool _isCartScanInProgress;
        bool _disposed;
        #endregion

        #region Public Properties
        public int ChannelNumber => _channelNumber;
        public HardwareType HardwareType => _hardwareType;

        public SlotInfo[] SlotInfo
        {
            get
            {
                lock (_lockObject) return _slotInfo;
            }
        }

        public bool IsConnected
        {
            get
            {
                lock (_lockObject) return _isConnected;
            }
        }

        public string FirmwareVersion => _firmwareVersion;
        public string BoardId => _boardId;
        public string LastError => _lastError;

        public ICart CartObj
        {
            get
            {
                lock (_lockObject)
                    return _currentCartObj;
            }
        }

        public int TotalDetectedCarts
        {
            get
            {
                lock (_lockObject)
                    return _totalDetectedCarts;
            }
        }

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
        public ChannelHardwareInfo(int channelNumber)
        {
            _channelNumber = channelNumber;
            InitializeSlots();
            InitializeCartPool();
        }
        #endregion

        #region Initialization
        void InitializeSlots()
        {
            _slotInfo = new SlotInfo[5]; // [0] unused, [1-4] for slots

            // Initialize default configuration
            for (int i = 0; i <= 4; i++)
                _slotInfo[i] = new SlotInfo(i);
        }

        void InitializeCartPool()
        {
            // Pre-create instances to prevent memory leaks
            _cartPool = new Dictionary<CartType, ICart>
            {
                { CartType.Darin2, new Darin2() },
                { CartType.Darin3, new Darin3() }
                // Note: Darin1 commented out as per original code
            };
        }

        #endregion

        #region Hardware Scanning
        public async Task<bool> ScanForHardwareAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || _isHwScanInProgress)
                return _isConnected;

            _isHwScanInProgress = true;

            try
            {
                // For MUX channels, assume hardware is already switched to this channel
                // Just need to establish communication protocol
                if (await EstablishChannelConnection(cancellationToken))
                {
                    lock (_lockObject)
                        _isConnected = true;

                    // Fire event on UI thread to prevent cross-thread access violations
                    var eventArgs = new HardwareDetectionEventArgs
                    {
                        HardwareType = _hardwareType,
                        IsConnected = true,
                        Message = $"Channel {_channelNumber}: Connected to {_hardwareType}"
                    };

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

                    return true;
                }

                lock (_lockObject)
                {
                    _isConnected = false;
                    _lastError = $"Channel {_channelNumber}: No hardware response";
                }

                return false;
            }
            catch (Exception ex)
            {
                _lastError = $"Channel {_channelNumber}: Hardware scan error - {ex.Message}";
                return false;
            }
            finally
            {
                _isHwScanInProgress = false;
            }
        }

        async Task<bool> EstablishChannelConnection(CancellationToken cancellationToken)
        {
            try
            {
                // MUX channels use the same COM port but switched hardware
                // We need to detect which COM port the MUX is on
                var availablePorts = System.IO.Ports.SerialPort.GetPortNames();

                foreach (var port in availablePorts)
                {
                    if (cancellationToken.IsCancellationRequested)
                        break;

                    try
                    {
                        _transport = new UartIspTransport(port);
                        _transport.Open();

                        if (!_transport.isPortOpen)
                        {
                            _transport.Dispose();
                            continue;
                        }

                        // Subscribe to port events for disconnect handling
                        _transport.PortClosed += OnTransportDisconnected;

                        // Initialize ISP communication
                        _processor = new IspSubCommandProcessor();
                        _cmdControl = new IspCmdControl(_transport, _processor);

                        // Try to communicate with hardware on this channel
                        var boardIdCmd = CreateIspCommand(IspSubCommand.BOARD_ID, new byte[0]);
                        var boardIdResponse = await _cmdControl.ExecuteCmd(boardIdCmd, (int)IspSubCmdRespLen.BOARD_ID, 2000);

                        if (boardIdResponse != null && boardIdResponse.Length > 0)
                        {
                            var boardId = (IspBoardId)boardIdResponse[0];
                            _boardId = boardId.ToString();

                            // Set hardware type based on board ID
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

                            // Get firmware version
                            var versionCmd = CreateIspCommand(IspSubCommand.FIRMWARE_VERSION, new byte[0]);
                            var versionResponse = await _cmdControl.ExecuteCmd(versionCmd, (int)IspSubCmdRespLen.FIRMWARE_VERSION, 2000);

                            if (versionResponse != null && versionResponse.Length >= 2)
                            {
                                _firmwareVersion = $"{versionResponse[0]}.{versionResponse[1]}";
                            }

                            // Initialize DataHandlerIsp for this channel
                            DataHandlerIsp.Instance.Initialize(_transport, null);

                            ConfigureHardwareSpecificSettings();
                            return true;
                        }

                        // If no response, disconnect and try next port
                        _transport.PortClosed -= OnTransportDisconnected;
                        _transport.Dispose();
                        _transport = null;
                    }
                    catch
                    {
                        // Try next port
                        if (_transport != null)
                        {
                            _transport.PortClosed -= OnTransportDisconnected;
                            _transport.Dispose();
                            _transport = null;
                        }
                    }
                }
            }
            catch (Exception ex)
            {
                _lastError = $"Channel {_channelNumber}: Connection error - {ex.Message}";
                _transport?.Dispose();
                _transport = null;
            }

            return false;
        }

        void ConfigureHardwareSpecificSettings()
        {
            switch (_hardwareType)
            {
                case HardwareType.DTCL:
                    // 3 slots: Darin1, Darin2, Darin3
                    // Only one cart active at a time
                    break;

                case HardwareType.DPS2_4_IN_1:
                    // 4 slots, all Darin2
                    for (int i = 1; i <= 4; i++)
                        _slotInfo[i].DetectedCartTypeAtSlot = CartType.Darin2;

                    break;

                case HardwareType.DPS3_4_IN_1:
                    // 4 slots, all Darin3
                    for (int i = 1; i <= 4; i++)
                        _slotInfo[i].DetectedCartTypeAtSlot = CartType.Darin3;

                    break;
            }
        }

        #endregion

        #region Cart Detection
        public async Task<bool> DetectCartsAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed || !_isConnected || _isCartScanInProgress)
                return false;

            _isCartScanInProgress = true;

            try
            {
                // Send cart status command
                var cartStatusCmd = CreateIspCommand(IspSubCommand.CART_STATUS, new byte[0]);
                var response = await _cmdControl.ExecuteCmd(cartStatusCmd, (int)IspSubCmdRespLen.CART_STATUS, 2000);

                if (response != null && response.Length >= GetSlotCount())
                {
                    await ProcessCartDetectionResponse(response);
                    return true;
                }

                return false;
            }
            catch (Exception ex)
            {
                _lastError = $"Channel {_channelNumber}: Cart detection error - {ex.Message}";
                return false;
            }
            finally
            {
                _isCartScanInProgress = false;
            }
        }

        public async Task<bool> DetectCartAsync(int slotNumber, CancellationToken cancellationToken = default)
        {
            // For now, detect all carts and return status for specific slot
            await DetectCartsAsync(cancellationToken);
            return _slotInfo[slotNumber]?.IsCartDetectedAtSlot ?? false;
        }

        async Task ProcessCartDetectionResponse(byte[] data)
        {
            var slotCount = GetSlotCount();
            var detectedCount = 0;
            var currentDetectedTypes = new HashSet<CartType>();
            var detectedSlots = new List<int>();

            // First pass: Count total detected carts across all slots
            for (int i = 0; i < slotCount; i++)
            {
                if (data[i] != 0) // Any non-zero value indicates cart detection
                {
                    detectedCount++;
                    detectedSlots.Add(i + 1);
                }
            }

            // Check for multi-cart scenario before individual processing
            var isMultiCartScenario = detectedCount > 1 && _hardwareType == HardwareType.DTCL;

            if (isMultiCartScenario)
            {
                Log.Log
                    .Info($"Channel {_channelNumber}: Multi-cart scenario detected - {detectedCount} carts in slots: {string.Join(", ", detectedSlots)}");
            }

            // Process each slot's detection data
            for (int i = 0; i < slotCount; i++)
            {
                var slotIndex = i + 1;
                CartType detectedType;

                if (isMultiCartScenario)
                {
                    // For multi-cart scenarios, set all detected slots to MultiCart type
                    detectedType = data[i] != 0 ? CartType.MultiCart : CartType.Unknown;

                    if (data[i] != 0)
                    {
                        Log.Log.Info($"Channel {_channelNumber}: Slot {slotIndex} set to MultiCart (original response: {data[i]})");
                    }
                }
                else
                {
                    // Normal single cart detection
                    detectedType = MapResponseToCartType(data[i]);

                    if (detectedType != CartType.Unknown)
                    {
                        Log.Log.Info($"Channel {_channelNumber}: Slot {slotIndex} detected cart type: {detectedType}");
                    }
                }

                await UpdateSlotStatus(slotIndex, detectedType);

                if (detectedType != CartType.Unknown)
                {
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

            // Handle cart detection event
            HandleCartDetectionEvent(detectedCount, currentDetectedTypes);
        }

        CartType MapResponseToCartType(byte responseValue)
        {
            switch (responseValue)
            {
                case 0:
                    return CartType.Unknown; // No cart detected
                case 1:
                    return CartType.Darin1;
                case 2:
                    return CartType.Darin2;
                case 3:
                    return CartType.Darin3;
                default:
                    // Handle unexpected response values
                    Log.Log.Warning($"Channel {_channelNumber}: Unexpected cart detection response value: {responseValue}");
                    return CartType.Unknown;
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
                var cartEventArgs = new CartDetectionEventArgs
                {
                    SlotNumber = slotIndex,
                    CartType = detectedType,
                    Status = slot.Status,
                    Message = detectedType != CartType.Unknown
                        ? $"Channel {_channelNumber}: Cart {detectedType} detected in slot {slotIndex}"
                        : $"Channel {_channelNumber}: No cart in slot {slotIndex}"
                };

                if (CartDetected != null)
                {
                    try
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CartDetected?.Invoke(this, cartEventArgs);
                        });
                    }
                    catch
                    {
                        // Fallback if no dispatcher available (non-UI context)
                        CartDetected?.Invoke(this, cartEventArgs);
                    }
                }
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

                                    if (newCartObj != null)
                                    {
                                        DataHandlerIsp.Instance.RegisterSubCommandHandlers(newCartObj);
                                    }

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

                                if (newCartObj != null)
                                {
                                    DataHandlerIsp.Instance.RegisterSubCommandHandlers(newCartObj);
                                }

                                break;
                            }
                        }

                        // For no cart in DPS, CartObj is null
                        break;
                }

                // Update current cart object (memory safe - reusing pre-created instances)
                _currentCartObj = newCartObj;
            }
        }

        void HandleCartDetectionEvent(int detectedCount, HashSet<CartType> currentDetectedTypes)
        {
            if (_hardwareType == HardwareType.DTCL && detectedCount > 1)
            {
                // Multi-cart detected in DTCL - fire special event for UI handling
                var multiCartEventArgs = new CartDetectionEventArgs
                {
                    SlotNumber = 0, // Indicates multi-cart scenario
                    CartType = CartType.MultiCart,
                    Status = DetectionStatus.Error,
                    Message = $"Channel {_channelNumber}: Multiple carts detected in DTCL ({detectedCount} carts)"
                };

                if (CartDetected != null)
                {
                    try
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CartDetected?.Invoke(this, multiCartEventArgs);
                        });
                    }
                    catch
                    {
                        // Fallback if no dispatcher available (non-UI context)
                        CartDetected?.Invoke(this, multiCartEventArgs);
                    }
                }
            }
            else
            {
                // Fire cart detection summary event for all hardware types
                var cartTypes = string.Join(", ", currentDetectedTypes);

                var message = detectedCount == 0
                    ? $"Channel {_channelNumber}: No carts detected"
                    : $"Channel {_channelNumber}: {detectedCount} cart(s) detected: {cartTypes}";

                var summaryEventArgs = new CartDetectionEventArgs
                {
                    SlotNumber = 0, // Summary event
                    CartType = detectedCount == 1 ? currentDetectedTypes.First() : CartType.Unknown,
                    Status = detectedCount > 0 ? DetectionStatus.Detected : DetectionStatus.NotDetected,
                    Message = message
                };

                if (CartDetected != null)
                {
                    try
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            CartDetected?.Invoke(this, summaryEventArgs);
                        });
                    }
                    catch
                    {
                        // Fallback if no dispatcher available (non-UI context)
                        CartDetected?.Invoke(this, summaryEventArgs);
                    }
                }
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

        #region Cart Management
        ICart GetCartInstanceSafely(CartType cartType)
        {
            return _cartPool.TryGetValue(cartType, out var cart) ? cart : null;
        }

        public ICart GetCartInstance(CartType cartType) => GetCartInstanceSafely(cartType);

        public bool SetActiveSlot(int slotNumber)
        {
            if (_disposed || slotNumber < 1 || slotNumber > GetSlotCount())
                return false;

            if (!_slotInfo[slotNumber].IsCartDetectedAtSlot)
                return false;

            SetSlotSelection(slotNumber, true);
            return true;
        }

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

                UpdateCartObjectSafely(detectedCount, currentDetectedTypes);
            }
        }

        public void SetSlotRole(int slotNumber, SlotRole role)
        {
            if (slotNumber < 1 || slotNumber > GetSlotCount()) return;

            lock (_lockObject)
                _slotInfo[slotNumber].IsSlotRole_ByUser = role;
        }

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

        public int GetActiveSlot() => _activeSlot;

        public void ResetSlots()
        {
            if (_disposed) return;

            for (int i = 1; i <= 4; i++)
                _slotInfo[i]?.Reset();

            _activeSlot = 0;
            _currentCartObj = null;
            _detectedCartTypeAtHw = CartType.Unknown;
            _totalDetectedCarts = 0;
        }

        #endregion

        #region Channel State Management
        /// <summary>
        /// Reset channel state when MUX switches away from this channel
        /// </summary>
        public void OnChannelDeactivated()
        {
            if (_disposed) return;

            lock (_lockObject)
                _isConnected = false;

            var res = LedState.FirmwareCtrlLed();
            Log.Log.Info($"Mux Channel Firmware Ctrl Led Executed and disposed");

            // Don't dispose transport - just disconnect
            _transport?.Close();
        }

        /// <summary>
        /// Re-establish connection when MUX switches back to this channel
        /// </summary>
        public async Task<bool> OnChannelActivatedAsync(CancellationToken cancellationToken = default)
        {
            if (_disposed) return false;

            return await ScanForHardwareAsync(cancellationToken);
        }

        /// <summary>
        /// Get current channel state summary
        /// </summary>
        public string GetChannelState()
        {
            if (_disposed) return "Disposed";

            var connectedCarts = 0;

            for (int i = 1; i <= GetSlotCount(); i++)
            {
                if (_slotInfo[i]?.IsCartDetectedAtSlot == true)
                    connectedCarts++;
            }

            return $"Channel {_channelNumber}: {_hardwareType} - {connectedCarts} carts - {(_isConnected ? "Connected" : "Disconnected")}";
        }

        #endregion

        #region Cleanup
        public async Task DisconnectAsync()
        {
            if (_disposed) return;

            if (_transport != null)
            {
                _transport.Close();
                _transport.Dispose();
                _transport = null;
            }

            lock (_lockObject)
                _isConnected = false;

            ResetSlots();
        }

        public void Dispose()
        {
            if (_disposed) return;

            _disposed = true;

            DisconnectAsync().Wait();

            foreach (var cart in _cartPool.Values)
                if (cart is IDisposable disposable)
                    disposable.Dispose();

            _cartPool.Clear();
        }

        #endregion

        #region Helper Methods
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

        void OnTransportDisconnected()
        {
            lock (_lockObject)
            {
                if (!_isConnected) return;

                _isConnected = false;
                ResetSlots();

                // Fire disconnection event
                var disconnectEventArgs = new HardwareDetectionEventArgs
                {
                    HardwareType = _hardwareType,
                    IsConnected = false,
                    Message = $"Channel {_channelNumber}: Hardware {_hardwareType} disconnected"
                };

                if (HardwareDisconnected != null)
                {
                    try
                    {
                        System.Windows.Application.Current?.Dispatcher?.Invoke(() =>
                        {
                            HardwareDisconnected?.Invoke(this, disconnectEventArgs);
                        });
                    }
                    catch
                    {
                        // Fallback if no dispatcher available (non-UI context)
                        HardwareDisconnected?.Invoke(this, disconnectEventArgs);
                    }
                }

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
    }
}