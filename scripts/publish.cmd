@echo off
setlocal

set ARG1=%1
if /i "%ARG1%"=="all" (
    powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -All
    exit /b %ERRORLEVEL%
)

set RUNTIME=%1
if "%RUNTIME%"=="" set RUNTIME=win-x64
set CONFIG=%2
if "%CONFIG%"=="" set CONFIG=Release

powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0publish.ps1" -Runtime %RUNTIME% -Configuration %CONFIG%
exit /b %ERRORLEVEL%
