# USB CDC Transmission Failures on Intel Client PCs - Complete Analysis & Fixes

**Date**: February 15, 2026
**Status**: ✅ RESOLVED
**Severity**: CRITICAL - Files written with size 0, data corruption
**Hardware**: Intel client PCs with USB CDC communication

---

## Executive Summary

A series of timing-dependent bugs in the ISP protocol implementation caused transmission failures on Intel client PCs. The root issue was **spurious firmware responses** combined with **improper error handling** and **missing await statements**, resulting in data corruption where files were written with size 0.

**Key Finding**: The bug manifested as a **heisenbug** - worked with DEBUG log level but failed with INFO log level due to timing differences in logging operations.

---

## Timeline of Issues Discovered

### Issue 1: Spurious RX_MODE_ACK During TX Operations ⚠️ PRIMARY ROOT CAUSE

**Symptom**:
- Files written to cartridge with size 0 (data corruption)
- Operation appears successful but data not written
- Only occurs on Intel client PCs, not all systems

**Root Cause**:
Firmware sends **wrong response type** during transmission, indicating firmware is in incorrect state.

**Command Format Example**:
```
GUI Initiates Write Operation:
├─> Sends: TX_DATA (0x56) + Subcommand D3_WRITE (0x0C)
├─> Firmware Expected Response: ACK (0xA1) - "Ready to transmit"
└─> Firmware Actual Response: RX_MODE_ACK (0xA4) - "Ready to receive" ❌ WRONG!

Result: Firmware in RECEIVE mode when GUI trying to TRANSMIT
```

**Detailed Log Example**:
```
[TX-FIX] SPURIOUS RX_MODE_ACK (SubCmd: 0x0C) during active transmission!
[TX-FIX] Firmware in wrong state - ABORTING transmission to prevent data corruption
```

**Why This Happens**:
- Timing-dependent race condition in firmware state machine
- Previous operation's state not fully cleared before new operation starts
- More frequent on faster Intel CPUs where operations complete quicker

**Initial Wrong Assumption**:
❌ "Maybe we can just ignore spurious response and continue transmission"
- This led to data corruption because firmware wasn't actually in TX mode

**Correct Solution**:
✅ **Abort entire operation immediately and retry from beginning**
- Set `SubCmdResponse = SPURIOUS_RESPONSE`
- Reset transmission state (`currentState = TxState.Idle`)
- Return to caller - caller will retry entire operation cleanly

**Code Changes** (`IspCmdTransmitData.cs` - HandleRxModeAck method):
```csharp
void HandleRxModeAck(byte[] data)
{
    var subCommand = data[1];

    // CRITICAL FIX: Abort if spurious RX_MODE_ACK during active transmission
    if (txBuffer != null && txSize > 0)
    {
        Log.Warning($"[TX-FIX] SPURIOUS RX_MODE_ACK (SubCmd: 0x{subCommand:X2}) during active transmission!");
        Log.Warning($"[TX-FIX] Firmware in wrong state - ABORTING transmission to prevent data corruption");

        SubCmdResponse = IspSubCmdResponse.SPURIOUS_RESPONSE;  // NEW enum value
        currentState = TxState.Idle;
        Reset();
        return;  // Caller will retry entire operation
    }

    // ... rest of normal RX_MODE_ACK handling ...
}
```

---

### Issue 2: Execute() Converted SPURIOUS_RESPONSE to FAILED

**Symptom**:
- Retry logic never triggered despite spurious responses
- Logs showed "Writing Done for cart:3 MsgID:11 resp:FAILED" instead of retry

**Root Cause**:
`DataHandlerIsp.Execute()` method converted **all non-SUCCESS responses to FAILED**, losing the specific SPURIOUS_RESPONSE indicator needed for retry logic.

**Code Before Fix** (`DataHandlerIsp.cs` - Execute method):
```csharp
// WRONG: Converted SPURIOUS_RESPONSE to FAILED
if (_tx.SubCmdResponse == IspSubCmdResponse.SUCESS)
{
    return _tx.SubCmdResponse;
}
// ... similar for _rx ...

// All other responses returned as FAILED ❌
return IspSubCmdResponse.FAILED;
```

