@echo off
setlocal
title Ultrasound Booking System - Network Setup

echo This tool allows TCP port 5090 on Windows Private networks.
echo Administrator permission is required.
echo.

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  echo Requesting Administrator permission...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

netsh advfirewall firewall delete rule name="Ultrasound Booking System - TCP 5090" >nul 2>&1
netsh advfirewall firewall add rule name="Ultrasound Booking System - TCP 5090" dir=in action=allow protocol=TCP localport=5090 profile=private

if "%errorlevel%"=="0" (
  echo.
  echo SUCCESS: Port 5090 is allowed on Private networks.
  echo The application can now be reached by other devices on the same LAN.
) else (
  echo.
  echo ERROR: Windows Firewall rule could not be created.
)

echo.
pause
