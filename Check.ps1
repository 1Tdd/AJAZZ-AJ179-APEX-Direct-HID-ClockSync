$ErrorActionPreference = "Continue"
$TaskName = "AJAZZ Direct HID Clock Sync"
$BaseDir = Join-Path $env:ProgramData "AJAZZ-HID-ClockSync"
$HostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"

Write-Host "=== AJAZZ DIRECT HID CHECK ===" -ForegroundColor Cyan
Write-Host ""

$Task = Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue
if ($Task) { Write-Host "TASK: OK - $($Task.State)" -ForegroundColor Green }
else { Write-Host "TASK: NOT FOUND" -ForegroundColor Yellow }

$Processes = Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object { ($_.Name -match 'AJAZZ|iot_driver') -or ($_.ExecutablePath -and $_.ExecutablePath -match 'AJAZZ|iot_driver') }
if ($Processes) {
    Write-Host "RUNNING AJAZZ/IOT PROCESSES:" -ForegroundColor Yellow
    $Processes | Select-Object Name,ProcessId,ExecutablePath | Format-Table -AutoSize
} else { Write-Host "AJAZZ/IOT PROCESSES: none" -ForegroundColor Green }

$Listener = Get-NetTCPConnection -LocalPort 3814 -State Listen -ErrorAction SilentlyContinue
if ($Listener) { Write-Host "PORT 3814: LISTENING" -ForegroundColor Yellow }
else { Write-Host "PORT 3814: closed" -ForegroundColor Green }

Write-Host ""
$Hosts = Get-Content $HostsPath -Raw
foreach ($Domain in @("w2.yiketianqi.com","api.rongyuan.tech","api2.qmk.top","api3.qmk.top")) {
    if ($Hosts -match [regex]::Escape("0.0.0.0 $Domain")) { Write-Host "BLOCKED $Domain" -ForegroundColor Green }
    else { Write-Host "NOT BLOCKED $Domain" -ForegroundColor Yellow }
}

Write-Host ""
Write-Host "RECENT LOGS:"
$Log = Join-Path $BaseDir "timesync.log"
if (Test-Path $Log) { Get-Content $Log -Tail 12 }
else { Write-Host "No log found." }

Read-Host "Press Enter"
