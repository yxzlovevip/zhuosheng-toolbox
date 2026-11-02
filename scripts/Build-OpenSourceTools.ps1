$ErrorActionPreference='Stop'
$czRoot=Split-Path $PSScriptRoot -Parent
Push-Location -LiteralPath $czRoot
try {
 # Existing pinned manifests are verified before generating launcher metadata.
 & (Join-Path $czRoot '.build/dotnet/dotnet.exe') run --project tests/Toolbox.OpenSourceTests -- --payload-only
 if($LASTEXITCODE -ne 0){throw '开源工具载荷验证失败'}
 & node scripts/integrate-opensource-tools.mjs --prepare
 if($LASTEXITCODE -ne 0){throw '开源工具入口生成失败'}
 & (Join-Path $czRoot '.build/dotnet/dotnet.exe') publish modules/OpenSourceTools/Toolbox.OpenSourceTools.csproj -c Release -r win-x64 --self-contained true -o .build/opensource/adapter-publish --nologo
 if($LASTEXITCODE -ne 0){throw '开源工具适配入口构建失败'}
 Copy-Item -LiteralPath '.build/opensource/adapter-publish/CZOpenSourceTools.exe' -Destination '工具/开源工具/CZOpenSourceTools.exe' -Force
 & node scripts/integrate-opensource-tools.mjs
 if($LASTEXITCODE -ne 0){throw '开源工具目录生成失败'}
}finally{Pop-Location}
