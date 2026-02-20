# DTCL Console Write Test

This is a console version of MainWindow.xaml.cs that uses the **exact same ExecuteWriteOperationOnSlots** method and write flow but without any GUI components.

## Overview

The `ConsoleMainWindow.cs` file is a direct copy of MainWindow logic with:
- All GUI components removed (no WPF dependencies)
- Same HardwareInfo instance usage
- Same event handlers and flow
- Same Write operations using ICart interface
- Console output instead of UI updates

## Building with Visual Studio 2022

1. Open `StandaloneWriteTest.sln` in Visual Studio 2022
2. The project references the main DTCL project to use all existing classes
3. Build (F6 or Build > Build Solution)

## Running the Test

```bash
# From command line
StandaloneWriteTest.exe [COM_PORT] [ITERATIONS]

# Examples
StandaloneWriteTest.exe              # Auto-detect port, 1000 iterations
StandaloneWriteTest.exe COM3         # Use COM3, 1000 iterations  
StandaloneWriteTest.exe COM3 5000    # Use COM3, 5000 iterations
```

## What It Does

1. Initializes HardwareInfo exactly like MainWindow
2. Subscribes to all the same events  
3. Detects hardware automatically
4. **Sets up mock slots** - assumes all 4 slots have Darin2 cartridges
5. **Uses ExecuteWriteOperationOnSlots** - the exact method from MainWindow line 570
6. **Uses ExecuteWriteOnSingleSlot** - calls cartInstance.WriteUploadFiles()
7. **Uses same error handling** - ProcessWriteResult, GetUploadPathForCartType
8. Logs detailed results to TestLogs folder

### Key Methods Copied from MainWindow:
- `ExecuteWriteOperationOnSlots()`
- `ExecuteWriteOnSingleSlot()`  
- `ProcessWriteResult()`
- `preCommandExeOper()` / `postCommandExeOper()`

## Key Differences from GUI Version

- No Dispatcher.Invoke (runs synchronously)
- No MessageBox dialogs (console output)
- No button states or colors (text status)
- No progress bars (console progress indicators)
- Simplified event handlers

## Output

- Console shows real-time progress
- Detailed log in `TestLogs\ConsoleTest_[timestamp].log`
- Success/failure statistics
- Uses exact same protocol and commands as GUI

This approach tests the actual production code paths without any GUI overhead.