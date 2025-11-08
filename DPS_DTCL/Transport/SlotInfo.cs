using System;

namespace DTCL
{
    /// <summary>
    /// Simplified slot information data model with essential parameters only
    /// Focuses on core functionality: detection status, cart type, and UI selection
    /// </summary>
    public class SlotInfo
    {
        /// <summary>
        /// Physical slot number (1-based indexing)
        /// DTCL: 1=Darin1, 2=Darin2, 3=Darin3
        /// DPS: 1-4 for respective cart types
        /// </summary>
        public int SlotNumber { get; set; }

        /// <summary>
        /// Single source of truth for cart detection state
        /// NotDetected = No cart present
        /// Detected = Cart present and operational
        /// Error = Cart present but failed initialization
        /// </summary>
        public DetectionStatus Status { get; set; } = DetectionStatus.NotDetected;

        /// <summary>
        /// Single source of truth for cart detection state
        /// NotDetected = No cart present
        /// Detected = Cart present and operational
        /// Error = Cart present but failed initialization
        /// </summary>
        public bool IsCartDetectedAtSlot { get; set; } = false;

        /// <summary>
        /// UI selection state - whether this slot is selected for operations
        /// DTCL: Only one slot can be selected at a time
        /// DPS: Multiple slots can be selected simultaneously
        /// Updated by UI based on user selection during runtime
        /// </summary>
        public bool IsSlotSelected_ByUser { get; set; } = false;

        /// <summary>
        /// Type of cartridge detected in this slot
        /// Unknown when Status is NotDetected
        /// Specific cart type when Status is Detected or Error
        /// </summary>
        public CartType DetectedCartTypeAtSlot { get; set; } = CartType.Unknown;

        /// <summary>
        /// Master/Slave role for DPS copy/compare operations
        /// Updated by UI when user selects master/slave checkboxes
        /// DTCL: Always None (no master/slave concept for single cart operations)
        /// DPS: Master (source slot) or Slave (target slot) or None (not participating)
        /// </summary>
        public SlotRole IsSlotRole_ByUser { get; set; } = SlotRole.None;

        /// <summary>
        /// PC log name for this slot
        /// Used for logging and file operations specific to this slot
        /// </summary>
        public string SlotPCLogName { get; set; } = string.Empty;

        /// <summary>
        /// Constructor
        /// </summary>
        public SlotInfo()
        {
        }

        /// <summary>
        /// Constructor with slot number
        /// </summary>
        /// <param name="slotNumber">1-based slot number</param>
        public SlotInfo(int slotNumber) => SlotNumber = slotNumber;

        /// <summary>
        /// Reset slot to default state
        /// </summary>
        public void Reset()
        {
            DetectedCartTypeAtSlot = CartType.Unknown;
            Status = DetectionStatus.NotDetected;
            IsCartDetectedAtSlot = false;
            IsSlotSelected_ByUser = false;
            IsSlotRole_ByUser = SlotRole.None;
            SlotPCLogName = string.Empty;
        }

        /// <summary>
        /// Mark cart as detected and operational
        /// </summary>
        /// <param name="cartType">Detected cart type</param>
        public void SetDetected(CartType cartType)
        {
            DetectedCartTypeAtSlot = cartType;
            Status = DetectionStatus.Detected;
            IsCartDetectedAtSlot = true;
        }

        /// <summary>
        /// Mark cart as not present
        /// </summary>
        public void SetNotPresent()
        {
            DetectedCartTypeAtSlot = CartType.Unknown;
            Status = DetectionStatus.NotDetected;
            IsCartDetectedAtSlot = false;
            IsSlotSelected_ByUser = false;
            // Note: IsSlotRole_ByUser is NOT reset here - UI controls master/slave selection independently
        }

        /// <summary>
        /// Mark cart as error state
        /// </summary>
        /// <param name="error">Error message</param>
        public void SetError(string error)
        {
            Status = DetectionStatus.Error;
            IsCartDetectedAtSlot = false;
            // Keep DetectedCartType even in error state for troubleshooting
        }

        /// <summary>
        /// String representation for debugging
        /// </summary>
        public override string ToString()
        {
            return $"Slot {SlotNumber}: {DetectedCartTypeAtSlot} - {Status} - Selected: {IsSlotSelected_ByUser} - Role: {IsSlotRole_ByUser} - LogName: {SlotPCLogName}";
        }
    }
}