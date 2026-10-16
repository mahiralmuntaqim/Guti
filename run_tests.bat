@echo off
rem Runs every automated smoke test headlessly. Needs the *console* Godot executable
rem (Godot_v4.4.1-stable_win64_console.exe, shipped in the same zip as the normal .exe).
rem Set GODOT_CONSOLE to its path, or put the portable Godot in ..\tools\godot.
setlocal
cd /d "%~dp0"

if not defined GODOT_CONSOLE (
    if exist "%~dp0..\tools\godot\Godot_v4.4.1-stable_win64_console.exe" (
        set "GODOT_CONSOLE=%~dp0..\tools\godot\Godot_v4.4.1-stable_win64_console.exe"
    ) else (
        echo Set GODOT_CONSOLE to the path of Godot_v4.4.1-stable_win64_console.exe
        exit /b 1
    )
)

set FAILED=0
set "LOG=%TEMP%\brine_test.log"
"%GODOT_CONSOLE%" --headless --path . --import >nul 2>&1

for %%T in (--smoke --smoke-islands) do (
    echo === %%T
    "%GODOT_CONSOLE%" --headless --path . --fixed-fps 60 -- %%T > "%LOG%" 2>&1
    findstr /l /b /c:"[smoke]" "%LOG%"
    findstr /l /c:"[smoke] PASS" "%LOG%" >nul || set FAILED=1
)

echo === multiplayer (host + client on 127.0.0.1)
if exist "%TEMP%\brine_host.log" del "%TEMP%\brine_host.log"
start "brine-host" /b cmd /c ""%GODOT_CONSOLE%" --headless --path . -- --smoke-host > "%TEMP%\brine_host.log" 2>&1"
rem (ping is used as a sleep because "timeout" refuses to run without an interactive console)
ping -n 4 127.0.0.1 >nul
"%GODOT_CONSOLE%" --headless --path . -- --smoke-join=127.0.0.1 > "%TEMP%\brine_client.log" 2>&1
set /a WAITED=0
:waithost
findstr /l /c:"[smoke] PASS" /c:"[smoke] FAIL" "%TEMP%\brine_host.log" >nul 2>&1 && goto hostdone
ping -n 2 127.0.0.1 >nul
set /a WAITED+=1
if %WAITED% lss 30 goto waithost
:hostdone
findstr /l /b /c:"[smoke]" "%TEMP%\brine_host.log" "%TEMP%\brine_client.log"
findstr /l /c:"[smoke] PASS" "%TEMP%\brine_host.log" >nul || set FAILED=1
findstr /l /c:"[smoke] PASS" "%TEMP%\brine_client.log" >nul || set FAILED=1

echo.
if "%FAILED%"=="0" (echo ALL TESTS PASSED) else (echo SOME TESTS FAILED & exit /b 1)