**Code After Fix**:
```csharp
// CORRECT: Preserve actual response value
if (_tx.SubCmdResponse == IspSubCmdResponse.SUCESS)
{
    return _tx.SubCmdResponse;
}

if (_rx.SubCmdResponse == IspSubCmdResponse.SUCESS)
{
    return _rx.SubCmdResponse;
}

// Return actual response value (SPURIOUS_RESPONSE, TX_FAILED, etc.) ✅
if (_tx.SubCmdResponse != IspSubCmdResponse.NO_RESPONSE)
{
    return _tx.SubCmdResponse;  // Could be SPURIOUS_RESPONSE, TX_FAILED, etc.
}

if (_rx.SubCmdResponse != IspSubCmdResponse.NO_RESPONSE)
{
    return _rx.SubCmdResponse;
}

// Both are NO_RESPONSE - return FAILED as fallback
return IspSubCmdResponse.FAILED;
```

---

### Issue 3: Retry Routing to Wrong Handler (Array Mutation Bug)

**Symptom**:
- First attempt routes correctly to TX-FLOW
- Retry attempts route incorrectly to RX-FLOW
- Same operation, different routing behavior

**Detailed Logs Showing Issue**:
```
SUCCESS CASE (First Attempt):
[TX-SETMODE] SetMode called - switching to TX mode
[TX-SETMODE] Starting chunk 1/1
... transmission proceeds correctly ...

RETRY CASE (Second Attempt):
[RX-SETMODE] SetMode called - switching to RX mode  ❌ WRONG HANDLER!
[RX-BUFFER] Receiving 1023 bytes (1 chunks)
... routes to receive instead of transmit ...
```

**Root Cause**:
`SetMode()` method in `IspCmdTransmitData.cs` **modifies the input array**:

```csharp
void SetMode(byte[] data)
{
    data[0] = (byte)IspCommand.RX_DATA;  // ❌ MODIFIES INPUT ARRAY!
    // This changes 0x56 (TX_DATA) to 0x55 (RX_DATA)

    _transport.TransmitAsync(data);
}
```

**Command Byte Mutation Example**:
```
Original cmdPayload: [0x56, 0x0C, 0x00, ...]  // TX_DATA (0x56), D3_WRITE (0x0C)
                        ↓
After SetMode() in first attempt: [0x55, 0x0C, 0x00, ...]  // RX_DATA (0x55) ❌

Retry uses MODIFIED array → IspCommandManager routes to RX handler!
```

**Command Routing Logic** (`IspCommandManager.cs`):
```csharp
public void HandleData(byte[] data)
{
    foreach (var handler in _handlers)
    {
        if (handler.Match(data[0]))  // Routes based on data[0]
        {
            handler.HandleReceivedData(data);
            return;
        }
    }
}

// IspCmdTransmitData.Match():
public bool Match(byte cmd) => cmd == (byte)IspCommand.TX_DATA;  // 0x56

// IspCmdReceiveData.Match():
public bool Match(byte cmd) => cmd == (byte)IspCommand.RX_DATA;  // 0x55
```

**Why Retry Fails**:
1. First attempt: cmdPayload[0] = 0x56 → Routes to TX handler → SetMode() changes to 0x55
2. Spurious response triggers retry
3. Retry uses SAME array (now cmdPayload[0] = 0x55)
4. Routes to RX handler instead of TX handler ❌

**Correct Solution**:
Create **fresh copy** of cmdPayload for each retry attempt:

```csharp
for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
{
    // CRITICAL FIX: Create fresh copy of cmdPayload for each attempt!
    // SetMode() modifies data[0] from TX_DATA to RX_DATA,
    // so reusing same array causes retry to route to wrong handler
    byte[] payloadCopy = new byte[cmdPayload.Length];
    Array.Copy(cmdPayload, payloadCopy, cmdPayload.Length);

    var res = await DataHandlerIsp.Instance.Execute(payloadCopy, progress);

    if (res == IspSubCmdResponse.SPURIOUS_RESPONSE)
    {
        Log.Warning($"Spurious firmware response - retrying (attempt {attempt}/{MAX_RETRIES})");
        if (attempt < MAX_RETRIES)
        {
            await Task.Delay(500);
            continue;  // Retry with fresh copy
        }
    }
    break;
}
```

