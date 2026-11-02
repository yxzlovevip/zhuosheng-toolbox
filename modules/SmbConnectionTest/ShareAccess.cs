using System;
using System.ComponentModel;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading;

namespace Cz.NetworkTools
{
    public static class ShareAccess
    {
        // Use an isolated process because Windows redirector calls may ignore cancellation.
        public static void Check(SmbTarget target, CancellationToken token)
        {
            var start = new ProcessStartInfo(Assembly.GetEntryAssembly().Location,
                "--share-check " + Convert.ToBase64String(Encoding.UTF8.GetBytes(target.UncPath)));
            start.UseShellExecute = false; start.CreateNoWindow = true; start.WindowStyle = ProcessWindowStyle.Hidden;
            using (Process process = Process.Start(start))
            {
                try
                {
                    var clock = Stopwatch.StartNew();
                    while (!process.WaitForExit(80))
                    {
                        token.ThrowIfCancellationRequested();
                        if (clock.ElapsedMilliseconds > 8000) throw new TimeoutException("共享访问超时。SMB 协商已通过；请检查 Windows 凭据、共享权限与 NAS 响应。");
                    }
                    token.ThrowIfCancellationRequested();
                    int code = process.ExitCode;
                    if (code != 0) throw new IOException(ExplainError(code));
                }
                finally
                {
                    try { if (!process.HasExited) { process.Kill(); process.WaitForExit(1000); } }
                    catch (InvalidOperationException) { }
                    catch (Win32Exception) { }
                }
            }
        }
        public static string ExplainError(int code)
        {
            string prefix = "共享访问失败（Windows 错误 " + code + "）。";
            switch (code)
            {
                case 5: case 65: case 86: case 1326: case 1909: case 1219: case 1244:
                    return prefix + "请检查当前程序账户的 Windows 凭据、NAS 账号和共享权限。可先在资源管理器中连接相同地址；管理员运行时的连接可能与桌面账户不同。";
                case 2: case 3: case 53: case 67:
                    return prefix + "请核对共享名是否存在、路径是否正确，以及 Windows 是否能访问该共享。";
                case 1231: case 1232: case 64: case 121:
                    return prefix + "网络路径不可达、连接中断或超时。请检查 NAS、路由和 SMB 服务。";
                default: return prefix + "SMB 协商与 Windows 共享访问是不同步骤，请检查共享路径、Windows 凭据及 NAS 策略。";
            }
        }
        [StructLayout(LayoutKind.Sequential, CharSet = CharSet.Unicode)]
        private struct FindData
        {
            public uint Attributes;
            public System.Runtime.InteropServices.ComTypes.FILETIME Creation, Access, Write;
            public uint SizeHigh, SizeLow, Reserved0, Reserved1;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 260)] public string FileName;
            [MarshalAs(UnmanagedType.ByValTStr, SizeConst = 14)] public string Alternate;
        }
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern IntPtr FindFirstFile(string path, out FindData data);
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern bool FindClose(IntPtr handle);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern uint GetFileAttributes(string path);
        public static int ReadDirectory(string path)
        {
            // Revalidate the private worker input; never accept an arbitrary local/device path.
            SmbTarget target;
            try { target = SmbTarget.Parse(path, 445, ""); }
            catch (ArgumentException) { return 87; }
            if (!path.StartsWith(@"\\", StringComparison.Ordinal) || target.Share.Length == 0 || path.StartsWith(@"\\?\") || path.StartsWith(@"\\.\")) return 87;
            FindData data;
            IntPtr handle = FindFirstFile(target.UncPath.TrimEnd('\\') + @"\*", out data);
            if (handle != new IntPtr(-1)) { FindClose(handle); return 0; }
            int code = Marshal.GetLastWin32Error();
            // ERROR_FILE_NOT_FOUND/NO_MORE_FILES can mean an empty accessible directory.
            if (code == 2 || code == 18)
            {
                uint attributes = GetFileAttributes(target.UncPath);
                if (attributes != 0xffffffff && (attributes & 0x10) != 0) return 0;
                if (attributes == 0xffffffff) return Marshal.GetLastWin32Error();
                return 267;
            }
            return code;
        }
    }
}
