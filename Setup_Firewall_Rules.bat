@echo off
:: Batch script to create Firewall rules with Auto-Elevation prompt
net session >nul 2>&1
if %errorLevel% neq 0 (
    echo Requesting Administrator privileges...
    powershell -Command "Start-Process '%~0' -Verb RunAs"
    exit /b
)

echo ====================================================
echo   Maple Guardian - Setting up Firewall Rules
echo ====================================================
echo.

powershell -ExecutionPolicy Bypass -File "%~dp0setup_rules.ps1"

echo.
echo ====================================================
echo   Done! Press any key to exit.
echo ====================================================
pause
