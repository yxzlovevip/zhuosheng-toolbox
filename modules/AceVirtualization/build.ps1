param([string]$OutputPath=(Join-Path $PSScriptRoot 'build-output/ACE虚拟化环境检测修复.exe'),[string]$PayloadDirectory=(Join-Path $PSScriptRoot 'payload'),[string]$EntrySource=(Join-Path $PSScriptRoot 'Program.cs'),[string]$ManifestPath=(Join-Path $PSScriptRoot 'app.manifest'),[switch]$Console)
$ErrorActionPreference='Stop'
$czBuildDirectory=Join-Path $PSScriptRoot ('build-output/compile-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $czBuildDirectory,(Split-Path ([IO.Path]::GetFullPath($OutputPath)) -Parent) -Force | Out-Null
$czCompiler=Join-Path $env:WINDIR 'Microsoft.NET/Framework64/v4.0.30319/csc.exe'
$czFiles=@(Get-ChildItem -LiteralPath $PayloadDirectory -Recurse -File | Sort-Object FullName)
$czResources=@(); $czRecords=@()
foreach($czFile in $czFiles){
    $czRelative=[IO.Path]::GetRelativePath([IO.Path]::GetFullPath($PayloadDirectory),$czFile.FullName).Replace('\','/')
    $czResources+=('/resource:'+$czFile.FullName+',Ace.Payload.'+$czRelative)
    $czRecords+=(' {"'+$czRelative+'","'+(Get-FileHash -LiteralPath $czFile.FullName -Algorithm SHA256).Hash+'"}')
}
$czHashes=Join-Path $czBuildDirectory 'PayloadHashes.cs'
$czText='namespace Cz.AceVirtualization { internal static class PayloadHashes { internal static readonly System.Collections.Generic.Dictionary<string,string> Files=new System.Collections.Generic.Dictionary<string,string> {'+($czRecords -join ',')+'}; } }'
[IO.File]::WriteAllText($czHashes,$czText,[Text.UTF8Encoding]::new($false))
$czSources=@('Core.cs','PayloadRunner.cs','MainForm.cs') | ForEach-Object {Join-Path $PSScriptRoot $_}
$czTarget=if($Console){'/target:exe'}else{'/target:winexe'}
& $czCompiler /nologo /warnaserror+ /optimize+ /platform:x64 $czTarget /r:System.Windows.Forms.dll /r:System.Drawing.dll "/win32manifest:$ManifestPath" "/out:$OutputPath" @czResources @czSources $czHashes $EntrySource
if($LASTEXITCODE -ne 0){throw 'ACE 封装构建失败'}
Get-FileHash -LiteralPath $OutputPath -Algorithm SHA256 | Select-Object Path,Hash
