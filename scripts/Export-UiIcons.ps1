$ErrorActionPreference='Stop'
$source=Get-Content -LiteralPath (Join-Path $PSScriptRoot '../src/Toolbox.App/ToolIcon.cs') -Raw
$destination=Join-Path $PSScriptRoot '../assets/icons/ui'
New-Item -ItemType Directory -Path $destination -Force | Out-Null
$icons=[regex]::Matches($source,'\["([a-z]+)"\]="([^"]+)"')
foreach($icon in $icons) {
 $name=$icon.Groups[1].Value
 $geometry=$icon.Groups[2].Value
 $svg='<svg xmlns="http://www.w3.org/2000/svg" width="24" height="24" viewBox="0 0 24 24" fill="none" stroke="currentColor" stroke-width="1.65" stroke-linecap="round" stroke-linejoin="round"><path d="'+$geometry+'"/></svg>'
 Set-Content -LiteralPath (Join-Path $destination ($name+'.svg')) -Value $svg -Encoding utf8 -NoNewline
}
Write-Output "Exported $($icons.Count) matching vector icons."
