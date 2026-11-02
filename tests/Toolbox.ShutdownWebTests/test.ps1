$ErrorActionPreference='Stop'
$czProject=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location -LiteralPath $czProject
$czOutput=Join-Path $czProject ('.build/shutdown-web-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $czOutput | Out-Null
$czFixture=Join-Path $czOutput 'ToolFixture.exe'
& (Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe') /nologo /target:winexe /platform:x64 "/out:$czFixture" (Join-Path $PSScriptRoot 'Fixture/Program.cs')
if($LASTEXITCODE -ne 0){throw 'Fixture build failed'}
& (Join-Path $czProject '.build/dotnet/dotnet.exe') run --project $PSScriptRoot -c ShutdownFinal -- $czFixture
if($LASTEXITCODE -ne 0){throw 'Native shutdown check failed'}
