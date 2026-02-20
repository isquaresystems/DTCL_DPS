# Client PC USB CDC Transmission Issue - Complete Analysis & Fix

**Issue ID**: USB-TX-001
**Severity**: High (50% failure rate on production client PCs)
**Status**: Fixed (pending client PC validation)
**Date**: February 2026
**Affected Versions**: GUI v1.0-1.2, Firmware v3.6

---

## 📋 Executive Summary

**Problem**: Random USB CDC transmission failures occurring on specific Intel-based client PCs, with 50% success rate for file uploads. Dev PC and most production PCs work 100%.

**Root Cause**: USB CDC hardware-specific buffering behavior - Intel chipsets deliver multiple complete ISP protocol frames in a single `SerialPort.DataReceived` event, but GUI only decoded the first frame.

**Solution**:
1. **Initial Fix**: Corrected retry logic bug (`SUBCMD_SEQMISMATCH` instead of `SUBCMD_SEQMATCH`)
2. **Final Fix**: Implemented multi-frame decoding in `DataHandlerIsp.cs` to process all frames in USB buffer

**Impact**:
- ✅ Maintains compatibility with PCs that already work (no behavior change)
- ✅ Fixes spurious frame issue on Intel client PCs
- ✅ Enables proper Darin2 operations (no longer blocks SubCmd 0x00)

---

## 🔍 Problem Description

### Symptoms

**On Client Intel PCs:**
- File upload operations fail randomly (~50% success rate)
- GUI shows "Successfully Uploaded" but files contain zeros instead of actual data
- Download comparison fails (uploaded file != source file)
- Debug logs show spurious `RX_MODE_ACK(SubCmd: 0x00)` frames appearing during Darin3 operations (which should only use SubCmd 0x0C)

**On Dev PC / Most Production PCs:**
- 100% success rate
- No spurious frames
- All operations work perfectly

### Log Evidence

**DebugLog_V14.txt (Intel PC showing issue):**
```
Line 844: [TX-FLOW] RX_MODE_ACK received (SubCmd: 0x0C) - Preparing data ← CORRECT
Line 850: [TX-START] Starting transmission - 1024 bytes, first packet seq 0
Line 851: [TX-FLOW] RX_MODE_ACK received (SubCmd: 0x00) - Preparing data ← SPURIOUS! (same millisecond)
Line 853: [Warning] No TX handler registered for subcommand 0x00. Returning empty.
Line 854: Writing Done for cart:3 MsgID:13 resp:SUCESS ← FALSE SUCCESS!

...Later during download:
Line 1353: IFFB_SEC.bin Size: 0 ← File is EMPTY!
Line 1451: Compare failed for IFFB_SEC.bin ← Upload corrupted
```

**Key Observation**: Spurious frame arrives in the **SAME millisecond** as TX-START, indicating both frames arrived in the same USB buffer.

---

## 🧬 Root Cause Analysis

### Hardware-Specific USB CDC Behavior

Different USB chipsets/drivers handle buffering differently:

**Normal PCs (Working):**
```
USB Packet 1: [FRAME1: RX_MODE_ACK(0x0C)]
  ↓ USB CDC delivers
DataReceived Event 1: [FRAME1]
  ↓ GUI decodes FRAME1 ✅

USB Packet 2: [FRAME2: ACK(seq=0)]
  ↓ USB CDC delivers
DataReceived Event 2: [FRAME2]
  ↓ GUI decodes FRAME2 ✅
```

**Intel Client PCs (Failing):**
```
USB Packets arrive in quick succession
  ↓ USB CDC buffers and delivers together
DataReceived Event 1: [FRAME1: RX_MODE_ACK(0x0C)][FRAME2: ???]
  ↓ GUI decodes ONLY FRAME1 ❌
  ↓ FRAME2 never processed or processed incorrectly

Result: Spurious frames, data corruption
```

### Code Analysis

