@echo off
setlocal
pushd "%~dp0"
powershell.exe -NoProfile -ExecutionPolicy Bypass -Command "Start-Process powershell.exe -Verb RunAs -ArgumentList '-NoLogo -NoProfile -ExecutionPolicy Bypass -File ""%~dp0Install.ps1""'"
popd
