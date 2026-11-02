@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 936 >nul
rem Preserve the original full DG -Disable operation, including EFI/BCD handling.
if not exist "%~dp0DG_Readiness\DG_Readiness_Tool_v3.6.ps1" exit /b 2
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -ExecutionPolicy Bypass -File "%~dp0DG_Readiness\DG_Readiness_Tool_v3.6.ps1" -Disable
if errorlevel 1 exit /b 5
reg query "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios" /v "EnableVirtualizationBasedSecurity" >nul 2>&1
if errorlevel 1 goto hello
reg add "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios" /v "EnableVirtualizationBasedSecurity" /t REG_DWORD /d 0 /f
if errorlevel 1 exit /b 5
:hello
reg query "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\WindowsHello" /v "Enabled" >nul 2>&1
if errorlevel 1 exit /b 3010
reg add "HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\WindowsHello" /v "Enabled" /t REG_DWORD /d 0 /f
if errorlevel 1 exit /b 5
exit /b 3010
