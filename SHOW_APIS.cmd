@echo off
setlocal
cd /d "%~dp0"
title DareCare - API Documentation
start "" explorer.exe "%~dp0integration"
start "" notepad.exe "%~dp0docs\TEAM_HANDOFF_AR.md"
start "" notepad.exe "%~dp0DareCare.http"
echo Import DareCare_Postman_Collection.json and DareCare_Local.postman_environment.json into Postman.
echo When START_DARECARE.cmd is RUNNING, the live OpenAPI endpoint is:
echo http://127.0.0.1:5208/openapi/v1.json
start "" "http://127.0.0.1:5208/openapi/v1.json"
pause
endlocal
