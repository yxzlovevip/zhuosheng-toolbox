$ErrorActionPreference='Stop'
$czCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$czTestDirectory=Join-Path $PSScriptRoot 'test-output'
New-Item -ItemType Directory -Path $czTestDirectory -Force | Out-Null
& $czCompiler /nologo /warnaserror+ /target:exe "/out:$czTestDirectory/CoreTests.exe" (Join-Path $PSScriptRoot 'Core.cs') (Join-Path $PSScriptRoot 'tests/CoreTests.cs')
if($LASTEXITCODE -ne 0){throw '核心测试编译失败'}
& (Join-Path $czTestDirectory 'CoreTests.exe')
if($LASTEXITCODE -ne 0){throw 'ACE 核心测试失败'}
