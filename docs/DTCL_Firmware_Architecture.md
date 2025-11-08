# DTCL Firmware Architecture Documentation
## Data Transfer and Control Link - STM32 Firmware

**Version:** 1.0  
**Date:** November 2025  
**Author:** ISquare Systems

---

## Table of Contents
1. [Executive Summary](#1-executive-summary)
2. [System Overview](#2-system-overview)
3. [Hardware Architecture](#3-hardware-architecture)
4. [Software Architecture](#4-software-architecture)
5. [Communication Protocol](#5-communication-protocol)
6. [Core Components](#6-core-components)
7. [Data Flow Architecture](#7-data-flow-architecture)
8. [Cartridge Interface](#8-cartridge-interface)
9. [Security & Safety](#9-security--safety)
10. [Performance Specifications](#10-performance-specifications)

---

## 1. Executive Summary

The DTCL firmware is an embedded system designed for the STM32F411VET6 microcontroller, implementing a hybrid data storage system that supports both NAND Flash (Darin-II) and Compact Flash (Darin-III) cartridge types. The firmware provides a robust communication interface via USB CDC-ACM, enabling high-speed data transfer and control operations.

### Key Features:
- **Dual Cartridge Support**: Simultaneous operation with NAND Flash and CF cards
- **USB CDC Communication**: High-speed USB 2.0 interface
- **Real-time LED Status**: Hardware status indicators
- **Professional Protocol Stack**: ISP Protocol with CRC validation
- **FatFS Integration**: File system support for CF cards
- **Hardware Abstraction**: Clean separation between drivers and application

---

## 2. System Overview

### 2.1 Block Diagram - High Level Architecture

```
┌─────────────────────────────────────────────────────────────────┐
│                        DTCL Firmware System                       │
├─────────────────────────────────────────────────────────────────┤
│                                                                   │
│  ┌───────────────┐     ┌──────────────────┐    ┌──────────────┐ │
│  │   USB Host    │     │  Application     │    │   Hardware    │ │
│  │   (PC GUI)    │◄───►│     Layer        │◄──►│   Drivers     │ │
│  └───────────────┘     └──────────────────┘    └──────────────┘ │
│          ▲                      │                      │         │
│          │                      ▼                      ▼         │
│  ┌───────▼───────┐     ┌──────────────────┐    ┌──────────────┐ │
│  │  USB CDC/ACM  │     │   ISP Protocol   │    │  Cartridge   │ │
│  │   Transport   │◄───►│     Manager      │    │  Interfaces  │ │
│  └───────────────┘     └──────────────────┘    └──────────────┘ │
│          ▲                      │                      │         │
│          │                      ▼                      ▼         │
│  ┌───────▼───────┐     ┌──────────────────┐    ┌──────────────┐ │
│  │  STM32 HAL    │     │  Command/SubCmd  │    │ NAND │  CF   │ │
│  │  USB Driver   │     │    Processors    │    │Driver│Driver │ │
│  └───────────────┘     └──────────────────┘    └──────────────┘ │
│                                                                   │
└─────────────────────────────────────────────────────────────────┘
```

### 2.2 Hardware Platform
- **MCU**: STM32F411VET6 (ARM Cortex-M4)
- **Clock**: 96 MHz System Clock
- **Memory**: 512KB Flash, 128KB RAM
- **Interfaces**: USB 2.0 FS, GPIO, External Bus Interface
- **Storage Support**: NAND Flash + Compact Flash

---

## 3. Hardware Architecture

### 3.1 Pin Configuration Overview

```
┌────────────────────────────────────────────────────────────┐
│                    STM32F411VET6 Pin Map                    │
├────────────────────────────────────────────────────────────┤
│                                                             │
│  USB Interface:                                             │
│  ├─ PA11: USB_DM                                           │
│  └─ PA12: USB_DP                                           │
│                                                             │
│  LED Indicators:                                            │
│  ├─ PB0: LED1 (Green - Slot 1)                            │
│  ├─ PB1: LED2 (Red - Slot 1)                              │
│  ├─ PB2: LED3 (Green - Slot 2)                            │
│  └─ PB3: LED4 (Red - Slot 2)                              │
│                                                             │
│  Darin-II (NAND) Interface:                                │
│  ├─ Data Bus: PC[0:7] (8-bit)                             │
│  ├─ Address Bus: PA[0:15] + PC[8:11]                      │
│  ├─ Control: CLE, ALE, CE, WE, RE, RB                     │
│  └─ Slot Select: PC12 (SLTS1)                             │
│                                                             │
│  Darin-III (CF) Interface:                                 │
│  ├─ Data Bus: Shared with NAND                            │
│  ├─ Address Bus: Shared with NAND                         │
│  ├─ Control: CE1, CE2, OE, WE, REG, RESET                 │
│  └─ Power Control: PA6 (PWR_CYCLE)                        │
│                                                             │
└────────────────────────────────────────────────────────────┘
```

### 3.2 Memory Map

```
┌─────────────────┬──────────────────┬────────────────────┐
│ Memory Region   │ Start Address    │ Size              │
├─────────────────┼──────────────────┼────────────────────┤
│ Flash           │ 0x08000000       │ 512 KB            │
│ SRAM            │ 0x20000000       │ 128 KB            │
│ NAND Interface  │ 0x60000000       │ External          │
│ CF Interface    │ 0x64000000       │ External          │
└─────────────────┴──────────────────┴────────────────────┘
```

---

## 4. Software Architecture

### 4.1 System Boot Sequence

The following diagram shows the complete firmware initialization process:

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

### 4.2 Layered Architecture

```
┌─────────────────────────────────────────────────────────┐
│                    Application Layer                     │
│  ┌─────────────┐  ┌─────────────┐  ┌────────────────┐  │
│  │   Main.cpp  │  │  Darin2.cpp │  │  Darin3.cpp   │  │
│  │  App Entry  │  │ NAND Handler│  │  CF Handler    │  │
│  └─────────────┘  └─────────────┘  └────────────────┘  │
├─────────────────────────────────────────────────────────┤
│                    Protocol Layer                        │
│  ┌─────────────────────────┐  ┌───────────────────┐    │
│  │   IspCommandManager     │  │ IspSubCmdProcessor│    │
│  │  - Command Routing      │  │ - SubCmd Handlers │    │
│  │  - Frame Processing     │  │ - Response Gen    │    │
│  └─────────────────────────┘  └───────────────────┘    │
├─────────────────────────────────────────────────────────┤
│                   Transport Layer                        │
│  ┌─────────────────────────┐  ┌───────────────────┐    │
│  │    SerialTransport      │  │  IspFramingUtils  │    │
│  │  - USB CDC Interface    │  │  - CRC8 Calc      │    │
│  │  - Data Buffering       │  │  - Frame Encode   │    │
│  └─────────────────────────┘  └───────────────────┘    │
├─────────────────────────────────────────────────────────┤
│                    Driver Layer                          │
│  ┌─────────────────┐  ┌─────────────┐  ┌────────────┐  │
│  │ Darin2Cart_Driver│ │Darin3Cart_  │  │  FatFS     │  │
│  │  - NAND Control │  │   Driver    │  │  Wrapper   │  │
│  └─────────────────┘  └─────────────┘  └────────────┘  │
├─────────────────────────────────────────────────────────┤
│                     HAL Layer                            │
│  ┌─────────────┐  ┌───────────┐  ┌──────────────────┐  │
│  │  STM32 HAL  │  │  USB HAL  │  │   GPIO HAL       │  │
│  └─────────────┘  └───────────┘  └──────────────────┘  │
└─────────────────────────────────────────────────────────┘
```

### 4.2 Command Processing Flow

The following diagram shows the complete command processing flow from USB reception to handler execution:

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

### 4.3 NAND Flash (Darin2) Operation Flow

Detailed flow for NAND flash write operations:

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

### 4.4 Compact Flash (Darin3) Operation Flow

Detailed flow for Compact Flash write operations via FatFS:

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

### 4.5 LED State Management Flow

The firmware manages LED status indicators based on cartridge detection and GUI control:

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

### 4.6 Component Interaction Diagram

```
┌────────────┐       ┌─────────────────┐      ┌─────────────┐
│  USB Host  │       │ IspCommandMgr   │      │  Darin2/3   │
│   (GUI)    │       │                 │      │  Handler    │
└─────┬──────┘       └────────┬────────┘      └──────┬──────┘
      │                       │                       │
      │ USB Frame            │                       │
      ├─────────────────────►│                       │
      │                       │ Decode & Route       │
      │                       ├─────────────────────►│
      │                       │                       │
      │                       │                       │ Execute
      │                       │                       ├────────┐
      │                       │                       │        │
      │                       │      Response         │◄───────┘
      │                       │◄─────────────────────┤
      │    USB Frame          │                       │
      │◄──────────────────────┤                       │
      │                       │                       │
```

---

## 5. Communication Protocol

### 5.1 ISP Protocol Frame Structure

```
┌─────────┬──────────┬─────────────────────┬───────┬─────────┐
│  START  │  LENGTH  │      PAYLOAD        │  CRC8 │   END   │
│  (0x7E) │  (1-2B)  │    (Variable)       │  (1B) │  (0x7F) │
└─────────┴──────────┴─────────────────────┴───────┴─────────┘

Payload Structure:
┌──────────┬────────────┬──────────────────────────────┐
│ Command  │ SubCommand │       Data (Optional)        │
│   (1B)   │    (1B)    │        (Variable)            │
└──────────┴────────────┴──────────────────────────────┘
```

### 5.2 Command Flow Sequence

```
       GUI                    Firmware                 Cart Driver
        │                        │                          │
        │   CMD_REQ(SubCmd)     │                          │
        ├──────────────────────►│                          │
        │                        │                          │
        │                        │   Process SubCommand     │
        │                        ├─────────────────────────►│
        │                        │                          │
        │                        │     Execute Operation    │
        │                        │◄─────────────────────────┤
        │                        │                          │
        │   CMD_RESP(Result)    │                          │
        │◄──────────────────────┤                          │
        │                        │                          │
```

### 5.3 Data Transfer Protocol

```
Large Data Transfer (>56 bytes):

┌──────────┐                ┌──────────┐
│   GUI    │                │ Firmware │
└────┬─────┘                └────┬─────┘
     │                           │
     │   TX_DATA_RESET           │
     ├─────────────────────────►│
     │   TX_MODE_ACK             │
     │◄─────────────────────────┤
     │                           │
     │   TX_DATA(Chunk 1)        │
     ├─────────────────────────►│
     │   ACK                     │
     │◄─────────────────────────┤
     │                           │
     │   TX_DATA(Chunk 2)        │
     ├─────────────────────────►│
     │   ACK                     │
     │◄─────────────────────────┤
     │         ...               │
     │   TX_DATA(Last Chunk)     │
     ├─────────────────────────►│
     │   ACK_DONE                │
     │◄─────────────────────────┤
     │                           │
```

---

## 6. Core Components

### 6.1 Main Application (main.cpp)

**Responsibilities:**
- System initialization
- Component registration
- Main event loop
- LED status management

**Key Functions:**
```cpp
int main(void)
{
    // 1. HAL & Clock initialization
    HAL_Init();
    SystemClock_Config();
    
    // 2. Peripheral initialization
    MX_GPIO_Init();
    MX_USB_DEVICE_Init();
    
    // 3. Protocol stack setup
    IspManager.addHandler(&IspRx);
    IspManager.addHandler(&IspTx);
    IspManager.addHandler(&IspCtrl);
    
    // 4. Register cartridge handlers
    subcmdProcess.registerHandler(D2_WRITE, &darin2Obj);
    subcmdProcess.registerHandler(D3_WRITE, &darin3Obj);
    
    // 5. Main loop - LED status updates
    while(1) {
        UpdateSlotLed();
    }
}
```

### 6.2 ISP Command Manager

**Class Hierarchy:**
```
IspCommandManager
├── IspCmdControl (Command requests)
├── IspCmdReceiveData (RX data operations)
└── IspCmdTransmitData (TX data operations)

IspSubCommandProcessor
├── Control SubCommands
│   ├── FirmwareVersion_SubCmdProcess
│   ├── BoardID_SubCmdProcess
│   ├── CartStatus_SubCmdProcess
│   └── LED Control Handlers
└── Data SubCommands
    ├── Darin2 (NAND operations)
    └── Darin3 (CF operations)
```

### 6.3 Cartridge Handlers

#### Darin2 (NAND Flash) Handler

```cpp
class Darin2 : public IIspSubCommandHandler
{
    // Handles NAND Flash operations
    - Page-based read/write
    - Block erase operations
    - Bad block management
    - ECC handling
}
```

#### Darin3 (Compact Flash) Handler

```cpp
class Darin3 : public IIspSubCommandHandler  
{
    // Handles CF card operations
    - File system operations (FatFS)
    - Sector-based access
    - Directory management
    - Format support
}
```

---

## 7. Data Flow Architecture

### 7.1 Read Operation Flow

```
┌────────┐  Read Cmd   ┌──────────┐  Validate  ┌──────────┐
│  GUI   ├────────────►│ IspMgr   ├───────────►│ SubCmd   │
└────────┘             └──────────┘            └────┬─────┘
                                                     │
                                               Route │
     ┌───────────────────────────────────────────────┘
     │                             │
     ▼                             ▼
┌─────────┐                  ┌──────────┐
│ Darin2  │                  │ Darin3   │
│ Handler │                  │ Handler  │
└────┬────┘                  └────┬─────┘
     │                            │
     ▼                            ▼
┌─────────┐                  ┌──────────┐
│  NAND   │                  │   CF     │
│ Driver  │                  │ Driver   │
└────┬────┘                  └────┬─────┘
     │                            │
     ▼                            ▼
┌─────────┐                  ┌──────────┐
│  NAND   │                  │   CF     │
│  Flash  │                  │  Card    │
└─────────┘                  └──────────┘
```

### 7.2 Write Operation Flow

```
1. Command Phase:
   GUI → TX_DATA_RESET → Firmware
   GUI ← TX_MODE_ACK ← Firmware

2. Data Transfer Phase:
   GUI → TX_DATA(chunk) → Firmware
   GUI ← ACK ← Firmware
   [Repeat for all chunks]

3. Write Phase:
   Firmware → Cart Driver → Physical Media

4. Completion:
   GUI ← ACK_DONE ← Firmware
```

---

## 8. Cartridge Interface

### 8.1 NAND Flash Interface (Darin-II)

```
Command/Address Latch Enable (CLE/ALE) Timing:
     _____       _____
CLE:      |_____|     
         _____
ALE: ___|     |_______
     _______________
DATA: XXX|CMD|ADDR|XXX

Page Program Sequence:
1. Send command 0x80
2. Send 5-byte address
3. Transfer page data (2048 bytes)
4. Send command 0x10
5. Wait for RB (Ready/Busy)
6. Read status (0x70)
```

### 8.2 Compact Flash Interface (Darin-III)

```
CF Register Map:
┌─────────────┬─────────┬──────────────────┐
│  Register   │ Address │   Function       │
├─────────────┼─────────┼──────────────────┤
│ Data        │  0x00   │ 16-bit data      │
│ Error       │  0x01   │ Error info       │
│ Sector Count│  0x02   │ Sectors to xfer  │
│ LBA[7:0]    │  0x03   │ LBA bits 7-0     │
│ LBA[15:8]   │  0x04   │ LBA bits 15-8    │
│ LBA[23:16]  │  0x05   │ LBA bits 23-16   │
│ Drive/Head  │  0x06   │ Drive select     │
│ Status/Cmd  │  0x07   │ Status/Command   │
└─────────────┴─────────┴──────────────────┘
```

---

## 9. Security & Safety

### 9.1 Error Handling

```
Error Detection & Recovery:
┌─────────────┐     ┌──────────────┐     ┌────────────┐
│ CRC8 Check  ├────►│ Retry Logic  ├────►│  Timeout   │
│   Failed    │     │ (3 attempts) │     │  Handler   │
└─────────────┘     └──────────────┘     └────────────┘
                           │
                           ▼
                    ┌──────────────┐
                    │ Error Report │
                    │   to GUI     │
                    └──────────────┘
```

### 9.2 Safety Features

1. **Watchdog Timer**: System reset on firmware hang
2. **CRC Validation**: All communication frames verified
3. **Timeout Protection**: All operations have timeout limits
4. **Power Cycle**: Hardware reset capability for CF cards
5. **LED Indicators**: Real-time hardware status

---

## 10. Performance Specifications

### 10.1 Communication Performance

| Parameter | Value |
|-----------|-------|
| USB Speed | Full Speed (12 Mbps) |
| Frame Size | Max 64 bytes |
| Payload Size | Max 56 bytes |
| CRC Overhead | 1 byte |
| Protocol Efficiency | ~87.5% |

### 10.2 Storage Performance

| Operation | NAND Flash | Compact Flash |
|-----------|------------|---------------|
| Page Size | 2048 bytes | 512 bytes |
| Read Speed | ~25 MB/s | ~20 MB/s |
| Write Speed | ~8 MB/s | ~15 MB/s |
| Erase Unit | 128 KB block | File level |

### 10.3 Response Times

| Command | Typical Response |
|---------|-----------------|
| Status Query | < 1 ms |
| Page Read | < 5 ms |
| Page Write | < 10 ms |
| Block Erase | < 500 ms |
| Format | < 2 seconds |

---

## Appendix A: Build Configuration

### Compiler Settings
```makefile
MCU = STM32F411VETx
CPU = cortex-m4
FPU = fpv4-sp-d16
FLOAT-ABI = hard
OPT = -O2
C_STANDARD = -std=gnu11
CPP_STANDARD = -std=gnu++14
```

### Memory Configuration
```
FLASH (rx) : ORIGIN = 0x08000000, LENGTH = 512K
RAM (xrw)  : ORIGIN = 0x20000000, LENGTH = 128K
```

---

**Document Version History:**
- v1.0 - Initial architecture documentation
- Created by: Professional Development Team
- Last Updated: November 2025