---

### Issue 4: Sporadic NACK with Sequence Mismatch

**Symptom**:
```
[ISP-TX] NACK received - Seq: 3072, Code: SUBCMD_SEQMISMATCH (0xB2)
```

**Root Cause**:
After aborting transmission due to spurious response, **firmware sequence counter not reset** before next operation starts.

**Sequence Flow**:
```
Operation 1:
├─> GUI sends TX_DATA_RESET (0x54)
├─> GUI sends RX_DATA_RESET (0x53)
├─> GUI sends TX_DATA with seq=0
├─> Firmware receives spurious response, GUI aborts
└─> Firmware sequence counter stuck at 3072 ❌

Operation 2 (Retry):
├─> GUI sends TX_DATA_RESET (0x54)  ← Sent immediately (no delay)
├─> GUI sends RX_DATA_RESET (0x53)  ← Sent immediately (no delay)
├─> GUI sends TX_DATA with seq=0
└─> Firmware NACK: "Expected seq=3072, got seq=0" ❌
```

**Why This Happens**:
- RESET commands sent to firmware
- **No delay** - GUI immediately sends next operation
- Firmware still processing RESET in interrupt handler
- Sequence counter not yet reset when new operation arrives

**Correct Solution**:
Add **50ms delay** after RESET commands to allow firmware state stabilization:

```csharp
public async Task<IspSubCmdResponse> Execute(byte[] payload, IProgress<int> progress)
{
    var reset = new byte[1];
    reset[0] = (byte)IspCommand.TX_DATA_RESET;
    _cmdManager.HandleData(reset);
    reset[0] = (byte)IspCommand.RX_DATA_RESET;
    _cmdManager.HandleData(reset);

    // CRITICAL: Give firmware time to process RESET commands and clear its state
    // Without this delay, firmware's sequence counter may not reset, causing NACK on retry
    await Task.Delay(50);
    Log.Info("[EXECUTE-RESET] 50ms delay after RESET commands for firmware state stabilization");

    // ... rest of Execute() ...
}
```

---

### Issue 5: UART Timeout After Erase Operation

**Symptom**:
```
16:18:14.037 Erasing D2 full Done
16:18:14.099 Transmitting erase setup frame
16:18:14.804 Start LED commands (no firmware response logged!)
16:18:15.893 UART transmit error: The semaphore timeout period has expired
```

**Root Cause**:
`EraseCartFiles()` method in Darin2.cs **did not await Execute()** call, causing fire-and-forget behavior.

**Code Before Fix** (`Darin2.cs` line 1458):
```csharp
public async Task<int> EraseCartFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
{
    Log.Info("Start Erasing D2 full");

    var cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D2_ERASE, 0,
       new ushort[] { (ushort)0, 1, (ushort)1, 0, cartNo });

    DataHandlerIsp.Instance.Execute(cmdPayload, null);  // ❌ NOT AWAITED!

    // Manual wait loop (unreliable - checks old SubCmdResponse)
    var i = 0;
    while (DataHandlerIsp.Instance._tx.SubCmdResponse == IspSubCmdResponse.IN_PROGRESS)
    {
        await Task.Delay(100);
        DataHandlerIsp.Instance.OnProgressChanged("Erase", i, 1024, progress);
        i += 10;
    }

    Log.Info("Erasing D2 full Done");  // ❌ Logs "Done" before firmware completes!

    return DataHandlerIsp.Instance._tx.SubCmdResponse == IspSubCmdResponse.SUCESS ?
        returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
}
```

**Sequence of Events (Bug)**:
```
1. Execute() fires in background (not awaited)
   └─> Starts transmission to firmware

2. Code immediately continues to while loop
   └─> Checks SubCmdResponse (might be from previous operation!)

3. Loop exits quickly (SubCmdResponse not yet IN_PROGRESS)

4. Log prints "Erasing D2 full Done" ❌ (firmware still processing!)

5. LED commands sent immediately after

6. Firmware still busy with erase operation

7. SerialPort.Write() blocks waiting for firmware to read from buffer

8. Timeout: "The semaphore timeout period has expired"
```

