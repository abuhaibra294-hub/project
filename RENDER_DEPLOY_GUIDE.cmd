@echo off
setlocal
cd /d "%~dp0"
echo Opening Render deployment guide and the runtime API handoff checklist...
start "" notepad.exe "%~dp0docs\RENDER_DEPLOY_AR.md"
start "" notepad.exe "%~dp0docs\TEAM_HANDOFF_AR.md"
start "" "https://dashboard.render.com/"
echo REMINDER: No service has been deployed automatically. Render account, SQL Server and SMTP must be configured.
pause
endlocal
