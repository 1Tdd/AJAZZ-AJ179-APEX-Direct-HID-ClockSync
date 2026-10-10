param(
    [int]$StartupDelaySeconds = 0,
    [switch]$VerboseOutput
)

$ErrorActionPreference = "Stop"
$BaseDir = Split-Path -Parent $MyInvocation.MyCommand.Path
$CsPath = Join-Path $BaseDir "AjazzHidClock.cs"
$LogPath = Join-Path $BaseDir "timesync.log"

# User's AJ179 APEX family target.
$Vid = [UInt16]0x3151
$DevicePid = [UInt16]0x5007

function Log([string]$s) {
    $line = "{0}  {1}" -f (Get-Date -Format "yyyy-MM-dd HH:mm:ss.fff"), $s
    Add-Content -Path $LogPath -Value $line -Encoding UTF8
    if ($VerboseOutput) { Write-Host $line }
}

try {
    if ($StartupDelaySeconds -gt 0) {
        Start-Sleep -Seconds $StartupDelaySeconds
    }

    if (-not ("AjazzHidClock" -as [type])) {
        Add-Type -Path $CsPath -ErrorAction Stop
    }

    for ($i = 1; $i -le 12; $i++) {
        $now = [DateTime]::Now
        $r = [AjazzHidClock]::SyncClock($Vid, $DevicePid, $now)

        if ($r.StartsWith("OK|")) {
            Log ("SUCCESS attempt {0}/12: {1}; local={2}" -f `
                $i, $r.Substring(3), $now.ToString("yyyy-MM-dd HH:mm:ss"))
            exit 0
        }

        Log ("Attempt {0}/12: {1}" -f $i, $r)
        if ($i -lt 12) { Start-Sleep -Milliseconds 1500 }
    }

    exit 2
}
catch {
    Log ("ERROR: " + $_.Exception.ToString())
    exit 3
}
