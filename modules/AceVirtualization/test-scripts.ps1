# Exercise actual repair BAT files against harmless substitutes for every system command.
$ErrorActionPreference='Stop'
$czTest=Join-Path $PSScriptRoot ('test-output/script-'+[guid]::NewGuid().ToString('N'))
$czBin=Join-Path $czTest 'system/System32'; $czPs=Join-Path $czBin 'WindowsPowerShell/v1.0'; $czWorking=Join-Path $czTest 'scripts'
New-Item -ItemType Directory -Path $czPs,$czWorking,(Join-Path $czWorking 'DG_Readiness') -Force | Out-Null
$czCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$czStub=Join-Path $czBin 'bcdedit.exe'
& $czCompiler /nologo /warnaserror+ /target:exe "/out:$czStub" (Join-Path $PSScriptRoot 'tests/SystemCommandFixture.cs')
if($LASTEXITCODE -ne 0){throw '命令替身构建失败'}
Copy-Item -LiteralPath $czStub -Destination (Join-Path $czBin 'reg.exe')
Copy-Item -LiteralPath $czStub -Destination (Join-Path $czPs 'powershell.exe')
foreach($czName in @('Disable_hyp.bat','Disable_vbs.bat','Enable_VMX.bat')){Copy-Item -LiteralPath (Join-Path $PSScriptRoot ('payload/'+$czName)) -Destination $czWorking}
# An inert marker stands in for the Microsoft script; fake PowerShell never reads/executes it.
[IO.File]::WriteAllText((Join-Path $czWorking 'DG_Readiness/DG_Readiness_Tool_v3.6.ps1'),'not executed')
$czCmd=Join-Path $env:WINDIR 'System32/cmd.exe'; $czRealSystem=Join-Path $env:WINDIR 'System32'
$czAssertions=0
foreach($czCase in @(
    @{Script='Disable_hyp.bat';State='Auto';Fail='';Want=3010;Ops=@('bcdedit /set hypervisorlaunchtype off')},
    @{Script='Disable_hyp.bat';State='Off';Fail='';Want=0;Ops=@()},
    @{Script='Disable_vbs.bat';State='Auto';Fail='';Want=3010;Ops=@('powershell -NoProfile -ExecutionPolicy Bypass -File',' -Disable','reg add HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios /v EnableVirtualizationBasedSecurity /t REG_DWORD /d 0 /f','reg add HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\WindowsHello /v Enabled /t REG_DWORD /d 0 /f')},
    @{Script='Enable_VMX.bat';State='Auto';Fail='';Want=3010;Ops=@('bcdedit /set hypervisorlaunchtype off','powershell -NoProfile -ExecutionPolicy Bypass -File','reg add HKLM\SYSTEM\CurrentControlSet\Control\DeviceGuard\Scenarios\WindowsHello /v Enabled /t REG_DWORD /d 0 /f')},
    @{Script='Disable_hyp.bat';State='Auto';Fail='hyp';Want=5;Ops=@()},
    @{Script='Disable_vbs.bat';State='Auto';Fail='vbs';Want=5;Ops=@()}
)){
    $czLog=Join-Path $czTest ([guid]::NewGuid().ToString('N')+'.log')
    $czStart=[Diagnostics.ProcessStartInfo]::new($czCmd)
    $czStart.Arguments='/d /v:off /s /c ""'+(Join-Path $czWorking $czCase.Script)+'""'
    $czStart.WorkingDirectory=$czWorking;$czStart.UseShellExecute=$false;$czStart.CreateNoWindow=$true
    $czStart.RedirectStandardOutput=$true;$czStart.RedirectStandardError=$true
    $czStart.EnvironmentVariables['PATH']=$czBin+';'+$czRealSystem
    $czStart.EnvironmentVariables['SystemRoot']=Join-Path $czTest 'system'
    $czStart.EnvironmentVariables['ComSpec']=$czCmd
    $czStart.EnvironmentVariables['CZ_ACE_FIXTURE_LOG']=$czLog
    $czStart.EnvironmentVariables['CZ_ACE_FIXTURE_HYP']=$czCase.State
    $czStart.EnvironmentVariables['CZ_ACE_FIXTURE_FAIL']=$czCase.Fail
    $czProcess=[Diagnostics.Process]::Start($czStart)
    $czOutput=$czProcess.StandardOutput.ReadToEnd();$czError=$czProcess.StandardError.ReadToEnd();$czProcess.WaitForExit()
    if($czProcess.ExitCode -ne $czCase.Want){throw ('退出码错误 '+$czCase.Script+' '+$czProcess.ExitCode+' '+$czOutput+' '+$czError)};$czAssertions++
    $czCommands=[IO.File]::ReadAllText($czLog)
    foreach($czOperation in $czCase.Ops){if(-not $czCommands.Contains($czOperation)){throw ('原修复操作丢失：'+$czOperation+' / '+$czCommands)};$czAssertions++}
    if($czCommands.Contains('Set-ExecutionPolicy') -or $czCommands -match '(?i)shutdown|restart-computer'){throw '出现永久策略修改或自动重启'};$czAssertions++
}
Write-Output ('RESULT scripts '+$czAssertions+' passed, 0 failed; all system commands were harmless substitutes')
