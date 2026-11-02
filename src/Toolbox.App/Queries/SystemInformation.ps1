$ErrorActionPreference = 'Stop'
$ProgressPreference = 'SilentlyContinue'
$WarningPreference = 'SilentlyContinue'
$OutputEncoding = New-Object System.Text.UTF8Encoding($false)
$script:queryErrors = @{}

function Read-Cim($class, $namespace = 'root/cimv2') {
    try { Get-CimInstance -Namespace $namespace -ClassName $class -OperationTimeoutSec 5 -ErrorAction Stop }
    catch { $script:queryErrors[$class] = $_.Exception.Message }
}
function Query-Errors($classes) {
    (@($classes | ForEach-Object { if ($script:queryErrors.ContainsKey($_)) { $_ + ': ' + $script:queryErrors[$_] } }) -join '; ')
}
function Field($label, $value) {
    $text = if ($null -eq $value) { '' } else { (@($value) | ForEach-Object { ([string]$_).Trim() }) -join ', ' }
    if ([string]::IsNullOrWhiteSpace($text) -or $text -match '^\[?N/A\]?$|^\[Not Supported\]$') { $text = '未提供' }
    @{ Label = $label; Value = $text }
}
function Bytes($value) {
    if ($null -eq $value -or [double]$value -le 0) { return '未提供' }
    if ([double]$value -ge 1TB) { return ('{0:N2} TiB' -f ([double]$value / 1TB)) }
    if ([double]$value -ge 1GB) { return ('{0:N2} GiB' -f ([double]$value / 1GB)) }
    return ('{0:N0} MiB' -f ([double]$value / 1MB))
}
function YesNo($value) { if ($null -eq $value) { '未提供' } elseif ($value) { '已启用' } else { '未启用' } }
function Partition-Type($partition) {
    $guid = ([string]$partition.GptType).Trim('{}').ToLowerInvariant()
    if ($guid -and $guid -ne '00000000-0000-0000-0000-000000000000') {
        return $(switch ($guid) {
            'c12a7328-f81f-11d2-ba4b-00a0c93ec93b' { 'EFI 系统分区' }
            'e3c9e316-0b5c-4db8-817d-f92df00215ae' { 'Microsoft 保留分区 MSR' }
            'ebd0a0a2-b9e5-4433-87c0-68b6b72699c7' { '基本数据分区' }
            'de94bba4-06d1-4d40-a16a-bfd50179d6ac' { 'Windows 恢复分区' }
            '5808c8aa-7e8f-42e0-85d2-e1e90434cfb3' { '动态磁盘元数据' }
            'af9b60a0-1431-4f62-bc68-3311714a69ad' { '动态磁盘数据' }
            default { 'GPT ' + $guid }
        })
    }
    switch ([int]$partition.MbrType) {
        7 { 'MBR IFS 数据分区' }
        11 { 'MBR FAT32 分区' }
        12 { 'MBR FAT32 LBA 分区' }
        39 { 'MBR 恢复 / 隐藏分区' }
        239 { 'EFI 系统分区' }
        default { if ($null -ne $partition.MbrType) { 'MBR 0x{0:X2}' -f [int]$partition.MbrType } else { '未提供' } }
    }
}
function Device($id, $name, $fields = @(), $children = @(), $capacity = $null, $free = $null) {
    @{ Id = [string]$id; Name = [string]$name; Fields = @($fields); Children = @($children); CapacityBytes = $capacity; FreeBytes = $free }
}
function Section($id, $title, $devices = @(), $errorText = '', $notice = '') {
    @{ Id = $id; Title = $title; Devices = @($devices); Error = [string]$errorText; Notice = $notice }
}
function Graphics-Section {
    $adapters = @(Read-Cim 'Win32_VideoController')
    $rows = @(foreach ($gpu in $adapters) {
        $vram = $null
        try {
            $driverKey = (Get-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Enum\' + $gpu.PNPDeviceID) -Name Driver -ErrorAction Stop).Driver
            $vram = (Get-ItemProperty -LiteralPath ('HKLM:\SYSTEM\CurrentControlSet\Control\Class\' + $driverKey) -Name 'HardwareInformation.qwMemorySize' -ErrorAction Stop).'HardwareInformation.qwMemorySize'
        } catch { }
        $fields = @(
            Field '名称' $gpu.Name
            Field '芯片 / 处理器' $gpu.VideoProcessor
            Field '驱动版本' $gpu.DriverVersion
            Field '驱动日期' $(if ($gpu.DriverDate) { $gpu.DriverDate.ToString('yyyy-MM-dd') })
            Field '设备状态' $gpu.Status
            Field 'PNP 设备标识' $gpu.PNPDeviceID
            Field '当前分辨率' $(if ($gpu.CurrentHorizontalResolution -and $gpu.CurrentVerticalResolution) { "$($gpu.CurrentHorizontalResolution) x $($gpu.CurrentVerticalResolution)" })
            Field '当前刷新率' $(if ($gpu.CurrentRefreshRate -gt 1) { "$($gpu.CurrentRefreshRate) Hz" })
        )
        if ($vram -gt 0) { $fields += Field '显存（驱动注册信息）' (Bytes $vram) }
        else {
            $fields += Field '显存（WMI 参考值）' (Bytes $gpu.AdapterRAM)
            $fields += Field '显存说明' 'WMI 字段为 32 位，可能截断或不准确；NVIDIA 显卡可查询下方驱动详情。共享显存不计入此值。'
        }
        Device $gpu.DeviceID $gpu.Name $fields
    })
    Section 'graphics' '显卡' $rows (Query-Errors @('Win32_VideoController')) 'PNP 设备标识不是显卡 UUID。通用信息适用于 Windows 能识别的所有显卡。'
}
function Nvidia-Section {
    $candidates = @(
        (Join-Path $env:WINDIR 'System32\nvidia-smi.exe'),
        (Join-Path $env:ProgramFiles 'NVIDIA Corporation\NVSMI\nvidia-smi.exe')
    )
    $exe = $candidates | Where-Object { Test-Path -LiteralPath $_ -PathType Leaf } | Select-Object -First 1
    if (-not $exe) { return (Section 'nvidia' 'NVIDIA 驱动详情' @() '' '未找到 NVIDIA 查询组件。AMD / Intel 显卡请查看上方通用信息；NVIDIA 显卡请确认官方驱动已安装。') }
    try {
        # Query attributes only. No reset, settings, WMI termination or network commands.
        $csv = @(& $exe '--query-gpu=index,name,uuid,serial,memory.total,driver_version,pci.bus_id,temperature.gpu,utilization.gpu' '--format=csv,noheader,nounits' 2>$null)
        if ($LASTEXITCODE -ne 0) { throw 'NVIDIA 驱动未就绪，或当前设备不支持查询。' }
        $entries = @($csv | ConvertFrom-Csv -Header Index,Name,Uuid,Serial,Memory,Driver,Bus,Temperature,Utilization)
        $rows = @(foreach ($gpu in $entries) {
            $memoryText = $null
            $numericMemory = 0.0
            if ([double]::TryParse($gpu.Memory.Trim(), [Globalization.NumberStyles]::Float, [Globalization.CultureInfo]::InvariantCulture, [ref]$numericMemory)) { $memoryText = Bytes ($numericMemory * 1MB) }
            Device ('nvidia-' + $gpu.Index.Trim()) $gpu.Name.Trim() @(
                Field '显卡序号' $gpu.Index
                Field 'GPU UUID' $gpu.Uuid
                Field '显卡序列号' $gpu.Serial
                Field '显存（NVIDIA 驱动）' $memoryText
                Field '驱动版本' $gpu.Driver
                Field 'PCI 总线地址' $gpu.Bus
                Field '温度（摄氏度）' $gpu.Temperature
                Field 'GPU 使用率（%）' $gpu.Utilization
            )
        })
        Section 'nvidia' 'NVIDIA 驱动详情' $rows '' '此处为查询时的快照。消费级显卡可能不提供序列号；未提供的字段不会生成替代值。'
    } catch { Section 'nvidia' 'NVIDIA 驱动详情' @() $_.Exception.Message '通用显卡信息仍可在上方查看。' }
}

$sections = New-Object System.Collections.Generic.List[object]
$sections.Add((Graphics-Section))
if ($QueryMode -eq 'graphics') {
    $sections.Add((Nvidia-Section))
    @{ CollectedAt = [DateTimeOffset]::Now.ToString('o'); Sections = @($sections.ToArray()) } | ConvertTo-Json -Depth 12 -Compress
    return
}

$systems = @(Read-Cim 'Win32_ComputerSystem')
$products = @(Read-Cim 'Win32_ComputerSystemProduct')
$operatingSystems = @(Read-Cim 'Win32_OperatingSystem')
$cpus = @(Read-Cim 'Win32_Processor')
$memory = @(Read-Cim 'Win32_PhysicalMemory')
$boards = @(Read-Cim 'Win32_BaseBoard')
$biosList = @(Read-Cim 'Win32_BIOS')
$disks = @(Read-Cim 'Win32_DiskDrive')
$networks = @(Read-Cim 'Win32_NetworkAdapterConfiguration')
$allDevices = @(Read-Cim 'Win32_PnPEntity')

$sections.Add((Section 'computer' '电脑与系统' @(foreach ($system in $systems) {
    Device $system.Name $system.Name @(
        Field '电脑名称' $system.Name
        Field '制造商' $system.Manufacturer
        Field '设备型号' $system.Model
        Field '系统类型' $system.SystemType
        Field '物理内存总量' (Bytes $system.TotalPhysicalMemory)
        Field '操作系统' @($operatingSystems | ForEach-Object { $_.Caption })
        Field '系统版本' @($operatingSystems | ForEach-Object { "$($_.Version) / Build $($_.BuildNumber)" })
        Field '系统安装时间' @($operatingSystems | ForEach-Object { if ($_.InstallDate) { $_.InstallDate.ToString('yyyy-MM-dd HH:mm:ss') } })
    )
}) (Query-Errors @('Win32_ComputerSystem','Win32_OperatingSystem'))))
$sections.Add((Section 'cpu' '处理器 CPU' @(foreach ($cpu in $cpus) {
    Device $cpu.DeviceID $cpu.Name @(
        Field '制造商' $cpu.Manufacturer
        Field '插槽' $cpu.SocketDesignation
        Field '核心数' $cpu.NumberOfCores
        Field '逻辑处理器数' $cpu.NumberOfLogicalProcessors
        Field '最大频率' $(if ($cpu.MaxClockSpeed) { "$($cpu.MaxClockSpeed) MHz" })
        Field 'CPU ID' $cpu.ProcessorId
        Field '固件虚拟化状态' (YesNo $cpu.VirtualizationFirmwareEnabled)
    )
}) (Query-Errors @('Win32_Processor'))))
$sections.Add((Section 'memory' '内存' @(foreach ($stick in $memory) {
    $type = switch ([int]$stick.SMBIOSMemoryType) { 20 {'DDR'} 21 {'DDR2'} 24 {'DDR3'} 26 {'DDR4'} 30 {'LPDDR4'} 34 {'DDR5'} 35 {'LPDDR5'} default {'未提供 / SMBIOS ' + $stick.SMBIOSMemoryType} }
    Device ($stick.DeviceLocator + '/' + $stick.BankLabel) $(if ($stick.DeviceLocator) { $stick.DeviceLocator } else { $stick.Tag }) @(
        Field '容量' (Bytes $stick.Capacity)
        Field '类型' $type
        Field '制造商' $stick.Manufacturer
        Field '插槽 / 通道' $stick.BankLabel
        Field '料号（型号）' $stick.PartNumber
        Field '序列号' $stick.SerialNumber
        Field '标称传输速率' $(if ($stick.Speed) { "$($stick.Speed) MT/s" })
        Field '配置传输速率' $(if ($stick.ConfiguredClockSpeed) { "$($stick.ConfiguredClockSpeed) MT/s" })
    ) @() $stick.Capacity
}) (Query-Errors @('Win32_PhysicalMemory'))))
$sections.Add((Section 'board' '主板' @(foreach ($board in $boards) {
    Device $board.Tag $board.Product @(
        Field '制造商' $board.Manufacturer
        Field '型号' $board.Product
        Field '版本' $board.Version
        Field '序列号' $board.SerialNumber
    )
}) (Query-Errors @('Win32_BaseBoard'))))

$firmware = '未提供'
try {
    Add-Type -TypeDefinition 'using System.Runtime.InteropServices; public static class ToolboxFirmware { [DllImport("kernel32.dll")] public static extern bool GetFirmwareType(out uint type); }'
    [uint32]$firmwareType = 0
    if ([ToolboxFirmware]::GetFirmwareType([ref]$firmwareType)) { $firmware = switch ($firmwareType) { 1 {'Legacy BIOS'} 2 {'UEFI'} default {'未知'} } }
} catch { }
$secureBoot = '未提供（固件 / 权限不支持）'
try { $secureBoot = YesNo (Confirm-SecureBootUEFI -ErrorAction Stop) } catch { }
$sections.Add((Section 'bios' 'BIOS 与固件状态' @(foreach ($bios in $biosList) {
    Device $bios.Name $bios.SMBIOSBIOSVersion @(
        Field 'BIOS 制造商' $bios.Manufacturer
        Field 'BIOS 版本' $bios.SMBIOSBIOSVersion
        Field '发布日期' $(if ($bios.ReleaseDate) { $bios.ReleaseDate.ToString('yyyy-MM-dd') })
        Field 'BIOS 序列号' $bios.SerialNumber
        Field '启动模式' $firmware
        Field '安全启动 Secure Boot' $secureBoot
        Field '固件虚拟化状态（各 CPU）' @($cpus | ForEach-Object { $_.DeviceID + ': ' + (YesNo $_.VirtualizationFirmwareEnabled) })
    )
}) (Query-Errors @('Win32_BIOS')) '仅显示 Windows 可读取的固件信息与状态。XMP / EXPO、风扇、超频、启动顺序等厂商设置需要在 BIOS 中查看。虚拟化状态由 Windows 报告，启用 Hyper-V 等功能时可能不完整。'))

$storageDisks = @(Read-Cim 'MSFT_Disk' 'root/Microsoft/Windows/Storage')
$partitions = @(Read-Cim 'MSFT_Partition' 'root/Microsoft/Windows/Storage')
$volumes = @(Read-Cim 'MSFT_Volume' 'root/Microsoft/Windows/Storage')
$legacy = $script:queryErrors.ContainsKey('MSFT_Partition') -or ($partitions.Count -eq 0 -and $disks.Count -gt 0)
if ($legacy) {
    $partitions = @(Read-Cim 'Win32_DiskPartition')
    $volumes = @(Read-Cim 'Win32_LogicalDisk')
    $links = @(Read-Cim 'Win32_LogicalDiskToPartition')
}
$diskRows = @(foreach ($disk in $disks) {
    $storage = @($storageDisks | Where-Object { $_.Number -eq $disk.Index }) | Select-Object -First 1
    $ownPartitions = @($partitions | Where-Object { if ($legacy) { $_.DiskIndex -eq $disk.Index } else { $_.DiskNumber -eq $disk.Index } } | Sort-Object Offset,StartingOffset)
    $children = @(foreach ($partition in $ownPartitions) {
        $matched = @()
        if ($legacy) {
            $letters = @($links | Where-Object { $_.Antecedent.DeviceID -eq $partition.DeviceID } | ForEach-Object { $_.Dependent.DeviceID })
            $matched = @($volumes | Where-Object { $letters -contains $_.DeviceID })
        } else {
            $matched = @($volumes | Where-Object { ($partition.AccessPaths -contains $_.Path) -or ($partition.DriveLetter -and $_.DriveLetter -eq $partition.DriveLetter) })
        }
        $volume = $matched | Select-Object -First 1
        $letter = if ($legacy) { $volume.DeviceID } elseif ($partition.DriveLetter) { [string]$partition.DriveLetter + ':' } else { '' }
        $type = if ($legacy) { $partition.Type } else { Partition-Type $partition }
        $number = if ($legacy) { $partition.Index } else { $partition.PartitionNumber }
        $volumeSize = $volume.Size
        $free = if ($legacy) { $volume.FreeSpace } else { $volume.SizeRemaining }
        $fileSystem = if ($legacy) { $volume.FileSystem } else { $volume.FileSystem }
        $label = if ($legacy) { $volume.VolumeName } else { $volume.FileSystemLabel }
        $name = if ($letter) { "$letter $label".Trim() } else { "分区 $number · $type" }
        Device ("disk-$($disk.Index)-partition-$number") $name @(
            Field '盘符' $(if ($letter) { $letter } else { '无盘符' })
            Field '分区类型' $type
            Field '分区容量' (Bytes $partition.Size)
            Field '文件系统' $fileSystem
            Field '卷标' $label
            Field '卷容量' (Bytes $volumeSize)
            Field '卷可用空间' $(if ($null -ne $free) { if ($free -eq 0) { '0 MiB' } else { Bytes $free } })
            Field '挂载路径' $(if ($legacy) { $letter } else { $partition.AccessPaths })
        ) @() $partition.Size $free
    })
    $style = if ($storage) { switch ([int]$storage.PartitionStyle) { 0 {'RAW'} 1 {'MBR'} 2 {'GPT'} default {'未提供'} } } elseif ($legacy -and @($ownPartitions | Where-Object { $_.Type -like 'GPT:*' }).Count -gt 0) { 'GPT（兼容接口识别）' } else { '未提供' }
    Device ('disk-' + $disk.Index) ("磁盘 $($disk.Index) · $($disk.Model)") @(
        Field '型号' $disk.Model
        Field '容量' (Bytes $disk.Size)
        Field '序列号' $disk.SerialNumber
        Field '固件版本' $disk.FirmwareRevision
        Field '接口（系统报告）' $disk.InterfaceType
        Field '分区表' $style
        Field '设备路径' $disk.DeviceID
        Field '状态' $disk.Status
    ) $children $disk.Size
})
$diskErrors = if ($legacy) { Query-Errors @('Win32_DiskDrive','Win32_DiskPartition','Win32_LogicalDisk','Win32_LogicalDiskToPartition') } else { Query-Errors @('Win32_DiskDrive','MSFT_Disk','MSFT_Partition','MSFT_Volume') }
$diskNotice = '分区按物理磁盘归属显示。无盘符分区仍会列出；卷剩余容量不代表整块硬盘的未分配空间。'
if ($legacy) { $diskNotice += ' Storage 接口不可用或未返回分区，已使用兼容查询。' }
$sections.Add((Section 'disks' '磁盘与分区' $diskRows $diskErrors $diskNotice))

$sections.Add((Section 'network' '网络适配器' @(foreach ($network in $networks) {
    Device ([string]$network.Index) $network.Description @(
        Field 'MAC 地址' $network.MACAddress
        Field 'IP 地址' $network.IPAddress
        Field '子网' $network.IPSubnet
        Field '网关' $network.DefaultIPGateway
        Field 'DNS' $network.DNSServerSearchOrder
        Field 'DHCP' (YesNo $network.DHCPEnabled)
        Field 'DHCP 服务器' $network.DHCPServer
    )
}) (Query-Errors @('Win32_NetworkAdapterConfiguration'))))
$sections.Add((Section 'devices' '全部设备' @(foreach ($item in $allDevices) {
    Device $item.DeviceID $item.Name @(
        Field '设备类别' $item.PNPClass
        Field '制造商' $item.Manufacturer
        Field '状态' $item.Status
        Field '设备管理器错误码' $item.ConfigManagerErrorCode
        Field 'PNP 设备标识' $item.PNPDeviceID
        Field '服务 / 驱动' $item.Service
    )
}) (Query-Errors @('Win32_PnPEntity')) '列出 Windows PnP 枚举返回的全部设备，包含系统、USB、显示、音频、网络及虚拟设备。'))

$identifiers = @(
    foreach ($product in $products) { Device $product.IdentifyingNumber ('整机 · ' + $product.Name) @((Field '设备型号' $product.Name); (Field '整机序列号' $product.IdentifyingNumber); (Field '系统 UUID' $product.UUID)) }
    foreach ($cpu in $cpus) { Device $cpu.DeviceID ('CPU · ' + $cpu.Name) @((Field 'CPU ID' $cpu.ProcessorId)) }
    foreach ($bios in $biosList) { Device $bios.Name ('BIOS · ' + $bios.SMBIOSBIOSVersion) @((Field 'BIOS 序列号' $bios.SerialNumber)) }
    foreach ($board in $boards) { Device $board.Tag ('主板 · ' + $board.Product) @((Field '主板序列号' $board.SerialNumber)) }
    foreach ($disk in $disks) { Device $disk.DeviceID ('硬盘 · ' + $disk.Model) @((Field '硬盘序列号' $disk.SerialNumber)) }
    foreach ($stick in $memory) { Device $stick.Tag ('内存 · ' + $stick.DeviceLocator) @((Field '内存序列号' $stick.SerialNumber); (Field '内存料号' $stick.PartNumber)) }
    foreach ($network in $networks) { Device ([string]$network.Index) ('网卡 · ' + $network.Description) @((Field 'MAC 地址' $network.MACAddress); (Field 'IP 地址' $network.IPAddress)) }
)
$duid = $null
try {
    $duidBytes = (Get-ItemProperty -LiteralPath 'HKLM:\SYSTEM\CurrentControlSet\Services\Tcpip6\Parameters' -Name Dhcpv6DUID -ErrorAction Stop).Dhcpv6DUID
    if ($duidBytes) { $duid = [BitConverter]::ToString($duidBytes) }
} catch { }
$identifiers += Device 'dhcpv6-duid' '网络 · DHCPv6' @((Field 'DHCPv6 DUID' $duid))
$sections.Add((Section 'identifiers' '机器码与设备标识' $identifiers (Query-Errors @('Win32_ComputerSystemProduct','Win32_Processor','Win32_BIOS','Win32_BaseBoard','Win32_DiskDrive','Win32_PhysicalMemory','Win32_NetworkAdapterConfiguration')) '均为系统或设备固件报告的原始标识；CPU ID 不是保证唯一的芯片序列号，内存料号不是生产时间。'))

@{ CollectedAt = [DateTimeOffset]::Now.ToString('o'); Sections = @($sections.ToArray()) } | ConvertTo-Json -Depth 12 -Compress
