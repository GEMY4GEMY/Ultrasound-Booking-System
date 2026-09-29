param([int]$Port=5090)
$ErrorActionPreference='SilentlyContinue'
$created=$false
$mutex=New-Object System.Threading.Mutex($true,'Global\UltrasoundBookingSystemWatchdog',[ref]$created)
if(!$created){ exit 0 }
$root=Split-Path -Parent $MyInvocation.MyCommand.Path
$exe=Join-Path $root 'UltrasoundBookingSystem.exe'
$logDir=Join-Path $root 'logs'
$log=Join-Path $logDir 'watchdog.log'
New-Item -ItemType Directory -Force -Path $logDir | Out-Null
function Log($m){ Add-Content -Path $log -Value ("[{0}] {1}" -f (Get-Date -Format 'yyyy-MM-dd HH:mm:ss'),$m) }
function FindServer { Get-CimInstance Win32_Process | Where-Object { $_.ExecutablePath -eq $exe } }
function StartServer {
  if(!(Test-Path $exe)){ Log "EXE missing: $exe"; Start-Sleep 10; return }
  Start-Process -FilePath $exe -WorkingDirectory $root -WindowStyle Hidden | Out-Null
  Log 'Server process started.'
}
Log 'Watchdog started.'
while($true){
  $p=@(FindServer)
  if($p.Count -eq 0){ StartServer; Start-Sleep 4; continue }
  $healthy=$false
  try{
    $r=Invoke-WebRequest -UseBasicParsing -Uri ("http://127.0.0.1:{0}/api/health" -f $Port) -TimeoutSec 5
    $healthy=($r.StatusCode -eq 200)
  }catch{}
  if($healthy){ Start-Sleep 5; continue }
  Log 'Health check failed. Retrying before restart.'
  Start-Sleep 3
  try{
    $r=Invoke-WebRequest -UseBasicParsing -Uri ("http://127.0.0.1:{0}/api/health" -f $Port) -TimeoutSec 5
    $healthy=($r.StatusCode -eq 200)
  }catch{$healthy=$false}
  if(!$healthy){
    Log 'Server unresponsive. Restarting process.'
    @(FindServer) | ForEach-Object { Stop-Process -Id $_.ProcessId -Force }
    Start-Sleep 2
    StartServer
    Start-Sleep 5
  }
}