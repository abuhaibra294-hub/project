@echo off
setlocal
cd /d "%~dp0"
title DareCare - End-to-End Smoke Test
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Smoke-Workflow.ps1"
if errorlevel 1 echo Workflow test failed. Look at the error above.
pause
endlocal
