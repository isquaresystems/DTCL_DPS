# DTCL GUI Architecture Documentation
## Data Transfer and Control Link - WPF Application

**Version:** 1.0  
**Date:** November 2025  
**Author:** ISquare Systems

---

## Table of Contents
1. [Executive Summary](#1-executive-summary)
2. [System Overview](#2-system-overview)
3. [Application Architecture](#3-application-architecture)
4. [Component Architecture](#4-component-architecture)
5. [Communication Layer](#5-communication-layer)
6. [Data Management](#6-data-management)
7. [User Interface Architecture](#7-user-interface-architecture)
8. [Threading & Concurrency](#8-threading--concurrency)
9. [Error Handling & Logging](#9-error-handling--logging)
10. [Performance & Optimization](#10-performance--optimization)

---

## 1. Executive Summary

The DTCL GUI is a professional WPF application built on .NET Framework 4.8, providing a comprehensive interface for managing data transfer operations with DTCL hardware. The application implements a sophisticated multi-layered architecture with robust hardware communication, real-time status monitoring, and support for both single-unit and multi-channel (MUX) operations.

### Key Features:
- **Real-time Hardware Detection**: Automatic USB device discovery
- **Multi-Cartridge Support**: Darin-I, II, and III cartridge types
- **MUX Integration**: 8-channel multiplexer support
- **Performance Testing**: Comprehensive testing with logging
- **Thread-Safe Operations**: Async/await patterns throughout
- **Professional UI**: MVVM-based WPF implementation

---

## 2. System Overview

### 2.1 Application Startup Flow

The following diagram shows the complete application initialization process:

```
Application Launch
      │
      ▼
┌─────────────────┐
│   App.xaml.cs   │
│                 │
│ OnStartup()     │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────┐
│       SplashScreenWindow            │
│  ┌───────────────────────────────┐  │
│  │   Display Logo & Version      │  │
│  │   Start 3-second Timer        │  │
│  │   Show Loading Animation      │  │
│  └───────────────┬───────────────┘  │
└──────────────────┼──────────────────┘
                   │ Timer Elapsed
                   ▼
┌─────────────────────────────────────┐
│         MainWindow Init             │
├─────────────────────────────────────┤
│                                     │
│  1. Create HardwareInfo Singleton   │
│     └─> Lazy<HardwareInfo>          │
│                                     │
│  2. Initialize UI Components        │
│     ├─> Setup Tab Control           │
│     ├─> Configure Buttons           │
│     └─> Set Event Handlers          │
│                                     │
│  3. Load Configuration              │
│     ├─> Read JSON Messages          │
│     └─> Setup File Paths            │
│                                     │
│  4. Start Hardware Scanning         │
│     └─> Timer @ 1Hz                 │
│                                     │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────┐
│  Main UI Loop   │
│                 │
│ • Handle Events │
│ • Update UI     │
│ • Process Cmds  │
└─────────────────┘
```

### 2.2 High-Level Architecture Diagram

```
┌─────────────────────────────────────────────────────────────────────┐
│                          DTCL GUI Application                        │
├─────────────────────────────────────────────────────────────────────┤
│                                                                      │
│  ┌─────────────────┐  ┌──────────────────┐  ┌──────────────────┐   │
│  │  Presentation   │  │  Business Logic  │  │   Data Access    │   │
│  │     Layer       │  │     Layer        │  │     Layer        │   │
│  │                 │  │                  │  │                  │   │
│  │  ┌───────────┐  │  │  ┌────────────┐ │  │  ┌────────────┐  │   │
│  │  │MainWindow │  │  │  │HardwareInfo│ │  │  │IspProtocol │  │   │
│  │  ├───────────┤  │  │  ├────────────┤ │  │  ├────────────┤  │   │
│  │  │MuxWindow  │  │  │  │CartManager │ │  │  │DataHandler │  │   │
│  │  ├───────────┤  │  │  ├────────────┤ │  │  ├────────────┤  │   │
│  │  │Utility    │  │  │  │Performance │ │  │  │FileOps     │  │   │
│  │  └───────────┘  │  │  │   Check    │ │  │  └────────────┘  │   │
│  └─────────────────┘  │  └────────────┘ │  └──────────────────┘   │
│                       └──────────────────┘                          │
│                                │                                    │
│  ┌─────────────────────────────┼───────────────────────────────┐   │
│  │                    Communication Layer                       │   │
│  │  ┌─────────────────────┐   │   ┌───────────────────────┐   │   │
│  │  │   ISP Protocol      │   │   │   USB CDC Transport   │   │   │
│  │  │  ┌───────────────┐  │   │   │  ┌────────────────┐  │   │   │
│  │  │  │Frame Encoder  │  │   │   │  │Serial Port Mgr │  │   │   │
│  │  │  ├───────────────┤  │   ▼   │  ├────────────────┤  │   │   │
│  │  │  │CRC8 Validator │  ◄───┴──►│  │Event Handler   │  │   │   │
│  │  │  ├───────────────┤  │       │  ├────────────────┤  │   │   │
│  │  │  │Command Router │  │       │  │Buffer Manager  │  │   │   │
│  │  │  └───────────────┘  │       │  └────────────────┘  │   │   │
│  │  └─────────────────────┘       └───────────────────────┘   │   │
│  └──────────────────────────────────────────────────────────────┘   │
│                                    │                                │
│                                    ▼                                │
│                         ┌──────────────────┐                       │
│                         │   USB Hardware   │                       │
│                         └──────────────────┘                       │
└─────────────────────────────────────────────────────────────────────┘
```

### 2.2 Technology Stack
- **Framework**: .NET Framework 4.8
- **UI Framework**: WPF (Windows Presentation Foundation)
- **Language**: C# 7.3
- **Pattern**: MVVM with Code-Behind
- **Dependencies**: System.Text.Json, System.IO.Ports
- **Build System**: MSBuild / Visual Studio 2022

---

## 3. Application Architecture

### 3.1 Hardware Detection Flow

The following diagram shows the hardware scanning and detection process:

```
Timer Tick (1Hz)
      │
      ▼
┌─────────────────────────────────────┐
│    ScanTimerHandler_Elapsed()       │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│    Check Scan In Progress           │
│    (Prevent Overlapping)            │
└────────┬────────────────────────────┘
         │ Not In Progress
         ▼
┌─────────────────────────────────────┐
│       Set Scan Mode                 │
│  ┌───────────────┬────────────────┐ │
│  │  Hardware     │   Cartridge    │ │
│  │  Detection    │   Detection    │ │
│  └───────┬───────┴────────┬───────┘ │
└──────────┼────────────────┼─────────┘
           │                │
           ▼                ▼
┌─────────────────┐ ┌─────────────────┐
│ Scan Hardware   │ │ Scan Cartridges │
│                 │ │                 │
│ • Find USB Port │ │ • Check 4 Slots │
│ • Open Connect  │ │ • Read Cart Type│
│ • Get Board ID  │ │ • Update Status │
└────────┬────────┘ └────────┬────────┘
         │                   │
         │                   │
         └────────┬──────────┘
                  │
                  ▼
         ┌────────────────────┐
         │  Process Results   │
         ├────────────────────┤
         │                    │
         │ Hardware Found?    │
         │    Yes: Update UI  │
         │    No: Show Disc.  │
         │                    │
         │ Cart Changes?      │
         │    Yes: Notify     │
         │    No: Continue    │
         │                    │
         └────────┬───────────┘
                  │
                  ▼
         ┌────────────────────┐
         │   Update UI        │
         │                    │
         │ • Status Text      │
         │ • LED Indicators   │
         │ • Enable Buttons   │
         │ • Slot Tabs        │
         └────────────────────┘
```

### 3.2 Operation Execution Flow

Detailed flow for user-initiated operations:

```
User Click Operation (e.g., Write)
      │
      ▼
┌─────────────────────────────────────┐
│        Pre-Operation Check          │
├─────────────────────────────────────┤
│ • Hardware Connected?               │
│ • Cart Present?                     │
│ • Slot Selected?                    │
│ • Files Valid?                      │
└────────┬────────────────────────────┘
         │ All Checks Pass
         ▼
┌─────────────────────────────────────┐
│      Disable UI Controls            │
│   (Prevent Multiple Ops)            │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│     Create Operation Task           │
│                                     │
│  Task.Run(async () => {            │
│    // Operation Logic               │
│  });                               │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────┐
│            Execute Operation                     │
├─────────────────────────────────────────────────┤
│                                                 │
│  1. Send Command                                │
│     └─> await _cmdControl.ExecuteCmd()          │
│                                                 │
│  2. Handle Large Data                           │
│     ┌─────────────────────┐                    │
│     │ For Each File:      │                    │
│     │   Open File         │                    │
│     │   while(data left)  │                    │
│     │   {                 │                    │
│     │     Read Chunk      │                    │
│     │     Send via USB    │                    │
│     │     Update Progress │                    │
│     │   }                 │                    │
│     │   Close File        │                    │
│     └─────────────────────┘                    │
│                                                 │
│  3. Wait for Completion                         │
│     └─> Monitor Response                        │
│                                                 │
└────────┬────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│      Update Progress UI             │
│                                     │
│  Dispatcher.Invoke(() => {          │
│    ProgressBar.Value = percent;     │
│    StatusText.Text = status;        │
│  });                               │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│     Operation Complete              │
├─────────────────────────────────────┤
│ • Log Results                       │
│ • Show Message                      │
│ • Re-enable UI                      │
│ • Update Status                     │
└─────────────────────────────────────┘
```

### 3.3 MUX Window Operation Flow

Flow for multi-channel MUX operations:

```
Open MUX Window
      │
      ▼
┌─────────────────────────────────────┐
│      Initialize MuxManager          │
│                                     │
│  channels[0..7] = new Channel       │
│  Each Channel:                      │
│    • Own HardwareInfo               │
│    • Own Transport                  │
│    • 4 Slots                        │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│    Channel Selection UI             │
├─────────────────────────────────────┤
│ ┌─────────────────────────────────┐ │
│ │ □ Channel 1  [Status: Ready]    │ │
│ │ □ Channel 2  [Status: Ready]    │ │
│ │ □ Channel 3  [Status: No HW]    │ │
│ │ ...                             │ │
│ │ □ Channel 8  [Status: Ready]    │ │
│ └─────────────────────────────────┘ │
└────────┬────────────────────────────┘
         │ User Selects Channels
         ▼
┌─────────────────────────────────────┐
│    Parallel Operation Execution     │
├─────────────────────────────────────┤
│                                     │
│  selectedChannels.ForEach(ch => {   │
│    Task.Run(async () => {           │
│      // Switch to channel           │
│      await SwitchMuxChannel(ch);    │
│                                     │
│      // Execute operation           │
│      await ExecuteOnChannel(ch);    │
│                                     │
│      // Update UI                   │
│      UpdateChannelStatus(ch);       │
│    });                             │
│  });                               │
│                                     │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│     Aggregate Results               │
│                                     │
│  • Collect all channel results      │
│  • Generate combined log            │
│  • Show summary message             │
└─────────────────────────────────────┘
```

### 3.4 Performance Check Flow

Comprehensive performance testing workflow:

```
Performance Check Start
      │
      ▼
┌─────────────────────────────────────┐
│    Configure PC Parameters          │
│  • Iteration Mode / Duration Mode   │
│  • With Cart / Without Cart         │
│  • Selected Slots                   │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│      Create Log Entry               │
│  PCLog.Instance.CreateNewLog()      │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────────────────┐
│            Performance Test Loop                 │
├─────────────────────────────────────────────────┤
│                                                 │
│  while (!stop && iterations < max)              │
│  {                                              │
│    foreach (slot in selectedSlots)              │
│    {                                            │
│      ┌────────────────────────────┐            │
│      │  1. LoopBack Test          │            │
│      │     • LED Control          │            │
│      │     • Response Time        │            │
│      │     • Log: DateTime.Now    │            │
│      └────────────┬───────────────┘            │
│                   │                             │
│      ┌────────────▼───────────────┐            │
│      │  2. Erase Test             │            │
│      │     • Clear Memory         │            │
│      │     • Verify Blank         │            │
│      │     • Log: DateTime.Now    │            │
│      └────────────┬───────────────┘            │
│                   │                             │
│      ┌────────────▼───────────────┐            │
│      │  3. Write Test             │            │
│      │     • Write Pattern        │            │
│      │     • Verify Written       │            │
│      │     • Log: DateTime.Now    │            │
│      └────────────┬───────────────┘            │
│                   │                             │
│      ┌────────────▼───────────────┐            │
│      │  4. Read Test              │            │
│      │     • Read Data            │            │
│      │     • Compare Pattern      │            │
│      │     • Log: DateTime.Now    │            │
│      └────────────┬───────────────┘            │
│                   │                             │
│      ┌────────────▼───────────────┐            │
│      │  Log Results               │            │
│      │  Update Progress           │            │
│      └────────────────────────────┘            │
│    }                                            │
│    iterations++;                                │
│  }                                              │
│                                                 │
└────────┬────────────────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│      Generate Final Report          │
│  • Test Summary                     │
│  • Pass/Fail Statistics             │
│  • Performance Metrics              │
└─────────────────────────────────────┘
```

### 3.5 Project Structure

```
DPS_DTCL/
├── App.xaml                    # Application entry point
├── MainWindow.xaml(.cs)        # Main application window
├── Cartridges/                 # Cartridge implementations
│   ├── ICart.cs               # Cart interface
│   ├── Darin1.cs              # Darin-I implementation
│   ├── Darin2.cs              # Darin-II implementation
│   └── Darin3.cs              # Darin-III implementation
├── Transport/                  # Hardware communication
│   ├── HardwareInfo.cs        # Hardware manager (Singleton)
│   ├── SlotInfo.cs            # Slot state management
│   └── ChannelHardwareInfo.cs # MUX channel management
├── IspProtocol/               # Protocol implementation
│   ├── IspProtocolDefs.cs     # Protocol definitions
│   ├── IspCommandManager.cs   # Command management
│   └── UartIspTransport.cs    # UART transport layer
├── Mux/                       # Multiplexer support
│   ├── MuxWindow.xaml(.cs)    # MUX control window
│   ├── MuxManager.cs          # MUX business logic
│   └── MuxChannelManager.cs   # Channel management
├── Log/                       # Logging infrastructure
│   ├── Log.cs                 # General logging
│   └── PCLog.cs              # Performance test logging
└── Messages/                  # Data structures
    ├── UploadMessage.cs       # Upload definitions
    └── DownloadMessage.cs     # Download definitions
```

### 3.6 Application Lifecycle

```
Application Start
       │
       ▼
┌──────────────┐
│  App.xaml.cs │
└──────┬───────┘
       │ Initialize
       ▼
┌──────────────────┐     ┌─────────────────┐
│ SplashScreen     │────►│  MainWindow     │
│ (3 seconds)      │     │  Initialize     │
└──────────────────┘     └────────┬────────┘
                                  │
                         ┌────────┴────────┐
                         ▼                 ▼
                  ┌──────────────┐  ┌──────────────┐
                  │ HardwareInfo │  │  UI Setup    │
                  │  Singleton   │  │              │
                  └──────┬───────┘  └──────────────┘
                         │
                         ▼
                  ┌──────────────┐
                  │ Start Timer  │
                  │  Scanning    │
                  └──────────────┘
```

---

## 4. Component Architecture

### 4.1 Core Components

#### HardwareInfo (Singleton Pattern)
```csharp
public sealed class HardwareInfo : IHardwareInfo, IDisposable
{
    // Thread-safe singleton with lazy initialization
    private static readonly Lazy<HardwareInfo> _lazy = 
        new Lazy<HardwareInfo>(() => new HardwareInfo());
    
    // Core responsibilities:
    - Hardware detection and monitoring
    - Slot management (4 slots)
    - Cart object lifecycle
    - Path configuration
    - Event notifications
}
```

#### ICart Interface Hierarchy
```
ICart (Interface)
├── Darin1 : ICart
│   └── Basic cartridge operations
├── Darin2 : ICart  
│   └── NAND Flash operations
└── Darin3 : ICart
    └── Compact Flash + FatFS operations

Common Operations:
- WriteUploadFiles()
- ReadDownloadFiles()
- EraseCartFiles()
- CopyCartFiles()
- CompareCartFiles()
- ExecutePC() // Performance Check
```

#### MainWindow Component Model
```
MainWindow
├── Hardware Management
│   ├── Detection Timer (1Hz)
│   ├── Status Display
│   └── LED Control
├── Operation Controls
│   ├── Read/Write/Erase
│   ├── Copy/Compare
│   └── Performance Check
├── UI Components
│   ├── Tab Control (Slots 1-4)
│   ├── Progress Bars
│   └── Status Messages
└── Event Handlers
    ├── Hardware Events
    ├── Operation Events
    └── UI Events
```

### 4.2 MUX Architecture

```
MuxWindow
    │
    ├── MuxManager (8 channels)
    │   ├── Channel[0-7]
    │   │   └── ChannelHardwareInfo
    │   │       ├── SlotInfo[0-3]
    │   │       ├── Transport
    │   │       └── CartObj
    │   │
    │   └── Channel Selection Logic
    │
    └── MuxPerformanceCheck
        ├── Parallel Execution
        └── Result Aggregation
```

---

## 5. Communication Layer

### 5.1 ISP Protocol Implementation

```
Frame Structure:
┌────────┬────────┬─────────────┬───────┬────────┐
│ START  │ LENGTH │   PAYLOAD   │ CRC8  │  END   │
│ (0x7E) │ (1-2B) │ (Variable)  │ (1B)  │ (0x7F) │
└────────┴────────┴─────────────┴───────┴────────┘

Protocol Stack:
┌─────────────────────┐
│ IspCommandManager   │ ← Command routing
├─────────────────────┤
│ IspFramingUtils     │ ← Frame encode/decode
├─────────────────────┤
│ UartIspTransport    │ ← Serial communication
├─────────────────────┤
│ SerialPort (.NET)   │ ← USB CDC driver
└─────────────────────┘
```

### 5.2 Command Flow

```csharp
// Typical command execution flow
public async Task<int> ExecuteOperation()
{
    // 1. Create command
    var cmd = CreateIspCommand(subCmd, payload);
    
    // 2. Send via transport
    await _transport.TransmitAsync(cmd);
    
    // 3. Wait for response
    var response = await _cmdControl.ExecuteCmd(
        cmd, expectedLen, timeout);
    
    // 4. Process result
    return ProcessResponse(response);
}
```

### 5.3 Data Transfer Protocol

```
Large File Transfer (Chunked):

GUI                              Hardware
 │                                  │
 ├─── TX_DATA_RESET ───────────────►│
 │◄── TX_MODE_ACK ──────────────────┤
 │                                  │
 ├─── TX_DATA(chunk1, 56B) ────────►│
 │◄── ACK ──────────────────────────┤
 │                                  │
 ├─── TX_DATA(chunk2, 56B) ────────►│
 │◄── ACK ──────────────────────────┤
 │         ...                      │
 ├─── TX_DATA(final chunk) ────────►│
 │◄── ACK_DONE ─────────────────────┤
 │                                  │
```

---

## 6. Data Management

### 6.1 File Organization

```
C:\MPS\
├── DARIN2\
│   ├── upload\         # Files to write to cart
│   ├── download\       # Files read from cart
│   ├── upload\temp\    # Staging area
│   ├── download\temp\  # Processing area
│   ├── copy\          # Copy operation workspace
│   ├── compare1\      # Compare source
│   └── compare2\      # Compare target
└── DARIN3\
    └── [Same structure as DARIN2]
```

### 6.2 Message System

```csharp
// Upload/Download message structures
public class UploadMessageInfo : IMessageInfo
{
    public int MsgID { get; set; }
    public string FileName { get; set; }
    public int ActualFileSize { get; set; }
    public int HeaderFileSize { get; set; }
    public bool isFileValid { get; set; }
    // ... other properties
}

// JSON-based configuration
UploadMessageInfoContainer
├── MessageInfoList[]
└── Serialized to: D2UploadMessageDetails.json
```

### 6.3 Data Flow Diagram

```
Write Operation:
┌──────────┐    Validate    ┌───────────┐    Chunk    ┌──────────┐
│   File   ├───────────────►│  Message  ├────────────►│ Protocol │
│  System  │                │  Handler  │             │  Layer   │
└──────────┘                └───────────┘             └────┬─────┘
                                                           │
                                                           ▼
┌──────────┐    Store       ┌───────────┐   Transfer  ┌──────────┐
│   Cart   │◄───────────────┤  Buffer   │◄────────────┤   USB    │
│  Memory  │                │  Manager  │             │  Port    │
└──────────┘                └───────────┘             └──────────┘

Read Operation:
[Reverse flow of Write Operation]
```

---

## 7. User Interface Architecture

### 7.1 Main Window Layout

```
┌─────────────────────────────────────────────────────┐
│  DTCL - Data Transfer Control Link          [─][□][X]│
├─────────────────────────────────────────────────────┤
│ ┌─────────────────┐ ┌─────────────────────────────┐ │
│ │  Hardware Info  │ │      Operation Panel        │ │
│ │                 │ ├─────────────────────────────┤ │
│ │ Status: Ready   │ │ [Read] [Write] [Erase]      │ │
│ │ FW Ver: 3.6     │ │ [Copy] [Compare] [PC Check] │ │
│ │ Board: DTCL     │ └─────────────────────────────┘ │
│ └─────────────────┘                                 │
│ ┌───────────────────────────────────────────────┐  │
│ │            Slot Tabs                          │  │
│ │ ┌────┬────┬────┬────┐                        │  │
│ │ │Slot│Slot│Slot│Slot│                        │  │
│ │ │ 1  │ 2  │ 3  │ 4  │                        │  │
│ │ └────┴────┴────┴────┘                        │  │
│ │ ┌────────────────────────────────────────┐   │  │
│ │ │     Slot Details & Operations          │   │  │
│ │ │                                        │   │  │
│ │ │  Cart Type: [Darin-II]                │   │  │
│ │ │  Status: Ready                        │   │  │
│ │ │  [Execute Operation]                  │   │  │
│ │ └────────────────────────────────────────┘   │  │
│ └───────────────────────────────────────────────┘  │
│ ┌───────────────────────────────────────────────┐  │
│ │  Progress: [████████████░░░░░░] 60%          │  │
│ │  Status: Writing file DR.bin...              │  │
│ └───────────────────────────────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

### 7.2 MVVM Implementation

```csharp
// View-Model binding example
public partial class MainWindow : Window
{
    // Properties for data binding
    public string UserStatus 
    { 
        get => _userStatus;
        set 
        {
            _userStatus = value;
            OnPropertyChanged();
        }
    }
    
    // Command pattern
    public ICommand ReadCommand { get; }
    public ICommand WriteCommand { get; }
    
    // Event handling
    private async void Read_Click(object sender, RoutedEventArgs e)
    {
        await PerformReadOperation();
    }
}
```

### 7.3 UI State Management

```
State Transitions:
┌─────────┐  Connect   ┌──────────┐  Select   ┌─────────┐
│  Init   ├───────────►│ Hardware ├──────────►│  Ready  │
│  State  │            │ Detected │  Cart     │  State  │
└─────────┘            └──────────┘           └────┬────┘
                                                   │
                                           Execute │
                                                   ▼
┌─────────┐  Complete  ┌──────────┐         ┌─────────┐
│  Idle   │◄───────────┤Operation │◄────────┤  Busy   │
│  State  │            │ Complete │  Progress│  State  │
└─────────┘            └──────────┘         └─────────┘
```

---

## 8. Threading & Concurrency

### 8.1 Threading Model

```csharp
// Main thread - UI operations
Application.Current.Dispatcher.Invoke(() => 
{
    UpdateUIElement();
});

// Background operations with async/await
public async Task<int> PerformOperation()
{
    // Run on ThreadPool
    return await Task.Run(async () =>
    {
        // Long-running operation
        var result = await DataOperation();
        
        // Update UI on main thread
        await Application.Current.Dispatcher.InvokeAsync(() =>
        {
            UpdateProgress(result);
        });
        
        return result;
    });
}
```

### 8.2 Concurrency Patterns

```
Timer-based Hardware Scanning:
┌─────────────┐     1Hz      ┌──────────────┐
│ Scan Timer  ├─────────────►│ Scan Method  │
└─────────────┘              └──────┬───────┘
                                    │
                          ┌─────────┴────────┐
                          ▼                  ▼
                   ┌──────────────┐   ┌──────────────┐
                   │ HW Detection │   │Cart Detection│
                   └──────┬───────┘   └──────┬───────┘
                          │                  │
                          └─────────┬────────┘
                                    ▼
                            ┌──────────────┐
                            │  UI Update   │
                            │ (Dispatcher) │
                            └──────────────┘
```

### 8.3 Thread Safety

```csharp
public sealed class HardwareInfo
{
    private readonly object _lockObject = new object();
    
    public SlotInfo[] SlotInfo 
    { 
        get 
        { 
            lock (_lockObject) 
            {
                // Return defensive copy
                var copy = new SlotInfo[_slotInfo.Length];
                Array.Copy(_slotInfo, copy, _slotInfo.Length);
                return copy;
            }
        } 
    }
    
    // Thread-safe event firing
    private void OnHardwareStatusChanged(HardwareEventArgs e)
    {
        HardwareStatusChanged?.Invoke(this, e);
    }
}
```

---

## 9. Error Handling & Logging

### 9.1 Error Handling Strategy

```csharp
// Layered error handling
try
{
    // Operation level
    await PerformOperation();
}
catch (TimeoutException tex)
{
    // Specific handling
    Log.Warning($"Operation timeout: {tex.Message}");
    ShowUserMessage("Operation timed out. Please try again.");
}
catch (IOException ioex)
{
    // IO specific
    Log.Error($"IO Error: {ioex.Message}");
    HandleIOError(ioex);
}
catch (Exception ex)
{
    // General fallback
    Log.Fatal($"Unexpected error: {ex}");
    ShowCriticalError(ex);
}
```

### 9.2 Logging Architecture

```
Logging Infrastructure:
┌────────────────┐
│   Log.cs      │ ← Central logger
├────────────────┤
│ - Info()      │
│ - Warning()   │
│ - Error()     │
│ - Fatal()     │
└───────┬────────┘
        │
        ▼
┌────────────────┐     ┌────────────────┐
│  File Logger  │     │ Console Logger │
│ (rotating)    │     │ (debug mode)   │
└────────────────┘     └────────────────┘

Performance Logging:
┌────────────────┐
│   PCLog.cs    │ ← Performance test logger
├────────────────┤
│ - Test results│
│ - Timestamps  │
│ - Statistics  │
└────────────────┘
```

### 9.3 Custom Message Box

```csharp
// Professional error display
public static MessageBoxResult Show(
    PopUpMessage message, 
    Window owner)
{
    var msgBox = new CustomMessageBox
    {
        Owner = owner,
        WindowStartupLocation = WindowStartupLocation.CenterOwner,
        MessageText = message.Text,
        MessageType = message.Type
    };
    
    // Configure buttons based on type
    ConfigureButtons(msgBox, message.Type);
    
    msgBox.ShowDialog();
    return msgBox.Result;
}
```

---

## 10. System Integration Diagrams

### 10.1 End-to-End Data Flow

Complete flow from user action to hardware execution:

```
┌─────────────────┐                    ┌─────────────────┐                    ┌─────────────────┐
│   User Action   │                    │   GUI Process   │                    │ Firmware Process│
│                 │                    │                 │                    │                 │
│  Click "Write"  │                    │                 │                    │                 │
└────────┬────────┘                    └────────┬────────┘                    └────────┬────────┘
         │                                      │                                      │
         │          Button Click Event          │                                      │
         ├─────────────────────────────────────►│                                      │
         │                                      │                                      │
         │                                      │ Validate Input                       │
         │                                      ├──────────┐                           │
         │                                      │          │                           │
         │                                      │◄─────────┘                           │
         │                                      │                                      │
         │                                      │ Create ISP Command                   │
         │                                      ├──────────┐                           │
         │                                      │          │                           │
         │                                      │◄─────────┘                           │
         │                                      │                                      │
         │                                      │          USB Frame                   │
         │                                      ├─────────────────────────────────────►│
         │                                      │                                      │
         │                                      │                                      │ Decode Frame
         │                                      │                                      ├──────────┐
         │                                      │                                      │          │
         │                                      │                                      │◄─────────┘
         │                                      │                                      │
         │                                      │                                      │ Route to Handler
         │                                      │                                      ├──────────┐
         │                                      │                                      │          │
         │                                      │                                      │◄─────────┘
         │                                      │                                      │
         │                                      │          ACK Response                │
         │                                      │◄─────────────────────────────────────┤
         │                                      │                                      │
         │                                      │ Send File Data                       │
         │                                      ├─────────────────────────────────────►│
         │                                      │                                      │
         │                                      │                                      │ Write to Storage
         │                                      │                                      ├──────────┐
         │                                      │                                      │          │
         │                                      │                                      │◄─────────┘
         │                                      │                                      │
         │                                      │        Success Response              │
         │                                      │◄─────────────────────────────────────┤
         │                                      │                                      │
         │          Update UI Status            │                                      │
         │◄─────────────────────────────────────┤                                      │
         │                                      │                                      │
```

### 10.2 Hardware Abstraction Layers

Layered architecture showing abstraction boundaries:

```
┌──────────────────────────────────────────────────────────────────────┐
│                         Application Layer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│  │ MainWindow   │  │ MuxWindow    │  │ Utility      │            │
│  └──────────────┘  └──────────────┘  └──────────────┘            │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                                 ▼
┌──────────────────────────────────────────────────────────────────────┐
│                      Hardware Abstraction Layer                     │
│  ┌──────────────────────────────────────────────────────────────┐   │
│  │                    HardwareInfo (Singleton)                 │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │   │
│  │  │  Slot 1  │  │  Slot 2  │  │  Slot 3  │  │  Slot 4  │  │   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │   │
│  │                                                             │   │
│  │  ┌──────────────────────────────────────────────────┐  │   │
│  │  │              Cart Object Pool                        │  │   │
│  │  │  ┌────────┐  ┌────────┐  ┌────────┐                │  │   │
│  │  │  │Darin1  │  │Darin2  │  │Darin3  │                │  │   │
│  │  │  └────────┘  └────────┘  └────────┘                │  │   │
│  │  └──────────────────────────────────────────────────┘  │   │
│  └────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                                 ▼
┌──────────────────────────────────────────────────────────────────────┐
│                         Protocol Layer                              │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐   │
│  │ IspCommandMgr   │  │ IspFramingUtils │  │ IspCmdControl   │   │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘   │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                                 ▼
┌──────────────────────────────────────────────────────────────────────┐
│                        Transport Layer                              │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │                    UartIspTransport                         │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │   │
│  │  │SerialPort│  │  Buffer  │  │  Events  │  │  Thread  │  │   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │   │
│  └────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬─────────────────────────────────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │   USB Driver    │
                        └─────────────────┘
```

### 10.3 ISP Protocol State Machine

Protocol state management for reliable communication:

```
                           ┌─────────────┐
                           │    IDLE     │
                           │   State     │
                           └──────┬──────┘
                                  │
                    ┌─────────────┼─────────────┬─────────────┐
                    │             │             │             │
              CMD_REQ│       RX_DATA│      TX_DATA│      ERROR│
                    ▼             ▼             ▼             ▼
           ┌──────────────┐ ┌──────────────┐ ┌──────────────┐ ┌──────────────┐
           │   COMMAND    │ │   RECEIVE    │ │  TRANSMIT    │ │    ERROR     │
           │   Process    │ │    Mode      │ │    Mode      │ │   Handler    │
           └──────┬───────┘ └──────┬───────┘ └──────┬───────┘ └──────┬───────┘
                  │                │                │                │
                  │          RX_DATA│         TX_DATA│               │
                  │                ▼                ▼                │
                  │       ┌──────────────┐ ┌──────────────┐         │
                  │       │  Receiving   │ │ Transmitting │         │
                  │       │    Data      │ │    Data      │         │
                  │       └──────┬───────┘ └──────┬───────┘         │
                  │              │                │                  │
                  │         Complete│        Complete│               │
                  │              ▼                ▼                  │
                  │       ┌──────────────┐ ┌──────────────┐         │
                  │       │   RX_DONE    │ │   TX_DONE    │         │
                  │       └──────┬───────┘ └──────┬───────┘         │
                  │              │                │                  │
                  └──────────────┴──────────────┴──────────────────┘
                                           │
                                           ▼
                                    ┌─────────────┐
                                    │    IDLE     │
                                    └─────────────┘
```

## 11. Performance & Optimization

### 10.1 Memory Management

```csharp
// Cart object pooling
private Dictionary<CartType, ICart> _cartInstancePool;

private ICart GetOrCreateCart(CartType type)
{
    if (!_cartInstancePool.ContainsKey(type))
    {
        _cartInstancePool[type] = CreateCartInstance(type);
    }
    return _cartInstancePool[type];
}

// Proper disposal
public void Dispose()
{
    foreach (var cart in _cartInstancePool.Values)
    {
        (cart as IDisposable)?.Dispose();
    }
    _cartInstancePool.Clear();
}
```

### 10.2 Performance Metrics

| Operation | Target Time | Actual (Typical) |
|-----------|------------|------------------|
| Hardware Detection | < 100ms | 50-80ms |
| UI Update | < 16ms | 5-10ms |
| File Transfer (1MB) | < 1s | 0.8s |
| Performance Check | < 30s | 20-25s |

### 10.3 Optimization Strategies

```
1. Async Operations:
   - All IO operations are async
   - UI remains responsive
   - Cancellation token support

2. Buffering:
   - 64KB read/write buffers
   - Chunked transfers (56 bytes/frame)
   - Progress reporting throttled

3. Caching:
   - Hardware status cached (1s)
   - Cart type detection cached
   - File metadata cached

4. UI Optimization:
   - Virtualized lists for large data
   - Deferred UI updates
   - Progress throttling (10Hz max)
```

---

## 11. Error Recovery Flow

Robust error handling and recovery mechanisms:

```
Error Detected
      │
      ▼
┌─────────────────┐
│ Classify Error  │
└────────┬────────┘
         │
         ├─────────────┬─────────────┬─────────────┬─────────────┐
         │             │             │             │             │
     CRC Error    Timeout      NACK Response  Hardware Error  Unknown
         │             │             │             │             │
         ▼             ▼             ▼             ▼             ▼
   ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐
   │  Retry   │  │  Retry   │  │  Resend  │  │  Reset   │  │   Log    │
   │  Frame   │  │ Command  │  │ Command  │  │ Hardware │  │  Error   │
   └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘  └────┬─────┘
        │             │             │             │             │
        │             │             │             │             │
        └─────────────┴─────────────┴─────────────┴─────────────┘
                                    │
                          ┌─────────┴─────────┐
                          │                   │
                    Success?            Max Retries?
                          │                   │
                     Yes  │              Yes  │  No
                          ▼                   ▼
                   ┌──────────┐        ┌──────────┐
                   │ Continue │        │  Report  │──┐
                   │Operation │        │  Error   │  │
                   └──────────┘        └──────────┘  │
                                                     │
                                              Retry  │
                                              ◄──────┘
```

## Appendix A: Configuration Files

### App.config
```xml
<?xml version="1.0" encoding="utf-8"?>
<configuration>
    <startup>
        <supportedRuntime version="v4.0" 
                         sku=".NETFramework,Version=v4.8"/>
    </startup>
    <appSettings>
        <add key="ScanInterval" value="1000"/>
        <add key="CommandTimeout" value="5000"/>
        <add key="MaxRetries" value="3"/>
    </appSettings>
</configuration>
```

### packages.config
```xml
<?xml version="1.0" encoding="utf-8"?>
<packages>
  <package id="System.Text.Json" 
           version="9.0.0" 
           targetFramework="net48" />
</packages>
```

---

## Appendix B: Extension Points

### Adding New Cartridge Types
```csharp
// 1. Implement ICart interface
public class DarinX : ICart
{
    // Implementation
}

// 2. Register in HardwareInfo
private ICart CreateCartInstance(CartType type)
{
    switch (type)
    {
        case CartType.DarinX:
            return new DarinX();
        // ...
    }
}

// 3. Update UI elements
// Add to cartridge selection combos
```

### Adding New Operations
```csharp
// 1. Define command in protocol
enum IspSubCommand 
{
    NEW_OPERATION = 0x20
}

// 2. Implement handler
class NewOperation_SubCmdProcess : IIspSubCommandHandler
{
    // Implementation
}

// 3. Add UI controls
// Update MainWindow with new button/handler
```

---

**Document Version History:**
- v1.0 - Initial architecture documentation
- Created by: Professional Development Team  
- Last Updated: November 2025