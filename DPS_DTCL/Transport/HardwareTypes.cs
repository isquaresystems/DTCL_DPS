using System;

namespace DTCL
{
    /// <summary>
    /// Unified cartridge type enumeration - single source of truth
    /// Replaces multiple conflicting CartType definitions
    /// </summary>
    public enum CartType : byte
    {
        Unknown = 0,
        Darin1 = 1,    // Basic cart - DTCL slot 1
        Darin2 = 2,    // NAND flash - DTCL slot 2, DPS2 units  
        Darin3 = 3,    // Compact flash - DTCL slot 3, DPS3 units
        MultiCart = 4  // Special multi-type support
    }

    /// <summary>
    /// Hardware device type enumeration
    /// Defines the different DTCL/DPS hardware variants
    /// </summary>
    public enum HardwareType : byte
    {
        Unknown = 0,
        DTCL = 1,       // 3-slot multi-cart unit (supports Darin1/2/3)
        DPS2_4_IN_1 = 2, // 4-slot Darin2 only unit
        DPS3_4_IN_1 = 3  // 4-slot Darin3 only unit
    }

    /// <summary>
    /// Slot role for DPS copy/compare operations
    /// DTCL uses None (only one active cart at a time)
    /// </summary>
    public enum SlotRole : byte
    {
        None = 0,     // Default for DTCL, no role assigned
        Master = 1,   // Source cart for DPS operations
        Slave = 2     // Target cart for DPS operations
    }

    /// <summary>
    /// Hardware detection status
    /// </summary>
    public enum DetectionStatus : byte
    {
        NotDetected = 0,
        Detecting = 1,
        Detected = 2,
        Error = 3
    }
}