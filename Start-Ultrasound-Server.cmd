@echo off
setlocal
cd /d "%~dp0"
title Ultrasound Booking System - Server

echo ==================================================
echo       ULTRASOUND BOOKING SYSTEM - SERVER
echo ==================================================
echo.

set "EXE="
for %%F in (*.exe) do (
  if /I not "%%~nxF"=="Start-Ultrasound-Server.exe" if not defined EXE set "EXE=%%~fF"
)

if not defined EXE (
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
echo.
start "" "http://localhost:5090"
"%EXE%"

echo.
echo Server stopped.
pause
