@echo off
setlocal
cd /d "%~dp0"
title DareCare - Build and Tests
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Start-DareCare.ps1" -TestOnly
if errorlevel 1 echo Some tests or setup failed.
pause
endlocal
