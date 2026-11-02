# 卓盛工具箱

面向 Windows 10/11 x64 的电脑维护工具箱。使用 .NET 10、WPF 和 WebView2 承载 React 界面，集中提供本机系统信息、维护工具入口、软件网站导航及签名更新。

本仓库保存项目源码、配置清单、界面资源、构建脚本和测试。第三方工具载荷及安装包单独管理，不随源码仓库分发；克隆仓库不等于获得可直接使用的完整离线安装包。

## 功能概览

- **工具工作台**：分类浏览、用途别名搜索、收藏、详情、文件完整性检查及工具运行状态。
- **系统信息**：只读采集本机软硬件信息，提供会话缓存、采集时间和手动刷新。
- **软件合集**：按分类提供软件网站入口、来源信息和图标，不自动安装软件。
- **访问与界面**：访问验证、会话锁定、深浅主题、键盘操作及减少动画支持。
- **维护模块**：SMB 连接诊断、ACE 虚拟化环境检测修复、浏览器清理封装、Windows 10 优化和开源工具适配器。
- **更新与安装**：验证 RSA 签名清单、安装包大小和 SHA-256；安装器支持流式解压、覆盖安装、配置保留及事务回滚。

当前项目配置版本为 **2.0.0**。仓库中的历史发布和验证记录仅说明对应时期的操作，不代表当前环境、线上服务或所有功能已经重新验证。

## 技术结构

```text
frontend/src → WebView2 消息桥 → WorkbenchController → Core / 本机服务
                                                     ↓
                                              工具启动与进程组管理
```

| 路径 | 职责 |
| --- | --- |
| `frontend/` | React + Vite 界面、消息桥客户端及 Playwright 测试 |
| `src/Toolbox.App/` | WPF/WebView2 宿主、本机信息查询、桥接控制器 |
| `src/Toolbox.Core/` | 工具目录、访问验证、载荷校验、进程生命周期和更新 |
| `src/Toolbox.Setup/` | Windows 安装/卸载、固实载荷展开及安装事务 |
| `src/Shared/` | 安装路径与权限策略 |
| `modules/` | 维护模块源码、构建与定向验证入口 |
| `assets/` | 工具/软件目录、图标、更新公钥及公开更新配置 |
| `scripts/` | 构建、资源处理、开源工具获取和更新发布脚本 |
| `tests/` | C#、PowerShell 和 Node 定向测试 |
| `docs/` | 使用说明、构建说明、设计和历史变更记录 |

## 本地开发

需要 Windows x64、Node.js、pnpm、.NET 10 SDK；运行桌面界面还需要 Microsoft Edge WebView2 Runtime。测试通常使用本机 Edge。完整安装包构建还依赖 Windows .NET Framework 编译器。

```powershell
pnpm --dir frontend install --frozen-lockfile
pnpm --dir frontend build
```

前端输出到 `assets/web/`。`pnpm --dir frontend dev` 可用于界面开发；真实本机操作需要 WebView2 宿主消息桥。

```powershell
dotnet restore src/Toolbox.App/Toolbox.App.csproj
dotnet run --project src/Toolbox.App/Toolbox.App.csproj
```

主程序会申请管理员权限。工具启动需要另行准备并审核清单对应的 `工具/`、`重装/` 载荷；缺少载荷时相应功能不可用。

现有完整构建脚本使用 `.build/dotnet/dotnet.exe`，不会自动改用全局 SDK。准备该路径下的 .NET 10 SDK、全部工具及独立依赖后，才执行：

```powershell
./scripts/build.ps1 -Version 2.0.0
```

完整构建会重建适配器、更新图标/清单、校验工具并打包，部分步骤可能联网。详见 [构建说明](docs/构建说明.md)；该文档中的旧版本命令仅作历史示例，以当前源码和参数为准。

## 验证入口

C# 测试采用可执行程序中的断言，应使用 `dotnet run`，而不是直接把所有项目交给 `dotnet test`。

```powershell
dotnet run --project tests/Toolbox.Tests
dotnet run --project tests/Toolbox.SetupTests
dotnet run --project tests/Toolbox.UpdateFlowTests
node --test tests/SoftwareCollection/source-metadata.test.mjs
pnpm --dir frontend build
pnpm --dir frontend exec playwright test
```

按改动选择相关验证。部分集成测试需要工具载荷、安装包、Windows 桌面或管理员权限。浏览器测试使用模拟宿主桥，通过结果不能替代原生宿主与真实安装验证。维护工具、清理、系统修复和实际安装测试具有副作用，运行前应阅读相应脚本和模块说明。

涉及真实宿主登录的 C# 测试从 `CZ_TEST_PASSWORD` 环境变量读取本机访问口令；运行前请在当前测试进程中配置，不要提交到仓库或命令日志。前端模拟桥和 Playwright 测试使用独立的 `ui-test-only-password` 测试值，该值不能用于真实宿主登录。

## 分发与配置边界

- `.private/`、更新私钥、环境凭据、本地 SDK、依赖缓存、测试输出和构建产物不进入仓库。
- `assets/update-public.pem` 是客户端验签公钥，可以公开；签名发布需要单独保管的私钥，不能随意更换已有客户端使用的密钥。
- 完整离线包需要单独准备第三方工具及其依赖；软件网站导航和缺少 WebView2 时的运行库引导仍需联网。
- 本地访问验证用于工具箱会话控制，不作为服务器端身份认证或机密存储方案。
- 第三方工具、脚本和图标的权利归各自权利人；公开本仓库不改变第三方资源的许可条件。本次未为整个项目新增开源许可证。

## 文档入口

- [使用说明](docs/使用说明.md)
- [构建说明](docs/构建说明.md)
- [更新发布说明](docs/更新发布说明.md)
- [软件合集接入说明](docs/软件合集接入说明.md)
- [项目上下文入口](docs/PROJECT_CONTEXT.md)

`AGENTS.md` 中的中央上下文路径用于原工作站的多项目协作；独立克隆仓库时可从本 README 和 `docs/` 开始。
