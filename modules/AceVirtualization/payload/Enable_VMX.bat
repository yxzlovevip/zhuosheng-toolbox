@echo off
setlocal EnableExtensions DisableDelayedExpansion
chcp 936 >nul
rem Original combined operation: disable Hypervisor and VBS. Does not enable BIOS VMX.
call "%~dp0Disable_hyp.bat"
set "HYPRC=%errorlevel%"
if "%HYPRC%"=="0" goto vbs
if "%HYPRC%"=="3010" goto vbs
exit /b %HYPRC%
:vbs
call "%~dp0Disable_vbs.bat"
exit /b %errorlevel%
