@echo off
setlocal
powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -STA -File "%~dp0Build-Local.ps1" %*
set "buildResult=%ERRORLEVEL%"
if not "%buildResult%"=="0" echo Build failed. Review the message above.
pause
exit /b %buildResult%
