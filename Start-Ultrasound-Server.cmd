@echo off
setlocal
cd /d "%~dp0"
title Ultrasound Booking System - Server

echo ==================================================
echo       ULTRASOUND BOOKING SYSTEM - SERVER
echo ==================================================
echo.

set "EXE=%~dp0UltrasoundBookingSystem.exe"

if not exist "%EXE%" (
  echo ERROR: Application EXE was not found in this folder.
  echo Keep this launcher in the same folder as the published application.
  pause
  exit /b 1
)

for /f "tokens=2 delims=:" %%A in ('ipconfig ^| findstr /R /C:"IPv4 Address" /C:"IPv4-adresse"') do if not defined LANIP set "LANIP=%%A"
set "LANIP=%LANIP: =%"
if not defined LANIP set "LANIP=SERVER-IP"

echo Server address for this computer:
echo   http://localhost:5090
echo.
echo Address for other computers on the same network:
echo   http://%LANIP%:5090
echo.
echo Keep this window open while other computers use the system.
echo Daily staff use their operator name only. Admin settings use a separate PIN.
echo.
start "" powershell -NoProfile -WindowStyle Hidden -Command "Start-Sleep -Seconds 2; Start-Process 'http://localhost:5090'"
:RUN_SERVER
echo [%date% %time%] Starting server...
"%EXE%"
set "EXITCODE=%ERRORLEVEL%"
echo.
echo [%date% %time%] Server stopped unexpectedly. Exit code: %EXITCODE%
echo Restarting automatically in 3 seconds...
timeout /t 3 /nobreak >nul
goto RUN_SERVER
