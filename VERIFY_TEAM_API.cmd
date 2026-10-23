@echo off
setlocal
cd /d "%~dp0"
title DareCare - Team API Health and Security Check
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Verify-TeamApi.ps1" %*
if errorlevel 1 ( echo [FAILED] API check failed. ) else ( echo [OK] Basic API checks passed. )
pause
endlocal
