@echo off
setlocal DisableDelayedExpansion
if "%~1"=="" goto all
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev-common.ps1" logs "%~1"
exit /b %ERRORLEVEL%
:all
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoLogo -NoProfile -ExecutionPolicy Bypass -File "%~dp0dev-common.ps1" logs
exit /b %ERRORLEVEL%
