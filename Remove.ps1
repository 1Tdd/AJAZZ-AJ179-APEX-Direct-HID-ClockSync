$ErrorActionPreference = "Stop"
$TaskName = "AJAZZ Direct HID Clock Sync"
$BaseDir = Join-Path $env:ProgramData "AJAZZ-HID-ClockSync"
$HostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"

$Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
$Principal = New-Object Security.Principal.WindowsPrincipal($Identity)
if (-not $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)) {
    Write-Host "Run REMOVE.cmd as administrator." -ForegroundColor Red
    Read-Host "Press Enter"
    exit 1
}

if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

$Hosts = Get-Content $HostsPath -Raw
$Pattern = '(?ms)\r?\n?# BEGIN AJAZZ-HID-CLOCKSYNC-BLOCK.*?# END AJAZZ-HID-CLOCKSYNC-BLOCK\r?\n?'
$Hosts = [regex]::Replace($Hosts, $Pattern, "`r`n")
Set-Content -Path $HostsPath -Value $Hosts.TrimEnd() -Encoding ASCII
ipconfig /flushdns | Out-Null

if (Test-Path $BaseDir) { Remove-Item $BaseDir -Recurse -Force }
Write-Host "AJAZZ Direct HID Clock Sync removed." -ForegroundColor Green
Read-Host "Press Enter"