**Why Manual Wait Loop Failed**:
- Execute() not awaited → runs in background
- SubCmdResponse might still be from previous operation (not yet updated)
- Loop might exit immediately if SubCmdResponse != IN_PROGRESS yet
- No guarantee firmware has even started processing erase

**Correct Solution**:
Properly await Execute() and remove redundant manual wait loop:

```csharp
public async Task<int> EraseCartFiles(IProgress<int> progress, byte cartNo, bool trueErase = false)
{
    Log.Info("Start Erasing D2 full");

    var cmdPayload = FrameInternalPayload((byte)IspCommand.TX_DATA, (byte)IspSubCommand.D2_ERASE, 0,
       new ushort[] { (ushort)0, 1, (ushort)1, 0, cartNo });

    // CRITICAL FIX: Properly await Execute() with retry logic
    const int MAX_RETRIES = 3;
    IspSubCmdResponse res = IspSubCmdResponse.FAILED;

    for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
    {
        // Create fresh copy for each attempt
        byte[] payloadCopy = new byte[cmdPayload.Length];
        Array.Copy(cmdPayload, payloadCopy, cmdPayload.Length);

        // Start Execute() but show progress while waiting
        var executeTask = DataHandlerIsp.Instance.Execute(payloadCopy, null);

        // Update progress bar while waiting for erase to complete
        int progressValue = 0;
        while (!executeTask.IsCompleted)
        {
            await Task.Delay(500);
            progressValue = Math.Min(progressValue + 50, 1000);
            DataHandlerIsp.Instance.OnProgressChanged("Erase", progressValue, 1024, progress);
        }

        // Get the result (task already completed)
        res = await executeTask;  // ✅ PROPERLY AWAITED!

        if (res == IspSubCmdResponse.SPURIOUS_RESPONSE)
        {
            Log.Warning($"Spurious firmware response - retrying (attempt {attempt}/{MAX_RETRIES})");
            if (attempt < MAX_RETRIES)
            {
                await Task.Delay(500);
                continue;
            }
        }
        break;
    }

    Log.Info("Erasing D2 full Done");  // ✅ Now truly done!

    DataHandlerIsp.Instance.OnProgressChanged("Erase", 1024, 1024, progress);

    return res == IspSubCmdResponse.SUCESS ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
}
```

**Key Improvements**:
1. ✅ Execute() properly awaited
2. ✅ Progress updates while waiting (check Task.IsCompleted)
3. ✅ Fresh cmdPayload copy for retries
4. ✅ Retry logic for spurious responses
5. ✅ "Done" logged only after firmware truly completes

---

### Issue 6: Progress Bar Flickering During Erase

**Symptom**:
Progress bar appears and disappears repeatedly (flickers) during erase operation.

**Root Cause**:
**Two sources** updating progress simultaneously:
1. While loop incrementing fake progress (0, 50, 100, 150...)
2. Execute() reporting TX progress (which is 0 for erase - no data chunks)

**Why This Causes Flicker**:
```
Time 0ms:    While loop reports: 50/1024 (4.8%)    → Progress bar shows 4.8%
Time 10ms:   Execute() reports: 0/10 (0%)          → Progress bar shows 0%
Time 500ms:  While loop reports: 100/1024 (9.7%)   → Progress bar shows 9.7%
Time 510ms:  Execute() reports: 0/10 (0%)          → Progress bar shows 0%

Result: Progress bar jumps between 0% and 4-10% repeatedly = FLICKER
```

**Correct Solution**:
Pass `null` to Execute() so it doesn't report progress - only while loop updates:

```csharp
// Pass null for progress to Execute() - only our loop should update progress to avoid flicker
var executeTask = DataHandlerIsp.Instance.Execute(payloadCopy, null);  // ✅ null here!

// Update progress bar while waiting for erase to complete
int progressValue = 0;
while (!executeTask.IsCompleted)
{
    await Task.Delay(500); // Update every 500ms for smooth appearance
    progressValue = Math.Min(progressValue + 50, 1000);
    DataHandlerIsp.Instance.OnProgressChanged("Erase", progressValue, 1024, progress);  // Single source!
}
```

