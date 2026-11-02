$ErrorActionPreference = 'Stop'
$smbRoot = $PSScriptRoot
$smbCompiler = Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$smbTestDir = Join-Path $smbRoot 'test-output'
New-Item -ItemType Directory -Path $smbTestDir -Force | Out-Null
$smbSources = @('SmbCore.cs', 'Diagnostics.cs', 'ShareAccess.cs', 'MainForm.cs', 'tests/UiTests.cs') | ForEach-Object { Join-Path $smbRoot $_ }
& $smbCompiler /nologo /warnaserror+ /target:exe /reference:System.Windows.Forms.dll /reference:System.Drawing.dll "/out:$smbTestDir/UiTests.exe" $smbSources
if ($LASTEXITCODE -ne 0) { throw '界面测试程序编译失败' }
& (Join-Path $smbTestDir 'UiTests.exe') $smbTestDir
if ($LASTEXITCODE -ne 0) { throw '界面测试失败' }
