@echo off
REM ============================================================
REM  AETHER SEQUENCE - play
REM  Starts the installed game. If it is not installed yet,
REM  runs the copy that sits next to this script.
REM ============================================================
setlocal

set "INSTALLED=%LOCALAPPDATA%\AetherSequence\AetherSequence.exe"
set "PORTABLE=%~dp0AetherSequence.exe"

if exist "%INSTALLED%" (
    start "" "%INSTALLED%"
) else if exist "%PORTABLE%" (
    echo Game is not installed yet - running the portable copy.
    echo For the WiFi duel run Install.bat once as administrator.
    echo.
    start "" "%PORTABLE%"
) else (
    echo ERROR: AetherSequence.exe not found.
    pause
    exit /b 1
)

endlocal
