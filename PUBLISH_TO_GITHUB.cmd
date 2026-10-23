@echo off
setlocal
cd /d "%~dp0"
echo =======================================================
echo DareCare - Publish PRIVATE repository on your GitHub
 echo =======================================================
 echo Target: abuhaibra294-hub/DareCare.Backend
 echo Source: this complete folder (not unrelated 'project')
 echo =======================================================
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0scripts\Publish-GitHub.ps1"
set "EXIT_CODE=%ERRORLEVEL%"
if not "%EXIT_CODE%"=="0" echo Publish failed. Read the error above. GitHub was not overwritten.
pause
exit /b %EXIT_CODE%
