$ErrorActionPreference = "Stop"

Get-CimInstance Win32_Process -ErrorAction SilentlyContinue |
    Where-Object {
        ($_.Name -match 'AJAZZ|iot_driver') -or
        ($_.ExecutablePath -and $_.ExecutablePath -match 'AJAZZ|iot_driver')
    } |
    ForEach-Object {
        Stop-Process -Id ([int]$_.ProcessId) -Force -ErrorAction SilentlyContinue
    }

Start-Sleep -Milliseconds 800

& powershell.exe -NoLogo -NoProfile -ExecutionPolicy Bypass `
    -File (Join-Path $PSScriptRoot "SyncClock.ps1") `
    -VerboseOutput

exit $LASTEXITCODE
