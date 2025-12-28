@echo off

:: Set code page to UTF-8
chcp 65001 >nul

title DynamicDbApi Startup Script

echo ========================================
echo    DynamicDbApi Project Startup Script
echo ========================================
echo.
echo Starting PowerShell startup script...
echo.

where powershell >nul 2>nul
if %ERRORLEVEL% neq 0 (
    echo Error: PowerShell not found. Please make sure PowerShell 5.1 or higher is installed.
    echo Press any key to exit...
    pause >nul
    exit /b 1
)

powershell -ExecutionPolicy Bypass -File "%~dp0\startup.ps1" %*

set EXIT_CODE=%ERRORLEVEL%

if %EXIT_CODE% neq 0 (
    echo.
    echo Script execution failed with exit code: %EXIT_CODE%
    echo Press any key to exit...
    pause >nul
    exit /b %EXIT_CODE%
)

echo.
echo Script execution completed.
echo Press any key to exit...
pause >nul
exit /b 0
