# Own read-only detector. No repair script or DG -Capable action is called here.
$ErrorActionPreference='Stop'
[Console]::OutputEncoding=[Text.UTF8Encoding]::new($false)
$cpuState='Unknown'; $dmaState='Unknown'; $hypState='Unknown'; $vbsState='Unknown'
try {
    $cpus=@(Get-CimInstance -ClassName Win32_Processor -ErrorAction Stop)
    Write-Output ('DETAIL=系统：'+[Environment]::OSVersion.VersionString+'；64 位：'+[Environment]::Is64BitOperatingSystem)
    Write-Output ('DETAIL=处理器：'+(($cpus | ForEach-Object {$_.Name}) -join ' / '))
    if($cpus.Count -gt 0){
        if(@($cpus | Where-Object {$_.VMMonitorModeExtensions -or $_.VirtualizationFirmwareEnabled}).Count -eq $cpus.Count){$cpuState='On'}
        elseif(@($cpus | Where-Object {$null -ne $_.VMMonitorModeExtensions -and $null -ne $_.VirtualizationFirmwareEnabled}).Count -eq $cpus.Count){$cpuState='Off'}
    }
} catch {Write-Output ('DETAIL=CPU 检测失败：'+$_.Exception.Message)}
try {
    $dg=Get-CimInstance -Namespace root\Microsoft\Windows\DeviceGuard -ClassName Win32_DeviceGuard -ErrorAction Stop
    if($null -ne $dg.VirtualizationBasedSecurityStatus){
        if([int]$dg.VirtualizationBasedSecurityStatus -eq 2){$vbsState='Running'}
        elseif([int]$dg.VirtualizationBasedSecurityStatus -eq 1){$vbsState='Configured'}
        elseif([int]$dg.VirtualizationBasedSecurityStatus -eq 0){$vbsState='Off'}
    }
} catch {Write-Output ('DETAIL=VBS 检测失败：'+$_.Exception.Message)}
try {
    $bcd=& "$env:SystemRoot\System32\bcdedit.exe" /enum 2>&1
    if($LASTEXITCODE -ne 0){throw ($bcd -join [Environment]::NewLine)}
    $line=@($bcd | Select-String '^\s*hypervisorlaunchtype\s+(.+)')
    if($line.Count -eq 0){$hypState='NotConfigured'}
    elseif($line[0].Matches[0].Groups[1].Value.Trim() -match '^(Off|0x0)$'){$hypState='Off'}
    elseif($line[0].Matches[0].Groups[1].Value.Trim() -match '^(Auto|0x3)$'){$hypState='Auto'}
} catch {Write-Output ('DETAIL=Hypervisor 检测失败：'+$_.Exception.Message)}
# Preserve the original msinfo32 DMA detection, using a unique private report.
$report=Join-Path $env:TEMP ('CzAce-msinfo-'+[guid]::NewGuid().ToString('N')+'.txt')
try {
    $msinfo=Start-Process -FilePath "$env:SystemRoot\System32\msinfo32.exe" -ArgumentList @('/report',('"'+$report+'"'),'/categories','SystemSecurity') -WindowStyle Hidden -PassThru
    if(-not $msinfo.WaitForExit(60000)){$msinfo.Kill();throw '系统安全报告生成超时。'}
    if(Test-Path -LiteralPath $report){
        $lines=Get-Content -LiteralPath $report -Encoding Unicode
        $dmaLine=$lines | Where-Object {$_ -match '(内核\s*DMA\s*保护|Kernel\s+DMA\s+Protection)'} | Select-Object -First 1
        if($dmaLine -match '(\t|:|：)\s*(启用|开启|On|Enabled)\s*$'){$dmaState='On'}
        elseif($dmaLine -match '(\t|:|：)\s*(关闭|禁用|Off|Disabled)\s*$'){$dmaState='Off'}
        if($vbsState -eq 'Unknown'){
            $vbsLine=$lines | Where-Object {$_ -match '(基于虚拟化的安全性|Virtualization.based security)'} | Select-Object -First 1
            if($vbsLine -match '(正在运行|Running)\s*$'){$vbsState='Running'}
            elseif($vbsLine -match '(未启用|Not enabled)\s*$'){$vbsState='Off'}
        }
    }
} catch {Write-Output ('DETAIL=DMA 检测失败：'+$_.Exception.Message)}
finally {if([IO.File]::Exists($report)){[IO.File]::Delete($report)}}
Write-Output ('CPU='+$cpuState)
Write-Output ('DMA='+$dmaState)
Write-Output ('HYPERVISOR='+$hypState)
Write-Output ('VBS='+$vbsState)
