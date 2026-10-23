@echo off
setlocal
cd /d "%~dp0"
title DareCare - One Click Start
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-DareCare.ps1"
if errorlevel 1 (
    echo.
    echo Setup or startup failed. Check the message above.
    echo Open START_HERE_AR.md for troubleshooting.
    pause
)
endlocal
