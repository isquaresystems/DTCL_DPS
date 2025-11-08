using System;
using System.Threading;
using System.Threading.Tasks;
using DTCL.Cartridges;

namespace DTCL
{
    /// <summary>
    /// Hardware abstraction interface - replaces DPSInfo
    /// Supports both singleton (standalone) and non-singleton (MUX) patterns
    /// </summary>
    public interface IHardwareInfo
    {
        /// <summary>
        /// Type of hardware device (DTCL, DPS2, DPS3)
        /// </summary>
        HardwareType HardwareType { get; }

        /// <summary>
        /// Array of slot information (1-based indexing)
        /// DTCL: [0]=unused, [1]=Darin1, [2]=Darin2, [3]=Darin3
        /// DPS: [0]=unused, [1-4]=cart slots
        /// </summary>
        SlotInfo[] SlotInfo { get; }

        /// <summary>
        /// Whether hardware is connected and responding
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// Firmware version of connected hardware
        /// </summary>
        string FirmwareVersion { get; }

        /// <summary>
        /// Hardware board ID
        /// </summary>
        string BoardId { get; }

        /// <summary>
        /// Last communication error
        /// </summary>
        string LastError { get; }

        /// <summary>
        /// Scan for hardware and establish connection
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if hardware found and connected</returns>
        Task<bool> ScanForHardwareAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Detect cartridges in all slots
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if detection completed successfully</returns>
        Task<bool> DetectCartsAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Detect cartridge in specific slot
        /// </summary>
        /// <param name="slotNumber">Slot number (1-based)</param>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if detection completed successfully</returns>
        Task<bool> DetectCartAsync(int slotNumber, CancellationToken cancellationToken = default);

        /// <summary>
        /// Get cart instance for operations
        /// </summary>
        /// <param name="cartType">Type of cart</param>
        /// <returns>Cart interface instance</returns>
        ICart GetCartInstance(CartType cartType);

        /// <summary>
        /// Set active slot for DTCL (only one cart active at a time)
        /// For DPS, multiple slots can be active
        /// </summary>
        /// <param name="slotNumber">Slot number (1-based)</param>
        /// <returns>True if slot activated successfully</returns>
        bool SetActiveSlot(int slotNumber);

        /// <summary>
        /// Get the currently active slot number
        /// DTCL: Returns single active slot or 0 if none
        /// DPS: Returns first active slot or 0 if none
        /// </summary>
        int GetActiveSlot();

        /// <summary>
        /// Reset all slot information
        /// </summary>
        void ResetSlots();

        /// <summary>
        /// Disconnect from hardware
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Hardware detection/connection event
        /// </summary>
        event EventHandler<HardwareDetectionEventArgs> HardwareDetected;

        /// <summary>
        /// Hardware disconnection event
        /// </summary>
        event EventHandler<HardwareDetectionEventArgs> HardwareDisconnected;

        /// <summary>
        /// Cart detection event
        /// </summary>
        event EventHandler<CartDetectionEventArgs> CartDetected;
    }

    /// <summary>
    /// Transport connection interface
    /// Separates transport concerns from hardware logic
    /// </summary>
    public interface ITransportConnection
    {
        /// <summary>
        /// Whether transport is connected
        /// </summary>
        bool IsConnected { get; }

        /// <summary>
        /// COM port name (if applicable)
        /// </summary>
        string PortName { get; }

        /// <summary>
        /// Connect to transport
        /// </summary>
        /// <param name="cancellationToken">Cancellation token</param>
        /// <returns>True if connected successfully</returns>
        Task<bool> ConnectAsync(CancellationToken cancellationToken = default);

        /// <summary>
        /// Disconnect from transport
        /// </summary>
        Task DisconnectAsync();

        /// <summary>
        /// Get transport interface for protocol operations
        /// </summary>
        /// <returns>Transport interface</returns>
        object GetTransport();  // Return object since ITransport is internal to ISP protocol

        /// <summary>
        /// Scan for available ports
        /// </summary>
        /// <returns>Array of available port names</returns>
        string[] ScanAvailablePorts();
    }

    /// <summary>
    /// Event arguments for hardware detection events
    /// </summary>
    public class HardwareDetectionEventArgs : EventArgs
    {
        public HardwareType HardwareType { get; set; }
        public bool IsConnected { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Event arguments for cart detection events
    /// </summary>
    public class CartDetectionEventArgs : EventArgs
    {
        public int SlotNumber { get; set; }
        public CartType CartType { get; set; }
        public DetectionStatus Status { get; set; }
        public string Message { get; set; }
    }

    /// <summary>
    /// Scanning mode enumeration
    /// </summary>
    public enum ScanMode
    {
        /// <summary>
        /// Scanning for hardware devices (when not connected)
        /// </summary>
        Hardware,

        /// <summary>
        /// Scanning for cartridges (when hardware is connected)
        /// </summary>
        Cartridge
    }

    /// <summary>
    /// Current scanning state information
    /// </summary>
    public class ScanningState
    {
        /// <summary>
        /// Whether the scanning timer is enabled
        /// </summary>
        public bool IsScannerActive { get; set; }

        /// <summary>
        /// Current scanning mode (Hardware or Cartridge)
        /// </summary>
        public ScanMode CurrentMode { get; set; }

        /// <summary>
        /// Whether a scan operation is currently in progress
        /// </summary>
        public bool IsScanInProgress { get; set; }

        /// <summary>
        /// Whether currently scanning for hardware
        /// </summary>
        public bool IsHardwareScanning => IsScannerActive && CurrentMode == ScanMode.Hardware;

        /// <summary>
        /// Whether currently scanning for cartridges
        /// </summary>
        public bool IsCartridgeScanning => IsScannerActive && CurrentMode == ScanMode.Cartridge;
    }
}