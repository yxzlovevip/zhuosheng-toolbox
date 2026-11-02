$ErrorActionPreference = 'Stop'
$smbRoot = $PSScriptRoot
$smbCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$smbTestDir = Join-Path $smbRoot 'test-output'
New-Item -ItemType Directory -Path $smbTestDir -Force | Out-Null
& $smbCompiler /nologo /warnaserror+ /target:exe "/out:$smbTestDir/ShareTests.exe" (Join-Path $smbRoot 'SmbCore.cs') (Join-Path $smbRoot 'ShareAccess.cs') (Join-Path $smbRoot 'tests/ShareTests.cs')
if ($LASTEXITCODE -ne 0) { throw '共享测试程序编译失败' }
& (Join-Path $smbTestDir 'ShareTests.exe')
if ($LASTEXITCODE -ne 0) { throw '共享检查测试失败' }
