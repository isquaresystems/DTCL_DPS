# CLAUDE.md

This file provides guidance to Claude Code (claude.ai/code) when working with code in this repository.

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

### Hardware Configuration
- **MCU**: STM32F411VET6 (Cortex-M4, 512KB Flash, 128KB RAM)
- **Interface**: USB CDC for PC communication
- **Cartridge Support**: Darin-I, Darin-II, Darin-III cartridges
- **Multi-slot**: 4 cartridge slots for simultaneous operations (2 slots for DTCL hardware)

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
├── Mux/                       # 8-channel multiplexer support
│   ├── MuxWindow.xaml(.cs)   # MUX control window
│   ├── MuxManager.cs         # MUX business logic
│   └── MuxChannelManager.cs  # Channel management
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

### STM32 Firmware Development
```bash
# Build firmware (with versioning)
cd DTCL
make clean all

# Build with custom version
make VERSION_MAJOR=3 VERSION_MINOR=6 clean all

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

---

## Critical Development Notes

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

---

## Common Issues & Solutions

### Build Issues
```bash
# C# build fails - restore packages
nuget restore DPS_DTCL/DTCL.sln

# Firmware build fails - clean first
cd DTCL && make clean all

# Version conflicts
make VERSION_MAJOR=3 VERSION_MINOR=6 clean all
```

### Communication Issues
- **No Hardware Detected**: Check USB drivers, COM port permissions
- **Timeouts**: Verify cable connection, check LED status on hardware
- **Protocol Errors**: Check CRC8 implementation, frame boundaries

### Threading Issues
- **UI Freezing**: Use async/await, avoid blocking UI thread
- **Race Conditions**: Use locks around shared state (HardwareInfo)
- **Memory Leaks**: Dispose transport objects, unsubscribe events

---

## Development Memories & Important Notes

### Refactoring History
- **Transport Layer**: Moved all newly created files from core to transport folder
- **Protocol Naming**: Changed from "VennaProtocol" to "IspProtocol"
- **Architecture**: Added comprehensive documentation (Nov 2025)
- **Documentation Update**: Integrated all block diagrams into architecture documents (Nov 2025)
- **Documentation Organization**: Moved architecture docs to docs/ folder for better organization
- **Version Update**: Updated to Firmware 3.6 and GUI 9.8
- **Performance**: Implemented real-time logging for performance checks
- **Version 9.8 Changes**: Fixed bugs in Darin2 copy function, improved error handling

### Current State
- ✅ **Architecture Documented**: Complete system documentation created
- ✅ **ISP Protocol**: Unified protocol implementation  
- ✅ **Performance Logging**: Real-time timestamps in performance checks
- ✅ **Thread Safety**: Proper disposal and thread management
- 🔄 **Known Issues**: Some edge cases in hardware detection still being refined

### Future Considerations
- **Protocol Evolution**: ISP protocol is stable, avoid breaking changes
- **Performance**: Consider optimizing large file transfers
- **Testing**: Expand unit test coverage for protocol layers
- **Documentation**: Keep architecture docs updated with changes

---

## File Locations & Key Components

### Critical Files to Understand
1. **MainWindow.xaml.cs**: Main GUI logic and event handling
2. **HardwareInfo.cs**: Hardware management singleton
3. **UartIspTransport.cs**: USB communication transport
4. **main.cpp**: Firmware entry point and initialization
5. **IspCommandManager.cpp**: Firmware command routing
6. **Darin2.cpp/Darin3.cpp**: Cartridge-specific implementations

### Configuration Files
- **App.config**: GUI application configuration
- **version.h**: Firmware version definitions
- **Makefile**: Firmware build configuration
- **DTCL.csproj**: C# project configuration

### Documentation Files
- **README.md**: Project overview and build instructions
- **docs/DTCL_Firmware_Architecture.md**: Detailed firmware architecture with integrated flow diagrams
- **docs/DTCL_GUI_Architecture.md**: Detailed GUI architecture with integrated system diagrams

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
MuxManager        // Multiplexer support

// Firmware Key Classes  
IspCommandManager // Command routing
SerialTransport   // USB transport
Darin2/Darin3     // Cartridge handlers
```

---

**Last Updated**: November 2025  
**Architecture Documentation**: Complete with integrated diagrams  
**Current Versions**: Firmware 3.6, GUI 9.8  
**Status**: Production Ready
