# DTCL System Block Diagrams
## Comprehensive Flow Diagrams for Firmware and GUI

**Version:** 1.0  
**Date:** November 2025  
**Author:** ISquare Systems

---

## Table of Contents
1. [Firmware Block Diagrams](#1-firmware-block-diagrams)
2. [GUI Block Diagrams](#2-gui-block-diagrams)
3. [System Integration Diagrams](#3-system-integration-diagrams)
4. [Protocol Flow Diagrams](#4-protocol-flow-diagrams)

---

# 1. Firmware Block Diagrams

## 1.1 Firmware Boot Sequence

```
┌─────────────┐
│  Power On   │
└──────┬──────┘
       │
       ▼
┌─────────────┐     ┌──────────────┐     ┌─────────────┐
│  Reset      │────►│ SystemInit() │────►│    HAL      │
│  Handler    │     │              │     │    Init     │
└─────────────┘     └──────────────┘     └──────┬──────┘
                                                 │
                    ┌────────────────────────────┼────────────────────────┐
                    │                            │                        │
                    ▼                            ▼                        ▼
            ┌──────────────┐            ┌──────────────┐         ┌──────────────┐
            │ System Clock │            │     GPIO     │         │     USB      │
            │    Config    │            │     Init     │         │     Init     │
            │   (96 MHz)   │            │              │         │   (CDC-ACM)  │
            └──────────────┘            └──────┬───────┘         └──────┬───────┘
                                               │                         │
                                               ▼                         │
                                       ┌──────────────┐                 │
                                       │   LED Blink  │                 │
                                       │  (3x @ 300ms)│                 │
                                       └──────┬───────┘                 │
                                              │                         │
                    ┌─────────────────────────┴─────────────────────────┘
                    │
                    ▼
            ┌──────────────────────────────┐
            │   Protocol Stack Setup       │
            ├──────────────────────────────┤
            │ • Register ISP Handlers      │
            │ • Setup Command Manager      │
            │ • Configure SubCmd Process  │
            │ • Initialize Cart Handlers  │
            └───────────────┬──────────────┘
                            │
                            ▼
                    ┌──────────────┐
                    │  Main Loop   │
                    │              │
                    │ while(1) {   │
                    │   UpdateLED  │
                    │ }            │
                    └──────────────┘
```

## 1.2 Command Processing Flow

```
USB Interrupt
     │
     ▼
┌─────────────────────────────────────────────────────────────────┐
│                      USB CDC Receive Handler                     │
│  ┌────────────┐    ┌────────────┐    ┌───────────────────────┐ │
│  │  Get Data  │───►│  Buffer    │───►│ Isp_forward_data()    │ │
│  │  from USB  │    │  Receive   │    │                       │ │
│  └────────────┘    └────────────┘    └───────────┬───────────┘ │
└─────────────────────────────────────────────────┼───────────────┘
                                                  │
                                                  ▼
                                    ┌─────────────────────────┐
                                    │  IspFramingUtils::     │
                                    │  decodeFrame()         │
                                    ├─────────────────────────┤
                                    │ • Check START (0x7E)   │
                                    │ • Extract Length       │
                                    │ • Verify CRC8          │
                                    │ • Check END (0x7F)     │
                                    └───────────┬─────────────┘
                                                │ Valid Frame
                                                ▼
                                    ┌─────────────────────────┐
                                    │  IspCommandManager::   │
                                    │  handleData()          │
                                    └───────────┬─────────────┘
                                                │
                ┌───────────────────────────────┼───────────────────────────────┐
                │                               │                               │
                ▼                               ▼                               ▼
    ┌──────────────────────┐       ┌──────────────────────┐       ┌──────────────────────┐
    │   IspCmdControl      │       │  IspCmdReceiveData   │       │  IspCmdTransmitData  │
    │  (CMD_REQ = 0x52)    │       │  (RX_DATA = 0x55)    │       │  (TX_DATA = 0x56)    │
    └──────────┬───────────┘       └──────────┬───────────┘       └──────────┬───────────┘
               │                               │                               │
               ▼                               ▼                               ▼
    ┌──────────────────────┐       ┌──────────────────────┐       ┌──────────────────────┐
    │  Process SubCommand  │       │   Prepare for RX     │       │   Prepare for TX     │
    │  • LED Control       │       │   • Allocate Buffer  │       │   • Get Data Ready   │
    │  • Status Query      │       │   • Set RX Mode      │       │   • Set TX Mode      │
    │  • Erase/Format      │       │   • Start Transfer   │       │   • Start Transfer   │
    └──────────┬───────────┘       └──────────┬───────────┘       └──────────┬───────────┘
               │                               │                               │
               └───────────────┬───────────────┴───────────────────────────────┘
                               │
                               ▼
                    ┌──────────────────────┐
                    │  SubCommand Router   │
                    │                      │
                    │  Based on SubCmd ID: │
                    └──────────┬───────────┘
                               │
        ┌──────────────────────┼──────────────────────┐
        │                      │                      │
        ▼                      ▼                      ▼
┌──────────────┐      ┌──────────────┐      ┌──────────────┐
│   Darin2     │      │   Darin3     │      │   Control    │
│   Handler    │      │   Handler    │      │   Handler    │
│              │      │              │      │              │
│ • D2_WRITE   │      │ • D3_WRITE   │      │ • FW_VER     │
│ • D2_READ    │      │ • D3_READ    │      │ • BOARD_ID   │
│ • D2_ERASE   │      │ • D3_ERASE   │      │ • LED_CTRL   │
└──────────────┘      │ • D3_FORMAT  │      │ • CART_STAT  │
                      └──────────────┘      └──────────────┘
```

## 1.3 Darin2 (NAND) Operation Flow

```
D2_WRITE Command
      │
      ▼
┌─────────────────┐
│ Darin2::        │
│ prepareForRx()  │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  Extract Parameters:                 │
│  • Address (5 bytes)                │
│  • Data Length                      │
│  • Cart ID                          │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  NAND Write Sequence                │
├─────────────────────────────────────┤
│                                     │
│  1. Set Data Direction (Input)      │
│     └─> GPIO Configure              │
│                                     │
│  2. Select Cart (CE Low)            │
│     └─> HAL_GPIO_WritePin()         │
│                                     │
│  3. Send Command 0x80               │
│     ├─> CLE = High                  │
│     ├─> Write Data Bus              │
│     └─> WE Pulse                    │
│                                     │
│  4. Send Address (5 cycles)         │
│     ├─> ALE = High                  │
│     ├─> Write Addr[0..4]            │
│     └─> WE Pulse each               │
│                                     │
│  5. Write Data (2048 bytes)         │
│     ├─> Receive USB Data            │
│     ├─> Buffer Management           │
│     └─> Write to NAND               │
│                                     │
│  6. Send Command 0x10               │
│     └─> Program Execute             │
│                                     │
│  7. Wait Ready/Busy                 │
│     └─> Poll RB Pin                 │
│                                     │
│  8. Read Status                     │
│     ├─> Send 0x70                   │
│     └─> Check Success               │
│                                     │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────┐
│ Return Status   │
│ to USB Host     │
└─────────────────┘
```

## 1.4 Darin3 (CF) Operation Flow

```
D3_WRITE Command
      │
      ▼
┌─────────────────┐
│ Darin3::        │
│ prepareForRx()  │
└────────┬────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  Extract Parameters:                 │
│  • File ID (MsgID)                  │
│  • File Size                        │
│  • Cart ID                          │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  FatFS File Operations              │
├─────────────────────────────────────┤
│                                     │
│  1. Mount File System               │
│     └─> f_mount()                   │
│                                     │
│  2. Open/Create File                │
│     └─> f_open(FA_CREATE_ALWAYS)    │
│                                     │
│  3. Receive & Write Data            │
│     ┌─────────────────┐             │
│     │ while(remaining)│             │
│     │ {               │             │
│     │   Get USB Data  │             │
│     │   f_write()     │             │
│     │   Update Pos    │             │
│     │ }               │             │
│     └─────────────────┘             │
│                                     │
│  4. Close File                      │
│     └─> f_close()                   │
│                                     │
│  5. Sync to Media                   │
│     └─> f_sync()                    │
│                                     │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────────────────────────┐
│  CF Hardware Access (via FatFS)     │
├─────────────────────────────────────┤
│                                     │
│  Sector Write:                      │
│  1. Select CF Card                  │
│  2. Write LBA Registers             │
│  3. Send Write Command (0x30)       │
│  4. Transfer 512-byte Sector        │
│  5. Wait for Ready                  │
│                                     │
└────────┬────────────────────────────┘
         │
         ▼
┌─────────────────┐
│ Return Status   │
└─────────────────┘
```

## 1.5 LED State Management

```
UpdateSlotLed()
      │
      ▼
┌─────────────────┐     ┌─────────────────┐
│ Check GUI LED   │────►│ GUI Control?    │
│ Control State   │     │ (Yes/No)        │
└─────────────────┘     └────────┬────────┘
                                 │
                    No ┌─────────┴─────────┐ Yes
                       ▼                   ▼
              ┌──────────────┐    ┌──────────────┐
              │ Auto LED Mode│    │ GUI LED Mode │
              │              │    │ (Skip Auto)  │
              └──────┬───────┘    └──────────────┘
                     │
                     ▼
        ┌────────────────────────┐
        │ UpdateD3SlotStatus()   │
        │ UpdateD2SlotStatus()   │
        └────────────┬───────────┘
                     │
     ┌───────────────┴───────────────┐
     │                               │
     ▼                               ▼
┌─────────────┐             ┌─────────────┐
│ Cart Present│             │  No Cart    │
│ (Status 2/3)│             │ (Status 0/1)│
└──────┬──────┘             └──────┬──────┘
       │                           │
       ▼                           ▼
┌─────────────┐             ┌─────────────┐
│ Green LED   │             │ All LEDs    │
│    ON       │             │    OFF      │
└─────────────┘             └─────────────┘
```

---

# 2. GUI Block Diagrams

## 2.1 Application Startup Flow

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

## 2.2 Hardware Detection Flow

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

## 2.3 Operation Execution Flow

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

## 2.4 MUX Window Operation Flow

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

## 2.5 Performance Check Flow

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

---

# 3. System Integration Diagrams

## 3.1 End-to-End Data Flow

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

## 3.2 Hardware Abstraction Layers

```
┌────────────────────────────────────────────────────────────────────┐
│                         Application Layer                           │
│  ┌──────────────┐  ┌──────────────┐  ┌──────────────┐            │
│  │ MainWindow   │  │ MuxWindow    │  │ Utility      │            │
│  └──────────────┘  └──────────────┘  └──────────────┘            │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│                      Hardware Abstraction Layer                     │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │                    HardwareInfo (Singleton)                 │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │   │
│  │  │  Slot 1  │  │  Slot 2  │  │  Slot 3  │  │  Slot 4  │  │   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │   │
│  │                                                             │   │
│  │  ┌──────────────────────────────────────────────────────┐  │   │
│  │  │              Cart Object Pool                        │  │   │
│  │  │  ┌────────┐  ┌────────┐  ┌────────┐                │  │   │
│  │  │  │Darin1  │  │Darin2  │  │Darin3  │                │  │   │
│  │  │  └────────┘  └────────┘  └────────┘                │  │   │
│  │  └──────────────────────────────────────────────────────┘  │   │
│  └────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│                         Protocol Layer                              │
│  ┌─────────────────┐  ┌─────────────────┐  ┌─────────────────┐   │
│  │ IspCommandMgr   │  │ IspFramingUtils │  │ IspCmdControl   │   │
│  └─────────────────┘  └─────────────────┘  └─────────────────┘   │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
┌────────────────────────────────────────────────────────────────────┐
│                        Transport Layer                              │
│  ┌────────────────────────────────────────────────────────────┐   │
│  │                    UartIspTransport                         │   │
│  │  ┌──────────┐  ┌──────────┐  ┌──────────┐  ┌──────────┐  │   │
│  │  │SerialPort│  │  Buffer  │  │  Events  │  │  Thread  │  │   │
│  │  └──────────┘  └──────────┘  └──────────┘  └──────────┘  │   │
│  └────────────────────────────────────────────────────────────┘   │
└────────────────────────────────┬───────────────────────────────────┘
                                 │
                                 ▼
                        ┌─────────────────┐
                        │   USB Driver    │
                        └─────────────────┘
```

---

# 4. Protocol Flow Diagrams

## 4.1 ISP Protocol State Machine

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
                  └──────────────┴────────────────┴──────────────────┘
                                           │
                                           ▼
                                    ┌─────────────┐
                                    │    IDLE     │
                                    └─────────────┘
```

## 4.2 Error Recovery Flow

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

---

**Document Version History:**
- v1.0 - Initial block diagram documentation
- Created by: Professional Development Team
- Last Updated: November 2025