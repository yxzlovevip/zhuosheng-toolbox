# 内部维护工具箱 Implementation Plan

> **For agentic workers:** Use superpowers:executing-plans to implement task-by-task in this session. 用户已明确“直接制作”，不再增加方案批准轮次。

**Goal:** 交付管理员运行、每次口令登录、分类说明、完整性校验和签名更新支持的 Windows 安装 EXE。
**Architecture:** WPF 主界面、可独立测试的 Core、.NET Framework 安装器。安装器包含完整压缩包，自动更新下载并验证同一安装器；主程序及工具安装于 Program Files 下。
**Tech Stack:** .NET 10 / C# / WPF；安装器使用 Windows 自带 .NET Framework 编译器；无运行时第三方 NuGet 依赖。
**Spec:** docs/superpowers/specs/2026-10-21-internal-toolbox-design.md，用户新增授权关闭 UAC 工具。

## Global Constraints

- 口令 [本机访问口令，公开版省略]，每次启动要求输入；生产 EXE 清单为 requireAdministrator。
- 电脑信息查询不做；关闭 UAC 按真实名称接入并确认；不明 COM 不接入。浏览器 BAT 按用户 2026-10-21 新增要求保留所有功能，原字节封装为独立 EXE，准确披露收藏夹等删除范围并确认启动；模块已登记于 `assets/catalog.json`。
- 不执行当前目录的第三方工具来做测试，不修改当前电脑 UAC。
- 未配置服务器必须显示“尚未配置”，不得伪装已是最新版。
- 保留原始工具文件；源码、构建依赖和成品分开。

## Review Focus

- 解锁前及权限不足时，启动服务必须拒绝调用。
- 路径穿越、重解析点、丢失和被替换工具不能运行。
- 取消影响确认不得执行工具，注册表工具不得静默导入。
- 断网、无效签名、跨域重定向和损坏下载不能执行更新。
- 更新不能在工具运行时覆盖；安装失败恢复先前版本，保留有用错误。

### Task 1: Core 与测试

Files: src/Toolbox.Core/{Toolbox.Core.csproj,AuthGate.cs,ToolCatalog.cs,ToolLauncher.cs,UpdateService.cs}; tests/Toolbox.Tests/{Toolbox.Tests.csproj,Program.cs}。
Interfaces: AuthGate.TryUnlock(string), IsUnlocked；ToolCatalog.Load(string), Search(string,string)；ToolLauncher.Validate(ToolItem), LaunchAsync(ToolItem)；UpdateService.CheckAsync(), DownloadAsync(UpdateInfo)。
- [ ] 创建断言式控制台测试：口令正确/错误/节流，未登录启动拒绝，路径越界和哈希不符拒绝，分类搜索，签名清单与版本、坏下载。
- [ ] 执行测试，观察预期失败；实现上述接口后重跑至全部通过。

### Task 2: 工具清单与 WPF 界面

Files: assets/catalog.json; src/Toolbox.App/{App.xaml,App.xaml.cs,MainWindow.xaml,MainWindow.xaml.cs,app.manifest,Toolbox.App.csproj}; tests/Toolbox.UiTests。
- [ ] 以现有文件生成固定哈希清单与逐项中文操作说明，18 项开放，3 项信息查询延后、2 项不开放。
- [ ] 实现密码页、侧栏分类、搜索、工具详情、高影响确认、日志提示、说明页和更新页。
- [ ] 测试专用宿主展示同一窗口，核验密码、搜索、分类、取消确认、锁定，保存界面图；发布主程序无测试权限开关。

### Task 3: 更新与安装器

Files: src/Toolbox.Setup/{Program.cs,app.manifest}; scripts/{build.ps1,New-UpdateRelease.ps1,New-UpdateKey.ps1}; assets/update-settings.json。
- [ ] 测试安装器 Zip 路径边界、覆盖失败回滚和升级目录约束，再实现解包、版本目录发布、快捷方式、卸载。
- [ ] 更新在登录后检查，支持自动检查、手动检查、下载进度、签名与哈希验证、确认安装重启。
- [ ] 本轮默认更新地址为空，随附配置与签名发布流程；无需提供密码或发布凭据。

### Task 4: 构建与最终验证

Files: dist/内部维护工具箱-安装-v1.0.0.exe; docs/使用说明.md; docs/更新发布说明.md; docs/验证记录.md。
- [ ] 完整测试、Release 自包含发布、安装器资源与清单/工具哈希核验。
- [ ] 原生界面渲染检查；隔离工作目录内测试安装文件展开与回滚，避免实际运行维护工具。
- [ ] 一次独立代码审核，修复重要问题，再执行受影响测试并重建。
- [ ] 交付安装 EXE、使用说明、更新部署材料，明确尚未完成生产更新和目标机器验收。

## Execution ledger

- 开始：用户要求直接制作；本轮原地创建新源码，不创建 Git 工作树（目录原本不是仓库）。
- 决定：更新地址未提供，不擅自发布官网；提供可验签更新客户端与发布工具，显示真实未配置状态。
- 决定：增加关闭 UAC 注册表入口，明确重启与影响；测试只验证调用参数，不导入注册表。
