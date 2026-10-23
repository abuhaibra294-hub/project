@echo off
setlocal
cd /d "%~dp0"
title DareCare - SQL Server Mode
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-DareCare.ps1" -SqlServer
if errorlevel 1 (
    echo.
    echo Check START_HERE_AR.md for SQL Server LocalDB requirements.
    pause
)
endlocal
