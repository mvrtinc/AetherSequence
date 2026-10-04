@echo off
REM ============================================================
REM  AETHER SEQUENCE - install
REM  Copies the game to your profile, opens the firewall port
REM  for the WiFi duel, and creates a desktop shortcut.
REM  Right-click this file and choose "Run as administrator".
REM ============================================================

REM Файл сохранён в UTF-8, а консоль Windows по умолчанию читает OEM-кодировку.
REM Без этой строки весь русский текст ниже превратится в кракозябры, и
REM главная подсказка про "Домашнюю сеть" станет нечитаемой.
chcp 65001 >nul

setlocal

set "GAME=AetherSequence.exe"
set "TARGET=%LOCALAPPDATA%\AetherSequence"
set "RULE_TCP=AETHER SEQUENCE Duel TCP"
set "RULE_UDP=AETHER SEQUENCE Duel UDP"

net session >nul 2>&1
if not %errorlevel% equ 0 (
    echo.
    echo Requesting administrator rights...
    powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
    exit /b
)

echo.
echo === AETHER SEQUENCE - installation ===
echo.

if not exist "%~dp0%GAME%" (
    echo ERROR: %GAME% not found next to this script.
    pause
    exit /b 1
)

if not exist "%TARGET%" mkdir "%TARGET%" >nul 2>&1
copy /Y "%~dp0%GAME%" "%TARGET%\" >nul
if errorlevel 1 (
    echo ERROR: could not copy the game to %TARGET%
    pause
    exit /b 1
)
echo [ok] game installed to %TARGET%

REM Правила указываем для программы, а не только для порта: иначе Windows
REM не спросит разрешение при запуске и может заблокировать игру целиком.
REM profile=any - работает и на "Общественной" сети, где по умолчанию
REM всё входящее блокируется.
netsh advfirewall firewall delete rule name="%RULE_TCP%" >nul 2>&1
netsh advfirewall firewall delete rule name="%RULE_UDP%" >nul 2>&1
netsh advfirewall firewall add rule name="%RULE_TCP%" dir=in action=allow protocol=TCP localport=47801 profile=any enable=yes >nul 2>&1
netsh advfirewall firewall add rule name="%RULE_UDP%" dir=in action=allow protocol=UDP localport=47800 profile=any enable=yes >nul 2>&1

REM Правило на саму программу - надёжнее, чем на порт, и переживает
REM смену папки установки.
netsh advfirewall firewall delete rule name="AETHER SEQUENCE app" >nul 2>&1
netsh advfirewall firewall add rule name="AETHER SEQUENCE app" dir=in action=allow program="%TARGET%\AetherSequence.exe" enable=yes profile=any >nul 2>&1

echo [ok] firewall rules for the WiFi duel added

REM ВАЖНО: команда PowerShell должна быть в одну строку - символ переноса
REM строки внутри кавычек попадает в сам аргумент и ломает команду.
set "SHORTCUT_CMD=$w = New-Object -ComObject WScript.Shell; $lnk = $w.CreateShortcut([Environment]::GetFolderPath('Desktop') + '\AETHER SEQUENCE.lnk'); $lnk.TargetPath = '%TARGET%\AetherSequence.exe'; $lnk.WorkingDirectory = '%TARGET%'; $lnk.Save()"
powershell -NoProfile -Command "%SHORTCUT_CMD%" >nul 2>&1
if exist "%USERPROFILE%\Desktop\AETHER SEQUENCE.lnk" (
    echo [ok] desktop shortcut created
) else (
    powershell -NoProfile -Command "%SHORTCUT_CMD%"
    echo [!!] desktop shortcut was not created - try creating it manually
)

echo.
echo Done. Launching the game...
echo.
echo ВАЖНО: чтобы оба компьютера видели комнаты друг друга,
echo   сделайте эту сеть "Домашней":
echo   Параметры Windows -^> Сеть и Интернет -^> Свойства сети
echo   -^> Тип сети: "Домашняя сеть".
echo.
echo Если комнаты всё равно не находятся, подключайтесь вручную:
echo   в игре "ДУЭЛЬ" -^> "ПОДКЛЮЧИТЬСЯ ПО IP".
echo   Хост увидит свой адрес на экране ожидания.
echo.
start "" "%TARGET%\AetherSequence.exe"
endlocal
