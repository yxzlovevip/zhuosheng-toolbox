# SMB 连接测试

独立 Windows 工具，入口位于主项目 `assets/catalog.json` 的 `网络工具` 分类，标识为 `smb-connection-test`。工具的实际文件为 `工具/网络/SMB连接测试.exe`，清单路径相对于主程序的 `tools` 目录。主程序以既有的管理员、登录和 SHA-256 校验流程启动此模块。

## 构建与测试

在 Windows PowerShell 7 中执行本目录的 `build.ps1`，使用 Windows 自带的 .NET Framework C# 编译器生成 EXE，不需要安装额外 SDK 或修改全局环境。当前电脑已安装 .NET Framework 4.8。

`build.ps1` 会同步当前 EXE 的 SHA-256 和中文说明至主工具清单；若重新生成了整份清单，需再次执行 `Register-SmbTool.ps1`。脚本仅添加或更新本模块，保留其他工具条目。

- `test.ps1`：地址处理、SMB 报文校验、本地 TCP 分片/关闭/超时/取消，以及分层报告。
- `test-ui.ps1`：真实窗口控件的输入校验、开始/取消/重试、最终结果、复制报告和窄窗口截图；测试后恢复剪贴板。测试图片位于 `test-output`。
- `test-share.ps1`：工作进程输入限制、拒绝访问、8 秒超时及取消后的子进程退出。

命令行诊断：`SMB连接测试.exe --diagnose nas.local 445 media`。GUI EXE 的控制台输出需由调用方重定向。退出码 0 表示已执行项目没有失败，1 表示检测失败，87 表示参数错误；0 不代表未填写的共享已验证，调用方应读取完整报告。

`--share-check` 是内部工作进程参数，使用 Base64 编码的 UNC 路径；不接收用户名或密码。独立进程隔离 Windows 共享目录调用，以便超时或取消时终止。不会修改驱动器映射或主动创建持久凭据。

## 实现边界

协议检测仅发送 SMB2 NEGOTIATE，提出 2.0.2、2.1、3.0、3.0.2、3.1.1；3.1.1 携带 SHA-512 预认证上下文。校验响应签名标识、帧长度、消息号、命令、状态、版本、安全缓冲区和预认证上下文。它不建立认证会话或验证加密，不读取文件。

共享检测通过 Windows 现有凭据调用只读目录枚举，成功只代表能读取目录列表。非标准端口跳过 Windows 共享检查，并说明不会另测 445。程序启动不主动连接历史 NAS 地址，不保存配置或日志。

协议依据：[Microsoft SMB2 NEGOTIATE 请求](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-smb2/e14db7ff-763a-4263-8b10-0c3944f52fc5)、[响应](https://learn.microsoft.com/en-us/openspecs/windows_protocols/ms-smb2/63abf97c-0d09-47e2-88d6-6bfa552949a5)。

已在开发电脑本机 Windows SMB 服务实际协商到 SMB 3.1.1。真实 NAS、用户账号共享权限以及安装版主程序的最终点击验收仍需分别核验。
