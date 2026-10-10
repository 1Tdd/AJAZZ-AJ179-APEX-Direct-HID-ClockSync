Option Explicit
Dim shell, fso, baseDir, ps1, cmd
Set shell = CreateObject("WScript.Shell")
Set fso = CreateObject("Scripting.FileSystemObject")
baseDir = fso.GetParentFolderName(WScript.ScriptFullName)
ps1 = fso.BuildPath(baseDir, "SyncClock.ps1")
cmd = "powershell.exe -NoLogo -NoProfile -NonInteractive -WindowStyle Hidden -ExecutionPolicy Bypass -File """ & ps1 & """ -StartupDelaySeconds 3"
shell.Run cmd, 0, True
