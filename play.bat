@echo off
rem Launch Brine Time. Finds Godot 4.4 via (1) the GODOT environment variable,
rem (2) "godot" on PATH, or (3) a portable copy in ..\tools\godot next to this folder.
setlocal
set "PROJ=%~dp0."

if defined GODOT goto run
where godot >nul 2>nul
if %errorlevel%==0 (
    set "GODOT=godot"
    goto run
)
if exist "%~dp0..\tools\godot\Godot_v4.4.1-stable_win64.exe" (
    set "GODOT=%~dp0..\tools\godot\Godot_v4.4.1-stable_win64.exe"
    goto run
)

echo.
echo  Could not find Godot 4.4.
echo  1. Download "Godot Engine 4.4.1 - Standard" for Windows from https://godotengine.org/download/archive/4.4.1-stable/
echo  2. Then EITHER set an environment variable GODOT to the full path of Godot_v4.4.1-stable_win64.exe
echo     OR open Godot, click Import, and pick this folder's project.godot.
echo.
pause
exit /b 1

:run
start "" "%GODOT%" --path "%PROJ%"
