$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$scratch = Join-Path $projectRoot ('.build/win10-wrapper-test-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $scratch | Out-Null
$fixture = Join-Path $scratch 'fixture.bat'
$manifest = Join-Path $scratch 'fixture.manifest'
$exe = Join-Path $scratch 'fixture.exe'
$marker = Join-Path $scratch 'result.txt'
$fixtureText = @'
@echo off
if "%1"=="child" goto child
echo %1 %2>"%CZ_OPT_TEST_MARKER%"
echo %~dp0>>"%CZ_OPT_TEST_MARKER%"
start "" /b "%SystemRoot%\System32\cmd.exe" /d /v:off /s /c ""%~f0" child"
exit /b 23
:child
"%SystemRoot%\System32\WindowsPowerShell\v1.0\powershell.exe" -NoProfile -Command "Start-Sleep -Milliseconds 400"
if exist "%~f0" echo retained>"%CZ_OPT_TEST_MARKER%.child"
exit /b 0
'@
[IO.File]::WriteAllText($fixture, $fixtureText.Replace("`n","`r`n"), [Text.Encoding]::ASCII)
[IO.File]::WriteAllText($manifest, '<?xml version="1.0"?><assembly manifestVersion="1.0" xmlns="urn:schemas-microsoft-com:asm.v1"><assemblyIdentity version="1.0.0.0" name="Cz.Win10Optimization.Test"/><trustInfo xmlns="urn:schemas-microsoft-com:asm.v3"><security><requestedPrivileges><requestedExecutionLevel level="asInvoker" uiAccess="false"/></requestedPrivileges></security></trustInfo></assembly>')
& (Join-Path $PSScriptRoot 'build.ps1') -PayloadPath $fixture -OutputPath $exe -ManifestPath $manifest
$start = [Diagnostics.ProcessStartInfo]::new($exe)
$start.UseShellExecute = $false
$start.CreateNoWindow = $true
$start.Arguments = 'ignored & echo injected'
$start.EnvironmentVariables['CZ_OPT_TEST_MARKER'] = $marker
$process = [Diagnostics.Process]::Start($start)
if (-not $process.WaitForExit(15000)) { $process.Kill(); throw '替身 BAT 执行超时' }
if ($process.ExitCode -ne 23) { throw "替身退出码没有传回：$($process.ExitCode)" }
$lines = [IO.File]::ReadAllLines($marker)
if ($lines[0] -ne 'ELEV ::') { throw '必须只传固定的 ELEV :: 标记，不能转发任意调用参数' }
$deadline = [DateTime]::UtcNow.AddSeconds(8)
while (-not (Test-Path -LiteralPath ($marker+'.child')) -and [DateTime]::UtcNow -lt $deadline) { Start-Sleep -Milliseconds 100 }
if (-not (Test-Path -LiteralPath ($marker+'.child'))) { throw '重新启动的替身进程无法读取保留的 BAT' }
$extracted = Join-Path $lines[1] 'win10-optimization.bat'
if (-not (Test-Path -LiteralPath $extracted)) { throw '原脚本自提权后仍需读取的 BAT 被提前清理' }
Write-Output 'PASS harmless BAT uses fixed elevation markers, ignores caller arguments, returns exit code and remains available to a detached child'
# The harmless detached child has finished; remove only its known script and empty directory.
[IO.File]::Delete($extracted)
[IO.Directory]::Delete($lines[1],$false)
$production = Join-Path $projectRoot '工具/系统设置/Win10优化系统.exe'
$assembly = [Reflection.Assembly]::LoadFile($production)
$payload = $assembly.GetManifestResourceStream('Cz.Win10Optimization.Payload.bat')
if ($null -eq $payload) { throw '成品缺少内嵌 BAT' }
try { $actual = [Convert]::ToHexString([Security.Cryptography.SHA256]::HashData($payload)) } finally { $payload.Dispose() }
if ($actual -ne 'CBCC92C1B91FA3ACB0B5C220D94E9D0D29D8FF9A76F1BF2D730364C572F6FA77') { throw '成品内嵌 BAT 与审查版本不一致' }
Write-Output 'PASS production embedded BAT SHA-256; production EXE was NOT executed'
