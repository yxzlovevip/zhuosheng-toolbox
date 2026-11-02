# ACE 虚拟化环境检测修复：原功能脚本封装

本模块保留原 ACE 工具的实际功能，替换原二进制启动器。默认只检测，修复由用户点击执行。不是腾讯官方程序。

## 功能对照

| 原功能 | 本封装 |
| --- | --- |
| 系统版本、架构、处理器信息 | 检测输出保留 |
| 内核 DMA 保护检测 | 保留 msinfo32 系统安全报告检查，支持中英文状态 |
| CPU 虚拟化检测 | 保留处理器监控扩展、固件虚拟化状态查询 |
| Hypervisor 状态检测 | 保留 bcdedit /enum 查询，区分 Auto、Off、未配置、未知 |
| VBS 状态检测 | 保留；增加 CIM 查询与“已启用但未运行”的区分 |
| 按检测结果给出系统修复建议 | 保留；未知状态不伪装成正常 |
| 仅关闭 Hypervisor | Disable_hyp.bat，仍执行 bcdedit /set hypervisorlaunchtype off |
| 仅关闭 VBS | Disable_vbs.bat，保留原 DG_Readiness -Disable 及两处条件注册表操作 |
| Hypervisor + VBS 组合修复 | Enable_VMX.bat，保留两项操作；名称沿用资源名，界面显示实际作用 |
| BIOS 中的 VT-d/IOMMU、VMX/SVM 指引 | 保留，不声称 BAT 能开启 BIOS 选项 |
| 本地账户、PIN 准备提示 | 保留 |
| UEFI 中 Credential Guard/VBS 的 F3、Enter 提示 | 保留 |

DG_Readiness_Tool_v3.6.ps1 使用微软官方下载的原始字节，数字签名有效；6 个配套文件均与提取到的文件逐字节一致。没有删掉其 `-Disable` 分支中的注册表、代码完整性策略文件、Hyper-V 组件、EFI/BCD 操作。

## 已明确的封装层差异

- 原 BAT 的 `Set-ExecutionPolicy RemoteSigned -Scope CurrentUser` 改为仅子进程的 `-ExecutionPolicy Bypass`，遵循本次用户选定的方案；不永久修改账户执行策略。系统修复功能和命令作用范围保留。
- 管理员权限由 EXE 清单申请，替代 BAT 内重复自提权；避免新进程退出后上层误判完成。
- 原程序用退出码 1 表示需要重启；封装改用 3010，实际失败单独展示，不把失败描述成完成。
- 输出写入界面和持久日志，保留错误；不自动重启，也不把“脚本退出”当作“修复生效”。
- 组合修复遇到前一步失败会报告失败并停止后续步骤，避免继续改变系统后误报完成；已执行的步骤没有自动回滚。
- 原始 BAT 字节另存于 `originals/*.bat.txt` 供审查。实际可构建源码在 `payload/`，并非将旧 EXE 改名或套壳继续运行。

## 来源与适用范围

- 原功能依据：项目提供的还原版 ACE 文件，SHA-256 `68720E95535E97DC8157B2CBF300DC79E930EDBA801183F5B6A604E9EEC8AC45`。静态解析 PyInstaller 目录和 Python 3.13 字节码，没有运行原程序。
- 原主程序标注作者“修理月亮”，并含未经授权不要商用的提示；本次没有将其原二进制启动器、Python 运行库和第三方 DLL 带入新封装，也没有对外发布。对外分发前还需核对原脚本和依赖的使用条件。
- [微软 Device Guard and Credential Guard hardware readiness tool 3.6](https://www.microsoft.com/en-us/download/details.aspx?id=53337)。官方适用系统列为 Windows 10 / Server 2016；不能仅凭在 Windows 11 上启动或检测成功，就宣称修复与重启流程兼容。
- 微软 ZIP SHA-256：`B351BE8E77C8D7994D97B8B9E60EF310EA5873A336FB8D3B5B009379F29BC6FC`。
- 内嵌微软 PS1 SHA-256：`C248C7EECF637E9CFEC4353B55336542A1AC13FBB5E58EBD622E4717CFFC09C7`。

## 构建、接入和验证

```powershell
./modules/AceVirtualization/test.ps1
./modules/AceVirtualization/build.ps1
./modules/AceVirtualization/register.ps1
```

构建输出 `build-output/ACE虚拟化环境检测修复.exe`；登记时保留原入口标识、名称和路径，并备份旧文件及清单到 `.build/diagnostics/ace-wrapper-before-*`。不改变其他 26 项工具，不切换线上更新，也不重写其他工作生成的安装器。

生产 EXE 内嵌 10 个源码/依赖文件，提取到随机受限临时目录，逐文件检查 SHA-256 并持有只读锁；执行结束后仅清理本次已知文件和空目录。调用固定 Windows 系统程序，不接收任意命令行修复入口。`--check` 只进行检测。

日志：`%LOCALAPPDATA%/CzToolbox/AceVirtualization/Logs/`。运行脚本期间不允许通过窗口正常关闭来中断操作；从工具箱退出时仍遵循工具箱现有进程组规则。

测试执行实际封装与实际修复 BAT，但所有系统命令均由无修改替身替代；不在本机执行真实 DG/VBS/Hypervisor/EFI 修改。真实检测单独验证，不等同于真实修复或重启验收。检查 EXERESX/KBHKS 资源不等同于完整杀毒扫描。
