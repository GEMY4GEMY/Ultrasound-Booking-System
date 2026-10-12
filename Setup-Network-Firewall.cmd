@echo off
setlocal
title Ultrasound Booking System - Network Setup

echo This tool allows TCP port 5050 on Windows Private networks.
echo Administrator permission is required.
echo.

net session >nul 2>&1
if not "%errorlevel%"=="0" (
  echo Requesting Administrator permission...
  powershell -NoProfile -ExecutionPolicy Bypass -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)

netsh advfirewall firewall delete rule name="Ultrasound Booking System - TCP 5050" >nul 2>&1
netsh advfirewall firewall add rule name="Ultrasound Booking System - TCP 5050" dir=in action=allow protocol=TCP localport=5050 profile=private enable=yes

if "%errorlevel%"=="0" (
  echo.
  echo SUCCESS: Port 5050 is allowed on Private networks.
  echo The application can now be reached by other devices on the same LAN.
  echo.
  echo Verifying firewall rule...
  netsh advfirewall firewall show rule name="Ultrasound Booking System - TCP 5050"
) else (
  echo.
  echo ERROR: Windows Firewall rule could not be created.
)

echo.
pause
