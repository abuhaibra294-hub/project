@echo off
setlocal
cd /d "%~dp0"
title DareCare - Prepare Synthetic Team Accounts
if not exist ".localdata\admin-login.txt" (
    echo Start START_DARECARE.cmd first, and leave the API running.
    pause
    exit /b 1
)
powershell.exe -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Smoke-Workflow.ps1" -SessionDemo
if errorlevel 1 (
    echo [FAILED] Test users not ready. Check API logs in .localdata.
) else (
    echo [OK] LOCAL DEMO identities saved in .localdata\demo-accounts.txt
    echo Do NOT share passwords or patient data in source control or chat groups.
    start "" notepad.exe "%~dp0docs\TEAM_HANDOFF_AR.md"
)
pause
endlocal
