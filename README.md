# AJAZZ AJ179 APEX — Direct HID Clock Sync

Syncs the clock on the AJAZZ AJ179 APEX dock's color screen directly over USB HID, without the official AJAZZ Driver.

The official software (`iot_driver.exe`) runs background services, opens a local gRPC port (`3814`), and connects to remote servers in China (`yiketianqi.com` for weather data, `rongyuan.tech` and `qmk.top` for web-driver configs/telemetry) to populate the dock's screen. If you only want the dock's clock synchronized with your PC time, this project replaces all of that with a single HID feature report sent locally at logon — no internet access, no background processes, and zero telemetry.

## How it works

A C# class ([AjazzHidClock.cs](AjazzHidClock.cs)) enumerates HID devices via SetupAPI, finds the dock's vendor-specific control collection (UsagePage `0xFFFF`, Usage `0x0002`), and writes a 65-byte feature report containing the current date/time in the format the dock expects.

PowerShell compiles the C# on the fly via `Add-Type`. A VBS wrapper launches it invisibly at logon through a scheduled task.

The installer also adds the AJAZZ telemetry domains to your hosts file (`0.0.0.0`), so even if you run the official driver later, it won't phone home.

## Target device

| Field | Value |
|-------|-------|
| VID | `0x3151` |
| PID | `0x5007` |
| UsagePage | `0xFFFF` |
| Usage | `0x0002` |
| Feature report | 65 bytes |

This was confirmed on the AJ179 APEX. Other AJAZZ docks with screen displays likely use the same protocol — if yours has a different PID, edit the `$DevicePid` variable in [SyncClock.ps1](SyncClock.ps1).

## Usage

> [!IMPORTANT]
> Don't copy-paste the `.cmd` file contents into PowerShell. The `%~dp0` syntax is CMD-only. Just double-click the `.cmd` files from Explorer.

### Quick test

Double-click **TEST.cmd** (requests admin). Watch the dock's clock — it should update within a few seconds.

### Install

Double-click **INSTALL.cmd** (requests admin). It will:

1. Copy the needed files to `%ProgramData%\AJAZZ-HID-ClockSync`
2. Kill any running AJAZZ/iot_driver processes
3. Run a test sync and ask you to confirm the clock is correct
4. Remove old scheduled tasks from previous versions
5. Block AJAZZ telemetry domains in the hosts file
6. Register a scheduled task that runs silently at logon

### Verify

Double-click **CHECK.cmd** to see task status, running AJAZZ processes, blocked domains, and recent sync logs.

### Uninstall

Double-click **REMOVE.cmd** (requests admin). Removes the scheduled task, cleans the hosts file, and deletes the installed files.

## Files

| File | Purpose |
|------|---------|
| `AjazzHidClock.cs` | C# — HID enumeration, device matching, feature report write |
| `SyncClock.ps1` | PowerShell — compiles the C#, retries sync up to 12 times |
| `RunHidden.vbs` | VBS — launches SyncClock.ps1 invisibly (no console flash) |
| `Install.ps1` | PowerShell — full install: files, hosts, scheduled task |
| `Remove.ps1` | PowerShell — full uninstall |
| `Check.ps1` | PowerShell — status check |
| `Test.ps1` | PowerShell — kills AJAZZ processes and runs a sync |
| `*.cmd` | Double-click launchers for the PowerShell scripts |

## Requirements

- Windows 10/11
- PowerShell 5.1 (ships with Windows)
- .NET Framework (ships with Windows — needed for `Add-Type`)
- Admin rights for install/uninstall (the sync itself runs as a normal user)

## License

[MIT](LICENSE)
