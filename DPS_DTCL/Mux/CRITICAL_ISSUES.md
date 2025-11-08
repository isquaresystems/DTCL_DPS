# 🚨 CRITICAL ISSUES FOUND IN MUX REFACTORING

## Issues That Must Be Fixed Immediately:

### 1. **MuxWindow.xaml.cs - BROKEN REFERENCES**
Lines that reference non-existent `MuxChannelInfos[i]`:
- Line 350: `MuxChannelInfos[i].channel_SlotInfo[MuxChannelInfos[i].cartNo]`
- Line 362: `MuxChannelInfos[i].channel_SlotInfo[MuxChannelInfos[i].cartNo].cartType`
- Line 371: `MuxChannelInfos[i].channel_SlotInfo[MuxChannelInfos[i].cartNo].cartType`
- Line 377: `MuxChannelInfos[i].channel_SlotInfo[MuxChannelInfos[i].cartNo].cartType`
- Line 383: `MuxChannelInfos[i].channel_SlotInfo[MuxChannelInfos[i].cartNo].cartType`
- Line 391: `MuxChannelInfos[i].channel_SlotInfo[MuxChannelInfos[i].cartNo]`
- Line 424: `MuxChannelInfos[i].channel_SlotInfo[MuxChannelInfos[i].cartNo]`

**FIX**: Replace with `muxManager.channels[i].channel_SlotInfo[muxManager.channels[i].cartNo]`

### 2. **MuxManager.cs - SINGLETON DESTRUCTION**
```csharp
// Line 292 - CRITICAL BUG
hwInfo.Dispose();  // ❌ DESTROYS DpsInfo singleton!
```

**PROBLEM**: After first channel scan, DpsInfo singleton is disposed = ALL subsequent operations FAIL!

**FIX**: Remove `hwInfo.Dispose()` and reuse the singleton

### 3. **Architecture - DpsInfo Instance Management**
**CURRENT WRONG APPROACH**:
```csharp
// Creating multiple instances
HwInfo hwInfo = DpsInfo.Instance;  // In each method
_dpsInfo = DpsInfo.Instance;       // In MuxPerformanceCheck
```

**CORRECT APPROACH**:
- ONE DpsInfo singleton shared across ALL operations
- Mux switches channel → Same DpsInfo communicates with DTCL on that channel
- NO disposal, NO multiple instances

### 4. **Performance Check - NOT USING NEW CLASS**
`InitiatePC_Click` method:
- 200+ lines of old complex logic
- Completely ignores new `MuxPerformanceCheck` class
- Should be simplified to use `muxPerformanceCheck.ExecuteSelectedChannels()`

### 5. **MuxPerformanceCheck.cs - Architecture Issues**
```csharp
// Line in MuxPerformanceCheck
var cartObj = _dpsInfo.GetCartInstance(cartType);  // ❌ Method may not exist
```

## Correct Architecture Should Be:

```
Mux Hardware (8 channels)
    ↓ (Mux Protocol: '0'-'8')
MuxManager.switch_Mux(channelNo)
    ↓ (Channel switched)
ONE DpsInfo Instance (ISP Protocol)
    ↓ (Communicates with DTCL on active channel)
Cart Operations (D2/D3 operations)
```

## Required Fixes:
1. Replace ALL `MuxChannelInfos[i]` with `muxManager.channels[i]`
2. Remove `hwInfo.Dispose()` from MuxManager
3. Rewrite `InitiatePC_Click` to use `MuxPerformanceCheck`
4. Fix DpsInfo singleton usage consistency
5. Test MuxPerformanceCheck integration properly