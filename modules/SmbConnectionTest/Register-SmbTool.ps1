param([string]$ProjectRoot = (Join-Path $PSScriptRoot '../..'))
$ErrorActionPreference = 'Stop'
$smbCatalogPath = Join-Path $ProjectRoot 'assets/catalog.json'
$smbExePath = Join-Path $ProjectRoot '工具/网络/SMB连接测试.exe'
if (-not (Test-Path -LiteralPath $smbCatalogPath)) { throw '主程序工具清单尚未生成；请生成后重新运行本脚本。' }
$smbEntry = [ordered]@{
    Id = 'smb-connection-test'
    Name = 'SMB 连接测试'
    Category = '网络工具'
    RelativePath = '工具/网络/SMB连接测试.exe'
    Sha256 = (Get-FileHash -LiteralPath $smbExePath -Algorithm SHA256).Hash
    Summary = '一键检查 NAS 地址、端口、SMB 协商及共享目录访问，定位连接失败步骤。'
    Purpose = 'NAS 无法连接、端口能通但播放器或 Windows 仍无法访问共享时使用。'
    Preparation = '准备 NAS 的主机名或 IP、SMB 端口（默认 445）；需要检查共享权限时填写共享名。共享检查沿用当前程序账户已有的 Windows 凭据，不输入密码。'
    Steps = @('打开工具，输入 NAS 地址和端口；支持 IP、主机名、SMB URL 和 Windows 共享路径。', '可选填共享名，例如 media 或 电影；粘贴地址内的共享名会自动使用。', '点击“一键测试”，逐项查看地址解析、端口连接、SMB 协商和共享访问结果；需要时可取消。', '失败时按中文提示检查对应设置；点击“复制检测报告”可复制分步结果。')
    Impact = '只读检测，不修改 NAS 文件、Windows 设置或凭据，不保存密码。会向指定地址发起连接；共享检查只读取第一条目录项，不保存文件名。管理员运行的凭据环境可能与桌面账户不同。'
    Result = '端口通过不代表 SMB 可用；SMB 协商通过不代表账号、共享或文件读写可用。共享目录可读取也不代表文件内容和写入权限已验证。自定义端口仅检测协议，不转而访问 445。'
    Evidence = '自研模块；已验证 SMB 2.0.2—3.1.1 协商解析、本地 TCP 故障/取消与窗口交互。尚未连接用户真实 NAS 验收。'
    Risk = '只读检测'
    NeedsConfirmation = $false
    Restart = '不需要重启；需要 Windows .NET Framework 4.8（当前开发电脑已有）'
}
$smbOriginal = [IO.File]::ReadAllText($smbCatalogPath)
$smbItems = @($smbOriginal | ConvertFrom-Json)
$smbMatches = @($smbItems | Where-Object { $_.Id -eq $smbEntry.Id })
if ($smbMatches.Count -gt 1) { throw 'SMB 工具标识重复，未改动清单。' }
if ($smbMatches.Count -eq 0) {
    $smbEntryText = $smbEntry | ConvertTo-Json -Depth 8
    $smbPrefix = $smbOriginal.TrimEnd()
    if (-not $smbPrefix.EndsWith(']')) { throw '工具清单格式异常。' }
    $smbPrefix = $smbPrefix.Substring(0, $smbPrefix.Length - 1).TrimEnd()
    $smbNew = $smbPrefix + $(if ($smbItems.Count -gt 0) { ',' } else { '' }) + "`n" + $smbEntryText + "`n]`n"
} else {
    $smbNewItems = @($smbItems | ForEach-Object { if ($_.Id -eq $smbEntry.Id) { [pscustomobject]$smbEntry } else { $_ } })
    $smbNew = ConvertTo-Json -InputObject $smbNewItems -Depth 12
}
if ([IO.File]::ReadAllText($smbCatalogPath) -cne $smbOriginal) { throw '其他构建正在更新工具清单，请稍后重试。' }
[IO.File]::WriteAllText($smbCatalogPath, $smbNew, [Text.UTF8Encoding]::new($false))
Write-Output '已登记：网络工具 → SMB 连接测试（含当前 EXE 的 SHA-256）。'
