using System;
using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Security.Cryptography;
using System.Threading;

namespace Cz.NetworkTools
{
    public sealed class SmbTarget
    {
        public string Host;
        public int Port;
        public string Share;
        public string Endpoint { get { return (Host.IndexOf(':') >= 0 ? "[" + Host + "]" : Host) + ":" + Port; } }
        public string UncPath
        {
            get
            {
                string host = Host.IndexOf(':') < 0 ? Host : Host.Replace(":", "-").Replace("%", "s") + ".ipv6-literal.net";
                return @"\\" + host + @"\" + Share;
            }
        }
        public static SmbTarget Parse(string address, int port, string share)
        {
            if (port < 1 || port > 65535) throw new ArgumentException("端口需要填写 1 到 65535。 ");
            string value = (address ?? "").Trim();
            if (value.Length == 0 || value.Length > 2048) throw new ArgumentException("请填写 NAS 的 IP 地址或主机名。");
            foreach (char c in value) if (char.IsControl(c)) throw new ArgumentException("地址不能包含换行等控制字符。");
            string host, extracted = "";
            IPAddress ip;
            if ((!value.StartsWith("[") || value.EndsWith("]")) && IPAddress.TryParse(value, out ip)) host = ip.ToString();
            else
            {
                if (value.StartsWith(@"\\", StringComparison.Ordinal)) value = "smb://" + value.Substring(2).Replace('\\', '/');
                else if (value.IndexOf("://", StringComparison.Ordinal) < 0) value = "smb://" + value;
                Uri uri;
                if (!Uri.TryCreate(value, UriKind.Absolute, out uri) || uri.Scheme != "smb" || uri.Host.Length == 0)
                    throw new ArgumentException("地址格式不正确。支持 IP、主机名、\\\\服务器\\共享名或 smb://服务器/共享名。");
                if (uri.UserInfo.Length > 0) throw new ArgumentException("请移除地址中的用户名和密码；此工具不接收密码。");
                if (uri.Query.Length > 0 || uri.Fragment.Length > 0) throw new ArgumentException("地址不能包含查询参数或 # 片段。");
                // Validate before Uri normalization can hide dot segments in a pasted path.
                string raw = value.Substring(value.IndexOf("://", StringComparison.Ordinal) + 3);
                int slash = raw.IndexOf('/');
                if (slash >= 0) ValidateShare(Uri.UnescapeDataString(raw.Substring(slash + 1)));
                host = uri.DnsSafeHost.Trim('[', ']');
                if (IPAddress.TryParse(host, out ip)) host = ip.ToString();
                if (uri.Port != -1) port = uri.Port;
                extracted = Uri.UnescapeDataString(uri.AbsolutePath).Trim('/').Replace('/', '\\');
            }
            if (port < 1 || port > 65535 || host.Length > 253 || Uri.CheckHostName(host) == UriHostNameType.Unknown)
                throw new ArgumentException("主机名、IP 地址或端口不正确。");
            string selectedShare = string.IsNullOrWhiteSpace(share) ? extracted : share.Trim();
            selectedShare = selectedShare.Replace('/', '\\').Trim('\\');
            ValidateShare(selectedShare);
            return new SmbTarget { Host = host, Port = port, Share = selectedShare };
        }
        private static void ValidateShare(string share)
        {
            if (share.Length > 1024) throw new ArgumentException("共享路径太长。");
            string[] parts = share.Replace('/', '\\').Split('\\');
            foreach (string part in parts)
            {
                if (part == "." || part == ".." || (part.Length > 0 && (part.EndsWith(".") || part.EndsWith(" "))))
                    throw new ArgumentException("共享路径不能包含 .、.. 或以空格、点结尾的目录名。");
                foreach (char c in part)
                    if (char.IsControl(c) || "<>:\"|?*".IndexOf(c) >= 0) throw new ArgumentException("共享名含有不支持的字符。");
            }
        }
    }
    public sealed class SmbNegotiation
    {
        public string Dialect;
        public bool SigningRequired;
    }
    public static class SmbWire
    {
        private static readonly ushort[] Dialects = { 0x0202, 0x0210, 0x0300, 0x0302, 0x0311 };
        private static ushort U16(byte[] b, int p) { return (ushort)(b[p] | b[p + 1] << 8); }
        private static uint U32(byte[] b, int p) { return (uint)(b[p] | b[p + 1] << 8 | b[p + 2] << 16 | b[p + 3] << 24); }
        private static void W16(byte[] b, int p, int n) { b[p] = (byte)n; b[p + 1] = (byte)(n >> 8); }
        public static byte[] CreateRequest()
        {
            // Direct TCP frame, SMB header, five dialects and SHA-512 preauthentication context.
            byte[] b = new byte[162];
            b[3] = 158;
            b[4] = 0xfe; b[5] = 0x53; b[6] = 0x4d; b[7] = 0x42;
            W16(b, 8, 64); W16(b, 18, 1);
            W16(b, 68, 36); W16(b, 70, 5); W16(b, 72, 1);
            Buffer.BlockCopy(Guid.NewGuid().ToByteArray(), 0, b, 80, 16);
            b[96] = 112; W16(b, 100, 1);
            for (int i = 0; i < Dialects.Length; i++) W16(b, 104 + i * 2, Dialects[i]);
            W16(b, 116, 1); W16(b, 118, 38);
            W16(b, 124, 1); W16(b, 126, 32); W16(b, 128, 1);
            byte[] salt = new byte[32]; using (var rng = RandomNumberGenerator.Create()) rng.GetBytes(salt);
            Buffer.BlockCopy(salt, 0, b, 130, 32);
            return b;
        }
        public static SmbNegotiation ParseResponse(byte[] body)
        {
            if (body == null || body.Length < 64 || body[0] != 0xfe || body[1] != 0x53 || body[2] != 0x4d || body[3] != 0x42)
                throw new InvalidDataException("对端没有返回有效的 SMB2/3 响应；该端口可能不是 SMB 服务。");
            if (U16(body, 4) != 64 || U16(body, 12) != 0 || (U32(body, 16) & 1) == 0 || U32(body, 20) != 0 || U32(body, 24) != 0 || U32(body, 28) != 0)
                throw new InvalidDataException("收到的 SMB 消息与本次协商请求不匹配。");
            uint status = U32(body, 8);
            if (status != 0) throw new InvalidDataException("SMB 服务拒绝协商，状态码 0x" + status.ToString("X8") + "。请核对服务配置、版本限制与访问策略。");
            if (body.Length < 128 || U16(body, 64) != 65) throw new InvalidDataException("SMB 协商响应不完整或格式异常。");
            ushort dialect = U16(body, 68);
            int index = Array.IndexOf(Dialects, dialect);
            if (index < 0) throw new InvalidDataException("服务端选择了本工具未提出的 SMB 版本。");
            int securityOffset = U16(body, 120), securityLength = U16(body, 122);
            if (securityLength > 0 && (securityOffset < 128 || (long)securityOffset + securityLength > body.Length))
                throw new InvalidDataException("SMB 响应的安全信息长度不正确。");
            if (dialect == 0x0311)
            {
                int count = U16(body, 70); uint start = U32(body, 124);
                if (count < 1 || count > 64 || start < 128 || start % 8 != 0 || start > body.Length - 8 || start < securityOffset + securityLength)
                    throw new InvalidDataException("SMB 3.1.1 预认证上下文缺失或不完整。");
                int position = (int)start, preauth = 0;
                for (int i = 0; i < count; i++)
                {
                    if (position > body.Length - 8) throw new InvalidDataException("SMB 协商上下文被截断。");
                    int type = U16(body, position), length = U16(body, position + 2), data = position + 8;
                    if (length > body.Length - data) throw new InvalidDataException("SMB 协商上下文长度异常。");
                    if (type == 1)
                    {
                        preauth++;
                        if (length < 6 || U16(body, data) != 1 || U16(body, data + 4) != 1 || U16(body, data + 2) > length - 6)
                            throw new InvalidDataException("SMB 3.1.1 未选择本工具支持的 SHA-512 预认证算法。");
                    }
                    position = (data + length + 7) & ~7;
                }
                if (preauth != 1) throw new InvalidDataException("SMB 3.1.1 预认证信息缺失或重复。");
            }
            return new SmbNegotiation { Dialect = new[] { "2.0.2", "2.1", "3.0", "3.0.2", "3.1.1" }[index], SigningRequired = (U16(body, 66) & 2) != 0 };
        }
        public static SmbNegotiation Exchange(TcpClient client, int timeoutMs, CancellationToken token)
        {
            Stopwatch clock = Stopwatch.StartNew();
            using (token.Register(delegate { client.Close(); }))
            {
                try
                {
                    token.ThrowIfCancellationRequested();
                    NetworkStream stream = client.GetStream(); stream.WriteTimeout = timeoutMs;
                    byte[] request = CreateRequest(); stream.Write(request, 0, request.Length);
                    byte[] header = ReadExact(stream, 4, timeoutMs, clock, token);
                    int length = (header[1] << 16) | (header[2] << 8) | header[3];
                    if (header[0] != 0 || length < 64 || length > 65536) throw new InvalidDataException("对端响应不是有效的 SMB 帧或长度异常。");
                    return ParseResponse(ReadExact(stream, length, timeoutMs, clock, token));
                }
                catch (Exception ex)
                {
                    token.ThrowIfCancellationRequested();
                    IOException io = ex as IOException;
                    SocketException socket = io == null ? ex as SocketException : io.InnerException as SocketException;
                    if (socket != null && socket.SocketErrorCode == SocketError.TimedOut) throw new TimeoutException("端口已连接，但等待 SMB 协商响应超时。");
                    if (ex is IOException && !(ex is InvalidDataException)) throw new InvalidDataException("连接已被对端关闭或中断，未能完成 SMB 协商。");
                    throw;
                }
            }
        }
        private static byte[] ReadExact(NetworkStream stream, int length, int timeoutMs, Stopwatch clock, CancellationToken token)
        {
            byte[] data = new byte[length]; int offset = 0;
            while (offset < length)
            {
                token.ThrowIfCancellationRequested();
                int remaining = timeoutMs - (int)clock.ElapsedMilliseconds;
                if (remaining <= 0) throw new TimeoutException("端口已连接，但等待 SMB 协商响应超时。");
                stream.ReadTimeout = remaining;
                int n = stream.Read(data, offset, length - offset);
                if (n == 0) throw new InvalidDataException("端口已连接，但对端在完成 SMB 响应前关闭了连接。");
                offset += n;
            }
            return data;
        }
    }
}
