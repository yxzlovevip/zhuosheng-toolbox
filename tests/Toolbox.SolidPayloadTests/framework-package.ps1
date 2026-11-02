param([string]$Installer='dist/卓盛工具箱-安装-v1.4.4.exe',[string]$Previous='dist/卓盛工具箱-安装-v1.4.3.exe')
$ErrorActionPreference='Stop'
$czProject=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location -LiteralPath $czProject
$czProbe=Join-Path $czProject '.build/size-optimization/FrameworkPackageProbe.exe'
$czFrameworkCsc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$czProbeSource=Join-Path $PSScriptRoot 'FrameworkPackageProbe.cs'
& $czFrameworkCsc /nologo /target:exe /platform:x64 "/out:$czProbe" /r:System.dll /r:System.Core.dll /r:System.Runtime.Serialization.dll $czProbeSource
if($LASTEXITCODE -ne 0){throw 'Framework probe compilation failed'}
& $czProbe $Installer $Previous
if($LASTEXITCODE -ne 0){throw 'Actual Framework upgrade verification failed'}
