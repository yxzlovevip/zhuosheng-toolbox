param([Parameter(Mandatory)][ValidatePattern('^\d+\.\d+\.\d+$')][string]$Version,[Alias('Installer')][Parameter(Mandatory)][string]$InstallerPath,[Parameter(Mandatory)][string]$Notes,[string]$BaseUrl='',[string]$OutputDirectory='')
$ErrorActionPreference='Stop'
$czRoot=Split-Path $PSScriptRoot -Parent
if(-not $BaseUrl){$BaseUrl='https://zskj.top/cz-updates/releases/'+$Version}
if(-not $OutputDirectory){$OutputDirectory=Join-Path $czRoot ('artifacts/update-releases/'+$Version)}
if(Test-Path -LiteralPath $OutputDirectory){throw '输出目录已存在，拒绝覆盖已有发布材料'}
$czBase=[uri]$BaseUrl
if($czBase.Scheme -ne 'https' -or $czBase.UserInfo -or $czBase.Fragment -or $czBase.Query){throw 'BaseUrl 必须为 HTTPS 目录地址'}
$czFile=Get-Item -LiteralPath $InstallerPath
$czFileVersion=[Diagnostics.FileVersionInfo]::GetVersionInfo($czFile.FullName)
if(('{0}.{1}.{2}' -f $czFileVersion.FileMajorPart,$czFileVersion.FileMinorPart,$czFileVersion.FileBuildPart) -ne $Version){throw '安装器文件版本与发布版本不一致，请先按目标版本重新构建'}
$czName='CZToolbox-'+$Version+'-setup.exe'
$czHash=(Get-FileHash -LiteralPath $czFile.FullName -Algorithm SHA256).Hash
$czPayload=[ordered]@{Version=$Version;Url=$BaseUrl.TrimEnd('/')+'/'+$czName;Size=$czFile.Length;Sha256=$czHash;Notes=$Notes}
$czBytes=[Text.Encoding]::UTF8.GetBytes(($czPayload|ConvertTo-Json -Compress))
$czRsa=[Security.Cryptography.RSA]::Create()
$czVerifier=[Security.Cryptography.RSA]::Create()
try{
 $czRsa.ImportFromPem([IO.File]::ReadAllText((Join-Path $czRoot '.private/update-private.pem')))
 $czVerifier.ImportFromPem([IO.File]::ReadAllText((Join-Path $czRoot 'assets/update-public.pem')))
 $czSignature=$czRsa.SignData($czBytes,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)
 if(-not $czVerifier.VerifyData($czBytes,$czSignature,[Security.Cryptography.HashAlgorithmName]::SHA256,[Security.Cryptography.RSASignaturePadding]::Pkcs1)){throw '公私钥不匹配，请勿替换已有客户端公钥'}
}finally{$czRsa.Dispose();$czVerifier.Dispose()}
New-Item -ItemType Directory -Path $OutputDirectory | Out-Null
[ordered]@{payload=[Convert]::ToBase64String($czBytes);signature=[Convert]::ToBase64String($czSignature)}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $OutputDirectory 'latest.json') -Encoding utf8
Copy-Item -LiteralPath $czFile.FullName -Destination (Join-Path $OutputDirectory $czName)
if((Get-FileHash -LiteralPath (Join-Path $OutputDirectory $czName) -Algorithm SHA256).Hash -ne $czHash){throw '复制后校验失败'}
($czHash+'  '+$czName+"`n") | Set-Content -LiteralPath (Join-Path $OutputDirectory 'SHA256SUMS') -Encoding ascii -NoNewline
Write-Output ('更新发布目录：'+(Resolve-Path -LiteralPath $OutputDirectory).Path)
