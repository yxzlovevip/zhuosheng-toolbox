using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Cz.NetworkTools
{
    public sealed class CheckResult
    {
        public int Index;
        public string Name, State, Detail;
        public long Milliseconds;
    }
    public sealed class DiagnosticReport
    {
        public SmbTarget Target;
        public DateTime StartedAt = DateTime.Now;
        public readonly List<CheckResult> Steps = new List<CheckResult>();
        public string Summary;
        public string ToText()
        {
            var b = new StringBuilder();
            b.AppendLine("SMB 连接测试 v1.0.0");
            b.AppendLine("时间：" + StartedAt.ToString("yyyy-MM-dd HH:mm:ss zzz"));
            b.AppendLine("目标：" + Target.Endpoint);
            b.AppendLine("共享：" + (Target.Share.Length == 0 ? "未填写" : Target.Share));
            b.AppendLine("结论：" + Summary); b.AppendLine();
            foreach (CheckResult step in Steps)
                b.AppendLine((step.Index + 1) + ". " + step.Name + " [" + step.State + "] " + step.Milliseconds + " ms\r\n" + step.Detail);
            b.AppendLine();
            b.AppendLine("范围：SMB 协商不验证账号登录、文件读写或加密会话。共享检查使用当前程序账户的 Windows 凭据，只尝试读取第一条目录项，不记录文件名、不写入文件。");
            b.AppendLine("未完成的检测、文件内容与写入权限尚未验证。本工具不更改 NAS 或 Windows 设置，不保存密码。");
            return b.ToString();
        }
    }
    public sealed class DiagnosticRunner
    {
        private static readonly string[] Names = { "地址解析", "端口连接", "SMB 协商", "共享访问" };
        public DiagnosticReport Run(SmbTarget target, Action<CheckResult> progress, CancellationToken token)
        {
            var report = new DiagnosticReport { Target = target };
            IPAddress[] addresses = null;
            TcpClient connection = null;
            bool stopped = false, cancelled = false;
            try
            {
                for (int i = 0; i < 4; i++)
                {
                    var step = new CheckResult { Index = i, Name = Names[i], State = "未检测", Detail = "前序检测未完成，已跳过。" };
                    var clock = Stopwatch.StartNew();
                    if (!stopped)
                    {
                        if (progress != null) progress(new CheckResult { Index = i, Name = Names[i], State = "检测中", Detail = "正在检测，请稍候……" });
                        try
                        {
                            token.ThrowIfCancellationRequested();
                            switch (i)
                            {
                                case 0:
                                    addresses = Resolve(target.Host, token);
                                    string[] text = Array.ConvertAll(addresses, delegate(IPAddress a) { return a.ToString(); });
                                    step.Detail = "解析到 " + string.Join("、", text) + "。"; step.State = "通过"; break;
                                case 1:
                                    connection = Connect(addresses, target.Port, token);
                                    step.Detail = "已连接 " + connection.Client.RemoteEndPoint + "。端口连通本身不能证明 SMB 或共享可用。"; step.State = "通过"; break;
                                case 2:
                                    SmbNegotiation result = SmbWire.Exchange(connection, 5000, token);
                                    step.Detail = "协商版本 SMB " + result.Dialect + "；服务端" + (result.SigningRequired ? "要求" : "未要求") + "签名。这里只验证协议响应，尚未登录，也未建立加密会话。"; step.State = "通过"; break;
                                case 3:
                                    if (target.Share.Length == 0) step.Detail = "未填写共享名。可填写如 media 或 电影，再检测共享目录访问。";
                                    else if (target.Port != 445) step.Detail = "当前为自定义端口；Windows 普通共享路径使用标准 445 端口，本次不另连 445。共享访问尚未验证。";
                                    else
                                    {
                                        ShareAccess.Check(target, token);
                                        step.Detail = "当前程序账户可读取共享目录列表。没有读取文件内容，也没有测试写入权限。"; step.State = "通过";
                                    }
                                    break;
                            }
                        }
                        catch (OperationCanceledException) { step.State = "已取消"; step.Detail = "用户已取消检测。"; stopped = cancelled = true; }
                        catch (Exception ex)
                        {
                            step.State = "失败"; step.Detail = Explain(ex, i); stopped = true;
                        }
                        step.Milliseconds = clock.ElapsedMilliseconds;
                    }
                    report.Steps.Add(step);
                    if (progress != null) progress(step);
                }
            }
            finally { if (connection != null) connection.Close(); }
            if (cancelled) report.Summary = "检测已取消；未完成项目尚未验证。";
            else
            {
                CheckResult failure = report.Steps.Find(delegate(CheckResult s) { return s.State == "失败"; });
                if (failure != null) report.Summary = failure.Name + "失败；请查看对应原因与建议。";
                else if (report.Steps[3].State == "通过") report.Summary = "SMB 协商正常，共享目录可读取。";
                else report.Summary = "SMB 协商正常；共享访问尚未验证。";
            }
            return report;
        }
        private static IPAddress[] Resolve(string host, CancellationToken token)
        {
            IPAddress literal;
            if (IPAddress.TryParse(host, out literal)) return new[] { literal };
            Task<IPAddress[]> task = Dns.GetHostAddressesAsync(host);
            if (!task.Wait(5000, token))
            {
                task.ContinueWith(delegate(Task<IPAddress[]> t) { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                throw new TimeoutException("地址解析超时。请检查 DNS、主机名，或直接填写 NAS 的 IP 地址。");
            }
            if (task.Result.Length == 0) throw new IOException("没有解析到 IP 地址。请核对主机名。");
            return task.Result;
        }
        private static TcpClient Connect(IPAddress[] addresses, int port, CancellationToken token)
        {
            var clock = Stopwatch.StartNew(); Exception last = null;
            foreach (IPAddress address in addresses)
            {
                token.ThrowIfCancellationRequested();
                int remaining = 7000 - (int)clock.ElapsedMilliseconds;
                if (remaining <= 0) break;
                var client = new TcpClient(address.AddressFamily);
                try
                {
                    using (token.Register(delegate { client.Close(); }))
                    {
                        Task task = client.ConnectAsync(address, port);
                        if (!task.Wait(Math.Min(remaining, 2500), token))
                        {
                            task.ContinueWith(delegate(Task t) { var ignored = t.Exception; }, TaskContinuationOptions.OnlyOnFaulted);
                            throw new TimeoutException("连接端口超时。请检查地址、服务端口、网络、防火墙或端口转发。");
                        }
                    }
                    token.ThrowIfCancellationRequested(); return client;
                }
                catch (Exception ex) { client.Close(); token.ThrowIfCancellationRequested(); last = ex; }
            }
            throw last ?? new TimeoutException("连接端口超时。请核对 NAS 地址、网络和端口转发。");
        }
        private static string Explain(Exception ex, int stage)
        {
            while (ex is AggregateException && ex.InnerException != null) ex = ex.InnerException;
            SocketException socket = ex as SocketException;
            if (socket != null)
            {
                if (stage == 0) return "地址解析失败。请检查主机名、DNS 或改填 NAS 的 IP 地址。错误：" + socket.SocketErrorCode;
                if (socket.SocketErrorCode == SocketError.ConnectionRefused) return "目标拒绝连接。请确认 SMB 服务已启动，端口填写正确，端口转发指向 NAS。";
                return "网络连接失败（" + socket.SocketErrorCode + "）。请核对地址、路由、防火墙与 NAS 服务。";
            }
            return ex.Message;
        }
    }
}
