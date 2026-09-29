@echo off
setlocal
cd /d "%~dp0"
title Ultrasound Booking System - Auto Start Setup
net session >nul 2>&1
if not "%errorlevel%"=="0" (
  powershell -NoProfile -Command "Start-Process -FilePath '%~f0' -Verb RunAs"
  exit /b
)
set "TASK=Ultrasound Booking System Server"
set "LAUNCHER=%~dp0Start-Ultrasound-Server.cmd"
schtasks /Delete /TN "%TASK%" /F >nul 2>&1
schtasks /Create /TN "%TASK%" /SC ONSTART /RU SYSTEM /RL HIGHEST /TR ""%LAUNCHER%" /background" /F
if errorlevel 1 (
  echo Failed to install Auto Start task.
  pause
  exit /b 1
)
echo.
echo Auto Start installed successfully.
echo The server will start automatically whenever Windows starts.
echo Starting server now...
schtasks /Run /TN "%TASK%" >nul
timeout /t 3 /nobreak >nul
echo Done.
pause