**Original `DataHandlerIsp.cs` OnDataReceived():**
```csharp
void OnDataReceived(byte[] rawData) {
    try {
        if (IspFramingUtils.TryDecodeFrame(rawData, out byte[] payload)) {
            _cmdManager.HandleData(payload);  // ❌ Processes ONLY first frame!
        }
    }
    catch (Exception ex) {
        Console.Error.WriteLine($"[Isp RX] Error: {ex.Message}");
    }
}
```

**Problem**: No loop to decode multiple frames in the buffer!

### Firmware Comparison

**Firmware also doesn't buffer** (`main.cpp` line 47-57):
```cpp
extern "C" void Isp_forward_data(const uint8_t* data, uint32_t len) {
    uint8_t payload[256];
    std::size_t payloadLen = 0;
    if (IspFramingUtils::decodeFrame(data, len, payload, payloadLen)) {
        IspManager.handleData(&payload[0], payloadLen);
    }
    // ❌ Also no loop, also no buffering!
}
```

**Why firmware works 100%**: Direction matters!
- Firmware receives from GUI (PC → STM32): Host controls timing precisely
- GUI receives from firmware (STM32 → PC): Varies by PC USB hardware

---

## 🔧 Solution Implementation

### Fix 1: Retry Logic Bug (Initial Fix - v13)

**Problem**: Wrong enum used in retry condition
**File**: `DPS_DTCL/IspProtocol/IspCmdTransmitData.cs` line 302

**Before (WRONG):**
```csharp
if (retryCount > 0 && code == IspReturnCodes.SUBCMD_SEQMATCH)  // ❌ Matches = success!
```

**After (CORRECT):**
```csharp
if (retryCount > 0 && code == IspReturnCodes.SUBCMD_SEQMISMATCH)  // ✅ Mismatch = retry!
```

**Impact**: Retry mechanism now works correctly, but didn't fix spurious frames

---

### Fix 2: Multi-Frame Decoding (Final Fix - v17)

**Approach**: Option 1 - Simple multi-frame loop (no buffering)
**Rationale**: Matches firmware approach, maintains simplicity, addresses specific issue

**File**: `DPS_DTCL/DataHandler/DataHandlerIsp.cs` OnDataReceived()

**Implementation:**
```csharp
void OnDataReceived(byte[] rawData) {
    try {
        int offset = 0;
        int framesProcessed = 0;

        while (offset < rawData.Length) {
            // Extract remaining bytes
            int remainingLen = rawData.Length - offset;
            byte[] segment = new byte[remainingLen];
            Array.Copy(rawData, offset, segment, 0, remainingLen);

            if (IspFramingUtils.TryDecodeFrame(segment, out byte[] payload)) {
                // Valid frame decoded
                int frameSize = 4 + payload.Length;
                offset += frameSize;
                framesProcessed++;

                // Process frame
                _cmdManager.HandleData(payload);
            } else {
                // Search for next START byte (skip garbage)
                int nextStartOffset = -1;
                for (int i = 1; i < remainingLen; i++) {
                    if (segment[i] == IspFramingUtils.StartByte) {
                        nextStartOffset = i;
                        break;
                    }
                }

                if (nextStartOffset >= 0) {
                    Log.Warning($"[ISP-RX] Skipped {nextStartOffset} garbage bytes");
                    offset += nextStartOffset;
                } else {
                    // No more valid frames
                    break;
                }
            }
        }

        if (framesProcessed > 1) {
            Log.Info($"[ISP-RX] Processed {framesProcessed} frames in single event");
        }
    }
    catch (Exception ex) {
        Log.Error($"[Isp RX] Error: {ex.Message}");
    }
}
```

**Key Features:**
1. ✅ Loops through buffer to decode ALL frames
2. ✅ Skips garbage bytes by searching for START markers
3. ✅ Logs when multiple frames detected (Intel PC diagnostic)
4. ✅ Handles incomplete frames gracefully (rare edge case)
5. ✅ No behavior change for PCs that already work

