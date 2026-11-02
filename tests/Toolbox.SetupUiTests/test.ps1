param([string]$Installer='')
$ErrorActionPreference='Stop'
$czProject=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
Set-Location -LiteralPath $czProject
$czOut=Join-Path $czProject ('.build/setup-ui-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $czOut | Out-Null
$czInfo=Join-Path $czOut 'PayloadInfo.cs'
'namespace Toolbox.Setup { internal static class PayloadInfo { public const string Version="1.1.0";public const string Sha256="",ZipSha256="",DecoderSha256=""; public const long ZipSize=0; } }'|Set-Content -LiteralPath $czInfo -Encoding utf8
$czCsc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$czSources=@('src/Toolbox.Setup/Program.cs','src/Toolbox.Setup/InstallEngine.cs','src/Toolbox.Setup/SolidPayload.cs','src/Toolbox.Setup/OwnedFiles.cs','src/Shared/InstallPathPolicy.cs','tests/Toolbox.SetupUiTests/Program.cs')|ForEach-Object{(Resolve-Path -LiteralPath $_).Path}
$czExe=Join-Path $czOut 'SetupUiTests.exe'
& $czCsc /nologo /target:exe /platform:x64 /main:ToolboxSetupUiTests "/out:$czExe" /r:System.dll /r:System.Core.dll /r:System.Runtime.Serialization.dll /r:Microsoft.CSharp.dll /r:System.Drawing.dll /r:System.Windows.Forms.dll /r:System.IO.Compression.dll /r:System.IO.Compression.FileSystem.dll @czSources $czInfo
if($LASTEXITCODE -ne 0){throw 'Installer UI test build failed'}
if($Installer){& $czExe $Installer}else{& $czExe}
if($LASTEXITCODE -ne 0){throw 'Installer UI test failed'}
