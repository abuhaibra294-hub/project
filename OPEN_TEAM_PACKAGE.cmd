@echo off
setlocal
cd /d "%~dp0"
start "" explorer.exe "%~dp0integration"
start "" notepad.exe "%~dp0docs\TEAM_HANDOFF_AR.md"
start "" notepad.exe "%~dp0docs\STAGING_DEPLOY_AR.md"
start "" notepad.exe "%~dp0docs\RENDER_DEPLOY_AR.md"
echo Handoff files opened. Import the Postman collection AND the local environment.
pause
endlocal
