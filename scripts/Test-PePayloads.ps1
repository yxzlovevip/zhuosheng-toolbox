param([Parameter(Mandatory=$true)][string[]]$Path)
$ErrorActionPreference = 'Stop'
function Read-PeResources([string]$Path) {
    $stream = [IO.File]::OpenRead($Path)
    try {
        $pe = [System.Reflection.PortableExecutable.PEReader]::new($stream, [System.Reflection.PortableExecutable.PEStreamOptions]::LeaveOpen)
        try {
            $headers = $pe.PEHeaders
            $rva = $headers.PEHeader.ResourceTableDirectory.RelativeVirtualAddress
            if ($rva -eq 0) { return }
            $section = $headers.SectionHeaders | Where-Object { $rva -ge $_.VirtualAddress -and $rva -lt ($_.VirtualAddress + [Math]::Max($_.VirtualSize,$_.SizeOfRawData)) } | Select-Object -First 1
            if (-not $section) { throw 'Resource section not found' }
            $baseOffset = [long]$section.PointerToRawData + $rva - $section.VirtualAddress
            $reader = [IO.BinaryReader]::new($stream,[Text.Encoding]::Unicode,$true)
            $state = @{Count=0}
            function Walk-Resource([long]$Offset,[string]$Prefix,[int]$Depth) {
                if ($Depth -gt 5 -or $state.Count -gt 65536) { throw 'Resource directory exceeds bounded parser limits' }
                $stream.Position = $baseOffset + $Offset + 12
                $count = [int]$reader.ReadUInt16() + [int]$reader.ReadUInt16()
                if ($count -gt 4096) { throw 'Invalid resource count' }
                for ($i=0; $i -lt $count; $i++) {
                    $state.Count++
                    $stream.Position = $baseOffset + $Offset + 16 + 8*$i
                    $nameValue = $reader.ReadUInt32()
                    $dataValue = $reader.ReadUInt32()
                    if (($nameValue -band 2147483648) -ne 0) {
                        $stream.Position = $baseOffset + ($nameValue -band 2147483647)
                        $length = $reader.ReadUInt16()
                        if ($length -gt 4096) { throw 'Invalid resource name length' }
                        $name = [Text.Encoding]::Unicode.GetString($reader.ReadBytes(2*$length))
                    } else { $name = '#'+$nameValue }
                    $fullName = $Prefix+'/'+$name
                    if (($dataValue -band 2147483648) -ne 0) {
                        Walk-Resource ($dataValue -band 2147483647) $fullName ($Depth+1)
                    } else {
                        $stream.Position = $baseOffset + $dataValue
                        $dataRva = $reader.ReadUInt32()
                        $dataSize = $reader.ReadUInt32()
                        $dataSection = $headers.SectionHeaders | Where-Object { $dataRva -ge $_.VirtualAddress -and $dataRva -lt ($_.VirtualAddress + [Math]::Max($_.VirtualSize,$_.SizeOfRawData)) } | Select-Object -First 1
                        $magic = ''
                        if ($dataSection -and $dataSize -ge 2) {
                            $stream.Position = [long]$dataSection.PointerToRawData + $dataRva - $dataSection.VirtualAddress
                            $magic = [Text.Encoding]::ASCII.GetString($reader.ReadBytes(2))
                        }
                        [pscustomobject]@{Name=$fullName;Size=$dataSize;Magic=$magic}
                    }
                }
            }
            Walk-Resource 0 '' 0
        } finally { $pe.Dispose() }
    } finally { $stream.Dispose() }
}
$czFailures=@()
$czChecked=0
foreach($czDirectory in $Path){
 foreach($czFile in Get-ChildItem -LiteralPath $czDirectory -Recurse -File | Where-Object Extension -In '.exe','.dll','.com','.scr'){
  $czChecked++
  $czHits=@(Read-PeResources $czFile.FullName | Where-Object Name -Match '/(EXERESX|KBHKS)/')
  if($czHits.Count -gt 0){$czFailures+=$czFile.FullName}
 }
}
if($czFailures.Count -gt 0){throw ('发现 Synares 相关资源，停止打包：'+($czFailures -join '; '))}
Write-Output ('PASS PE resource check: '+$czChecked+' files; no EXERESX/KBHKS resources (not a full antivirus scan)')
