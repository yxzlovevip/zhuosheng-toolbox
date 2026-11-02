$ErrorActionPreference='Stop'
$czRoot=Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$czCatalogPath=Join-Path $czRoot 'assets/catalog.json'
$czOriginal=[IO.File]::ReadAllText($czCatalogPath)
$czItems=@($czOriginal | ConvertFrom-Json)
$czMatches=@($czItems | Where-Object Id -EQ 'ace-virtualization')
if($czMatches.Count -ne 1){throw 'ACE 入口不存在或重复，未修改。'}
$czEntry=$czMatches[0]
if($czEntry.RelativePath -ne '工具/系统设置/ACE虚拟化环境检测修复.exe'){throw 'ACE 入口路径已改变，需核对当前工程。'}
$czDestination=Join-Path $czRoot $czEntry.RelativePath
$czBefore=(Get-FileHash -LiteralPath $czDestination -Algorithm SHA256).Hash
if($czBefore -ne $czEntry.Sha256){throw 'ACE 文件与当前清单不一致，未覆盖。'}
$czCandidate=Join-Path $PSScriptRoot 'build-output/ACE虚拟化环境检测修复.exe'
$czNewHash=(Get-FileHash -LiteralPath $czCandidate -Algorithm SHA256).Hash
$czBackup=Join-Path $czRoot ('.build/diagnostics/ace-wrapper-before-'+[guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $czBackup | Out-Null
Copy-Item -LiteralPath $czCatalogPath -Destination (Join-Path $czBackup 'catalog.json')
Copy-Item -LiteralPath $czDestination -Destination (Join-Path $czBackup 'previous-ace.exe')
$czEntry.Summary='检测 DMA 保护、CPU 虚拟化、Hypervisor 和 VBS；保留原三种系统修复。'
$czEntry.Purpose='排查 ACE 虚拟化环境提示，按原工具功能选择 Hypervisor、VBS 或组合修复。'
$czEntry.Preparation='保存工作，阅读工具内 BIOS、Docker/WSL、账户/PIN 及 UEFI 提示；确保能用账户密码登录。'
$czEntry.Steps=@('打开后自动进行四项检测，不执行修复。','查看检测结果与原有 BIOS/UEFI 操作建议。','按需选择组合修复、仅关闭 Hypervisor 或仅关闭 VBS；点击执行。','保存工作后手动重启，按实际 UEFI 提示操作，再重新检测并验证目标应用。')
$czEntry.Impact='保留原 DG Readiness -Disable 全部操作，包括相关注册表、代码完整性策略、Hyper-V 组件及 EFI/BCD 引导配置处理；并保留原有 WindowsHello 设置处理。可能影响 Docker、WSL、虚拟机、Windows Hello 和系统保护。封装不自动重启，执行策略仅对子进程有效；无自动撤销。'
$czEntry.Result='显示原始输出、错误和持久日志；脚本退出不代表修复生效。重启并重新检测，最终以 Windows 状态和目标应用实际表现为准。'
$czEntry.Evidence='自有封装源码；静态提取原 3 个 BAT，保留四项检测、三种修复及 BIOS/UEFI 提示；所用 DG Readiness 3.6 的 6 个文件与微软官方 ZIP 逐字节一致，PS1 微软签名有效。已完成替身测试与本机真实只读检测；未在本机执行真实修复或重启验收，非完整杀毒扫描。'
$czEntry.Restart='修复后可能需要重启并在 UEFI 界面按 F3/Enter 确认；封装不自动重启。'
$czEntry.Sha256=$czNewHash
$czNewText=ConvertTo-Json -InputObject $czItems -Depth 12
if([IO.File]::ReadAllText($czCatalogPath) -cne $czOriginal){throw '工具清单被其他工作更新，未覆盖。'}
if((Get-FileHash -LiteralPath $czDestination -Algorithm SHA256).Hash -ne $czBefore){throw 'ACE 文件被其他工作更新，未覆盖。'}
Copy-Item -LiteralPath $czCandidate -Destination $czDestination -Force
try{[IO.File]::WriteAllText($czCatalogPath,$czNewText+"`n",[Text.UTF8Encoding]::new($false))}
catch{Copy-Item -LiteralPath (Join-Path $czBackup 'previous-ace.exe') -Destination $czDestination -Force;throw}
[ordered]@{RegisteredAt=(Get-Date).ToString('o');Id=$czEntry.Id;Path=$czDestination;PreviousHash=$czBefore;Sha256=$czNewHash;Backup=$czBackup;InstallerRebuilt=$false;OnlinePublished=$false}|ConvertTo-Json|Set-Content -LiteralPath (Join-Path $PSScriptRoot 'test-output/registration.json') -Encoding utf8
Write-Output ('已替换原 ACE 入口文件并更新完整性校验；保留 '+$czItems.Count+' 项工具。')