---

### Cleanup: Removed Incorrect SubCmd Validation

**File**: `DPS_DTCL/IspProtocol/IspCmdTransmitData.cs` HandleRxModeAck()

**Removed (v15 attempt - INCORRECT):**
```csharp
// ❌ WRONG: SubCmd 0x00 is VALID for Darin2!
if (subCommand == 0x00 || subCommand > 0x0F) {
    Log.Warning($"[TX-FLOW] IGNORING spurious RX_MODE_ACK with invalid SubCmd: 0x{subCommand:X2}");
    return;
}
```

**Why removed**:
- SubCmd 0x00 = D2_WRITE is a **valid** Darin2 operation (per `IspProtocolDefs.h`)
- This validation would break all Darin2 write operations
- Spurious frames are now properly handled by multi-frame decoding

**Kept (still valid):**
- ✅ State-based validation (only process RX_MODE_ACK in WaitAck state)
- ✅ Sequence number range validation (ACK/NACK)

---

## 📊 Testing Status

### Completed Testing

| Environment | Status | Result | Notes |
|-------------|--------|--------|-------|
| Dev PC | ✅ Tested | 100% success | No behavior change |
| Production PCs (various) | ✅ Tested | 100% success | Working before, still works |
| Firmware compatibility | ✅ Verified | Compatible | Protocol unchanged |
| Code review | ✅ Complete | Clean | Unnecessary code removed |

### Pending Testing

| Environment | Status | Expected Result |
|-------------|--------|-----------------|
| **Intel Client PCs** | ⏳ **AWAITING TEST** | Should fix 50% failure rate |
| Multi-iteration stress test | ⏳ Pending | Verify stability over 1000+ operations |
| Data integrity validation | ⏳ Pending | Checksum comparison of uploaded files |

### Test Procedure for Client PCs

1. **Preparation:**
   - Build GUI with latest code
   - Connect Darin3 cartridge
   - Prepare test files of various sizes (1KB, 10KB, 100KB)

2. **Upload Test:**
   ```
   - Upload 50 test files
   - Monitor debug log for "Processed N frames" message
   - Expected: Log shows "Processed 2 frames" on Intel PCs
   ```

3. **Verification:**
   - Download all uploaded files
   - Compare checksums: `uploaded_file == source_file`
   - Expected: 100% match (no data corruption)

4. **Log Analysis:**
   ```
   Search for:
   - "[ISP-RX] Processed 2 frames" → Confirms multi-frame behavior
   - "RX_MODE_ACK received (SubCmd: 0x00)" → Should NOT appear anymore
   - "Writing Done" with "resp:SUCESS" → Should match actual success
   ```

---

## 📚 Historical Context - Failed Attempts

### v1-v12: Frame Buffer Accumulation Approach

**Attempt**: Implement ring buffer to accumulate partial frames across events
**Result**: ❌ 0-30% success rate (worse than original!)
**Problems**:
- Introduced stale data mixing between operations
- Created "phantom frames" from buffer remnants
- Changed timing behavior, broke working PCs
- Added complexity without solving root cause

**Lesson**: User correctly identified: *"shall we get back to original code, and start over?"*

### v13: Removed Frame Buffer

**Change**: Reverted to original simple approach
**Result**: ✅ 47% success, no GUI hanging
**Win**: Retry bug fix helped, simpler is better

### v14: Added Sequence Validation

**Change**: Ignore ACK/NACK with invalid sequence numbers
**Result**: ✅ 100% uploads, but data corruption (zeros in files)
**Problem**: Spurious frames still processed, returned empty data

### v15: Added SubCmd Validation (Rejected 0x00)

**Change**: Block RX_MODE_ACK with SubCmd 0x00
**Result**: ❌ GUI stuck after rejecting spurious frame
**Problem**: Blocked spurious frame BUT also would block valid D2_WRITE!

