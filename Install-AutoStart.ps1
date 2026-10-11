$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
$watchdog=Join-Path $root 'Server-Watchdog.ps1'
if(!(Test-Path $watchdog)){ throw "Server-Watchdog.ps1 not found." }
$taskName='Ultrasound Booking System Server'
$taskRun='powershell.exe -NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$watchdog+'"'
& schtasks.exe /Create /TN $taskName /SC ONSTART /RU SYSTEM /RL HIGHEST /TR $taskRun /F | Out-Null
if($LASTEXITCODE -ne 0){ throw "Unable to create Windows startup task." }
& schtasks.exe /Run /TN $taskName | Out-Null
if($LASTEXITCODE -ne 0){ throw "Startup task was created but could not be started now." }
Write-Host 'Auto Start task installed and server watchdog started.'