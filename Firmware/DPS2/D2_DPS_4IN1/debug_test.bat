@echo off
echo ====================================================================
echo D2 Debug Test Script
echo ====================================================================
echo.

echo [1/5] Checking if ELF file exists...
for %%f in ("build\D2_DPS_4IN1_V*.elf") do (
    echo ✅ ELF file found: %%f
    goto :found
)
echo ❌ No versioned ELF file found
echo Run 'make all' to build the project first
goto :end

:found

echo.
echo [2/5] Checking ARM GDB...
arm-none-eabi-gdb --version >nul 2>&1
if errorlevel 1 (
    echo ❌ arm-none-eabi-gdb not found in PATH
    echo Install ARM GCC toolchain and add to PATH
) else (
    echo ✅ arm-none-eabi-gdb found
)

echo.
echo [3/5] Checking OpenOCD...
openocd --version >nul 2>&1
if errorlevel 1 (
    echo ❌ OpenOCD not found in PATH
    echo Install OpenOCD and add to PATH
) else (
    echo ✅ OpenOCD found
)

echo.
echo [4/5] Testing OpenOCD connection...
echo Starting OpenOCD test (will timeout in 5 seconds)...
timeout 5 openocd -f interface/stlink.cfg -f target/stm32f4x.cfg >nul 2>&1
if errorlevel 1 (
    echo ❌ OpenOCD connection failed - check ST-Link connection
) else (
    echo ✅ OpenOCD can connect to target
)

echo.
echo [5/5] VSCode Debug Instructions:
echo 1. Make sure VSCode has Cortex-Debug extension installed
echo 2. Start OpenOCD manually in one terminal:
echo    openocd -f interface/stlink.cfg -f target/stm32f4x.cfg
echo 3. Wait for "Listening on port 3333 for gdb connections"
echo 4. In VSCode, press F5 and select "Cortex Debug (OpenOCD External)"
echo.

:end
echo ====================================================================
pause