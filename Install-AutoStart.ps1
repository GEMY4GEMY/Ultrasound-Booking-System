$ErrorActionPreference='Stop'
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
$watchdog=Join-Path $root 'Server-Watchdog.ps1'
if(!(Test-Path $watchdog)){ throw "Server-Watchdog.ps1 not found." }
$taskName='Ultrasound Booking System Server'
$arg='-NoProfile -ExecutionPolicy Bypass -WindowStyle Hidden -File "'+$watchdog+'"'
$action=New-ScheduledTaskAction -Execute 'powershell.exe' -Argument $arg
$trigger=New-ScheduledTaskTrigger -AtStartup
$settings=New-ScheduledTaskSettingsSet -StartWhenAvailable -RestartCount 999 -RestartInterval (New-TimeSpan -Minutes 1) -ExecutionTimeLimit (New-TimeSpan -Days 3650)
$principal=New-ScheduledTaskPrincipal -UserId 'SYSTEM' -LogonType ServiceAccount -RunLevel Highest
Register-ScheduledTask -TaskName $taskName -Action $action -Trigger $trigger -Settings $settings -Principal $principal -Force | Out-Null
Start-ScheduledTask -TaskName $taskName
Write-Host 'Auto Start task installed and server watchdog started.'