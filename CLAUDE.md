# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

---

## 🚀 QUICK START FOR NEW SESSION

**If you're starting a new session, READ THIS FIRST:**

### Step 1: Read This File (CLAUDE.md)
You're reading it now. This file contains:
- Project overview and architecture
- Current development status
- Critical issues and known bugs
- Development guidelines and best practices
- Session continuity notes

### Step 2: Review Architecture Documentation
**Location: `docs/` folder**

Read in this order:
1. **`docs/DTCL_GUI_Architecture.md`** - Complete GUI architecture (1,290 lines)
   - Section 11 is CRITICAL: "DPS MUX Implementation (February 2026)"
   - Contains all recent implementations (Select All, Cart Validation, etc.)
   - Has architecture diagrams, code examples, and critical fixes

2. **`docs/DTCL_Firmware_Architecture.md`** - STM32 firmware architecture (825 lines)
   - Hardware configuration and pin maps
   - ISP protocol implementation
   - Command processing flows
   - Cartridge interface details

### Step 3: Check Known Issues
**Location: `DPS_DTCL/Mux/CRITICAL_ISSUES.md`**
- DTCL MUX Window known critical bugs (MUST READ before touching MUX code!)
- Singleton disposal anti-pattern
- DpsInfo instance management issues

### Step 4: Review README for Build Instructions
**Location: `README.md`**
- How to build GUI and firmware
- Version management
- Release process

### Step 5: Quick Reference Sections (For Any Bug Fix)
Jump to these sections in THIS FILE (CLAUDE.md):
- **"Bug Fixing Quick Reference"** (line ~729) - Subsystem navigation, file locations
- **"ISP Protocol Command Flow"** (line ~743) - Protocol debugging
- **"Cartridge Operation Patterns"** (line ~810) - Darin2/Darin3 operations
- **"Hardware State Management"** (line ~890) - HardwareInfo singleton
- **"Data Transfer Patterns"** (line ~940) - Chunked transfer details
- **"Thread Safety Checklist"** (line ~995) - Async/await patterns
- **"Common Bug Patterns by Symptom"** (line ~1050) - Quick diagnosis
- **"Error Handling Patterns"** (line ~1165) - Standard error handling
- **"Diagnostic Flowchart"** (line ~1230) - Step-by-step debugging process
- **"DPS MUX Quick Reference"** (line ~1350) - DPS MUX code snippets
- **"Critical Lessons from DPS MUX Implementation"** (line ~1260) - Avoid common mistakes

---

# DTCL - Data Transfer Cartridge Loader System

**Professional data programming system for cartridge-based storage devices**  
**Developed by ISquare Systems**

---

## Project Overview

**DTCL (Data Transfer Cartridge Loader)** is a professional data programming system for cartridge-based data storage devices, featuring a hybrid architecture with both C# WPF GUI and STM32 embedded firmware.

### Core Components
- **DTCL GUI**: C# WPF application for user interface and control
- **DTCL Firmware**: STM32-based firmware for hardware control
- **ISP Protocol**: Communication protocol between GUI and hardware (via USB COM port)
- **MUX Support**: 8-channel multiplexer for testing multiple DTCL units
- **TestConsole**: Interactive CLI testing tool for hardware operations (bypasses GUI)

### Hardware Configuration
- **MCU**: STM32F411VET6 (Cortex-M4, 512KB Flash, 128KB RAM)
- **Interface**: USB CDC for PC communication
- **Cartridge Support**: Darin-I, Darin-II, Darin-III cartridges
- **Multi-slot Variants**:
  - **DPS2 4IN1**: 4 NAND Flash slots (Darin-II)
  - **DPS3 4IN1**: 4 Compact Flash slots (Darin-III)
  - **DTCL Hybrid**: 2 slots (1 NAND + 1 CF)

### Supported Cartridge Types
1. **Darin-I**: Basic data cartridge with standard operations
2. **Darin-II**: Enhanced cartridge with NAND flash support  
3. **Darin-III**: Advanced cartridge with Compact Flash and FatFS

---

## Architecture Documentation

