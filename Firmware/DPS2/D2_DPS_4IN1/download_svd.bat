@echo off
echo Downloading STM32F411 SVD file for register-level debugging...
echo.
echo This file enables viewing of STM32 peripheral registers in VSCode debugger
echo.

REM Download STM32F411 SVD file from ST's official repository
curl -L -o STM32F411.svd "https://raw.githubusercontent.com/posborne/cmsis-svd/master/data/STMicro/STM32F411.svd"

if exist STM32F411.svd (
    echo ✅ STM32F411.svd downloaded successfully
    echo This file enables register viewing in VSCode debugger
) else (
    echo ❌ Download failed. You can manually download from:
    echo https://raw.githubusercontent.com/posborne/cmsis-svd/master/data/STMicro/STM32F411.svd
    echo Save as STM32F411.svd in the D2_DPS_4IN1 folder
)

echo.
pause