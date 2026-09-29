@echo off
setlocal
cd /d "%~dp0"
title Ultrasound Booking System - Server
set "EXE=%~dp0UltrasoundBookingSystem.exe"
set "WATCHDOG=%~dp0Server-Watchdog.ps1"
if not exist "%EXE%" (
  echo ERROR: Application EXE was not found in this folder.
  if /I not "%~1"=="/background" pause
  exit /b 1
)
if not exist "%WATCHDOG%" (
  echo ERROR: Server-Watchdog.ps1 was not found in this folder.
  if /I not "%~1"=="/background" pause
  exit /b 1
)
powershell -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "%WATCHDOG%"
if /I "%~1"=="/background" exit /b 0
for /f "tokens=2 delims=:" %%A in ('ipconfig ^| findstr /R /C:"IPv4 Address" /C:"IPv4-adresse"') do if not defined LANIP set "LANIP=%%A"
set "LANIP=%LANIP: =%"
if not defined LANIP set "LANIP=SERVER-IP"
echo ==================================================
echo       ULTRASOUND BOOKING SYSTEM - SERVER
echo ==================================================
echo.
echo Server watchdog is active with automatic restart.
echo Local: http://localhost:5090
echo LAN:   http://%LANIP%:5090
echo.
echo You may close this window. The watchdog keeps running.
echo To enable startup with Windows, run Install-AutoStart.cmd once as Administrator.
echo.
start "" powershell -NoProfile -WindowStyle Hidden -Command "Start-Sleep -Seconds 2; Start-Process 'http://localhost:5090'"
timeout /t 4 /nobreak >nul
exit /b 0