### Complete Documentation Available
The project includes comprehensive architecture documentation with fully integrated block diagrams located in the **docs/** folder:

1. **[docs/DTCL_Firmware_Architecture.md](docs/DTCL_Firmware_Architecture.md)**
   - Complete STM32 firmware architecture with integrated flow diagrams
   - Boot sequence and system initialization flows
   - Command processing and routing diagrams
   - NAND Flash (Darin2) operation sequences
   - Compact Flash (Darin3) operation workflows
   - LED state management diagrams
   - Hardware pin configurations and memory maps
   
2. **[docs/DTCL_GUI_Architecture.md](docs/DTCL_GUI_Architecture.md)**
   - WPF application architecture with system diagrams
   - Application startup and lifecycle flows
   - Hardware detection and scanning processes
   - Operation execution sequences
   - MUX window operation workflows
   - Performance check flow diagrams
   - Threading and concurrency models
   - End-to-end data flow visualizations
   - Protocol state machines
   - Error handling and recovery mechanisms

### Communication Architecture
- **Protocol**: ISP Protocol for command/response communication
- **Transport**: USB COM port (USB CDC-ACM)
- **Frame Format**: START(0x7E) + LENGTH + PAYLOAD + CRC8 + END(0x7F)
- **Max Frame Size**: 64 bytes
- **Data Payload**: 56 bytes per packet
- **Buffer Size**: 1023 bytes for chunk transfers

---

## Project Structure

### C# GUI Application (DPS_DTCL/)
```
DPS_DTCL/
├── MainWindow.xaml(.cs)        # Main application window
├── Cartridges/                 # Cartridge implementations
│   ├── ICart.cs               # Cartridge interface
│   ├── Darin1.cs              # Darin-I implementation
│   ├── Darin2.cs              # Darin-II (NAND flash)
│   ├── Darin3.cs              # Darin-III (Compact flash)
│   └── PerformanceCheck.cs    # Performance testing
├── IspProtocol/               # Protocol implementation (C# side)
│   ├── IspProtocolDefs.cs     # Protocol definitions
│   ├── UartIspTransport.cs    # UART transport layer
│   ├── IspCommandManager.cs   # Command management
│   └── IspFramingUtils.cs     # Frame encode/decode
├── Transport/                 # Hardware abstraction
│   ├── HardwareInfo.cs        # Hardware manager (Singleton)
│   ├── SlotInfo.cs           # Slot state management
│   └── ChannelHardwareInfo.cs # MUX channel management
├── Mux/                       # Multiplexer support
│   ├── MuxWindow.xaml(.cs)   # DTCL MUX window (2 slots: 1 D2 + 1 D3)
│   ├── MuxManager.cs         # DTCL MUX business logic
│   ├── MuxChannelManager.cs  # DTCL MUX channel management
│   ├── DPSMuxWindow.xaml(.cs) # DPS MUX window (4 slots per channel) ✅ NEW
│   ├── DPSMuxManager.cs      # DPS MUX manager (8 channels × 4 slots) ✅ NEW
│   └── DPSMuxChannelInfo.cs  # DPS MUX channel data model ✅ NEW
├── Messages/                  # File operation structures
├── Log/                       # Logging and performance tracking
└── DataHandler/              # Data transfer management
```

### STM32 Firmware (DTCL/)
```
DTCL/
├── Core/
│   ├── Inc/
│   │   ├── main.h            # Main header
│   │   └── version.h         # Version definitions
│   └── Src/
│       ├── main.cpp          # Application entry point
│       ├── Darin2.cpp(.h)    # NAND flash handler
│       ├── Darin3.cpp(.h)    # Compact flash handler
│       ├── Protocol/         # ISP Protocol (firmware side)
│       │   ├── IspCommandManager.cpp
│       │   ├── IspFramingUtils.h
│       │   ├── SerialTransport.cpp
│       │   └── IspProtocolDefs.h
│       └── FAT/              # FatFS for Darin-III
├── USB_DEVICE/               # USB CDC implementation
├── Drivers/                  # STM32 HAL drivers
├── Middlewares/              # USB middleware
└── Makefile                  # Build configuration
```

### TestConsole (Interactive CLI Testing Tool)
```
TestConsole/
├── Program.cs                # Main console application
└── TestConsole.csproj        # Project configuration
```

**Purpose**: Interactive command-line tool for hardware testing without GUI dependencies

**Features**:
- **Interactive COM port selection** - Lists available ports on startup
- **Automatic board detection** - Detects DTCL, DPS2, or DPS3 hardware
- **Automatic slot scanning** - Detects which slots have carts inserted
- **Adaptive menus** - Shows only relevant operations based on detected hardware:
  - DPS2 4IN1: Darin-2 operations only (Erase, Write, Read)
  - DPS3 4IN1: Darin-3 operations only (Format, Erase, Write, Read)
  - DTCL: Both Darin-2 and Darin-3 operations
- **Smart slot selection** - For DPS hardware, shows only detected slots with cart types
- **DTCL auto-execution** - Automatically runs on all detected slots without prompting
- **Iteration support** - Configurable number of iterations for stress testing
- **Run all tests** - Execute all operations sequentially
- **Per-slot results** - Individual pass/fail tracking for each slot

**Use Cases**:
- Quick hardware verification without launching GUI
- Automated testing and stress testing
- Debugging ISP protocol issues
- Production line testing
- CI/CD integration for hardware validation

**Usage**:
```bash
cd TestConsole
dotnet run
# or
TestConsole.exe
```

**Workflow**:
1. App lists COM ports → User selects port
2. Connects and detects board type (DTCL/DPS2/DPS3)
3. Shows adaptive menu based on detected hardware
4. Scans for inserted carts and shows detected slots
5. User selects operation → DTCL runs automatically, DPS prompts for slot selection
6. User enters iteration count → Operations execute
7. Results displayed per slot

---

## Development Workflow & Common Commands

### C# GUI Development
```bash
# Build the GUI application
cd DPS_DTCL
msbuild DTCL.sln /p:Configuration=Debug
# or open in Visual Studio 2022

# Release build
msbuild DTCL.sln /p:Configuration=Release

# Restore NuGet packages (if missing)
nuget restore DTCL.sln

# Run the application
bin\Debug\DTCL.exe        # Debug version
bin\Release\DTCL.exe      # Release version

# Clean build
msbuild DTCL.sln /t:Clean /p:Configuration=Debug
msbuild DTCL.sln /t:Rebuild /p:Configuration=Release
```

### TestConsole Development
```bash
# Build TestConsole
cd TestConsole
dotnet build

# Run TestConsole
dotnet run
# or
bin\Debug\net48\TestConsole.exe

# Build Release
dotnet build -c Release
bin\Release\net48\TestConsole.exe
```

### STM32 Firmware Development
```bash
# Individual firmware builds
# DTCL Firmware (Hybrid D2+D3)
cd DTCL
make clean all
make VERSION_MAJOR=3 VERSION_MINOR=6 clean all  # Custom version

# D2 Firmware (NAND Flash - 4 slots)
cd Firmware/D2_DPS_4IN1
make clean all
make VERSION_MAJOR=3 VERSION_MINOR=6 clean all

# D3 Firmware (Compact Flash - 4 slots)
cd Firmware/D3_DPS_4IN1
make clean all
make VERSION_MAJOR=3 VERSION_MINOR=6 clean all

# Build all firmware projects with same version (recommended)
cd Scripts
build_all.bat all 3 6      # Build all (D2, D3, DTCL) with version 3.6
build_all.bat both 3 6     # Build only D2 and D3 with version 3.6
build_all.bat dtcl 3 6     # Build only DTCL with version 3.6

# Generate specific formats
make hex    # Generate .hex file
make bin    # Generate .bin file

# Update VSCode configurations
make update-json
```

### Architecture Review Commands
```bash
# View firmware architecture (includes all firmware diagrams)
cat docs/DTCL_Firmware_Architecture.md

# View GUI architecture (includes all GUI diagrams) 
cat docs/DTCL_GUI_Architecture.md
```

---

## Key Design Patterns & Architecture

### GUI Architecture
- **Pattern**: MVVM with code-behind
- **Framework**: WPF (.NET Framework 4.8)
- **Threading**: Async/await with UI dispatcher
- **Communication**: Singleton HardwareInfo with event-driven updates
- **Memory Management**: Object pooling for cartridge instances

### Firmware Architecture
- **Pattern**: Layered architecture with command routing
- **Language**: Mixed C/C++ (C++ for application, C for HAL)
- **Communication**: Interrupt-driven USB with ring buffers
- **Commands**: ISP protocol with subcommand routing
- **Hardware**: Direct GPIO manipulation for cartridge interfaces

### Protocol Design
- **Transport**: USB CDC-ACM
- **Framing**: START(0x7E) + LEN + PAYLOAD + CRC8 + END(0x7F)
- **Commands**: CMD_REQ, RX_DATA, TX_DATA
- **Error Handling**: CRC8 validation, timeouts, retry logic
- **Data Transfer**: Chunked for large files (56 bytes/packet)

### DPS MUX Architecture (NEW - February 2026)
- **Pattern**: Event-driven with timer-based detection
- **Hardware**: 8-channel multiplexer supporting DPS2 4IN1 and DPS3 4IN1
- **Topology**: 8 channels × 4 slots = 32 total cartridge slots
- **Channel Manager**: `MuxChannelManager` reused for each DPS channel
- **Data Model**: `DPSMuxChannelInfo` with `INotifyPropertyChanged` for reactive UI
- **MUX Protocol**:
  - Channel switching: `(char)0` = OFF, `(char)1-8` = Channel 1-8
  - Command: `channelNumber + 0x30` offset
  - Response validation: byte[3] and byte[1] (A=65 or M=77)
- **Logging Structure**: `DPSMux/Channel-X/Slot-Y/TestLog/`
- **Key Design Principle**: Clean separation of MUX detection (timer) vs channel scanning (button)

---

## Critical Development Notes

### ⚠️ MUX Subsystem - KNOWN CRITICAL ISSUES

**IMPORTANT**: The MUX subsystem is undergoing refactoring and has known critical issues. Before making any changes to MUX-related code, READ `DPS_DTCL/Mux/CRITICAL_ISSUES.md`.

**Critical Issues Summary**:
1. **Broken References**: `MuxWindow.xaml.cs` references non-existent `MuxChannelInfos[i]`
   - FIX: Replace with `muxManager.channels[i]` throughout
   - Affected lines: 350, 362, 371, 377, 383, 391, 424

2. **Singleton Destruction Bug** (LINE 292 in MuxManager.cs):
   ```csharp
   hwInfo.Dispose();  // ❌ CRITICAL: Destroys DpsInfo singleton!
   ```
   - After first channel scan, all subsequent operations fail
   - FIX: Remove this line - reuse singleton instead

3. **DpsInfo Architecture**:
   - ONE DpsInfo singleton shared across ALL MUX channels
   - Mux switches channel → Same DpsInfo communicates with DTCL on active channel
   - NEVER create multiple instances or dispose the singleton

4. **Performance Check Not Using New Class**:
   - `InitiatePC_Click` has 200+ lines of old complex logic
   - Should be refactored to use `MuxPerformanceCheck` class

**Correct MUX Architecture**:
```
Mux Hardware (8 channels)
    ↓ (Mux Protocol: '0'-'8')
MuxManager.switch_Mux(channelNo)
    ↓ (Channel switched)
ONE DpsInfo Instance (ISP Protocol)
    ↓ (Communicates with DTCL on active channel)
Cart Operations (D2/D3 operations)
```

### C# Development Guidelines
- **Target**: .NET Framework 4.8 (legacy compatibility required)
- **Switch Statements**: Use traditional switch-case (no pattern matching)
- **Threading**: Always use Dispatcher.Invoke for UI updates
- **Memory**: Dispose resources properly, use singleton pattern for hardware
- **Protocol**: ISP Protocol (not VennaProtocol - old naming)

### Firmware Development Guidelines
- **Language**: Mixed C/C++ codebase
- **Memory**: Stack-based allocation preferred (limited heap)
- **Interrupts**: USB CDC handled via interrupts with ring buffer
- **GPIO**: Direct HAL calls for cartridge interface control
- **FatFS**: Used for Darin-III CF card operations

### Communication Protocol
- **Frame Size**: Maximum 64 bytes total frame
- **Payload Size**: Maximum 56 bytes payload per packet
- **CRC**: CRC8 for error detection
- **Chunking**: Large files transferred in 1023-byte chunks
- **Timeout**: 5-second default timeout for operations

### Darin2.cs (NAND Flash) Implementation Notes
- **File Structure**: See `DPS_DTCL/Cartridges/Darin2_Analysis.md` for detailed analysis
- **Page-based Operations**: All flash operations work on 512-byte pages
- **Block Structure**: 32 pages = 1 block (16KB)
- **FSB Management**: First Starting Block (page number) critical for file allocation
- **JSON Dependencies**: Uses `D2UploadMessageDetails.json` and `D2DownloadMessageDetails.json`
- **Known Patterns**: Contains repetitive code patterns (message initialization, FPL handling, read/write operations)
- **Refactoring**: Code duplication documented but functional as-is - refactor only when stable
- **Constants**: File allocation constants (NAV1_NOB, NAV2_NOB, etc.) define block allocation

---

## Common Issues & Solutions

### Build Issues
```bash
# C# build fails - restore packages
nuget restore DPS_DTCL/DTCL.sln

# Firmware build fails - clean first
cd DTCL && make clean all
cd Firmware/D2_DPS_4IN1 && make clean all
cd Firmware/D3_DPS_4IN1 && make clean all

# Version conflicts - use build script
cd Scripts
build_all.bat all 3 6      # Rebuild all with version 3.6

# Or manually
make VERSION_MAJOR=3 VERSION_MINOR=6 clean all
```

### Communication Issues
- **No Hardware Detected**: Check USB drivers, COM port permissions
- **Timeouts**: Verify cable connection, check LED status on hardware
- **Protocol Errors**: Check CRC8 implementation, frame boundaries
- **USB CDC Transmission Failures** (FIXED Feb 2026):
  - Spurious firmware responses → Added retry logic with SPURIOUS_RESPONSE enum
  - Array mutation in retries → Always use fresh cmdPayload copy
  - Missing awaits → Fixed all Execute() calls
  - See `docs/BugFixes/USB_CDC_Transmission_Failures_Intel_PCs.md` for details

### Threading Issues
- **UI Freezing**: Use async/await, avoid blocking UI thread
- **Race Conditions**: Use locks around shared state (HardwareInfo)
- **Memory Leaks**: Dispose transport objects, unsubscribe events
- **Singleton Disposal**: NEVER dispose HardwareInfo/DpsInfo singleton - reuse it

### MUX-Specific Issues
- **Channel Switching**: Use `muxManager.switch_Mux(channelNo)` to switch active channel
- **DpsInfo Singleton**: ONE instance shared across all MUX channels
- **Architecture**: Mux switches channel → Same DpsInfo communicates on that channel
- **Performance Check**: Use `MuxPerformanceCheck` class instead of inline logic

---

## Development Memories & Important Notes

### Refactoring History
- **Transport Layer**: Moved all newly created files from core to transport folder (2025)
- **Protocol Naming**: Changed from "VennaProtocol" to "IspProtocol" (2025)
- **Architecture**: Added comprehensive documentation (Nov 2025)
- **Documentation Update**: Integrated all block diagrams into architecture documents (Nov 2025)
- **Documentation Organization**: Moved architecture docs to docs/ folder for better organization (2025)
- **Version Update**: Updated to Firmware 3.6 and GUI 1.1 (2025)
- **Performance**: Implemented real-time logging for performance checks (2025)
- **DTCL MUX Refactoring**: In progress - new `MuxManager` and `MuxPerformanceCheck` classes added (2025)
- **DPS MUX Implementation**: ✅ Complete implementation for 8 channels × 4 slots (Feb 2026)
  - Created `DPSMuxWindow.xaml/.cs`, `DPSMuxManager.cs`, `DPSMuxChannelInfo.cs`
  - Extended `PCLog.cs` with backward-compatible DPS MUX logging support
  - Fixed critical XAML array binding issues (Mode=TwoWay, UpdateSourceTrigger required)
  - Fixed MUX protocol numeric casting issues (char literals vs numeric values)
  - Implemented timer-based MUX detection pattern
  - Clean validation flow for log confirmation
- **Inno Setup Installer Automation**: ✅ Complete automated installer system (Feb 2026)
  - Created Inno Setup script (`installer/DTCL_DPS.iss`) for Windows installer
  - Implemented GitHub Actions workflow (`.github/workflows/release-installer.yml`)
  - Manual trigger only (no automatic releases) - full control over when releases are created
  - Organized runtime files in `DPS_DTCL/Resources/` folder for clean source control
  - Configured `.csproj` to copy Resources to output directory during build
  - Renamed executable from `DTCL.exe` to `DTCL_DPS.exe` for better branding
  - Professional S-Wave icon integration (installer, shortcuts, uninstaller)
  - Default installation path: `D:\S-WAVE SYSTEMS\DTCL_DPS` (user-selectable)
  - Fixed Inno Setup Pascal script syntax (registry functions, .NET Framework detection)
  - Fixed missing embedded resources (MirageJet.jpg, swave_icon.png for GUI display)
  - Includes all dependencies: DTCL_DPS.exe, TestConsole.exe, data folders (D1/D2/D3), configs
- **Known Issues**: DTCL MUX window has critical issues documented in CRITICAL_ISSUES.md

### Current State
- ✅ **Architecture Documented**: Complete system documentation created
- ✅ **ISP Protocol**: Unified protocol implementation
- ✅ **Performance Logging**: Real-time timestamps in performance checks
- ✅ **DTCL Hardware Support**: GUI fully functional for DTCL hardware (2 slots: 1 D2 + 1 D3)
- ✅ **DPS2/DPS3 4IN1 Support**: Hardware detection and basic operations working
- ✅ **DPS MUX Window**: Fully implemented for 8 channels × 4 slots (32 total slots) - Feb 2026
- ✅ **Installer Automation**: Complete Inno Setup installer with GitHub Actions CI/CD - Feb 2026
  - Manual trigger workflow for controlled releases
  - Professional S-Wave branding throughout installer
  - Organized Resources folder structure for runtime files
  - Executable renamed to DTCL_DPS.exe
- 🔄 **DTCL MUX Subsystem**: Has known critical bugs (see CRITICAL_ISSUES.md)
- 🔄 **Performance Check**: Needs hardware type-specific conditions for DPS2/3 4IN1

### Current Development Objectives

#### 🎯 Objective 1: DPS2/DPS3 4IN1 Hardware Compatibility (IN PROGRESS)
**Status**: Mostly functional, needs performance check improvements

**Problem Statement**:
- GUI currently works fully for DTCL hardware (2 slots)
- DPS2 4IN1 and DPS3 4IN1 hardware (4 slots each) mostly work
- Performance check needs conditions for "with cart" vs "without cart" scenarios
- Issue: When "without cart" is selected, even if cart IS inserted, needs proper handling for DPS2/3 4IN1
- Vice versa: When "with cart" is selected, proper validation needed

**Required Changes**:
1. Add hardware type detection logic in performance check
2. Implement conditional logic for cart presence validation:
   - DTCL: Current logic (2 slots)
   - DPS2 4IN1: 4 NAND slots logic
   - DPS3 4IN1: 4 CF slots logic
3. Handle "with cart" / "without cart" selection properly for each hardware type
4. Update performance check flow to account for 4-slot configurations

**Files to Modify**:
- `MainWindow.xaml.cs` - Performance check logic
- `PerformanceCheck.cs` - Cart validation logic
- `HardwareInfo.cs` - Hardware type detection (if needed)

#### 🎯 Objective 2: DPS MUX Window for DPS2/DPS3 4IN1 (✅ COMPLETED - February 2026)
**Status**: Core functionality implemented and working

**Implementation Summary**:
Successfully implemented DPS MUX Window with clean architecture supporting 8 channels × 4 slots = 32 total slots for DPS2 4IN1 (NAND) and DPS3 4IN1 (Compact Flash) hardware.

**✅ Completed Components**:

1. **DPS MUX Window UI** (`DPSMuxWindow.xaml`)
   - Clean two-column layout matching DTCL MUX style
   - 8-channel DataGrid with 4 slot checkboxes per channel
   - Port selection checkboxes enabled only when DPS hardware detected
   - Slot selection checkboxes enabled based on:
     - Port checkbox selected (`isUserSelected = true`)
     - Cart detected in slot (`IsCartDetected[slot] = true`)
   - Real-time progress tracking with yellow row highlighting
   - Performance check result display per slot and overall per channel

2. **DPS MUX Manager** (`DPSMuxManager.cs`)
   - Manages 8 DPS MUX channels with 4 slots each
   - MUX hardware scanning on available COM ports
   - Channel switching protocol: `switch_Mux((char)channelNo)` with 0x30 offset
   - **CRITICAL FIX**: Used numeric casting `(char)0`, `(char)1-8` NOT ASCII literals `'0'`, `'1'`
   - Clean OFF → ON → Scan → OFF sequence for each channel
   - Reestablish connection logic for channel activation during PC

3. **DPS MUX Channel Info** (`DPSMuxChannelInfo.cs`)
   - Data model with `INotifyPropertyChanged` for UI binding
   - 4-slot arrays: `IsSlotSelected[1-4]`, `IsCartDetected[1-4]`, `DTCSerialNumbers[1-4]`
   - Per-slot PC status tracking: `PCStatus[1-4]`
   - Overall channel PC status calculation
   - Properties: `isDPSConnected`, `HardwareType`, `CartType`, `isUserSelected`

4. **Timer-based MUX Detection** (`DPSMuxWindow.xaml.cs`)
   - 100ms periodic scanning timer (`System.Timers.Timer`)
   - Automatic MUX hardware detection on window load
   - Event-driven architecture: `PortConnected`/`PortDisconnected` events
   - User notifications via popup messages from JSON

5. **Clean Scan Implementation**
   - **Scan Button**: Scans all 8 channels for DPS hardware (NOT MUX hardware)
   - **Timer**: Automatically scans for MUX hardware connection
   - Clear separation of concerns: timer = MUX detection, button = channel scan
   - Counts detected DPS units and total carts across all channels
   - Validation: Rejects Multi-cart hardware (not supported)

6. **ConfirmLog Validation Flow**
   - ✅ **CRITICAL FIX**: Array indexer bindings need explicit mode
     ```xaml
     Binding="{Binding IsSlotSelected[1], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"
     ```
   - Validates: Inspector name, test number, unit serial numbers
   - With Cartridge mode: Validates DTC serial numbers for selected slots
   - Without Cartridge mode: Validates only unit serial number
   - Warns about default "999" values with user confirmation
   - Creates logs per selected slot (with cart) or per channel (without cart)

7. **DPS MUX Logging Structure** (`PCLog.cs` - Extended, Backward Compatible)
   - **NEW**: Added `isDPSMux` optional parameter to `CreateNewLog()`
   - **DPS MUX Folder Structure**:
     ```
     DPSMux/
       Channel-1/
         Slot-1/
           999_log.txt
           TestLog/
             999_log_07-02-2026_14-30-25.txt
         Slot-2/ ... Slot-3/ ... Slot-4/
       Channel-2/ ... Channel-8/
     ```
   - **DTCL MUX**: Keeps existing `Mux/ChannelNo-X/OldLog/` structure (unchanged)
   - **Standalone**: Keeps existing `PC_Log/Slot-X/TestLog/` structure (unchanged)
   - Log file key management: `(ChNo * 10) + SlotNumber` for unique slot identification
   - All existing PCLog methods work seamlessly with new structure

8. **Performance Check Execution**
   - Iteration-based or duration-based modes
   - Sequential channel processing with MUX switching
   - Reestablishes connection for each channel before testing
   - Per-slot performance check execution
   - Real-time progress updates with timer
   - Stop/Cancel functionality with cleanup

**Architecture Decisions**:

1. **Clean Code Design**:
   - Single responsibility methods
   - Clear separation: UI thread vs background operations
   - ObservableCollection for DataGrid binding
   - Event-driven updates via `INotifyPropertyChanged`
   - No unnecessary complexity or color-based state management

2. **MUX Protocol**:
   - Channel values: 0 (OFF), 1-8 (channels)
   - Command format: `channelNumber + 0x30` offset
   - Response validation: Check byte[3] and byte[1] (65='A' or 77='M')
   - **Critical**: Use numeric casting `(char)0` NOT character literal `'0'`

3. **Data Binding**:
   - Array indexers require: `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`
   - Port checkbox: `isUserSelected` with explicit binding mode
   - Slot checkboxes: `IsSlotSelected[1-4]` with explicit binding mode
   - MultiDataTrigger for conditional enabling based on multiple properties

4. **Thread Safety**:
   - All UI updates via `Dispatcher.Invoke()` or `Dispatcher.InvokeAsync()`
   - Lock objects for MUX switching operations
   - Proper async/await patterns throughout
   - Cancellation token support for PC operations

**Files Created/Modified**:
- ✅ Created: `DPS_DTCL/Mux/DPSMuxWindow.xaml` - Clean UI layout
- ✅ Created: `DPS_DTCL/Mux/DPSMuxWindow.xaml.cs` - Window logic with timer
- ✅ Created: `DPS_DTCL/Mux/DPSMuxManager.cs` - 8-channel MUX manager
- ✅ Created: `DPS_DTCL/Mux/DPSMuxChannelInfo.cs` - 4-slot data model
- ✅ Modified: `DPS_DTCL/Log/PCLog.cs` - Added DPS MUX logging support
- ✅ Modified: `DPS_DTCL/DTCL.csproj` - Added new files to project

**Key Technical Solutions**:

1. **Slot Checkbox Binding Issue**:
   - **Problem**: Slot selections not persisting, not recognized in ConfirmLog
   - **Root Cause**: Array indexer bindings `IsSlotSelected[1]` without explicit mode
   - **Solution**: Added `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged` to all 4 slot bindings
   - **Result**: Immediate source updates, selections persist correctly

2. **Channel Switching Bug**:
   - **Problem**: Getting value 97 instead of 49 when switching to channel 1
   - **Root Cause**: Using `'0'` (ASCII 48) + `'1'` (ASCII 49) = 97
   - **Solution**: Use numeric casting: `(char)0` + `0x30`, `(char)1` + `0x30` = 49
   - **Result**: Correct MUX protocol communication

3. **Scan Sequence**:
   - **Initial Issue**: Channels not switching OFF between scans
   - **Solution**: Implemented proper OFF → Channel N ON → Scan → OFF → Next sequence
   - **Result**: Stable channel scanning without interference

**Pending Work** (Future Enhancements):
- 🔄 Performance check detailed logging during execution
- 🔄 Progress bar accuracy for duration-based mode
- 🔄 Individual command buttons (Erase, Write, Read, LoopBack) - currently placeholders
- 🔄 Enhanced error recovery and retry logic

**Testing Status**:
- ✅ MUX hardware detection working
- ✅ Channel scanning detecting all 8 channels correctly
- ✅ Port and slot selection working with proper binding
- ✅ Select All checkbox functionality complete
- ✅ Cart validation (with/without cart modes) working
- ✅ MUX reconnection improvements (transport cleanup, timer intervals) working
- ✅ ConfirmLog validation and log creation working
- ✅ Log folder structure created correctly (DPSMux/Channel-X/Slot-Y/)
- 🔄 Performance check execution (implemented, needs hardware testing)
- 🔄 Multi-iteration testing with real hardware

#### ✅ DPS MUX Enhancements (February 8, 2026) - COMPLETED
**Status**: All enhancements implemented and tested

Following the initial DPS MUX implementation (Feb 7), several critical enhancements were completed on February 8, 2026:

**1. Select All Checkbox Functionality**
- **Location**: `DPSMuxWindow.xaml.cs` lines 37, 83-89, 462, 1840-1967
- **Implementation**: Two-state checkbox with automatic state tracking
- **Pattern**: Event-driven with recursion prevention using `_isUpdatingSelectAllState` flag
- **Features**:
  - Checked: Selects all DPS-connected channels
  - Unchecked: Deselects all DPS-connected channels
  - Automatic update when individual channel checkboxes change
  - Only affects channels with `isDPSConnected = true`
- **Critical Fix**: Added `using System.ComponentModel;` for `PropertyChangedEventHandler`
- **Key Lesson**: Three-state checkboxes (`IsThreeState="True"`) can interfere with user interaction - use two-state for select-all functionality

**2. Cart Validation for Log Creation**
- **Location**: `DPSMuxWindow.xaml.cs` lines 1654-1698
- **Implementation**: Validates cart presence matches selected mode before creating logs
- **Validation Logic**:
  - "With Cart" mode: Ensures at least one selected slot has a cart detected
  - "Without Cart" mode: Ensures NO carts in selected slots
  - Only validates SELECTED channels and SELECTED slots
- **Messages**: Uses `PC_Insert_Cart_Msg` and `PC_Remove_Cart_Msg` from PopUpMessages.json
- **User Feedback**: Shows clear error messages when validation fails

**3. MUX Hardware Reconnection Improvements**
- **Problem**: MUX hardware re-detection failing (working only 1 out of 10 times after disconnect)
- **Fixes Implemented**:
  - **Increased timer interval**: 100ms → 2000ms (2 seconds) in `DPSMuxWindow.xaml.cs` line 145
  - **Added overlap prevention**: `_isScanningMux` flag prevents concurrent scans
  - **Transport cleanup**: `DPSMuxManager.cs` lines 76-101 - dispose and recreate transport before each scan
  - **Delays between attempts**: 100ms delays between COM port connection attempts
  - **Post-disconnect delay**: 500ms delay after disconnect before starting timer
- **Result**: Significantly improved reconnection reliability

**4. Dynamic Window Loading (DPS vs DTCL MUX)**
- **Location**: `MainWindow.xaml.cs` lines 3914-3960
- **Pattern**: Reads `Default.txt` file to determine which MUX window to open
- **Implementation**: Same logic as `App.xaml.cs` (lines 26-86)
- **File Format** (Default.txt):
  ```
  dps        # Layout mode: "dps" or "dtcl"
  Debug      # Log level: Debug, Info, Warning, Error, Data
  ```
- **Window Types**:
  - `LayoutMode.DPSLayout` → Opens `DPSMuxWindow` (4 slots per channel)
  - `LayoutMode.DTCLLayout` → Opens `MuxWindow` (2 slots per channel)
- **Field Type**: Changed from `DPSMuxWindow _muxWindow` to `Window _muxWindow` (base class)

**5. Exit vs X Button Handling**
- **Location**: `DPSMuxWindow.xaml.cs` lines 1023-1073
- **Implementation**: `_isExitingApplication` flag to differentiate exit modes
- **Behavior**:
  - **Exit Button**: Logs "Performance Check Exited" for all selected slots, closes entire application (`Application.Current.Shutdown()`)
  - **X Button (Window Close)**: Returns to MainWindow, reactivates hardware event handlers
- **Critical Fix**: Skips disconnect popup when `_isExitingApplication = true` (line 196-199)

**Files Modified (Feb 8, 2026)**:
- `DPSMuxWindow.xaml.cs` - Select All, cart validation, reconnection, exit handling
- `DPSMuxWindow.xaml` - Select All checkbox binding changes
- `DPSMuxManager.cs` - Transport cleanup, reconnection improvements
- `MainWindow.xaml.cs` - Dynamic window loading based on Default.txt

**Lessons Learned**:
1. Three-state checkboxes can be problematic for select-all due to indeterminate state transitions
2. Timer intervals are critical for hardware detection reliability (too fast = port conflicts)
3. Transport cleanup MUST happen before re-scanning COM ports
4. Exit mode tracking prevents unwanted popups during application shutdown
5. Reading configuration files (Default.txt) at usage time ensures current settings are applied

#### 🎯 Objective 3: Major Refactoring & Code Cleanup (PLANNED)
**Status**: Not started, requires completion of Objectives 1 & 2

**Problem Statement**:
- MUX subsystem has critical bugs (documented in CRITICAL_ISSUES.md)
- Singleton disposal anti-pattern causing system failures
- Code duplication in Darin2.cs and other areas
- Unnecessary complex logic in various components

**Required Changes**:
1. **Fix Critical MUX Issues**:
   - Remove singleton disposal in MuxManager.cs line 292
   - Replace all `MuxChannelInfos[i]` with `muxManager.channels[i]`
   - Refactor `InitiatePC_Click` to use `MuxPerformanceCheck` class
   - Fix DpsInfo instance management

2. **Code Cleanup**:
   - Consolidate repetitive patterns in Darin2.cs (message initialization, FPL handling)
   - Remove duplicate read/write operations
   - Simplify performance check logic
   - Remove dead code and unused variables

3. **Architecture Improvements**:
   - Ensure proper singleton pattern usage throughout
   - Improve thread safety with proper locking
   - Better separation of concerns
   - Consistent error handling patterns

**Files to Refactor**:
- `Mux/MuxWindow.xaml.cs` - Critical fixes
- `Mux/MuxManager.cs` - Singleton management
- `Cartridges/Darin2.cs` - Code consolidation
- `MainWindow.xaml.cs` - Logic simplification
- Various files - Thread safety improvements

### GUI Current Versions
- **MainWindow.xaml.cs**: GUI Version 1.3 (as of line 30)
- **version.h**: Firmware Version 3.6 (default)

### Known Issues & Technical Debt
- ⚠️ **MUX Window Critical Issues**: See `DPS_DTCL/Mux/CRITICAL_ISSUES.md`
  - Broken references to `MuxChannelInfos[i]` - should use `muxManager.channels[i]`
  - Singleton destruction issue: `hwInfo.Dispose()` in MuxManager line 292 destroys DpsInfo singleton
  - Performance check not using new `MuxPerformanceCheck` class
  - DpsInfo instance management needs refactoring
- 🔄 **Darin2.cs Code Duplication**: See `DPS_DTCL/Cartridges/Darin2_Analysis.md`
  - Repetitive initialization patterns across upload/download messages
  - Duplicated FPL file handling code
  - Multiple similar read/write operations that could be consolidated
  - Consider refactoring when stable, but functional as-is
- ✅ **Client PC USB Transmission Issue**: See `BugFixSupportLogs/ClientPC_USB_Transmission_Issue.md`
  - **Problem**: Random 50% failure rate on Intel client PCs (100% on dev PC)
  - **Root Cause**: USB CDC hardware-specific buffering - multiple frames in one DataReceived event
  - **Fix Applied**: Multi-frame decoding loop in DataHandlerIsp.cs + retry logic bug fix
  - **Status**: ⏳ Fixed, awaiting client PC validation
  - **Details**: Complete analysis, historical attempts (v1-v16), testing procedures, future debugging guide

#### ✅ Inno Setup Installer Automation (February 21, 2026) - COMPLETED
**Status**: Full automated installer system implemented and working

Successfully implemented professional Windows installer automation using Inno Setup with GitHub Actions CI/CD pipeline.

**✅ Completed Components**:

1. **Resources Folder Organization**
   - Created `DPS_DTCL/Resources/` for organized runtime files
   - Structure:
     ```
     Resources/
     ├── D1/               (data files)
     ├── D2/               (NAND flash files & JSON configs)
     ├── D3/               (Compact flash files & JSON configs)
     ├── PopUpMessage/     (JSON configuration)
     ├── Default.txt       (layout configuration)
     ├── MirageJet.jpg     (GUI image - embedded & copied)
     ├── swave_icon.ico    (S-Wave icon - embedded & copied)
     └── swave_icon.png    (S-Wave icon - embedded & copied)
     ```
   - Updated `.csproj` to copy Resources/** to output directory root
   - Images added as both embedded Resources (for GUI) and Content files (for installer)
   - Updated `.gitignore` to track Resources/ folder

2. **Executable Naming**
   - Renamed from `DTCL.exe` to `DTCL_DPS.exe` for professional branding
   - Updated AssemblyName in DTCL.csproj
   - Updated all installer references

3. **Inno Setup Script** (`installer/DTCL_DPS.iss`)
   - Professional installer with .NET Framework 4.8 detection
   - S-Wave icon integration:
     - SetupIconFile: Installer displays S-Wave icon
     - All shortcuts use S-Wave icon (Start Menu, Desktop, Quick Launch)
     - Uninstaller shows S-Wave icon
   - Default installation path: `D:\S-WAVE SYSTEMS\DTCL_DPS` (user-selectable)
   - Directory selection page enabled (`DisableDirPage=no`)
   - Includes all files:
     - DTCL_DPS.exe (main GUI)
     - TestConsole.exe (CLI tool)
     - All DLLs and dependencies
     - Data folders (D1/, D2/, D3/, PopUpMessage/)
     - Configuration files (*.config, Default.txt)
     - Images (MirageJet.jpg, swave_icon.ico, swave_icon.png)
     - Documentation (README.md)
   - Professional shortcuts:
     - Start Menu: DTCL_DPS (with S-Wave icon)
     - Desktop: DTCL_DPS (optional, with S-Wave icon)
     - TestConsole shortcut (with S-Wave icon)

4. **GitHub Actions Workflow** (`.github/workflows/release-installer.yml`)
   - **Manual trigger only** (no automatic releases)
   - User inputs:
     - Version number (e.g., 1.4)
     - Create GitHub Release checkbox (default: true)
   - Workflow steps:
     1. Checkout code
     2. Setup MSBuild and NuGet
     3. Install Inno Setup via Chocolatey
     4. Restore packages and build Release configuration
     5. Dynamically update version in Inno Setup script
     6. Compile installer with `iscc`
     7. Verify installer created
     8. Create GitHub Release (if checkbox enabled)
     9. Upload installer as artifact (90-day retention)
   - Output: `DTCL_DPS_Setup_vX.X.exe` (~5-10 MB)

5. **Key Fixes Applied**
   - Fixed Inno Setup Pascal syntax error:
     - Changed `HKLM` to `HKEY_LOCAL_MACHINE`
     - Changed registry value type from `Integer` to `Cardinal`
   - Fixed missing embedded resources:
     - Added MirageJet.jpg and swave_icon.png as embedded Resources
     - GUI now displays images correctly after installation
   - Cleaned up unused folders:
     - Deleted AutoTest/ (not used)
     - Deleted Connected Services/ (empty)
     - Removed WCFMetadata reference from .csproj

**Files Created/Modified**:
- ✅ Created: `installer/DTCL_DPS.iss` - Inno Setup script
- ✅ Created: `.github/workflows/release-installer.yml` - Installer automation
- ✅ Created: `DPS_DTCL/Resources/` - Runtime files folder
- ✅ Modified: `DPS_DTCL/DTCL.csproj` - AssemblyName, Resources, Content items
- ✅ Modified: `.gitignore` - Track Resources/, exclude installer/output/

**How to Create Release**:
1. Go to GitHub Actions: https://github.com/isquaresystems/DTCL_DPS/actions
2. Click "Create Installer Release"
3. Run workflow:
   - Version: e.g., `1.4`
   - Create GitHub Release: ✅ (or uncheck for artifact only)
4. Wait ~5 minutes for build completion
5. Download installer from Releases page or workflow artifacts

**Installation Features**:
- ✅ User-selectable installation path (default: D:\S-WAVE SYSTEMS\DTCL_DPS)
- ✅ .NET Framework 4.8 detection (blocks installation if missing)
- ✅ Professional S-Wave branding throughout
- ✅ Start Menu shortcuts with S-Wave icon
- ✅ Optional Desktop shortcut
- ✅ All runtime files included
- ✅ Clean uninstaller

**Future Enhancements** (Optional):
- 🔄 Code signing certificate (removes "Unknown Publisher" warning)
- 🔄 Automated version bumping from git tags
- 🔄 Multi-language support in installer

### Future Considerations
- **Protocol Evolution**: ISP protocol is stable, avoid breaking changes
- **MUX Refactoring**: Complete MUX window fixes per CRITICAL_ISSUES.md
- **Performance**: Consider optimizing large file transfers
- **Testing**: Expand unit test coverage for protocol layers
- **Documentation**: Keep architecture docs updated with changes

---

## File Locations & Key Components

### Critical Files to Understand
1. **MainWindow.xaml.cs**: Main GUI logic and event handling
2. **HardwareInfo.cs**: Hardware management singleton
3. **UartIspTransport.cs**: USB communication transport
4. **DPSMuxWindow.xaml.cs**: DPS MUX window (8 channels × 4 slots) ✅ NEW
5. **DPSMuxManager.cs**: DPS MUX hardware manager ✅ NEW
6. **PCLog.cs**: Performance check logging (extended for DPS MUX) ✅ MODIFIED
7. **main.cpp**: Firmware entry point and initialization
8. **IspCommandManager.cpp**: Firmware command routing
9. **Darin2.cpp/Darin3.cpp**: Cartridge-specific implementations

### Configuration Files
- **App.config**: GUI application configuration
- **version.h**: Firmware version definitions
- **Makefile**: Firmware build configuration
- **DTCL.csproj**: C# project configuration

### Documentation Files
- **README.md**: Project overview and build instructions
- **docs/DTCL_Firmware_Architecture.md**: Detailed firmware architecture with integrated flow diagrams
- **docs/DTCL_GUI_Architecture.md**: Detailed GUI architecture with integrated system diagrams
- **DPS_DTCL/Mux/CRITICAL_ISSUES.md**: Known MUX window issues requiring fixes
- **DPS_DTCL/Mux/RefactoringNotes.md**: MUX refactoring progress and notes
- **DPS_DTCL/Cartridges/Darin2_Analysis.md**: Darin2.cs code structure analysis
- **DPS_DTCL/Cartridges/Darin2_RefactoringNotes.md**: Darin2 refactoring considerations

---

## Refactoring Guidelines & Best Practices

### Before Modifying Code
1. **Read Existing Documentation**:
   - Check if component has analysis/refactoring notes (e.g., `Darin2_Analysis.md`)
   - Review `CRITICAL_ISSUES.md` files in relevant folders
   - Understand existing patterns before changing them

2. **MUX Subsystem**:
   - DO NOT dispose HardwareInfo/DpsInfo singleton
   - Use `muxManager.channels[i]` instead of direct channel access
   - Test all 8 channels after changes

3. **Singleton Pattern**:
   - HardwareInfo/DpsInfo is a singleton - never dispose, never create multiple instances
   - Use `HardwareInfo.Instance` or `DpsInfo.Instance` to access

4. **Threading**:
   - All UI updates must use `Dispatcher.Invoke()` or `Dispatcher.InvokeAsync()`
   - Cart operations are async - always await them
   - Use proper locking around shared state

5. **Testing**:
   - Test with actual hardware before committing
   - Verify all cartridge types (Darin1, Darin2, Darin3)
   - Test both single-slot and MUX configurations

### Common Pitfalls to Avoid
- ❌ Disposing singleton instances (causes entire app to fail)
- ❌ Using synchronous operations on UI thread (causes freezing)
- ❌ Direct UI updates from background threads (causes crashes)
- ❌ Creating multiple instances of hardware managers
- ❌ Modifying protocol constants without understanding full impact
- ❌ Breaking backward compatibility with existing hardware
- ❌ **Array indexer bindings without explicit mode** (causes binding not to update source)
  - WRONG: `Binding="{Binding IsSlotSelected[1]}"`
  - RIGHT: `Binding="{Binding IsSlotSelected[1], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"`
- ❌ **Using character literals for numeric values in MUX protocol**
  - WRONG: `switch_Mux('0')`, `switch_Mux((char)('0' + channelNo))`
  - RIGHT: `switch_Mux((char)0)`, `switch_Mux((char)channelNo)`
- ❌ **Calling Items.Refresh() when checkboxes are bound** (causes re-evaluation and clearing)
  - Use `CommitEdit()` instead, or rely on `INotifyPropertyChanged`

### Critical Lessons from DPS MUX Implementation (February 2026)

1. **WPF Array Indexer Binding**:
   - Array properties like `bool[] IsSlotSelected` require explicit binding mode
   - Always use: `Mode=TwoWay, UpdateSourceTrigger=PropertyChanged`
   - Without this, checkbox changes won't update the underlying array
   - `CommitEdit()` alone is not sufficient for array indexers

2. **MUX Protocol Numeric Values**:
   - MUX expects numeric byte values, not ASCII characters
   - Channel 0 (OFF) = 0x30 (48 decimal)
   - Channel 1 = 0x31 (49 decimal)
   - Use `(char)channelNo + 0x30` NOT `'0' + channelNo`
   - Character arithmetic can produce unexpected results (e.g., '0' + '1' = 97)

3. **Backward Compatible Extensions**:
   - When extending existing classes, use optional parameters with defaults
   - Example: `CreateNewLog(..., int ChNo = 0, bool isDPSMux = false)`
   - This preserves all existing call sites while adding new functionality
   - Document the new parameter clearly in code comments

4. **DataGrid Refresh Best Practices**:
   - `Items.Refresh()` forces re-evaluation of all bindings and triggers
   - Can cause checkboxes to clear if conditions change
   - Prefer `INotifyPropertyChanged` for automatic updates
   - Only use `CommitEdit()` to flush pending cell edits

5. **Logging Folder Structure**:
   - Separate folder hierarchies for different hardware types prevents conflicts
   - DPS MUX: `DPSMux/Channel-X/Slot-Y/`
   - DTCL MUX: `Mux/ChannelNo-X/`
   - Standalone: `PC_Log/Slot-X/`
   - Use unique log keys: `(ChNo * 10) + SlotNumber` for multi-slot per channel

6. **Timer-based Detection Pattern**:
   - Separate concerns: Timer for hardware detection, Button for user-initiated actions
   - Timer scans continuously until hardware found, then stops
   - Button performs channel scanning only when user requests
   - Event-driven notifications: `PortConnected`, `PortDisconnected`

---

## 🔧 BUG FIXING QUICK REFERENCE

This section provides immediate context for fixing bugs in ANY part of the codebase.

### 🎯 Subsystem Quick Navigation

**Need to fix a bug in...?**

| Subsystem | Jump To | Key Files | Common Issues |
|-----------|---------|-----------|---------------|
| **ISP Protocol** | [ISP Protocol Flow](#isp-protocol-command-flow) | `IspProtocol/*.cs` | Timeouts, CRC errors, frame corruption |
| **Cartridge Ops** | [Cartridge Operations](#cartridge-operation-patterns) | `Cartridges/Darin*.cs` | Flash errors, file I/O, block allocation |
| **Hardware Mgmt** | [Hardware State](#hardware-state-management) | `Transport/HardwareInfo.cs` | Connection loss, detection failures |
| **MUX System** | [MUX Architecture](#correct-mux-architecture) | `Mux/*.cs` | Singleton disposal, channel switching |
| **Logging** | [Logging System](#6-logging-system) | `Log/PCLog.cs`, `Log.cs` | Path issues, file locks |
| **Threading** | [Threading Issues](#threading-issues) | All async methods | Race conditions, UI freezing, deadlocks |
| **Data Transfer** | [Data Transfer](#data-transfer-patterns) | `IspCmdTransmitData.cs` | Chunking errors, buffer overflows |

---

### 🔍 ISP Protocol Command Flow

**Complete request/response cycle:**

```
1. GUI Creates Command
   ├─> IspCommandManager.CreateCommand(subCmd, payload)
   ├─> Add START (0x7E), LENGTH, CRC8, END (0x7F)
   └─> Frame ready for transmission

2. Transmit via Transport
   ├─> UartIspTransport.TransmitAsync(frame)
   ├─> SerialPort.Write(buffer)
   └─> Wait for response

3. Receive Response
   ├─> IspStreamDecoder monitors incoming bytes
   ├─> Validates START/END markers
   ├─> Checks CRC8
   └─> Returns validated frame

4. Process Response
   ├─> IspCmdControl.ExecuteCmd() waits for completion
   ├─> Timeout if no response (default 5000ms)
   └─> Parse response data

5. Handle Result
   ├─> Check status byte
   ├─> Extract payload data
   └─> Update UI via Dispatcher
```

**Key Methods:**
```csharp
// Command creation
IspCommandManager.CreateCommand(byte subCmd, byte[] payload)

// Command execution with timeout
IspCmdControl.ExecuteCmd(byte[] cmd, int expectedLen, int timeout)

// Frame encoding
IspFramingUtils.EncodeFrame(byte[] payload)

// Frame decoding
IspStreamDecoder.ProcessIncomingData(byte[] data)

// CRC calculation
IspFramingUtils.CalculateCRC8(byte[] data)
```

**Common Issues:**
- **Timeout errors**: Check timeout value (default 5000ms), increase for slow operations
- **CRC mismatch**: Verify frame boundary bytes, check for data corruption
- **Frame corruption**: Ensure START (0x7E) and END (0x7F) are not in payload (escape if needed)
- **No response**: Check hardware connection, verify COM port open, test cable
- **Wrong payload length**: Verify expectedLen matches actual response size

**Debug Pattern:**
```csharp
// Add logging at each step
Log.Debug($"Sending command: {BitConverter.ToString(cmd)}");
Log.Debug($"Expected response length: {expectedLen}");
Log.Debug($"Timeout: {timeout}ms");

// Receive
Log.Debug($"Received: {BitConverter.ToString(response)}");
Log.Debug($"Response length: {response.Length}");
Log.Debug($"CRC valid: {IspFramingUtils.ValidateCRC(response)}");
```

---

### 💾 Cartridge Operation Patterns

#### Darin2 (NAND Flash) Operation Flow

**File:** `Cartridges/Darin2.cs`

**Key Operations:**

1. **Erase Operation**
```
EraseCart()
  └─> For each file to erase:
      ├─> Get block allocation (NAV1_NOB, NAV2_NOB, etc.)
      ├─> Send ERASE_BLOCK subcommand
      ├─> Blocks = StartBlock to (StartBlock + NumBlocks)
      └─> Wait for completion (LED state = GREEN)
```

2. **Write Operation**
```
WriteToCart()
  └─> For each file to write:
      ├─> Read file data from PC
      ├─> TX_DATA_RESET to start transfer
      ├─> Send data in 1023-byte chunks (56 bytes per packet)
      ├─> TX_DATA packets until complete
      ├─> WRITE_FILE subcommand with FSB (First Starting Block)
      └─> Wait for completion
```

3. **Read Operation**
```
ReadFromCart()
  └─> For each file to read:
      ├─> RX_DATA_RESET to prepare receive
      ├─> READ_FILE subcommand with page number and size
      ├─> Receive data in 1023-byte chunks
      ├─> RX_DATA packets until complete
      └─> Save to PC file
```

**Common Darin2 Issues:**
- **FSB (First Starting Block) errors**: Check page-to-block conversion (page / 32 = block)
- **Block allocation**: Verify NAV1_NOB, NAV2_NOB constants match hardware
- **Spare area handling**: NAND requires 16-byte spare per 512-byte page
- **Bad block management**: Check RB (Ready/Busy) signal
- **File size mismatches**: Ensure proper padding to page boundaries (512 bytes)

#### Darin3 (Compact Flash) Operation Flow

**File:** `Cartridges/Darin3.cs`

**Key Operations:**

1. **Format Operation**
```
Format()
  └─> Send FORMAT_CART subcommand
      ├─> Creates FAT filesystem
      ├─> Initializes root directory
      └─> Wait for completion
```

2. **Write Operation**
```
WriteToCart()
  └─> For each file:
      ├─> TX_DATA_RESET
      ├─> Send file data in chunks
      ├─> WRITE_FILE with filename
      └─> FatFS creates file on CF card
```

3. **Read Operation**
```
ReadFromCart()
  └─> For each file:
      ├─> RX_DATA_RESET
      ├─> READ_FILE with filename
      ├─> Receive file data
      └─> Save to PC
```

**Common Darin3 Issues:**
- **FatFS mount failures**: Ensure card is formatted first
- **Filename issues**: Check 8.3 format compatibility
- **File size limits**: FAT32 has 4GB max file size
- **Card detection**: Verify CF card inserted and recognized
- **Read/write errors**: Check for bad sectors, try re-format

---

### 🔌 Hardware State Management

**File:** `Transport/HardwareInfo.cs`

**Singleton Architecture:**
```csharp
// ONE instance for entire application
HardwareInfo.Instance
  ├─> CurrentLayout (DPSLayout or DTCLLayout)
  ├─> HardwareType (DPS2_4_IN_1, DPS3_4_IN_1, DTCL)
  ├─> slotInfo[] - Array of SlotInfo (indexed 1-4)
  ├─> cartObj[] - Array of ICart instances
  └─> Transport - UartIspTransport instance

// CRITICAL: Never dispose! Never create multiple instances!
```

**Hardware Detection Flow:**
```
ScanForHardware()
  └─> For each COM port:
      ├─> Open port at 115200 baud (921600 on Win11 Pro)
      ├─> Send VERSION_REQ command
      ├─> Parse response: HardwareType, FirmwareVersion
      ├─> Create appropriate cart instances (Darin1/2/3)
      └─> Set CurrentLayout based on hardware type
```

**State Management Pattern:**
```csharp
// Check connection state
if (hwInfo.Transport?.IsConnected ?? false) {
    // Hardware connected - safe to send commands
}

// Slot state check
for (int i = 1; i <= 4; i++) {
    if (hwInfo.slotInfo[i] != null) {
        var cartType = hwInfo.slotInfo[i].CartType;
        var isCartDetected = hwInfo.slotInfo[i].IsCartDetected;
        // ... use slot info
    }
}

// Cart object access
if (hwInfo.cartObj[slotNum] != null) {
    await hwInfo.cartObj[slotNum].PerformOperation();
}
```

**Common Hardware Issues:**
- **Connection lost**: Check Transport.IsConnected before operations
- **Slot detection fails**: Verify SCAN_CART subcommand response
- **Wrong cart type**: Re-scan to refresh slot info
- **Null reference**: Always check slotInfo[i] != null before access
- **Disposed object**: Check if someone disposed singleton (CRITICAL BUG!)

---

### 🔄 Data Transfer Patterns

**Chunked Transfer (Large Files):**

**File:** `IspProtocol/IspCmdTransmitData.cs` and `IspCmdReceiveData.cs`

```csharp
// Transmit (PC to Hardware)
public async Task TransmitData(byte[] data) {
    // 1. Reset transfer mode
    await SendCommand(TX_DATA_RESET);

    // 2. Calculate chunks
    int totalChunks = (data.Length + 1022) / 1023;  // 1023 bytes per chunk

    // 3. Send chunks
    for (int chunk = 0; chunk < totalChunks; chunk++) {
        int offset = chunk * 1023;
        int length = Math.Min(1023, data.Length - offset);

        // Each chunk sent as multiple packets (56 bytes per packet)
        await SendChunk(data, offset, length);
    }

    // 4. Verify completion
    await WaitForAck();
}

// Receive (Hardware to PC)
public async Task<byte[]> ReceiveData(int expectedSize) {
    // 1. Reset receive mode
    await SendCommand(RX_DATA_RESET);

    // 2. Prepare buffer
    byte[] buffer = new byte[expectedSize];

    // 3. Receive chunks (1023 bytes each)
    int received = 0;
    while (received < expectedSize) {
        var chunk = await ReceiveChunk();
        Array.Copy(chunk, 0, buffer, received, chunk.Length);
        received += chunk.Length;
    }

    return buffer;
}
```

**Buffer Sizes:**
- **Max Frame**: 64 bytes
- **Max Payload per packet**: 56 bytes
- **Chunk size**: 1023 bytes (multiple packets)
- **Total transfer**: Multiple chunks

**Common Transfer Issues:**
- **Buffer overflow**: Check chunk size doesn't exceed 1023 bytes
- **Incomplete transfer**: Verify all chunks received/sent
- **Timeout during large transfer**: Increase timeout for file size
- **Data corruption**: Verify CRC8 on each packet
- **Out of order packets**: Check sequence numbering

---

### 🧵 Thread Safety Checklist

**When fixing threading bugs:**

1. **UI Updates:**
```csharp
// ✅ CORRECT - Dispatcher for UI thread
Dispatcher.Invoke(() => {
    StatusLabel.Content = "Updated";
});

// ❌ WRONG - Direct UI update from background thread
StatusLabel.Content = "Updated";  // Crashes!
```

2. **Shared State Access:**
```csharp
// ✅ CORRECT - Lock around shared data
lock (hwInfo._lockObject) {
    hwInfo.slotInfo[1] = new SlotInfo();
}

// ❌ WRONG - Direct access from multiple threads
hwInfo.slotInfo[1] = new SlotInfo();  // Race condition!
```

3. **Async Patterns:**
```csharp
// ✅ CORRECT - Async all the way
public async Task DoOperation() {
    await cartObj.WriteToCart();  // Properly awaited
    Dispatcher.Invoke(() => UpdateUI());
}

// ❌ WRONG - Blocking async
public void DoOperation() {
    cartObj.WriteToCart().Wait();  // Deadlock risk!
}
```

4. **Cancellation:**
```csharp
// ✅ CORRECT - Check cancellation token
public async Task LongOperation(CancellationToken ct) {
    for (int i = 0; i < 1000; i++) {
        if (ct.IsCancellationRequested) return;
        await DoWork();
    }
}

// ❌ WRONG - No cancellation support
public async Task LongOperation() {
    for (int i = 0; i < 1000; i++) {
        await DoWork();  // Can't stop!
    }
}
```

---

### 📁 Key Files by Functionality

**Need to fix functionality? Go to these files:**

| Functionality | Primary File | Line Count | Purpose |
|--------------|--------------|------------|---------|
| **Main UI** | `MainWindow.xaml.cs` | ~4,113 | Main window, all UI handlers |
| **Hardware Detection** | `Transport/HardwareInfo.cs` | ~800 | Singleton hardware manager |
| **ISP Commands** | `IspProtocol/IspCommandManager.cs` | ~500 | Command routing and execution |
| **Transport Layer** | `IspProtocol/UartIspTransport.cs` | ~600 | USB CDC communication |
| **Frame Encoding** | `IspProtocol/IspFramingUtils.cs` | ~200 | Frame encode/decode, CRC |
| **NAND Operations** | `Cartridges/Darin2.cs` | ~2,000 | Darin2 cart operations |
| **CF Operations** | `Cartridges/Darin3.cs` | ~1,500 | Darin3 cart operations |
| **Performance Check** | `Cartridges/PerformanceCheck.cs` | ~800 | PC execution logic |
| **DTCL MUX** | `Mux/MuxWindow.xaml.cs` | ~1,500 | DTCL MUX UI (2 slots) |
| **DPS MUX** | `Mux/DPSMuxWindow.xaml.cs` | ~2,000 | DPS MUX UI (4 slots) |
| **MUX Manager (Old)** | `Mux/MuxManager.cs` | ~400 | DTCL MUX channel management |
| **MUX Manager (New)** | `Mux/DPSMuxManager.cs` | ~500 | DPS MUX channel management |
| **Logging (PC)** | `Log/PCLog.cs` | ~600 | Performance check logs |
| **Logging (App)** | `Log/Log.cs` | ~200 | Application debug logging |
| **Slot State** | `Transport/SlotInfo.cs` | ~100 | Slot information model |
| **Channel State** | `Mux/DPSMuxChannelInfo.cs` | ~200 | DPS MUX channel model |

---

### 🐛 Common Bug Patterns by Symptom

**"Hardware not detected":**
- Check: COM port drivers installed?
- Check: USB cable connected properly?
- Check: `HardwareInfo.ScanForHardware()` timeout (increase if needed)
- Check: Correct baud rate (115200 or 921600)?
- Check: Firewall blocking COM port access?
- Debug: Add logging in `UartIspTransport.Connect()`

**"Operation timeout":**
- Check: Timeout value sufficient for operation? (default 5000ms)
- Check: Hardware performing operation? (check LED status)
- Check: Cable disconnected mid-operation?
- Check: Response frame CRC valid?
- Debug: Log in `IspCmdControl.ExecuteCmd()` before timeout

**"CRC error":**
- Check: Frame boundary bytes in payload? (0x7E, 0x7F need escaping)
- Check: Data corruption during transmission?
- Check: Correct CRC8 algorithm implementation?
- Debug: Log raw bytes before/after CRC calculation

**"UI freezing":**
- Check: Blocking operation on UI thread?
- Fix: Use `async/await` pattern
- Fix: Move long operations to background thread
- Fix: Use `Dispatcher.InvokeAsync()` instead of `Invoke()`
- Debug: Check for `.Wait()` or `.Result` on Task

**"Null reference exception":**
- Check: `slotInfo[i]` null before access?
- Check: `cartObj[i]` initialized?
- Check: Hardware scanned before operation?
- Fix: Add null checks before accessing
- Debug: Log object state before access

**"File not found":**
- Check: File path using correct separators (`\` on Windows)?
- Check: File exists before read operation?
- Check: Directory created before write operation?
- Fix: Use `Path.Combine()` for cross-platform paths
- Debug: Log full file path before I/O

**"Connection lost during operation":**
- Check: Port closed by another process?
- Check: Hardware disconnected physically?
- Check: Transport disposed unexpectedly?
- Fix: Add reconnection logic
- Fix: Check `IsConnected` before each operation
- Debug: Subscribe to `PortClosed` event and log

**"MUX channel won't switch":**
- Check: Using correct command format? (`(char)'0'` not `(char)0`)
- Check: Response validation correct? (check byte[3] or byte[1])
- Check: Previous channel switched OFF first?
- Fix: Add delays between switch operations (500-1000ms)
- Debug: Log MUX command bytes and response

**"Singleton disposed error":**
- Check: Someone called `Dispose()` on HardwareInfo/DpsInfo?
- **CRITICAL**: This is a major bug - singletons should NEVER be disposed
- Fix: Remove all `Dispose()` calls on singleton instances
- Fix: Search codebase for `hwInfo.Dispose()` or `DpsInfo.Dispose()`
- Ref: See `CRITICAL_ISSUES.md` for DTCL MUX bug

---

### 🔬 Error Handling Patterns

**Standard Error Handling Structure:**

```csharp
public async Task<bool> PerformOperation() {
    try {
        // Pre-check: Validate state
        if (!hwInfo.Transport.IsConnected) {
            Log.Error("Hardware not connected");
            return false;
        }

        // Main operation
        Log.Info("Starting operation...");
        var result = await ExecuteCommand();

        // Post-check: Validate result
        if (result == null || result.Length == 0) {
            Log.Warning("Empty response received");
            return false;
        }

        // Success
        Log.Info("Operation completed successfully");
        return true;

    } catch (TimeoutException ex) {
        Log.Error("Operation timeout", ex);
        // Show user message
        Dispatcher.Invoke(() => {
            CustomMessageBox.Show("Operation timeout - check hardware connection");
        });
        return false;

    } catch (IOException ex) {
        Log.Error("I/O error during operation", ex);
        // Attempt reconnection
        await hwInfo.ReconnectHardware();
        return false;

    } catch (Exception ex) {
        Log.Error($"Unexpected error: {ex.GetType().Name}", ex);
        // Log stack trace for debugging
        Log.Debug($"Stack trace: {ex.StackTrace}");
        return false;

    } finally {
        // Cleanup regardless of success/failure
        CleanupResources();
    }
}
```

**Exception Types to Handle:**

| Exception | Meaning | Common Fix |
|-----------|---------|------------|
| `TimeoutException` | Command took too long | Increase timeout, check hardware |
| `IOException` | COM port I/O error | Reconnect, check cable |
| `UnauthorizedAccessException` | Port access denied | Close other apps using port |
| `ObjectDisposedException` | Object already disposed | Check singleton disposal bug |
| `NullReferenceException` | Object not initialized | Add null checks |
| `InvalidOperationException` | Operation in wrong state | Check state before operation |
| `ArgumentException` | Invalid parameter | Validate input parameters |

**Logging Levels:**
```csharp
Log.Debug("Detailed info for debugging");        // Only in debug builds
Log.Info("Normal operation progress");           // General info
Log.Warning("Potential issue, operation continues"); // Warning
Log.Error("Operation failed", exception);        // Error with stack trace
Log.Data("Hex data", byteArray);                 // Data dumps
```

---

### 🩺 Diagnostic Flowchart

**When a bug is reported, follow this checklist:**

```
1. Reproduce the Bug
   ├─> Can you reproduce it consistently?
   │   ├─> YES: Note exact steps to reproduce
   │   └─> NO: Ask user for detailed steps
   │
   ├─> Check Debug Log (DebugLog.txt)
   │   └─> Look for errors/warnings before bug occurs
   │
   └─> Check hardware state
       ├─> Is hardware connected?
       ├─> What's the LED status?
       └─> Which slot/channel is affected?

2. Identify Subsystem
   ├─> GUI/UI issue? → Check MainWindow.xaml.cs
   ├─> Communication issue? → Check ISP Protocol files
   ├─> Cart operation issue? → Check Darin*.cs files
   ├─> MUX issue? → Check Mux/*.cs files
   └─> Hardware detection? → Check HardwareInfo.cs

3. Check Known Issues
   ├─> Read CRITICAL_ISSUES.md (for MUX)
   ├─> Check "Common Bug Patterns" section above
   └─> Search DebugLog.txt for similar errors

4. Add Diagnostic Logging
   ├─> Add Log.Debug() at key points
   ├─> Log input parameters
   ├─> Log intermediate results
   └─> Log response data (especially byte arrays)

5. Test Fix
   ├─> Test with affected hardware
   ├─> Test edge cases
   ├─> Test with different cart types
   └─> Verify no regression in other features

6. Document Fix
   ├─> Update CLAUDE.md if pattern is new
   ├─> Add comments explaining fix
   └─> Update version number if releasing
```

---

### 🔍 Debugging Tools and Techniques

**1. Enable Detailed Logging:**
```csharp
// In App.xaml.cs or MainWindow.xaml.cs
Log.SetLogLevel(LogLevel.Debug);  // Show all debug messages
```

**2. COM Port Monitoring:**
```csharp
// Add to UartIspTransport.cs for debugging
private void OnDataReceived(byte[] data) {
    Log.Data("RX", data);  // Log all received bytes
}

private void OnDataSent(byte[] data) {
    Log.Data("TX", data);  // Log all sent bytes
}
```

**3. State Inspection:**
```csharp
// Dump current hardware state
Log.Debug($"HardwareType: {hwInfo.HardwareType}");
Log.Debug($"Connected: {hwInfo.Transport?.IsConnected}");
for (int i = 1; i <= 4; i++) {
    if (hwInfo.slotInfo[i] != null) {
        Log.Debug($"Slot {i}: {hwInfo.slotInfo[i].CartType}, " +
                  $"Detected: {hwInfo.slotInfo[i].IsCartDetected}");
    }
}
```

**4. Performance Profiling:**
```csharp
// Measure operation timing
var stopwatch = System.Diagnostics.Stopwatch.StartNew();
await PerformOperation();
stopwatch.Stop();
Log.Info($"Operation took {stopwatch.ElapsedMilliseconds}ms");
```

**5. Exception Details:**
```csharp
try {
    // ... operation
} catch (Exception ex) {
    Log.Error($"Exception Type: {ex.GetType().FullName}");
    Log.Error($"Message: {ex.Message}");
    Log.Error($"Stack: {ex.StackTrace}");
    if (ex.InnerException != null) {
        Log.Error($"Inner: {ex.InnerException.Message}");
    }
}
```

---

### 📝 Quick Diagnostic Commands

**Check COM port availability:**
```csharp
string[] ports = System.IO.Ports.SerialPort.GetPortNames();
Log.Debug($"Available COM ports: {string.Join(", ", ports)}");
```

**Test ISP Protocol connection:**
```csharp
// Send version request
var versionCmd = IspCommandManager.CreateCommand(VERSION_REQ, new byte[0]);
var response = await IspCmdControl.ExecuteCmd(versionCmd, expectedLen: 10, timeout: 5000);
Log.Debug($"Version response: {BitConverter.ToString(response)}");
```

**Verify CRC calculation:**
```csharp
byte[] testData = { 0x01, 0x02, 0x03, 0x04 };
byte crc = IspFramingUtils.CalculateCRC8(testData);
Log.Debug($"CRC8 of test data: 0x{crc:X2}");
// Expected: Should match firmware CRC implementation
```

**Check file I/O:**
```csharp
string testPath = @"C:\MPS\DARIN2\upload\test.bin";
bool exists = System.IO.File.Exists(testPath);
Log.Debug($"File exists: {exists}");
if (exists) {
    long size = new System.IO.FileInfo(testPath).Length;
    Log.Debug($"File size: {size} bytes");
}
```

---

### 🎓 Best Practices for Bug Fixes

**1. Understand Before Changing:**
- Read relevant architecture docs (GUI or Firmware)
- Trace code flow from symptom to root cause
- Check if similar code exists elsewhere (consistency)

**2. Minimal Changes:**
- Fix only what's necessary
- Don't refactor while fixing bugs (separate commits)
- Preserve existing behavior unless it's the bug

**3. Test Thoroughly:**
- Test the specific bug scenario
- Test related functionality (regression testing)
- Test with different hardware types (DPS2, DPS3, DTCL)
- Test with MUX if applicable

**4. Document the Fix:**
- Add inline comments explaining WHY, not just WHAT
- Update CLAUDE.md if it's a common pattern
- Update version number if releasing
- Add to git commit message with clear description

**5. Consider Side Effects:**
- Will this fix affect other operations?
- Does it change any public interfaces?
- Does it break backward compatibility?
- Are there threading implications?

---

## Quick Reference

### Protocol Constants
```c
// Frame delimiters
#define FRAME_START     0x7E
#define FRAME_END       0x7F

// Commands
#define CMD_REQ         0x52
#define RX_DATA         0x55  
#define TX_DATA         0x56

// Frame limits
#define MAX_FRAME_SIZE  64
#define MAX_PAYLOAD     56
```

### Build Targets
```bash
# Firmware
make clean all    # Clean build with versioning
make hex         # Generate hex file  
make bin         # Generate bin file
make flash       # Flash to hardware

# GUI
msbuild /p:Configuration=Debug   # Debug build
msbuild /p:Configuration=Release # Release build
```

### Key Classes
```csharp
// GUI Key Classes
HardwareInfo      // Hardware singleton manager
UartIspTransport  // USB communication
ICart             // Cartridge interface
MuxManager        // DTCL Multiplexer support (2 slots)
DPSMuxManager     // DPS Multiplexer support (4 slots) ✅ NEW
DPSMuxChannelInfo // DPS MUX channel data model ✅ NEW
PCLog             // Performance check logging (extended for DPS MUX)

// Firmware Key Classes
IspCommandManager // Command routing
SerialTransport   // USB transport
Darin2/Darin3     // Cartridge handlers
```

### DPS MUX Quick Reference (NEW - February 2026)
```csharp
// DPS MUX Protocol
DPSMuxManager.switch_Mux((char)0);       // Switch all channels OFF
DPSMuxManager.switch_Mux((char)1);       // Switch to Channel 1
DPSMuxManager.switch_Mux((char)8);       // Switch to Channel 8

// Correct MUX Channel Switching Pattern
await switch_Mux((char)0);               // OFF first
await Task.Delay(500);
await switch_Mux((char)channelNo);       // Switch to channel
await Task.Delay(1000);                  // Wait for stabilization
// ... perform operations ...
await switch_Mux((char)0);               // Switch OFF after

// DPS MUX Logging
PCLog.Instance.CreateNewLog(
    testNumber, inspectorName, dtcSno, unitSno,
    withCart: true,
    slotInfo: channel.channel_SlotInfo[slot],
    ChNo: channelNo,
    isDPSMux: true  // Creates DPSMux/Channel-X/Slot-Y/ structure
);

// XAML Array Binding (CRITICAL - Must have explicit mode!)
<DataGridCheckBoxColumn Header="S1"
    Binding="{Binding IsSlotSelected[1], Mode=TwoWay, UpdateSourceTrigger=PropertyChanged}"/>

// ObservableCollection Usage
private ObservableCollection<DPSMuxChannelInfo> channelDataSource;
DPSMuxChannelGrid.ItemsSource = channelDataSource;
// Use channelDataSource in code-behind, not dpsMuxManager.channels

// Folder Structure
DPSMux/
  Channel-1/...Channel-8/
    Slot-1/...Slot-4/
      999_log.txt
      TestLog/
        999_log_DD-MM-YYYY_HH-MM-SS.txt
```

---

## Session Continuity Notes

**📋 COMPLETE DOCUMENTATION INDEX**

All project documentation is organized as follows:

### Primary Documentation (Start Here)
1. **`CLAUDE.md`** (THIS FILE) - Development guidance, architecture overview, critical issues
2. **`README.md`** - User-facing documentation, build instructions, quick start
3. **`docs/DTCL_GUI_Architecture.md`** - Complete GUI architecture (v1.1, Feb 2026, 1,290 lines)
4. **`docs/DTCL_Firmware_Architecture.md`** - Complete firmware architecture (v1.0, Nov 2025, 825 lines)

### Subsystem Documentation
5. **`DPS_DTCL/Mux/CRITICAL_ISSUES.md`** - DTCL MUX known bugs (MUST READ!)
6. **`DPS_DTCL/Mux/RefactoringNotes.md`** - MUX refactoring history
7. **`DPS_DTCL/Cartridges/Darin2_Analysis.md`** - Darin2.cs code analysis
8. **`DPS_DTCL/Cartridges/Darin2_RefactoringNotes.md`** - Darin2 refactoring notes

### Configuration Files
9. **`Default.txt`** - Runtime configuration (layout mode, log level)
10. **`App.config`** - GUI application configuration
11. **`version.h`** - Firmware version definitions (D2, D3, DTCL)

---

**🎯 FOR NEXT SESSION - QUICKEST PATH TO UNDERSTANDING:**

**If you need to understand the ENTIRE project (30-minute read):**
1. Read CLAUDE.md sections: "Project Overview", "Architecture Documentation", "Current State"
2. Read `docs/DTCL_GUI_Architecture.md` - Focus on Section 11 (DPS MUX)
3. Skim `docs/DTCL_Firmware_Architecture.md` - Focus on protocol and commands
4. Review "Bug Fixing Quick Reference" section in CLAUDE.md for all subsystems

**If you need to fix ANY bug (5-10 minute setup):**
1. Read "Bug Fixing Quick Reference" (line ~729) - Subsystem navigation table
2. Find your subsystem and jump to relevant section
3. Read "Common Bug Patterns by Symptom" (line ~1050) - Match your symptom
4. Follow "Diagnostic Flowchart" (line ~1230) - Step-by-step debugging
5. Use "Error Handling Patterns" (line ~1165) - Implement proper error handling

**If you need to work on DPS MUX (10-minute read):**
1. Read Section "✅ DPS MUX Enhancements (February 8, 2026)" in THIS FILE
2. Read `docs/DTCL_GUI_Architecture.md` Section 11
3. Check "Critical Lessons from DPS MUX Implementation" (line ~689)
4. Review "DPS MUX Quick Reference" (line ~777)

**If you need to work on DTCL MUX (5-minute read):**
1. **CRITICAL**: Read `DPS_DTCL/Mux/CRITICAL_ISSUES.md` FIRST!
2. Understand singleton anti-pattern (NEVER dispose HardwareInfo/DpsInfo)
3. Check "Correct MUX Architecture" diagram in THIS FILE

**If you need to build/release (5-minute read):**
1. Read README.md sections: "Building the Project", "Release Management"
2. Check current versions in THIS FILE (GUI 1.3, Firmware 3.6)

---

**📊 CURRENT PROJECT STATE (February 8, 2026)**

### What's Working ✅
- **DTCL Standalone**: Fully functional (2 slots: 1 D2 + 1 D3)
- **DPS2/DPS3 Standalone**: Fully functional (4 identical slots)
- **DPS MUX Window**: Fully implemented (8 channels × 4 slots = 32 total)
  - Select All checkbox
  - Cart validation (with/without cart modes)
  - MUX reconnection (improved reliability)
  - Dynamic window loading (reads Default.txt)
  - Performance check (awaiting hardware testing)

### What Needs Work 🔄
- **DTCL MUX Window**: Has critical bugs (see CRITICAL_ISSUES.md)
  - Singleton disposal bug (line 292 in MuxManager.cs)
  - Broken references (MuxChannelInfos[i])
  - Performance check not using new class
- **DPS MUX Hardware Testing**: Needs actual DPS2/3 4IN1 hardware
- **Performance Check**: Needs hardware-specific conditions for DPS2/3

### Don't Touch Without Reading First ⚠️
- **Singleton Classes**: HardwareInfo, DpsInfo (NEVER dispose them!)
- **DTCL MUX Code**: Read CRITICAL_ISSUES.md before any changes
- **Protocol Constants**: Breaking changes affect hardware compatibility

---

**🔧 RECENT WORK COMPLETED (February 2026)**

**February 7, 2026:**
- ✅ DPS MUX Window fully implemented
- ✅ 8 channels × 4 slots = 32 total slot support
- ✅ Timer-based MUX detection (2000ms intervals)
- ✅ Clean validation flow for ConfirmLog
- ✅ DPS MUX logging structure - backward compatible
- ✅ Fixed XAML array binding issues (Mode=TwoWay, UpdateSourceTrigger)
- ✅ Fixed MUX protocol numeric casting ((char)0 vs '0')

**February 8, 2026:**
- ✅ Select All checkbox functionality with recursion prevention
- ✅ Cart validation for log creation (with/without cart modes)
- ✅ MUX reconnection reliability improvements (transport cleanup, timer intervals)
- ✅ Dynamic window loading (DPS vs DTCL MUX) based on Default.txt
- ✅ Exit vs X button handling for proper window close
- ✅ Fixed missing System.ComponentModel namespace
- ✅ Documentation overhaul (CLAUDE.md, README.md, GUI Architecture doc)

**February 15, 2026:**
- ✅ **CRITICAL FIX**: USB CDC transmission failures on Intel client PCs (heisenbug)
- ✅ Fixed spurious firmware response handling (RX_MODE_ACK during TX operations)
- ✅ Added SPURIOUS_RESPONSE enum and retry logic for all operations
- ✅ Fixed array mutation bug in retry mechanism (SetMode() modifying input array)
- ✅ Added 50ms delay after RESET commands for firmware state stabilization
- ✅ Fixed missing await in EraseCartFiles() causing UART timeout errors
- ✅ Enhanced EraseBlockNo() with retry logic
- ✅ Fixed progress bar flickering (single source of progress updates)
- ✅ Comprehensive documentation: `docs/BugFixes/USB_CDC_Transmission_Failures_Intel_PCs.md`
- ✅ All operations tested successfully on Intel client PC (files write with correct sizes)

---

**🚀 NEXT STEPS (If Continuing Development)**

**Immediate Priority:**
- Hardware testing with actual DPS2 4IN1 and DPS3 4IN1 units
- Performance check execution validation with real hardware

**Future Enhancements:**
- Individual command buttons (Erase, Write, Read, LoopBack) - currently placeholders
- Progress bar accuracy for duration-based mode
- DTCL MUX critical issues fixes (see CRITICAL_ISSUES.md)

**Major Refactoring (Planned):**
- Consolidate Darin2.cs code duplication
- Improve thread safety throughout
- Better separation of concerns
- Fix DTCL MUX singleton disposal issues

---

**Last Updated**: February 15, 2026
**Architecture Documentation**: Complete with integrated diagrams
**Current Versions**: Firmware 3.6, GUI 1.3
**Critical Bug Fixes**: ✅ USB CDC transmission failures on Intel PCs resolved (Feb 15, 2026)
**DPS MUX Status**: ✅ Fully Implemented (Feb 2026) - Select All, Cart Validation, Reconnection fixes complete
**DTCL MUX Status**: 🔄 Has known critical issues (see CRITICAL_ISSUES.md)
**Overall Status**: Production Ready for DTCL and DPS2/3 standalone, DPS MUX functional pending hardware validation
