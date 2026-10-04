@echo off
REM ============================================================
REM  AETHER SEQUENCE - uninstall
REM  Removes the game, the desktop shortcut and the firewall rules.
REM  Right-click and choose "Run as administrator".
REM ============================================================
setlocal

set "TARGET=%LOCALAPPDATA%\AetherSequence"
set "RULE_TCP=AETHER SEQUENCE Duel TCP"
set "RULE_UDP=AETHER SEQUENCE Duel UDP"
set "RULE_APP=AETHER SEQUENCE app"

net session >nul 2>&1
if not %errorlevel% equ 0 (
    echo.
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo === AETHER SEQUENCE - removal ===
echo.

taskkill /IM AetherSequence.exe /F >nul 2>&1

if exist "%TARGET%" (
    rd /S /Q "%TARGET%" >nul 2>&1
    if exist "%TARGET%" (
        echo [!!] could not delete %TARGET% - close the game and try again
    ) else (
        echo [ok] game files removed
    )
) else (
    echo [ok] game files not found - nothing to remove
)

set "SHORTCUT_DEL=Remove-Item ([Environment]::GetFolderPath('Desktop') + '\AETHER SEQUENCE.lnk') -Force -ErrorAction SilentlyContinue"
powershell -NoProfile -Command "%SHORTCUT_DEL%" >nul 2>&1
if exist "%USERPROFILE%\Desktop\AETHER SEQUENCE.lnk" (
    echo [!!] desktop shortcut was not removed - delete it manually
) else (
    echo [ok] desktop shortcut removed
)

netsh advfirewall firewall delete rule name="%RULE_TCP%" >nul 2>&1
netsh advfirewall firewall delete rule name="%RULE_UDP%" >nul 2>&1
netsh advfirewall firewall delete rule name="%RULE_APP%" >nul 2>&1
echo [ok] firewall rules removed

echo.
echo Done.
pause
endlocal
