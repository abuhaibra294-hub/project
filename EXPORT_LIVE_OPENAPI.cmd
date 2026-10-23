@echo off
setlocal
cd /d "%~dp0"
title DareCare - Export LIVE OpenAPI
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Export-LiveApi.ps1" %*
if errorlevel 1 ( echo [FAILED] Inspect errors above. ) else ( echo [OK] LIVE schema saved inside .localdata\ )
pause
endlocal
