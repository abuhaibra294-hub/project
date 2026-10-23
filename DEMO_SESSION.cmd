@echo off
setlocal
cd /d "%~dp0"
title DareCare - Team Session Demo (Synthetic Data)
echo.
echo DareCare Session Demo: ensure START_DARECARE.cmd is running FIRST.
echo Only generated fake users and synthetic prescriptions are used.
echo.
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Smoke-Workflow.ps1" -SessionDemo
if errorlevel 1 (
    echo.
    echo [ERROR] Demo failed. See message above. The API must be running first.
    pause
    exit /b 1
)
start "" "http://127.0.0.1:5208/dev/auth"
start "" notepad.exe "%~dp0docs\SESSION_TODAY_AR.md"
start "" notepad.exe "%~dp0docs\SPRINT5_API_CONTRACTS.md"
echo.
echo [OK] Browser and Arabic session notes opened.
echo Demo accounts: .localdata\demo-accounts.txt - KEEP PRIVATE.
echo Administrator account: .localdata\admin-login.txt - KEEP PRIVATE.
pause
endlocal
