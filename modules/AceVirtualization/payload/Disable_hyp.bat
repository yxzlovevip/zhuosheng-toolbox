@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 936 >nul
rem Same repair scope as original: disable Hypervisor launch. No permanent PS policy change.
set "HVType=Unknown"
for /f "tokens=2" %%b in ('bcdedit /enum ^| findstr /i "hypervisorlaunchtype"') do set "HVType=%%b"
if /i "%HVType%"=="Off" exit /b 0
bcdedit /set hypervisorlaunchtype off
if errorlevel 1 exit /b 5
exit /b 3010
