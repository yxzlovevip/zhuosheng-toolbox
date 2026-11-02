param([string]$Source=(Join-Path $PSScriptRoot '../assets/icons/toolbox-master.png'),[string]$Destination=(Join-Path $PSScriptRoot '../assets/toolbox.ico'))
$ErrorActionPreference='Stop'
Add-Type -AssemblyName System.Drawing
$original=[Drawing.Bitmap]::FromFile([IO.Path]::GetFullPath($Source))
$frames=[Collections.Generic.List[byte[]]]::new()
$sizes=@(16,20,24,32,40,48,64,128,256)
try {
 foreach($size in $sizes) {
  $bitmap=[Drawing.Bitmap]::new($size,$size,[Drawing.Imaging.PixelFormat]::Format32bppArgb)
  $graphics=[Drawing.Graphics]::FromImage($bitmap)
  $stream=[IO.MemoryStream]::new()
  try {
   $graphics.CompositingMode=[Drawing.Drawing2D.CompositingMode]::SourceCopy
   $graphics.CompositingQuality=[Drawing.Drawing2D.CompositingQuality]::HighQuality
   $graphics.InterpolationMode=[Drawing.Drawing2D.InterpolationMode]::HighQualityBicubic
   $graphics.PixelOffsetMode=[Drawing.Drawing2D.PixelOffsetMode]::HighQuality
   $graphics.DrawImage($original,[Drawing.Rectangle]::new(0,0,$size,$size))
   $bitmap.Save($stream,[Drawing.Imaging.ImageFormat]::Png)
   $frames.Add($stream.ToArray())
  } finally {$stream.Dispose();$graphics.Dispose();$bitmap.Dispose()}
 }
 $file=[IO.File]::Create([IO.Path]::GetFullPath($Destination))
 $writer=[IO.BinaryWriter]::new($file)
 try {
  $writer.Write([UInt16]0);$writer.Write([UInt16]1);$writer.Write([UInt16]$sizes.Count)
  $offset=6+16*$sizes.Count
  for($i=0;$i -lt $sizes.Count;$i++) {
   $dimension=if($sizes[$i] -eq 256){0}else{$sizes[$i]}
   $writer.Write([byte]$dimension);$writer.Write([byte]$dimension);$writer.Write([UInt16]0)
   $writer.Write([UInt16]1);$writer.Write([UInt16]32)
   $writer.Write([UInt32]$frames[$i].Length);$writer.Write([UInt32]$offset)
   $offset+=$frames[$i].Length
  }
  foreach($frame in $frames){$writer.Write($frame)}
 } finally {$writer.Dispose()}
} finally {$original.Dispose()}
Write-Output "Created 9-resolution app icon: $Destination"