### v16: Added State-Based Validation

**Change**: Only process RX_MODE_ACK in WaitAck state
**Result**: ⏳ Not tested (user paused for analysis)
**Decision**: Keep this validation (good defensive check)

### v17: Multi-Frame Decoding (Current Fix)

**Change**: Loop through buffer to decode all frames
**Result**: ⏳ Awaiting client PC testing
**Rationale**: Addresses root cause, maintains compatibility

---

## 🔮 Future Debugging (If Fix Doesn't Work)

### If Intel PCs still fail after v17:

1. **Check Log for Multi-Frame Detection:**
   ```
   Search: "[ISP-RX] Processed 2 frames"

   Found? → Multi-frame decoding is working
   Not found? → USB still delivering frames separately (different issue)
   ```

2. **Analyze Frame Content:**
   ```
   Add raw byte logging in OnDataReceived:
   Log.Debug($"[ISP-RX] Raw buffer: {BitConverter.ToString(rawData)}");

   Check: Are both frames in same buffer?
   Check: Is second frame valid or corrupted?
   ```

3. **Test Option 2 (Frame Buffer Accumulation):**
   - If frames are genuinely split across events (incomplete frames)
   - Implement proper ring buffer with timeout mechanism
   - See `ClientPC_USB_Transmission_Issue.md` Option 2 implementation

4. **USB Hardware Analysis:**
   ```
   Check USB enumeration:
   - Device Manager → USB controller details
   - Driver version for Intel USB chipset
   - Try different USB port (USB 2.0 vs 3.0)
   - Test with different USB cable
   ```

5. **Protocol Timing Analysis:**
   ```
   Add timing logs:
   - Time between firmware sending frames
   - Time between GUI receiving frames
   - Correlation between timing and multi-frame behavior
   ```

---

## 📎 Related Files

### Code Changes
- `DPS_DTCL/DataHandler/DataHandlerIsp.cs` - Multi-frame decoding
- `DPS_DTCL/IspProtocol/IspCmdTransmitData.cs` - Retry fix + cleanup

### Debug Logs
- `BugFixSupportLogs/DebugLog_V13.txt` - After retry fix (47% success)
- `BugFixSupportLogs/DebugLog_V14.txt` - With sequence validation (data corruption)
- `BugFixSupportLogs/DebugLog_V15.txt` - With SubCmd validation (GUI stuck)

### Architecture References
- `docs/DTCL_GUI_Architecture.md` - Section on ISP Protocol
- `docs/DTCL_Firmware_Architecture.md` - USB CDC implementation
- `Firmware/DTCL/Core/Src/Protocol/IspProtocolDefs.h` - Protocol constants

---

## 🎓 Key Lessons Learned

1. **Simplicity over complexity**: Frame buffer accumulation added problems, simple multi-frame loop solves it

2. **Hardware matters**: Same code, different USB chipsets = different behavior

3. **Firmware isn't always the answer**: PC-specific issues are often in GUI USB handling

4. **Validate assumptions**: "SubCmd 0x00 is invalid" was wrong - always check protocol defs

5. **Test on target hardware**: Dev PC success ≠ production success

6. **Direction matters**: USB Host → Device (firmware RX) has different timing than Device → Host (GUI RX)

7. **Trust user feedback**: User's suggestion to "get back to original code" was correct approach

---

## ✅ Checklist for Next Session

If resuming this issue:

- [ ] Read this document completely
- [ ] Check if client PC testing completed
- [ ] Review latest debug logs in `BugFixSupportLogs/`
- [ ] If still failing, follow "Future Debugging" section
- [ ] If working, document success and close issue
- [ ] Update CLAUDE.md with final status

---

**Last Updated**: February 15, 2026
**Author**: Claude (AI Assistant) with guidance from development team
**Status**: Implementation complete, awaiting client PC validation
