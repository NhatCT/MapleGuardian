@echo off
:: Batch runner for live traffic test with elevation
net session >nul 2>&1
if %errorLevel% neq 0 (
    powershell -Command "Start-Process '%~0' -Verb RunAs"
    exit /b
)

powershell -ExecutionPolicy Bypass -File "%~dp0test_live_traffic.ps1"
echo.
pause
