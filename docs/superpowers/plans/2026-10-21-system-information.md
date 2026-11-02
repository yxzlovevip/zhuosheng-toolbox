# System Information Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement this plan task-by-task. 用户已要求在当前会话执行。

**Goal:** 在现有软件内完成只读系统、机器码和显卡查询。
**Architecture:** 固定内嵌 PowerShell/CIM 查询输出分组 JSON；C# 服务约束进程生命周期并解析；宿主鉴权后提供两个固定操作，React 渲染分组设备与磁盘子分区。
**Tech Stack:** .NET 10 / WPF / WebView2 / Windows PowerShell 5.1 / React 19。
**Spec:** docs/superpowers/specs/2026-10-21-system-information-design.md

## Global Constraints
- Windows 10/11 x64；查询只读，不执行原 BAT 的附带操作。
- 不改变现有工具分类和启动确认；不采集远端设备，不上传标识。
- 没有 Git 仓库，直接使用现有工作区，不执行 Git 工作树、提交或依赖 Git 的任务脚本。

## Review Focus
- 多设备不得被压缩为一个字符串；空数组保留空数组。
- 无盘符的分区仍属于正确硬盘；分区容量与卷容量分别标注。
- BIOS、显卡不支持或无驱动时不制造 UUID/显存值。
- 锁定和超时只能结束本次查询拥有的子进程。
- 失败刷新时旧数据有明确过期提示，卸载页面后异步结果不能重新出现。

### Task 1: 查询服务和宿主接入
**Files:** src/Toolbox.App/SystemInfoModels.cs, SystemInfoReader.cs, WindowsQueryRunner.cs, Queries/SystemInformation.ps1, WorkbenchController.cs, AppServices.cs, Toolbox.App.csproj; tests/Toolbox.SystemInfoTests; tests/Toolbox.HostTests/Program.cs。
**Interfaces:** SystemInfoReader.ReadAsync(CancellationToken), ReadGraphicsAsync(CancellationToken) 返回 SystemInfoReport；宿主 getSystemInfo / queryGraphicsInfo。
- [x] 新增测试：多实例、子分区、空数据、损坏响应、实际进程超时和取消；先运行失败。
- [x] 实现固定查询、超时进程、解析和宿主防重复/取消。
- [x] 运行服务、宿主测试及真实 Windows 查询，确认多实例和分区结果。

### Task 2: 系统信息页面
**Files:** frontend/src/SystemInfoPage.jsx, system-info.css, App.jsx, icons.jsx; frontend/tests/system-info.spec.js 和测试夹具。
**Interfaces:** 调用 getSystemInfo / queryGraphicsInfo，读取 CollectedAt、Sections、Devices、Fields、Children。
- [x] 先写并运行页面交互测试，确认缺少系统信息入口导致失败。
- [x] 实现六个页签、刷新、复制、设备筛选、加载和错误状态，沿用主题。
- [x] 验证多设备、隐藏分区、GPU 不支持、错误重试、主题、缩窄窗口和锁定。

### Task 3: 验证和交付
**Files:** tests/Toolbox.WebTests/Program.cs, docs/使用说明.md, docs/构建说明.md, frontend/src/UtilityPages.jsx。
- [x] 实机查询并通过 WebView2 验证显示，截图检查可读性。
- [x] 运行现有自动化测试、生产构建和安装包校验。
- [x] 检查实现与设计的遗漏，交付可运行安装包并说明兼容性边界。

## 最终审查

独立只读审查发现两项 P2，均已用失败后通过的回归测试修正：
- 原始 MSFT_Partition 没有整数 Type 属性，改按 GptType / MbrType 解码，测试 EFI、空盘符和零剩余空间。
- 页面卸载后查询仍在进行，重新进入可能被宿主拒绝；改为按登录会话复用进行中的请求，不同类型查询顺序执行。浏览器与原生宿主分别验证离开再返回。

未发现须延后处理的轻微问题；没有仓库，因此不创建 Git 提交，测试和审查记录保留在当前工作区文档。

## 验证结果与交付

- 系统查询测试 10/10（包括真实 Windows 查询、Storage 夹具、超时、取消）。
- 宿主控制器 16/16；系统信息原生 WebView2 5/5；现有原生 WebView2 4/4。
- 完整 Playwright 回归 14/14，覆盖 1380×860 与 1000×700、主题、复制、筛选、错误刷新、查询中切换页面和锁定。
- 核心回归 27/27，安装引擎 15/15，安装包校验 4/4（39 个文件全部匹配清单）。
- 实机读取 2 条内存、2 个显卡适配器、5 块磁盘、13 个分区和 240 个 PnP 设备。该机器的 Win32 磁盘容量字段为空，页面如实标注“未提供”，不据分区总和推断硬盘容量。
- 交付 `dist/内部工具箱-安装-v1.1.1.exe`，230812160 字节。
- SHA-256：`C994903B664BC0C8D5453C5CEB83A5336EC150CE5058100FE0BD7B3A96ABF925`。
