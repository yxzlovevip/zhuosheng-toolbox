$ErrorActionPreference='Stop'
$czProjectRoot=Split-Path $PSScriptRoot -Parent
$czCompressor=Join-Path $czProjectRoot '.build/compression/7zr-26.05.exe'
$czExpectedCompressorHash='9BF582B894DFA665F852D7E9112B02290A1C303C2D95A21FCDC41FFCB03143B7'
if(-not(Test-Path -LiteralPath $czCompressor)){
 New-Item -ItemType Directory -Path (Split-Path $czCompressor -Parent) -Force | Out-Null
 $czDownload=$czCompressor+'.download'
 try{
  Invoke-WebRequest -Uri 'https://github.com/ip7z/7zip/releases/download/26.05/7zr.exe' -OutFile $czDownload
  if((Get-FileHash -LiteralPath $czDownload -Algorithm SHA256).Hash -ne $czExpectedCompressorHash){throw '官方压缩工具下载校验失败。'}
  Move-Item -LiteralPath $czDownload -Destination $czCompressor
 }finally{if(Test-Path -LiteralPath $czDownload){Remove-Item -LiteralPath $czDownload}}
}
if((Get-FileHash -LiteralPath $czCompressor -Algorithm SHA256).Hash -ne $czExpectedCompressorHash){throw '压缩工具已变化，停止构建。'}
$czCompressor
