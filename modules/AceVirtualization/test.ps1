$ErrorActionPreference='Stop'
$czCheckScript=Join-Path $PSScriptRoot 'payload/Check.ps1'
$czParseCommand='$ProgressPreference=''SilentlyContinue'';$tokens=$null;$errors=$null;[System.Management.Automation.Language.Parser]::ParseFile('''+$czCheckScript.Replace("'","''")+''',[ref]$tokens,[ref]$errors)|Out-Null;if($errors.Count){Write-Output (''FAIL Windows PowerShell syntax errors: ''+$errors.Count);exit 1};Write-Output ''PASS Windows PowerShell script syntax'''
& "$env:WINDIR/System32/WindowsPowerShell/v1.0/powershell.exe" -NoProfile -EncodedCommand ([Convert]::ToBase64String([Text.Encoding]::Unicode.GetBytes($czParseCommand)))
if($LASTEXITCODE -ne 0){throw 'Windows PowerShell 无法解析检测脚本'}
& (Join-Path $PSScriptRoot 'test-core.ps1')
& (Join-Path $PSScriptRoot 'test-scripts.ps1')
$czTestRoot=Join-Path $PSScriptRoot ('test-output/session-'+[guid]::NewGuid().ToString('N'))
$czFixture=Join-Path $czTestRoot 'fixture'
New-Item -ItemType Directory -Path $czFixture -Force | Out-Null
[IO.File]::WriteAllText((Join-Path $czFixture 'Check.ps1'),"[Console]::OutputEncoding=[Text.UTF8Encoding]::new(`$false); 'CPU=On'; 'DMA=On'; 'HYPERVISOR=Auto'; 'VBS=Running'",[Text.UTF8Encoding]::new($false))
foreach($czCase in @(@('Disable_hyp.bat','Hypervisor',3010),@('Disable_vbs.bat','Vbs',37),@('Enable_VMX.bat','Combined',0))){
    [IO.File]::WriteAllText((Join-Path $czFixture $czCase[0]),("@echo off`r`necho FIXTURE="+$czCase[1]+"`r`necho SCRIPT=%~f0`r`necho STDERR-FIXTURE 1>&2`r`nexit /b "+$czCase[2]+"`r`n"),[Text.Encoding]::ASCII)
}
$czManifest=Join-Path $czTestRoot 'asInvoker.manifest'
[IO.File]::WriteAllText($czManifest,([IO.File]::ReadAllText((Join-Path $PSScriptRoot 'app.manifest')).Replace('requireAdministrator','asInvoker')))
$czRunner=Join-Path $czTestRoot 'RunnerTests.exe'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath $czRunner -PayloadDirectory $czFixture -EntrySource (Join-Path $PSScriptRoot 'tests/RunnerTests.cs') -ManifestPath $czManifest -Console
& $czRunner
if($LASTEXITCODE -ne 0){throw '封装执行测试失败'}
$czUi=Join-Path $czTestRoot 'UiTests.exe'
& (Join-Path $PSScriptRoot 'build.ps1') -OutputPath $czUi -PayloadDirectory $czFixture -EntrySource (Join-Path $PSScriptRoot 'tests/UiTests.cs') -ManifestPath $czManifest -Console
& $czUi $czTestRoot
if($LASTEXITCODE -ne 0){throw '封装界面测试失败'}
Write-Output ('验证截图：'+(Join-Path $czTestRoot 'ace-window.png'))
