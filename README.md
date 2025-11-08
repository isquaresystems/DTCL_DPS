# DTCL - Data Transfer Cartridge Loader System

A professional data programming system for cartridge-based storage devices with hybrid NAND/CF interface support.

**Developed by ISquare Systems**

---

## 📋 Table of Contents

- [Project Overview](#-project-overview)
- [System Architecture](#-system-architecture)
- [Hardware Variants](#-hardware-variants)
- [Quick Start](#-quick-start)
- [Building the Project](#-building-the-project)
- [Version Management](#-version-management)
- [Architecture Documentation](#-architecture-documentation)
- [Development](#-development)
- [Release Management](#-release-management)
- [Troubleshooting](#-troubleshooting)

---

## 🚀 Project Overview

**DTCL (Data Transfer Cartridge Loader)** is a specialized data programming system for cartridge-based data storage devices, supporting multiple cartridge types with professional-grade reliability and performance.

### Core Components

- **DTCL GUI**: C# WPF application for user interface and control
- **DTCL Firmware**: STM32-based firmware for hardware control  
- **ISP Protocol**: Communication protocol between GUI and hardware (via USB COM port)
- **MUX Support**: 8-channel multiplexer for testing multiple DTCL units

### Hardware Configuration
- **MCU**: STM32F411VET6 (Cortex-M4, 512KB Flash, 128KB RAM)
- **Interface**: USB CDC for PC communication
- **Cartridge Support**: Darin-I, Darin-II, Darin-III cartridges
- **Multi-slot**: 4 cartridge slots for simultaneous operations

### Supported Cartridge Types
1. **Darin-I**: Basic data cartridge with standard operations
2. **Darin-II**: Enhanced cartridge with NAND flash support
3. **Darin-III**: Advanced cartridge with Compact Flash and FatFS

---

## 🏗️ System Architecture

### Communication Flow
```
DTCL GUI ←→ USB COM Port ←→ STM32 Hardware ←→ Flash Interface
```

### ISP Protocol Communication
- **Protocol**: ISP Protocol for command/response communication  
- **Transport**: USB COM port (USB CDC-ACM)
- **Frame Format**: START(0x7E) + LENGTH + PAYLOAD + CRC8 + END(0x7F)
- **Max Frame Size**: 64 bytes
- **Data Payload**: 56 bytes per packet
- **Buffer Size**: 1023 bytes for chunk transfers

---

## 🔧 Hardware Variants

### DPS2 (D2) - NAND Flash Interface
- **Controller**: STM32F411VET6
- **Storage**: NAND Flash interface
- **Slots**: **4 slots** for cart programming
- **Operations**: Erase, Write, Read, Copy, Compare
- **Status**: LED indicators for operation feedback
- **Communication**: USB COM port to PC

### DPS3 (D3) - Compact Flash Interface  
- **Controller**: STM32F411VET6
- **Storage**: Compact Flash with FatFS filesystem
- **Slots**: **4 slots** for cart programming
- **Operations**: Erase, Write, Read, Copy, Compare, **Format**
- **Status**: LED indicators for operation feedback  
- **Communication**: USB COM port to PC

### DTCL - Hybrid D2+D3 Interface ✨ **NEW**
- **Controller**: STM32F411VET6
- **Storage**: **Dual interface** - Both NAND Flash and Compact Flash support
- **Slots**: **2 slots total** - 1 slot for D2-style carts + 1 slot for D3-style carts
- **Operations**: All D2 and D3 operations (Erase, Write, Read, Copy, Compare, Format)
- **Status**: LED indicators for operation feedback
- **Communication**: USB COM port to PC
- **Firmware**: Unified firmware combining D2 and D3 capabilities

### Supported Operations

| Operation | DPS2 (NAND Flash) | DPS3 (Compact Flash) | DTCL (Hybrid) |
|-----------|-------------------|---------------------|---------------|
| **Erase** | ✅ Block-based (4 slots) | ✅ File-based (4 slots) | ✅ Both types (1+1 slots) |
| **Write** | ✅ Page programming | ✅ File write | ✅ Both methods |
| **Read** | ✅ Page/block read | ✅ File read | ✅ Both methods |
| **Copy** | ✅ Internal copy | ✅ File-to-file | ✅ Cross-interface capable |
| **Compare** | ✅ Data verification | ✅ File comparison | ✅ Both methods |
| **Format** | ❌ Not applicable | ✅ FatFS format | ✅ D3-slot only |

---

## ⚡ Quick Start

### Prerequisites
- **.NET Framework 4.8** (for GUI application)
- **Visual Studio 2022** (recommended for C# development)
- **ARM GCC toolchain** (arm-none-eabi-gcc for firmware)
- **STM32 USB CDC drivers** (for hardware communication)

### Hardware Setup
1. **Install Drivers**: STM32 Virtual COM Port drivers
2. **Connect Hardware**: DPS2/DPS3 via USB cable
3. **Verify Connection**: Launch DPS_DTCL - should auto-detect hardware

### Quick Run
```bash
# Run the GUI application
cd DPS_DTCL/bin/Release
DTCL.exe
```

---

## 🏗️ Building the Project

### GUI Application

The C# WPF application uses **.NET Framework 4.8** and is compiled with the **Microsoft C# Compiler (Roslyn)** in Visual Studio 2022.

**Project Details:**
- **Target Framework**: .NET Framework 4.8
- **Project Type**: WPF Application (`DTCL.csproj`)
- **Compiler**: Microsoft C# Compiler (Roslyn) via Visual Studio 2022
- **Dependencies**: System.Text.Json 9.0.0, WPF controls
- **Output**: `DPS_DTCL/bin/Debug/DTCL.exe` or `DPS_DTCL/bin/Release/DTCL.exe`

#### Visual Studio 2022 (Recommended)
```bash
# Open in Visual Studio 2022
cd DPS_DTCL
start DTCL.sln

# Then use Visual Studio:
# - Build > Build Solution (Debug)
# - Build > Rebuild Solution (Release)
```

#### Command Line Build (Alternative)
```bash
# Debug build
cd DPS_DTCL
msbuild DTCL.sln /p:Configuration=Debug

# Release build
msbuild DTCL.sln /p:Configuration=Release

# Or using dotnet CLI (if available)
dotnet build DTCL.sln --configuration Debug
dotnet build DTCL.sln --configuration Release
```

### Firmware

#### 🆕 Auto-Versioning System (Recommended)

All three firmware projects (D2, D3, and DTCL) now feature professional auto-versioning with automatic ELF file naming and VSCode integration.

#### Individual Project Builds

**D2 Firmware (NAND Flash):**
```bash
# Default version build (1.0)
cd Firmware/D2_DPS_4IN1
make clean all
# Creates: D2_DPS_4IN1_V1_0.elf/hex/bin

# Custom version build
make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
# Creates: D2_DPS_4IN1_V1_5.elf/hex/bin
```

**D3 Firmware (Compact Flash):**
```bash
# Default version build (1.0)
cd Firmware/D3_DPS_4IN1
make clean all
# Creates: D3_DPS_4IN1_V1_0.elf/hex/bin

# Custom version build
make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
# Creates: D3_DPS_4IN1_V1_5.elf/hex/bin
```

**DTCL Firmware (Hybrid D2+D3):**
```bash
# Default version build (1.0)
cd Firmware/DTCL
make clean all
# Creates: DTCL_V1_0.elf/hex/bin

# Custom version build
make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
# Creates: DTCL_V1_5.elf/hex/bin
```

#### 🚀 Batch Building with Scripts

**Using build_all.bat Script (Recommended for Production):**

```bash
# Navigate to scripts directory
cd Scripts

# Build single project with version
build_all.bat d2 1 5        # Build D2 firmware with version 1.5
build_all.bat d3 1 5        # Build D3 firmware with version 1.5
build_all.bat dtcl 1 5      # Build DTCL firmware with version 1.5

# Build multiple projects with same version
build_all.bat both 1 5      # Build both D2 and D3 with version 1.5
build_all.bat all 1 5       # Build all firmware (D2, D3, DTCL) with version 1.5
```

**build_all.bat Features:**
- ✅ Supports individual (d2, d3, dtcl) or combined (both, all) builds
- ✅ Version parameter validation and error handling
- ✅ Automatic clean builds for reliable results
- ✅ Clear build status reporting
- ✅ Generated file naming confirmation

#### Manual Sequential Builds

```bash
# Build all projects sequentially (command line)
cd Firmware/D2_DPS_4IN1 && make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
cd ../D3_DPS_4IN1 && make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
cd ../DTCL && make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
```

#### Legacy Builds (Unversioned)

```bash
# Legacy builds without auto-versioning
cd Firmware/D2_DPS_4IN1 && make legacy  # Creates: D2_DPS_4IN1.elf
cd Firmware/D3_DPS_4IN1 && make legacy  # Creates: D3_DPS_4IN1.elf
cd Firmware/DTCL && make legacy         # Creates: DTCL.elf
```

#### VSCode Integration

The auto-versioning system automatically updates VSCode debug configurations:
- **Launch configurations**: Point to correct versioned ELF files
- **Task configurations**: Use proper version parameters
- **Settings**: Reference current build artifacts

All VSCode JSON files sync automatically when building with different versions.

---

## 🔄 Version Management

### Current Versions
- **D2 Default**: 3.6 (configurable)
- **D3 Default**: 3.6 (configurable)
- **DTCL Default**: 3.6 (configurable) - Hybrid D2+D3 system
- **GUI**: 9.8 (Set in `DPS_DTCL/MainWindow.xaml.cs`)

### Checking Firmware Versions

```bash
# Check current version settings
findstr "FIRMWARE_VERSION_" Firmware/D2_DPS_4IN1/Core/Inc/version.h
findstr "FIRMWARE_VERSION_" Firmware/D3_DPS_4IN1/Core/Inc/version.h
findstr "FIRMWARE_VERSION_" Firmware/DTCL/Core/Inc/version.h
```

### Setting Custom Versions

#### Method 1: Build Parameters (Recommended)
```bash
# Temporary version for single build
cd Firmware/D2_DPS_4IN1
make VERSION_MAJOR=2 VERSION_MINOR=1 clean all

cd Firmware/D3_DPS_4IN1
make VERSION_MAJOR=2 VERSION_MINOR=1 clean all

cd Firmware/DTCL
make VERSION_MAJOR=2 VERSION_MINOR=1 clean all
```

#### Method 2: Edit Version Headers (Persistent)
Edit these files:
- `Firmware/D2_DPS_4IN1/Core/Inc/version.h`
- `Firmware/D3_DPS_4IN1/Core/Inc/version.h`
- `Firmware/DTCL/Core/Inc/version.h`

Update these defines:
```c
#define FIRMWARE_VERSION_MAJOR  2
#define FIRMWARE_VERSION_MINOR  1
```

Then build normally:
```bash
cd Firmware/D2_DPS_4IN1 && make clean all
cd Firmware/D3_DPS_4IN1 && make clean all
cd Firmware/DTCL && make clean all
```

#### Method 3: Using build_all.bat Script
```bash
cd Scripts
build_all.bat both 2 1      # Sets both D2 and D3 projects to version 2.1
build_all.bat all 2 1       # Sets all projects (D2, D3, DTCL) to version 2.1
build_all.bat dtcl 2 1      # Sets only DTCL to version 2.1
```

### Version Integration Features

- **Firmware Embedding**: Version numbers embedded in firmware via `FirmwareVersion_SubCmdProcess`
- **Protocol Reporting**: ISP Protocol reports versions to GUI
- **File Naming**: ELF/HEX/BIN files automatically named with versions
- **VSCode Sync**: Debug configurations automatically update to match build artifacts

---

## 📚 Architecture Documentation

### Complete Architecture Documentation
This project includes comprehensive architecture documentation with integrated block diagrams:

1. **[DTCL_Firmware_Architecture.md](docs/DTCL_Firmware_Architecture.md)**
   - STM32 firmware architecture with detailed flow diagrams
   - Hardware abstraction layers
   - ISP protocol implementation
   - Command processing and execution flows
   - NAND/CF operation sequences
   - LED state management diagrams
   - Performance specifications

2. **[DTCL_GUI_Architecture.md](docs/DTCL_GUI_Architecture.md)**  
   - WPF application architecture with system flow diagrams
   - Application startup and lifecycle flows
   - Hardware detection and scanning processes
   - Operation execution and MUX workflows
   - Performance check sequences
   - MVVM implementation patterns
   - Threading and concurrency models
   - Communication layers and protocol state machines
   - Error handling and recovery strategies
   - End-to-end data flow visualizations

---

## 🏛️ Architecture

### Communication Flow
```
DPS_DTCL GUI ←→ USB COM Port ←→ STM32 Hardware ←→ Flash Interface
```

### Project Structure

```
DTCL4/
├── DPS_DTCL/                 # C# WPF GUI Application
│   ├── Cartridges/           # Cartridge implementations (Darin1, 2, 3)
│   ├── IspProtocol/          # ISP Protocol implementation (PC side)
│   ├── Transport/            # Hardware communication layer
│   ├── DataHandler/          # Data transfer management
│   ├── Mux/                  # MUX channel support
│   ├── Log/                  # Logging and performance tracking
│   └── Messages/             # File operation message handling
├── DTCL/                     # STM32 DTCL Firmware
│   ├── Core/                 # Main firmware source
│   │   ├── Inc/version.h     # Version definitions
│   │   └── Src/              # Source files
│   │       ├── main.cpp      # Main application
│   │       ├── Darin2.cpp    # NAND Flash handler
│   │       ├── Darin3.cpp    # Compact Flash handler
│   │       └── Protocol/     # ISP Protocol implementation
│   ├── Drivers/              # STM32 HAL drivers
│   ├── Middlewares/          # USB CDC middleware
│   └── Makefile              # Build configuration
├── docs/                     # Architecture documentation
│   ├── DTCL_Firmware_Architecture.md
│   └── DTCL_GUI_Architecture.md
└── CLAUDE.md                 # Development guidance
```

### ISP Protocol Communication
- **Transport**: USB CDC-ACM (Virtual COM Port)
- **Structure**: Frame-based protocol with CRC8 validation
- **Commands**: Hardware control, data transfer, status queries
- **Frame Format**: START(0x7E) + LENGTH + PAYLOAD + CRC8 + END(0x7F)
- **Current State**: Unified ISP Protocol for DTCL hardware

### Hardware Slot Configuration
- **DPS2 (D2)**: 4 independent NAND Flash slots
- **DPS3 (D3)**: 4 independent Compact Flash slots  
- **DTCL (Hybrid)**: 1 NAND Flash slot + 1 Compact Flash slot (total: 2 slots)

---

## 💻 Development

### Development Environment Setup

#### For C# Development
1. **Install Prerequisites**:
   - **Visual Studio 2022** with .NET Framework 4.8 support
   - **.NET Framework 4.8 Developer Pack**
   - **NuGet Package Manager** (included with Visual Studio)

2. **Clone and Build**:
   ```bash
   git clone https://github.com/isquaresystems/DPSFinalSolution.git
   cd DPSFinalSolution/DPS_DTCL
   
   # Method 1: Visual Studio 2022 (Recommended)
   start DTCL.sln
   # Then: Build > Build Solution
   
   # Method 2: Command line
   msbuild DTCL.sln /p:Configuration=Debug
   ```

#### For Firmware Development
1. **Install Prerequisites**:
   - STM32CubeIDE or VSCode with Cortex-Debug extension
   - ARM GCC toolchain (arm-none-eabi-gcc)
   - STM32CubeMX (for hardware configuration)

2. **Build Configuration**:
   - **Target MCU**: STM32F411VET6
   - **Debug Interface**: ST-Link
   - **Build System**: Makefile-based with auto-versioning

### Current Development Focus
- ✅ **Auto-versioning system**: Complete and tested
- ✅ **Build script automation**: Professional build_all.bat script
- ✅ **VSCode integration**: Automatic debug configuration updates
- 🔄 **Thread safety improvements**: In progress
- 🔄 **Protocol unification**: Planned for future releases

### Contributing Guidelines

**Development Workflow:**
- Follow existing code patterns and threading safety guidelines
- Test hardware communication thoroughly before commits
- Maintain backward compatibility with existing hardware
- Use auto-versioning system for all builds

**Version Management:**
- **Firmware**: Use `make VERSION_MAJOR=X VERSION_MINOR=Y all` or edit version.h
- **GUI**: Update `GUI_VERSION` constant in `MainWindow.xaml.cs`
- **Testing**: Verify ISP Protocol version reporting in GUI

---

## 📦 Release Management

### Professional Release Process

Follow these steps to create a complete release package:

#### 1. Update Version Numbers
```bash
# Update GUI Version
# Edit file: DPS_DTCL/MainWindow.xaml.cs
# Change: const string GUI_VERSION = "9.8";

# Update Firmware Versions
# Edit: DTCL/Core/Inc/version.h
# Then: cd DTCL && make VERSION_MAJOR=3 VERSION_MINOR=7 clean all
```

#### 2. Build GUI Release
```bash
# Method 1: Visual Studio 2022 (Recommended)
cd DPS_DTCL
start DTCL.sln
# Then in Visual Studio:
# 1. Select "Release" configuration (top toolbar)
# 2. Build > Rebuild Solution
# 3. Verify output: bin/Release/DTCL.exe created

# Method 2: Command line (Alternative)
msbuild DTCL.sln /p:Configuration=Release
```

#### 3. Prepare Release Package
```bash
cd Scripts
prepare_release.bat

# The script will:
# [1/4] Clean SetUpFiles directory and create FirmwareElf folder
# [2/4] Copy GUI files, DLLs, and data folders
# [3/4] Copy latest firmware ELF files to FirmwareElf/ folder  
# [4/4] Clean up firmware build folders
```

#### 4. Create Installer with InstallForge
```bash
# Open InstallForge application
# 1. Load project: Scripts\INSTALL_FORGE_DTCL.ifp
# 2. Verify Source Directory points to: SetUpFiles\
# 3. Update version number in installer settings if needed
# 4. Build installer -> Creates: Release\DPS_DTCL.exe
```

#### 5. Finalize Release
```bash
# Navigate to Release folder and rename installer
cd Release
ren DPS_DTCL.exe DTCL_V9.7.exe

# Verify contents
dir Release\
# Should show: DTCL_V9.7.exe (final installer)
```

#### 6. Release Verification
```bash
# Test the installer on a clean system
# 1. Run DTCL_V9.7.exe
# 2. Install to default location
# 3. Launch application and verify version in About dialog
# 4. Test hardware detection and basic operations
# 5. Check that firmware files are accessible in installation directory
```

### Release Package Contents

The `Scripts\prepare_release.bat` script automatically creates a complete installer package in the `SetUpFiles/` folder with the following structure:

#### SetUpFiles Directory Structure
```
SetUpFiles/
├── DTCL.exe                    # Main GUI application (Release build)
├── *.dll                      # .NET dependencies (System.Text.Json, etc.)
├── *.config                   # Application configuration files
├── *.json                     # Settings and configuration JSON files
├── *.xml                      # Documentation and metadata files
├── *.jpg, *.png, *.ico        # Application icons and images
├── Default.txt                # Default configuration file
├── D1/                        # Cart data folder for D1 hardware
├── D2/                        # Cart data folder for D2 hardware
├── D3/                        # Cart data folder for D3 hardware
├── PopUpMessage/              # GUI message definitions and translations
│   └── PopUpMessages.json     # Application messages and prompts
└── FirmwareElf/               # 🆕 Firmware binaries folder
    ├── D2_DPS_4IN1_V*.elf     # Latest D2 firmware (NAND Flash)
    └── D3_DPS_4IN1_V*.elf     # Latest D3 firmware (Compact Flash)
```

#### Script Features
The `prepare_release.bat` script performs these operations:
1. **Clean Setup**: Removes old `SetUpFiles/` and creates fresh directory structure
2. **GUI Application**: Copies all Release build files from `DPS_DTCL/bin/Release/`
3. **Dependencies**: Includes all required DLLs, configs, and assets
4. **Data Folders**: Copies D1/, D2/, D3/, and PopUpMessage/ directories
5. **🆕 Firmware Organization**: Creates `FirmwareElf/` folder and copies latest versioned ELF files
6. **Build Cleanup**: Removes firmware build folders to prepare for next development cycle

#### Firmware Selection Logic
- Automatically finds the **latest versioned ELF files** based on file modification time
- Copies the most recent `D2_DPS_4IN1_V*.elf` and `D3_DPS_4IN1_V*.elf` files
- Organizes firmware in dedicated `FirmwareElf/` subfolder for better installer organization

---

## 🔧 Troubleshooting

### Build Issues

#### C# Application
- **Missing NuGet packages**: 
  - Visual Studio: Tools > NuGet Package Manager > Package Manager Console > `Update-Package -reinstall`
  - Command line: `nuget restore DTCL.sln`
- **Build errors**: 
  - Verify .NET Framework 4.8 Developer Pack installation
  - Check Visual Studio 2022 has .NET Framework workload installed
- **Missing references**: 
  - Check `packages.config` and restore NuGet packages
  - Verify `System.Text.Json 9.0.0` is properly referenced
- **Compiler issues**:
  - Use Visual Studio 2022 (recommended) which includes Roslyn C# compiler
  - Alternative: Ensure MSBuild has .NET Framework 4.8 targeting pack

#### Firmware
- **Make command not found**: Ensure make and ARM GCC in PATH
- **Build timeouts**: Use `make clean` before rebuilding
- **Version conflicts**: Use `make clean all` for fresh builds

### build_all.bat Script Issues
```bash
# If script fails, check:
# 1. Current directory (should be in Scripts folder)
# 2. Makefile existence in both firmware directories
# 3. ARM GCC toolchain installation

# Manual alternative:
cd Firmware/D2_DPS_4IN1 && make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
cd ../D3_DPS_4IN1 && make VERSION_MAJOR=1 VERSION_MINOR=5 clean all
```

### Hardware Communication
- **No Hardware Detected**: Check USB drivers and COM port permissions
- **Communication Timeouts**: Verify cable connection and port availability
- **Operation Failures**: Check LED status on hardware for error indications

### VSCode Integration
- **Debug configs not updating**: Run `make update-json` manually
- **Wrong ELF references**: Rebuild with `make clean all`
- **Extension issues**: Ensure Cortex-Debug extension is installed

---

## 📞 Support

For technical support and development questions:
- **Development Documentation**: See `CLAUDE.md`
- **Issue Reporting**: Contact ISquare Systems development team
- **Hardware Support**: Refer to hardware-specific documentation

---

**© 2025 ISquare Systems. All rights reserved.**