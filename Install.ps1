$ErrorActionPreference = "Stop"

$TaskName = "AJAZZ Direct HID Clock Sync"
$BaseDir = Join-Path $env:ProgramData "AJAZZ-HID-ClockSync"
$HostsPath = Join-Path $env:SystemRoot "System32\drivers\etc\hosts"
$Files = @("AjazzHidClock.cs", "SyncClock.ps1", "RunHidden.vbs")
$OldTasks = @(
    "AJAZZ Secure Clock Sync",
    "AJAZZ Direct HID Time Sync",
    "AJAZZ Direct HID Time Sync V3",
    "AJAZZ Direct HID Time Sync V4",
    "AJAZZ Direct HID Time Sync V4.1"
)

function Test-Admin {
    $Identity = [Security.Principal.WindowsIdentity]::GetCurrent()
    $Principal = New-Object Security.Principal.WindowsPrincipal($Identity)
    return $Principal.IsInRole([Security.Principal.WindowsBuiltInRole]::Administrator)
}

function Clean-AjazzHosts([string]$Text) {
    $Patterns = @(
        '(?ms)\r?\n?# BEGIN AJAZZ-SECURE-BLOCK.*?# END AJAZZ-SECURE-BLOCK\r?\n?',
        '(?ms)\r?\n?# BEGIN AJAZZ-HID-TIMESYNC-BLOCK.*?# END AJAZZ-HID-TIMESYNC-BLOCK\r?\n?',
        '(?ms)\r?\n?# BEGIN AJAZZ-HID-V3-BLOCK.*?# END AJAZZ-HID-V3-BLOCK\r?\n?',
        '(?ms)\r?\n?# BEGIN AJAZZ-HID-V4-BLOCK.*?# END AJAZZ-HID-V4-BLOCK\r?\n?',
        '(?ms)\r?\n?# BEGIN AJAZZ-HID-CLOCKSYNC-BLOCK.*?# END AJAZZ-HID-CLOCKSYNC-BLOCK\r?\n?'
    )
    foreach ($Pattern in $Patterns) { $Text = [regex]::Replace($Text, $Pattern, "`r`n") }
    return $Text.TrimEnd()
}

if (-not (Test-Admin)) {
    Write-Host "Run INSTALL.cmd as administrator." -ForegroundColor Red
    Read-Host "Press Enter"
    exit 1
}

Write-Host ""
Write-Host "AJAZZ AJ179 APEX - DIRECT HID CLOCK SYNC" -ForegroundColor Cyan
Write-Host "Target: 3151:5007 / FFFF:0002 / Feature 65"
Write-Host ""

New-Item -ItemType Directory -Path $BaseDir -Force | Out-Null
foreach ($File in $Files) {
    $Source = Join-Path $PSScriptRoot $File
    if (-not (Test-Path $Source)) { throw "Missing file: $File" }
    Copy-Item $Source (Join-Path $BaseDir $File) -Force
}

Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object {
        ($_.Name -match 'AJAZZ|iot_driver') -or
        ($_.ExecutablePath -and $_.ExecutablePath -match 'AJAZZ|iot_driver')
    } |
    ForEach-Object { Stop-Process -Id ([int]$_.ProcessId) -Force -ErrorAction SilentlyContinue }

Start-Sleep -Milliseconds 800
Write-Host "DIRECT HID TEST: check the dock's clock." -ForegroundColor Yellow
Write-Host ""
& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass -File (Join-Path $BaseDir "SyncClock.ps1") -VerboseOutput

if ($LASTEXITCODE -ne 0) {
    Write-Host ""
    Write-Host "Test failed. Skipping task installation." -ForegroundColor Red
    Read-Host "Press Enter"
    exit 2
}

Write-Host ""
$Answer = Read-Host "Is the dock's clock correct? Type Y to confirm"
if ($Answer.Trim().ToUpperInvariant() -ne "Y") {
    Write-Host "Installation cancelled." -ForegroundColor Yellow
    Read-Host "Press Enter"
    exit 3
}

foreach ($OldTask in $OldTasks) {
    if (Get-ScheduledTask -TaskName $OldTask -ErrorAction SilentlyContinue) {
        Unregister-ScheduledTask -TaskName $OldTask -Confirm:$false
    }
}
if (Get-ScheduledTask -TaskName $TaskName -ErrorAction SilentlyContinue) {
    Unregister-ScheduledTask -TaskName $TaskName -Confirm:$false
}

$Hosts = Clean-AjazzHosts (Get-Content $HostsPath -Raw)
$Block = @(
    "", "",
    "# BEGIN AJAZZ-HID-CLOCKSYNC-BLOCK",
    "# Direct HID clock sync requires no network.",
    "0.0.0.0 w2.yiketianqi.com",
    "0.0.0.0 api.rongyuan.tech",
    "0.0.0.0 api2.qmk.top",
    "0.0.0.0 api3.qmk.top",
    "# END AJAZZ-HID-CLOCKSYNC-BLOCK", ""
) -join "`r`n"
Set-Content -Path $HostsPath -Value ($Hosts + $Block) -Encoding ASCII
ipconfig /flushdns | Out-Null

$CurrentUser = [Security.Principal.WindowsIdentity]::GetCurrent().Name
$Wscript = Join-Path $env:WINDIR "System32\wscript.exe"
$Vbs = Join-Path $BaseDir "RunHidden.vbs"
$Action = New-ScheduledTaskAction -Execute $Wscript -Argument ("//B //Nologo `"{0}`"" -f $Vbs)
$Trigger = New-ScheduledTaskTrigger -AtLogOn -User $CurrentUser
$Principal = New-ScheduledTaskPrincipal -UserId $CurrentUser -LogonType Interactive -RunLevel Limited
$Settings = New-ScheduledTaskSettingsSet -AllowStartIfOnBatteries -DontStopIfGoingOnBatteries -StartWhenAvailable -ExecutionTimeLimit (New-TimeSpan -Minutes 1) -Hidden

Register-ScheduledTask -TaskName $TaskName -Action $Action -Trigger $Trigger -Principal $Principal -Settings $Settings -Description "Invisible AJAZZ AJ179 APEX dock clock sync over HID." -Force | Out-Null

Write-Host ""
Write-Host "Installation complete." -ForegroundColor Green
Write-Host "At logon, AJAZZ Driver will not be launched and no console window should appear."
Write-Host "Log: $BaseDir\timesync.log"
Read-Host "Press Enter"