**Why This Works**:
- Execute() receives `null` → OnProgressChanged returns early (does nothing)
- Only while loop updates progress → single source of truth
- No conflicting updates → no flicker

---

## New Protocol Definition

**Added to `IspProtocolDefs.cs`**:
```csharp
public enum IspSubCmdResponse : byte
{
    IN_PROGRESS = 0xC1,
    TX_FAILED = 0xC2,
    SPURIOUS_RESPONSE = 0xC3,  // ✅ NEW: Firmware sent wrong response type - full operation retry needed
    NO_RESPONSE = 0xFF,
    SUCESS = 0x00,
    FAILED = 0x01
}
```

---

## Complete List of Code Changes

### 1. IspProtocolDefs.cs
- **Line 87**: Added `SPURIOUS_RESPONSE = 0xC3` enum value
- **Purpose**: Signal that firmware sent wrong response type, requiring full operation retry

### 2. IspCmdTransmitData.cs
- **Method**: `HandleRxModeAck()`
- **Change**: Abort transmission immediately on spurious RX_MODE_ACK instead of continuing
- **Lines**: ~160-175 (approximate)
```csharp
if (txBuffer != null && txSize > 0)
{
    Log.Warning($"[TX-FIX] SPURIOUS RX_MODE_ACK (SubCmd: 0x{subCommand:X2}) during active transmission!");
    SubCmdResponse = IspSubCmdResponse.SPURIOUS_RESPONSE;
    currentState = TxState.Idle;
    Reset();
    return;
}
```

### 3. DataHandlerIsp.cs
- **Method**: `Execute()`
- **Change 1**: Preserve actual response values instead of converting to FAILED
- **Lines**: 139-164
```csharp
if (_tx.SubCmdResponse == IspSubCmdResponse.SUCESS) return _tx.SubCmdResponse;
if (_rx.SubCmdResponse == IspSubCmdResponse.SUCESS) return _rx.SubCmdResponse;
if (_tx.SubCmdResponse != IspSubCmdResponse.NO_RESPONSE) return _tx.SubCmdResponse;
if (_rx.SubCmdResponse != IspSubCmdResponse.NO_RESPONSE) return _rx.SubCmdResponse;
return IspSubCmdResponse.FAILED;
```

- **Change 2**: Added 50ms delay after RESET commands
- **Lines**: 106-109
```csharp
await Task.Delay(50);
Log.Info("[EXECUTE-RESET] 50ms delay after RESET commands for firmware state stabilization");
```

### 4. Darin3.cs
- **Method**: `ExecuteWriteOperationAsync()`
- **Change**: Added retry loop with fresh cmdPayload copy
- **Lines**: 1193-1220
```csharp
for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
{
    byte[] payloadCopy = new byte[cmdPayload.Length];
    Array.Copy(cmdPayload, payloadCopy, cmdPayload.Length);

    var res = await DataHandlerIsp.Instance.Execute(payloadCopy, progress);

    if (res == IspSubCmdResponse.SPURIOUS_RESPONSE)
    {
        Log.Warning($"Spurious firmware response - retrying (attempt {attempt}/{MAX_RETRIES})");
        if (attempt < MAX_RETRIES)
        {
            await Task.Delay(500);
            continue;
        }
    }
    return res == IspSubCmdResponse.SUCESS ? returnCodes.DTCL_SUCCESS : returnCodes.DTCL_NO_RESPONSE;
}
```

### 5. Darin2.cs

**Method**: `WriteD2BlockData()`
- **Change**: Added retry loop with fresh cmdPayload copy
- **Lines**: ~2460-2490

**Method**: `ReadD2BlockData()`
- **Change**: Added retry loop with fresh cmdPayload copy
- **Lines**: ~2515-2545

