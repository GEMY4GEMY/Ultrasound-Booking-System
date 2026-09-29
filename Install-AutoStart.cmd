@echo off
setlocal
cd /d "%~dp0"
title Ultrasound Booking System - Auto Start Setup
net session >nul 2>&1
if not "%errorlevel%"=="0" (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
powershell -NoProfile -ExecutionPolicy Bypass -File "%~dp0Install-AutoStart.ps1"
if errorlevel 1 (
  echo.
  echo Failed to install Auto Start.
  pause
  exit /b 1
)
echo.
echo Auto Start installed successfully.
echo Server watchdog is running and will start automatically with Windows.
pause
