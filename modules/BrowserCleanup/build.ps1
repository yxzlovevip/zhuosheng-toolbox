param(
    [string]$PayloadPath,
    [string]$OutputPath,
    [string]$ManifestPath
)
$ErrorActionPreference = 'Stop'
$moduleRoot = $PSScriptRoot
$projectRoot = Split-Path (Split-Path $moduleRoot -Parent) -Parent
$originalPayload = Join-Path $projectRoot '工具\重装后工具\12.一键去浏览器配置推广(使用流氓PE安装后必须使用).bat'
if (-not $PayloadPath) { $PayloadPath = $originalPayload }
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot '工具\重装后工具\浏览器配置与推广清理.exe' }
if (-not $ManifestPath) { $ManifestPath = Join-Path $moduleRoot 'app.manifest' }
$PayloadPath = (Resolve-Path -LiteralPath $PayloadPath).Path
$ManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$payloadHash = (Get-FileHash -LiteralPath $PayloadPath -Algorithm SHA256).Hash
if ($PayloadPath -eq (Resolve-Path -LiteralPath $originalPayload).Path -and $payloadHash -ne 'A920497432039F668047863973684FD9247DFDE40049BB0B9286A7556498A88B') {
    throw '原始 BAT 已变化，请重新审查后更新构建依据。'
}
$buildDirectory = Join-Path $moduleRoot ('build-output\' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDirectory -Force | Out-Null
New-Item -ItemType Directory -Path (Split-Path $OutputPath -Parent) -Force | Out-Null
$metadata = Join-Path $buildDirectory 'PayloadInfo.cs'
[IO.File]::WriteAllText($metadata, "namespace Cz.BrowserCleanup { internal static class PayloadInfo { internal const string Sha256 = `"$payloadHash`"; } }", [Text.Encoding]::UTF8)
$compiler = Join-Path $env:WINDIR 'Microsoft.NET\Framework64\v4.0.30319\csc.exe'
& $compiler /nologo /warnaserror+ /optimize+ /target:exe /platform:anycpu "/win32manifest:$ManifestPath" "/resource:$PayloadPath,Cz.BrowserCleanup.Payload.bat" "/out:$OutputPath" (Join-Path $moduleRoot 'Program.cs') $metadata
if ($LASTEXITCODE -ne 0) { throw "浏览器封装 EXE 编译失败：$LASTEXITCODE" }
Write-Output "Built: $OutputPath"
Write-Output "Payload SHA256: $payloadHash"
