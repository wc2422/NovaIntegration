@echo off
:: Nova Integration - Emergency Watchdog Cleanup
:: This forcefully deletes orphaned frames if the C++ program crashes

set TARGET_DIR=D:\Capstone\SharedBuffer

if exist "%TARGET_DIR%" (
    echo Purging orphaned frames from %TARGET_DIR%...
    del /q /f "%TARGET_DIR%\*.jpg" 2>nul
    echo Cleanup complete.
) else (
    echo Directory not found. Is the USB plugged in?
)
pause