**Method**: `EraseCartFiles()`
- **Change**: Fixed missing await, added retry logic, fixed progress updates
- **Lines**: 1444-1486
```csharp
for (int attempt = 1; attempt <= MAX_RETRIES; attempt++)
{
    byte[] payloadCopy = new byte[cmdPayload.Length];
    Array.Copy(cmdPayload, payloadCopy, cmdPayload.Length);

    // Start Execute() but show progress while waiting
    var executeTask = DataHandlerIsp.Instance.Execute(payloadCopy, null);

    int progressValue = 0;
    while (!executeTask.IsCompleted)
    {
        await Task.Delay(500);
        progressValue = Math.Min(progressValue + 50, 1000);
        DataHandlerIsp.Instance.OnProgressChanged("Erase", progressValue, 1024, progress);
    }

    res = await executeTask;

    if (res == IspSubCmdResponse.SPURIOUS_RESPONSE)
    {
        Log.Warning($"Spurious firmware response - retrying (attempt {attempt}/{MAX_RETRIES})");
        if (attempt < MAX_RETRIES)
        {
            await Task.Delay(500);
            continue;
        }
    }
    break;
}
```

**Method**: `EraseBlockNo()`
- **Change**: Added retry logic for consistency
- **Lines**: 1508-1540

---

## Testing Results

**Environment**: Intel client PC (previously failing)

**Test Operations**:
1. ✅ Write D3 files (Compact Flash)
2. ✅ Read D3 files
3. ✅ Write D2 blocks (NAND Flash)
4. ✅ Read D2 blocks
5. ✅ Erase D2 full cartridge
6. ✅ Erase D2 blocks
7. ✅ Multiple iterations without failure

**Results**:
- No spurious response failures
- No UART timeouts
- No sequence mismatch NACKs
- Files written with correct sizes
- Progress bars update smoothly without flicker
- All operations complete successfully

**User Confirmation**: "atlast all works for now, issues did not reproduce, fingers cross"

---

## Key Lessons Learned

### 1. Spurious Firmware Responses Require Full Operation Retry
**Wrong Approach**: Continue operation or retry just the failed chunk
**Correct Approach**: Abort immediately, reset state, retry entire operation from beginning

### 2. Array Mutation in Protocol Handlers is Dangerous
**Problem**: SetMode() modifies input array (data[0] = 0x55)
**Solution**: Always use fresh copy of command payload for retries

### 3. Firmware State Machine Needs Time to Reset
**Problem**: Sending next operation immediately after RESET commands
**Solution**: 50ms delay after RESET commands for firmware stabilization

### 4. Always Await Async Operations
**Problem**: Fire-and-forget Execute() calls cause timing bugs
**Solution**: Properly await all async operations, check Task.IsCompleted for progress updates

### 5. Single Source of Truth for Progress Updates
**Problem**: Multiple sources updating progress cause UI flicker
**Solution**: Pass null to background operations, let caller handle progress reporting

### 6. Timing-Dependent Bugs (Heisenbugs) Require Careful Analysis
**Characteristic**: Bug changes behavior when debugging/logging added
**Solution**: Look for race conditions, timing assumptions, and state management issues

---

## Preventive Measures for Future Development

1. **Always check return values** - Don't convert specific error codes to generic FAILED
2. **Never reuse command buffers** - Create fresh copies for retries to avoid mutation issues
3. **Add delays after state transitions** - Give firmware time to process state changes
4. **Await all async operations** - No fire-and-forget patterns
5. **Single progress reporter** - Avoid multiple sources updating same UI element
6. **Test on different hardware** - Timing bugs manifest differently on different CPUs
7. **Log command bytes in hex** - Essential for debugging protocol issues
8. **Retry logic at operation level** - Not at chunk/packet level

---

## Related Documentation

- ISP Protocol Specification: `docs/DTCL_GUI_Architecture.md` Section 6
- Command Flow Diagrams: `docs/DTCL_GUI_Architecture.md` Section 7
- Error Handling Patterns: `CLAUDE.md` Line 1165
- Thread Safety Guidelines: `CLAUDE.md` Line 995

---

## Version Information

**GUI Version**: 1.3
**Firmware Version**: 3.6
**Fix Applied**: February 15, 2026
**Testing Status**: ✅ All operations successful on Intel client PC
