param([string]$CacheRoot=(Join-Path (Split-Path $PSScriptRoot -Parent) '.build/opensource'))
$ErrorActionPreference='Stop'
New-Item -ItemType Directory -Path $CacheRoot -Force | Out-Null
$headers=@{'User-Agent'='CZToolbox-PersonalIntegration';'Accept'='application/vnd.github+json'}
$requests=@(
 @{Id='localsend';Repo='localsend/localsend';Pattern='^LocalSend-.*-windows-x86-64.zip$'},
 @{Id='trafficmonitor';Repo='zhongyang219/TrafficMonitor';Pattern='^TrafficMonitor_.*_x64.zip$'},
 @{Id='sharex';Repo='ShareX/ShareX';Pattern='^ShareX-.*-portable-x64.zip$'},
 @{Id='screentogif';Repo='NickeManarin/ScreenToGif';Pattern='^ScreenToGif\.[\d.]+\.Portable.x64.zip$'},
 @{Id='quicklook';Repo='QL-Win/QuickLook';Pattern='^QuickLook-[\d.]+.7z$'},
 @{Id='bcuninstaller';Repo='BCUninstaller/Bulk-Crap-Uninstaller';Pattern='^BCUninstaller_.*_portable.7z$'},
 @{Id='fileconverter';Repo='Tichau/FileConverter';Pattern='^FileConverter-.*-x64-setup.msi$'},
 @{Id='powertoys';Repo='microsoft/PowerToys';Pattern='^PowerToysUserSetup-.*-x64.exe$'}
)
$locked=@()
foreach($request in $requests){
 $snapshot=Join-Path $CacheRoot ($request.Id+'-release.json')
 if(Test-Path -LiteralPath $snapshot){$release=Get-Content -LiteralPath $snapshot -Raw | ConvertFrom-Json}
 else{$release=Invoke-RestMethod -Uri ('https://api.github.com/repos/'+$request.Repo+'/releases/latest') -Headers $headers -TimeoutSec 40;$release | ConvertTo-Json -Depth 15 | Set-Content -LiteralPath $snapshot -Encoding utf8}
 if($release.prerelease -or $release.draft){throw ('Expected stable release: '+$request.Id)}
 $asset=@($release.assets | Where-Object name -Match $request.Pattern)
 if($asset.Count -ne 1){throw ('Ambiguous or missing official asset: '+$request.Id)}
 $asset=$asset[0]
 if($asset.digest -notmatch '^sha256:([0-9a-fA-F]{64})$'){throw ('Official SHA-256 missing: '+$request.Id)}
 $locked+= [pscustomobject]@{Id=$request.Id;Repository=$request.Repo;Version=$release.tag_name;ReleaseUrl=$release.html_url;Name=$asset.name;Url=$asset.browser_download_url;Size=[long]$asset.size;Sha256=$Matches[1].ToUpperInvariant()}
}
$locked | ConvertTo-Json -Depth 5 | Set-Content -LiteralPath (Join-Path $CacheRoot 'releases.lock.json') -Encoding utf8
$locked | ForEach-Object -Parallel {
 $ErrorActionPreference='Stop';$entry=$_;$cache=$using:CacheRoot;$file=Join-Path $cache $entry.Name
 if((Test-Path -LiteralPath $file) -and (Get-Item -LiteralPath $file).Length -eq $entry.Size -and (Get-FileHash -LiteralPath $file -Algorithm SHA256).Hash -eq $entry.Sha256){Write-Output ('CACHED '+$entry.Id);return}
 $part=$file+'.part'
 & curl.exe --fail --location --retry 3 --retry-delay 2 --connect-timeout 25 --max-time 1200 --silent --show-error --output $part $entry.Url
 if($LASTEXITCODE -ne 0){throw ('Download failed: '+$entry.Id)}
 if((Get-Item -LiteralPath $part).Length -ne $entry.Size -or (Get-FileHash -LiteralPath $part -Algorithm SHA256).Hash -ne $entry.Sha256){throw ('Official asset SHA-256 mismatch: '+$entry.Id)}
 Move-Item -LiteralPath $part -Destination $file -Force
 Write-Output ('VERIFIED '+$entry.Id+' '+$entry.Version+' '+$entry.Size)
 } -ThrottleLimit 3
Write-Output 'All eight official release assets verified.'
