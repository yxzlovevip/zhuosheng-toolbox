param([string]$PayloadPath, [string]$OutputPath, [string]$ManifestPath)
$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$original = Join-Path $PSScriptRoot 'original.bat'
if (-not $PayloadPath) { $PayloadPath = $original }
if (-not $OutputPath) { $OutputPath = Join-Path $projectRoot '工具/系统设置/Win10优化系统.exe' }
if (-not $ManifestPath) { $ManifestPath = Join-Path $PSScriptRoot 'app.manifest' }
$PayloadPath = (Resolve-Path -LiteralPath $PayloadPath).Path
$ManifestPath = (Resolve-Path -LiteralPath $ManifestPath).Path
$OutputPath = [IO.Path]::GetFullPath($OutputPath)
$payloadHash = (Get-FileHash -LiteralPath $PayloadPath -Algorithm SHA256).Hash
if ($PayloadPath -eq (Resolve-Path -LiteralPath $original).Path -and $payloadHash -ne 'CBCC92C1B91FA3ACB0B5C220D94E9D0D29D8FF9A76F1BF2D730364C572F6FA77') {
    throw '原始 Win10 BAT 已变化，需重新审查。'
}
$buildDirectory = Join-Path $projectRoot ('.build/win10-wrapper-' + [guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $buildDirectory,(Split-Path $OutputPath -Parent) -Force | Out-Null
$metadata = Join-Path $buildDirectory 'PayloadInfo.cs'
[IO.File]::WriteAllText($metadata, ('namespace Cz.Win10Optimization { internal static class PayloadInfo { internal const string Sha256 = "' + $payloadHash + '"; } }'))
$compiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
& $compiler /nologo /warnaserror+ /optimize+ /target:exe /platform:anycpu "/win32manifest:$ManifestPath" "/resource:$PayloadPath,Cz.Win10Optimization.Payload.bat" "/out:$OutputPath" (Join-Path $PSScriptRoot 'Program.cs') $metadata
if ($LASTEXITCODE -ne 0) { throw "Win10 封装编译失败：$LASTEXITCODE" }
Write-Output "Built: $OutputPath"
Write-Output "Payload SHA256: $payloadHash"
