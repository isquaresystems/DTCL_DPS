@echo off
echo ====================================================================
echo Download STM32F411 SVD file for register-level debugging
echo ====================================================================

set SVD_URL=https://raw.githubusercontent.com/posborne/cmsis-svd/master/data/STMicro/STM32F411.svd
set SVD_FILE=STM32F411.svd

echo.
echo Downloading %SVD_FILE% from GitHub...

powershell -Command "Invoke-WebRequest -Uri '%SVD_URL%' -OutFile '%SVD_FILE%'"

if exist %SVD_FILE% (
    echo.
    echo ✅ Successfully downloaded %SVD_FILE%
    echo.
    echo You can now use register-level debugging in VSCode with Cortex-Debug
) else (
    echo.
    echo ❌ Failed to download %SVD_FILE%
    echo Please download manually from:
    echo %SVD_URL%
)

echo ====================================================================
pause