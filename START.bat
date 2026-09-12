@echo off
setlocal EnableExtensions
cd /d "%~dp0"
set "EXE=%~dp0artifacts\quick-run\ChaoxingLearningAssistant.exe"
set "READY=%~dp0artifacts\QUICK_RUN_READY.txt"
if not exist "%EXE%" goto :BUILD
if not exist "%READY%" goto :BUILD
findstr /x /c:"Version: 1.45.0" "%READY%" >nul
if errorlevel 1 goto :BUILD
goto :RUN
:BUILD
call "%~dp0BUILD_V1_45.bat"
if errorlevel 1 exit /b 1
if not exist "%EXE%" exit /b 1
:RUN
start "" "%EXE%"
exit /b 0
