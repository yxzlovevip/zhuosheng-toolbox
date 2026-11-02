$ErrorActionPreference='Stop'
$czRoot=Split-Path $PSScriptRoot -Parent
$czCatalogPath=Join-Path $czRoot 'assets/catalog.json'
$czCatalog=Get-Content -LiteralPath $czCatalogPath -Raw | ConvertFrom-Json
$czDestination=Join-Path $czRoot 'frontend/public/tool-icons'
New-Item -ItemType Directory -Path $czDestination -Force | Out-Null
Add-Type -AssemblyName System.Drawing
if(-not('CzToolIcons.Native' -as [type])) {
 Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
namespace CzToolIcons {
 public static class Native {
  [DllImport("user32.dll",CharSet=CharSet.Unicode)] public static extern uint PrivateExtractIcons(string file,int index,int width,int height,IntPtr[] icons,uint[] ids,uint count,uint flags);
  [DllImport("user32.dll")] public static extern bool DestroyIcon(IntPtr icon);
 }
}
'@
}
$czPaths=@{
 'network'='M4 7h16v10H4z M8 21h8 M12 17v4 M8 11h2 M14 11h2'
 'disk'='M5 3h14l3 14v4H2v-4z M2 17h20 M16 19h.01 M19 19h.01'
 'shield'='M12 2 3 6v6c0 5 9 10 9 10s9-5 9-10V6z M8 12l3 3 5-6'
 'settings'='M4 6h16 M4 18h16 M8 3v6 M16 15v6 M4 12h16 M13 9v6'
 'install'='M4 3h16v4H4z M6 7v14h12V7 M12 17V10 M9 13l3-3 3 3'
 'repair'='M15 3a6 6 0 0 0-7 7l-6 6 6 6 6-6a6 6 0 0 0 7-7l-4 4-4-4z'
 'boxes'='M12 2 3 7v10l9 5 9-5V7z M3 7l9 5 9-5 M12 12v10'
}
$czColors=@('#7661e8','#13a78b','#db9444','#477ed8','#d45992','#5c8a58')
$czManifest=@()
$czIndex=0
foreach($czTool in $czCatalog) {
 $czIconSource=if($czTool.PSObject.Properties.Name -contains 'IconSourcePath'){$czTool.IconSourcePath}else{$czTool.RelativePath}
 $czFile=Join-Path $czRoot $czIconSource
 $czOriginal=$false
 $czPng=Join-Path $czDestination ($czTool.Id+'.png')
 if([IO.Path]::GetExtension($czFile) -in @('.exe','.dll') -and (Test-Path -LiteralPath $czFile)) {
  $czHandles=[IntPtr[]]::new(1)
  $czIds=[uint32[]]::new(1)
  try {
   $czCount=[CzToolIcons.Native]::PrivateExtractIcons($czFile,0,96,96,$czHandles,$czIds,1,0)
   if($czCount -eq 1 -and $czHandles[0] -ne [IntPtr]::Zero) {
    $czIcon=[Drawing.Icon]::FromHandle($czHandles[0])
    $czBitmap=$czIcon.ToBitmap()
    try {$czBitmap.Save($czPng,[Drawing.Imaging.ImageFormat]::Png);$czOriginal=$true} finally {$czBitmap.Dispose();$czIcon.Dispose()}
   }
  } finally {if($czHandles[0] -ne [IntPtr]::Zero){[CzToolIcons.Native]::DestroyIcon($czHandles[0]) | Out-Null}}
 }
 if($czOriginal) {$czRelative='tool-icons/'+$czTool.Id+'.png'} else {
  $czKind=switch($czTool.Category) {'网络工具'{'network'} '磁盘工具'{'disk'} '重装维护'{if($czTool.Group -eq '系统重装'){'install'}else{'repair'}} '系统设置'{'settings'} default {'boxes'}}
  if($czTool.Id -like '*defender*' -or $czTool.Id -eq 'uac'){$czKind='shield'}
  $czColor=$czColors[$czIndex % $czColors.Count]
  $czSvg='<svg xmlns="http://www.w3.org/2000/svg" width="96" height="96" viewBox="0 0 64 64"><defs><linearGradient id="g" x2="1" y2="1"><stop stop-color="'+$czColor+'"/><stop offset="1" stop-color="#263856"/></linearGradient></defs><rect x="2" y="2" width="60" height="60" rx="17" fill="url(#g)"/><path d="M10 16a12 12 0 0 1 12-8h20" fill="none" stroke="#fff" stroke-opacity=".18"/><g transform="translate(17 17) scale(1.25)" fill="none" stroke="#fff" stroke-width="1.7" stroke-linecap="round" stroke-linejoin="round"><path d="'+$czPaths[$czKind]+'"/></g></svg>'
  [IO.File]::WriteAllText((Join-Path $czDestination ($czTool.Id+'.svg')),$czSvg,[Text.UTF8Encoding]::new($false))
  $czRelative='tool-icons/'+$czTool.Id+'.svg'
 }
 $czTool | Add-Member -NotePropertyName IconPath -NotePropertyValue $czRelative -Force
 $czManifest+= [ordered]@{Id=$czTool.Id;IconPath=$czRelative;Source=$(if($czOriginal){'original'}else{'generated'});OriginalPath=$czTool.RelativePath}
 $czIndex++
}
$czCatalog | ConvertTo-Json -Depth 12 | Set-Content -LiteralPath $czCatalogPath -Encoding utf8
$czManifest | ConvertTo-Json -Depth 4 | Set-Content -LiteralPath (Join-Path $czRoot 'assets/tool-icon-manifest.json') -Encoding utf8
'Tool icons: '+@($czManifest | Where-Object Source -EQ 'original').Count+' original, '+@($czManifest | Where-Object Source -EQ 'generated').Count+' generated'
