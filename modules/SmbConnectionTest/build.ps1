param([string]$OutputDirectory = (Join-Path $PSScriptRoot '../../工具/网络'))
$ErrorActionPreference = 'Stop'
$smbCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
if (-not (Test-Path -LiteralPath $smbCompiler)) { throw '需要 Windows .NET Framework 4.x C# 编译器。' }
New-Item -ItemType Directory -Path $OutputDirectory -Force | Out-Null
$smbOutput = Join-Path $OutputDirectory 'SMB连接测试.exe'
$smbSources = @('SmbCore.cs', 'Diagnostics.cs', 'ShareAccess.cs', 'MainForm.cs', 'Program.cs') | ForEach-Object { Join-Path $PSScriptRoot $_ }
& $smbCompiler /nologo /warnaserror+ /optimize+ /target:winexe /platform:anycpu /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/win32manifest:$PSScriptRoot/app.manifest" "/out:$smbOutput" $smbSources
if ($LASTEXITCODE -ne 0) { throw 'SMB 工具编译失败。' }
Copy-Item -LiteralPath (Join-Path $PSScriptRoot 'SMB连接测试.exe.config') -Destination ($smbOutput + '.config') -Force
Get-FileHash -LiteralPath $smbOutput -Algorithm SHA256 | Select-Object Path, Hash
if (Test-Path -LiteralPath (Join-Path $PSScriptRoot '../../assets/catalog.json')) {
    & (Join-Path $PSScriptRoot 'Register-SmbTool.ps1')
}
