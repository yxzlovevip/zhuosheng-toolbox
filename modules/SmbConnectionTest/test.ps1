$ErrorActionPreference = 'Stop'
$smbRoot = $PSScriptRoot
$smbCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$smbTestDir = Join-Path $smbRoot 'test-output'
New-Item -ItemType Directory -Path $smbTestDir -Force | Out-Null
& $smbCompiler /nologo /warnaserror+ /target:exe "/out:$smbTestDir/CoreTests.exe" (Join-Path $smbRoot 'SmbCore.cs') (Join-Path $smbRoot 'Diagnostics.cs') (Join-Path $smbRoot 'ShareAccess.cs') (Join-Path $smbRoot 'tests/CoreTests.cs')
if ($LASTEXITCODE -ne 0) { throw '测试程序编译失败' }
& (Join-Path $smbTestDir 'CoreTests.exe')
if ($LASTEXITCODE -ne 0) { throw 'SMB 模块测试失败' }
