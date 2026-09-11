@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "LOG=%~dp0BUILD_LOG.txt"
set "DOTNET_CLI_UI_LANGUAGE=en-US"
set "BUILD_EXIT=1"
>"%LOG%" echo Chaoxing Learning Assistant - Build Log v1.32
>>"%LOG%" echo Date: %DATE% %TIME%
echo Chaoxing Learning Assistant - Build v1.32
echo Quick build is the default. Full portable packaging: BUILD_FULL.bat
echo Progress details are written to BUILD_LOG.txt. First restore needs Internet.
echo Build log: %LOG%
if not exist "ChaoxingLearningAssistant.sln" goto :FAIL
where dotnet.exe >nul 2>&1
if errorlevel 1 goto :FAIL
powershell.exe -NoProfile -ExecutionPolicy Bypass -File ".\scripts\build.ps1" %* >>"%LOG%" 2>&1
set "BUILD_EXIT=%ERRORLEVEL%"
if not "%BUILD_EXIT%"=="0" goto :FAIL
echo [SUCCESS] Build and tests completed.
echo Open: artifacts\quick-run\ChaoxingLearningAssistant.exe
start "" explorer.exe "%~dp0artifacts\quick-run"
goto :END
:FAIL
echo [ERROR] See BUILD_LOG.txt for details.
start "" notepad.exe "%LOG%"
:END
echo Press any key to close this window.
pause >nul
exit /b %BUILD_EXIT%
