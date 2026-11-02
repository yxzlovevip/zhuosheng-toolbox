param([ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version='2.0.0',[string]$WebRootSnapshot='')
$ErrorActionPreference='Stop'
$czRoot=Split-Path $PSScriptRoot -Parent
Set-Location -LiteralPath $czRoot
$czCompressor=& (Join-Path $PSScriptRoot 'Get-PackageCompressor.ps1')
$czCompressorHash=(Get-FileHash -LiteralPath $czCompressor -Algorithm SHA256).Hash
$czDotnet=Join-Path $czRoot '.build/dotnet/dotnet.exe'
if(-not(Test-Path -LiteralPath $czDotnet)){throw '项目本地 .NET SDK 缺失。请按 docs/构建说明.md 准备 SDK。'}
if(-not(Test-Path 'assets/update-public.pem')){throw '更新公钥缺失；不要为已有发布随意重新生成密钥。'}
$env:DOTNET_CLI_TELEMETRY_OPTOUT='1'
& (Join-Path $PSScriptRoot 'Build-OpenSourceTools.ps1')
& (Join-Path $PSScriptRoot 'Test-PePayloads.ps1') -Path @((Join-Path $czRoot '工具'),(Join-Path $czRoot '重装'))
& (Join-Path $PSScriptRoot 'Export-ToolIcons.ps1')
$czPublishArgs=@()
if($WebRootSnapshot){
 $WebRootSnapshot=(Resolve-Path -LiteralPath $WebRootSnapshot).Path
 if(-not(Test-Path -LiteralPath (Join-Path $WebRootSnapshot 'index.html'))){throw '界面快照缺少 index.html'}
 $czPublishArgs+=('-p:CzWebRoot='+$WebRootSnapshot)
}else{
 & pnpm --dir frontend build
 if($LASTEXITCODE -ne 0){throw '前端构建失败'}
}
& node --test tests/SoftwareCollection/logos.test.mjs
if($LASTEXITCODE -ne 0){throw '软件合集图标缺失或校验失败'}
$czCatalog=Get-Content -LiteralPath 'assets/catalog.json' -Raw | ConvertFrom-Json
$czCatalogHash=(Get-FileHash -LiteralPath 'assets/catalog.json' -Algorithm SHA256).Hash
foreach($czTool in $czCatalog){$czPath=Join-Path $czRoot $czTool.RelativePath;if((Get-FileHash -LiteralPath $czPath -Algorithm SHA256).Hash -ne $czTool.Sha256){throw ('工具已变化，请先重新审核并更新清单：'+$czTool.Name)}}
$czBuildRoot=Join-Path $czRoot ('.build/package-'+[guid]::NewGuid().ToString('N'))
$czApp=Join-Path $czBuildRoot 'app'
New-Item -ItemType Directory -Path $czApp,(Join-Path $czRoot 'dist') -Force | Out-Null
& $czDotnet publish src/Toolbox.App/Toolbox.App.csproj -c Release -r win-x64 --self-contained true -p:Version=$Version -o $czApp --nologo @czPublishArgs
if($LASTEXITCODE -ne 0){throw '主程序发布失败'}
& (Join-Path $PSScriptRoot 'Test-PePayloads.ps1') -Path @($czApp)
foreach($czTool in $czCatalog){$czFile=Join-Path $czApp ('tools/'+$czTool.RelativePath);if((Get-FileHash -LiteralPath $czFile -Algorithm SHA256).Hash -ne $czTool.Sha256){throw ('打包工具校验失败：'+$czTool.Name)}}
if((Get-FileHash -LiteralPath 'assets/catalog.json' -Algorithm SHA256).Hash -ne $czCatalogHash){throw '构建过程中工具清单被修改，请重新构建。'}
Copy-Item -LiteralPath 'docs/使用说明.md','docs/更新发布说明.md' -Destination $czApp
$czRuntimeBootstrap=Join-Path $czRoot '.build/MicrosoftEdgeWebview2Setup.exe'
if(-not(Test-Path -LiteralPath $czRuntimeBootstrap)){Invoke-WebRequest -Uri 'https://go.microsoft.com/fwlink/p/?LinkId=2124703' -OutFile $czRuntimeBootstrap}
$czRuntimeSignature=Get-AuthenticodeSignature -LiteralPath $czRuntimeBootstrap
if($czRuntimeSignature.Status -ne 'Valid' -or $czRuntimeSignature.SignerCertificate.Subject -notlike '*O=Microsoft Corporation*'){throw 'WebView2 安装引导程序签名验证未通过'}
Copy-Item -LiteralPath $czRuntimeBootstrap -Destination (Join-Path $czApp 'WebView2Bootstrapper.exe')
$czCsc=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$czSetupSource=Join-Path $czRoot 'src\Toolbox.Setup\Program.cs'
$czEngineSource=Join-Path $czRoot 'src\Toolbox.Setup\InstallEngine.cs'
$czSolidSource=Join-Path $czRoot 'src\Toolbox.Setup\SolidPayload.cs'
$czOwnedSource=Join-Path $czRoot 'src\Toolbox.Setup\OwnedFiles.cs'
$czPathPolicy=Join-Path $czRoot 'src\Shared\InstallPathPolicy.cs'
$czSetupManifest=Join-Path $czRoot 'src\Toolbox.Setup\app.manifest'
$czIcon=Join-Path $czRoot 'assets\toolbox.ico'
$czReferences=@('/r:System.dll','/r:System.Core.dll','/r:System.Runtime.Serialization.dll','/r:Microsoft.CSharp.dll','/r:System.Drawing.dll','/r:System.Windows.Forms.dll','/r:System.IO.Compression.dll','/r:System.IO.Compression.FileSystem.dll')
$czInfo=Join-Path $czBuildRoot 'PayloadInfo.cs'
[IO.File]::WriteAllText($czInfo,('[assembly:System.Reflection.AssemblyTitle("卓盛工具箱")][assembly:System.Reflection.AssemblyProduct("卓盛工具箱")][assembly:System.Reflection.AssemblyCompany("卓盛科技")][assembly:System.Reflection.AssemblyVersion("'+$Version+'.0")][assembly:System.Reflection.AssemblyFileVersion("'+$Version+'.0")]namespace Toolbox.Setup { internal static class PayloadInfo { public const string Version="'+$Version+'"; public const string Sha256="",ZipSha256="",DecoderSha256=""; public const long ZipSize=0; } }'))
& $czCsc /nologo /target:winexe /platform:x64 /optimize+ /define:UNINSTALL "/out:$czApp\Uninstall.exe" "/win32manifest:$czSetupManifest" "/win32icon:$czIcon" @czReferences $czSetupSource $czEngineSource $czSolidSource $czOwnedSource $czPathPolicy $czInfo
if($LASTEXITCODE -ne 0){throw '卸载程序编译失败'}
@'
Offline package compression: 7zr 26.05, Igor Pavlov, public domain.
Official source: https://www.7-zip.org/download.html
Binary: https://github.com/ip7z/7zip/releases/download/26.05/7zr.exe
SHA-256: 9BF582B894DFA665F852D7E9112B02290A1C303C2D95A21FCDC41FFCB03143B7
The original tool payloads and installed directory layout are preserved.
'@ | Set-Content -LiteralPath (Join-Path $czApp 'thirdparty-compression.txt') -Encoding utf8
$czManifest=@(Get-ChildItem -LiteralPath $czApp -Recurse -File | Where-Object { $_.FullName -ne (Join-Path $czApp 'payload-files.json') } | ForEach-Object { [ordered]@{Path=[IO.Path]::GetRelativePath($czApp,$_.FullName);Size=$_.Length;Sha256=(Get-FileHash -LiteralPath $_.FullName -Algorithm SHA256).Hash} })
$czManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $czApp 'payload-files.json') -Encoding utf8
$czZip=Join-Path $czBuildRoot 'payload.zip'
Add-Type -AssemblyName System.IO.Compression.FileSystem
[IO.Compression.ZipFile]::CreateFromDirectory($czApp,$czZip,[IO.Compression.CompressionLevel]::NoCompression,$false)
$czZipHash=(Get-FileHash -LiteralPath $czZip -Algorithm SHA256).Hash
$czZipSize=(Get-Item -LiteralPath $czZip).Length
if($czZipSize -gt 4294967295L -or $czManifest.Count -ge 65534){throw '流式安装包不支持 ZIP64，请调整打包方案。'}
$czPacked=Join-Path $czBuildRoot 'payload.7z'
& $czCompressor a $czPacked $czZip -t7z -m0=LZMA2 -mx=7 -md=64m -ms=on -mmt=4 -bsp0
if($LASTEXITCODE -ne 0){throw '固实压缩失败'}
$czHash=(Get-FileHash -LiteralPath $czPacked -Algorithm SHA256).Hash
[IO.File]::WriteAllText($czInfo,('[assembly:System.Reflection.AssemblyTitle("卓盛工具箱")][assembly:System.Reflection.AssemblyProduct("卓盛工具箱")][assembly:System.Reflection.AssemblyCompany("卓盛科技")][assembly:System.Reflection.AssemblyVersion("'+$Version+'.0")][assembly:System.Reflection.AssemblyFileVersion("'+$Version+'.0")]namespace Toolbox.Setup { internal static class PayloadInfo { public const string Version="'+$Version+'"; public const string Sha256="'+$czHash+'",ZipSha256="'+$czZipHash+'",DecoderSha256="'+$czCompressorHash+'"; public const long ZipSize='+$czZipSize+'L; } }'))
$czOutput=Join-Path $czRoot ('dist/卓盛工具箱-安装-v'+$Version+'.exe')
& $czCsc /nologo /target:winexe /platform:x64 /optimize+ "/out:$czOutput" "/win32manifest:$czSetupManifest" "/win32icon:$czIcon" "/resource:$czPacked,payload.7z" "/resource:$czCompressor,7zr.exe" @czReferences $czSetupSource $czEngineSource $czSolidSource $czOwnedSource $czPathPolicy $czInfo
if($LASTEXITCODE -ne 0){throw '安装程序编译失败'}
Copy-Item -LiteralPath 'docs/使用说明.md','docs/更新发布说明.md' -Destination dist
$czArtifact=Get-Item -LiteralPath $czOutput
$czResult=[ordered]@{Version=$Version;Installer=$czArtifact.FullName;Size=$czArtifact.Length;Sha256=(Get-FileHash -LiteralPath $czOutput -Algorithm SHA256).Hash;Compression='LZMA2 solid / stored ZIP';PayloadDirectory=$czApp;DecodedZipSize=$czZipSize;Tools=$czCatalog.Count;BuiltAt=(Get-Date).ToString('o')} | ConvertTo-Json
$czResult | Set-Content -LiteralPath dist/build-result.json -Encoding utf8
$czResult | Set-Content -LiteralPath ('dist/build-result-v'+$Version+'.json') -Encoding utf8
Get-Content -LiteralPath dist/build-result.json


