using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;

[assembly: AssemblyTitle("Win10 优化系统（原BAT封装）")]
[assembly: AssemblyDescription("保留原BAT内容，包含电源、防护、更新及资源管理器设置。")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Cz.Win10Optimization
{
    internal static class Program
    {
        private static int Main(string[] args)
        {
            string temporaryDirectory = null;
            string scriptPath = null;
            bool scriptStarted = false;
            try
            {
                temporaryDirectory = Path.Combine(Path.GetTempPath(), "CzWin10Optimization-" + Guid.NewGuid().ToString("N"));
                // Percent expansion occurs even inside quoted cmd paths. Reject such a
                // temporary root rather than letting it change which file is executed.
                // The original BAT also uses unquoted cd and delayed expansion.
                if (temporaryDirectory.IndexOfAny(new char[] { '%', '"', '\r', '\n', '&', '^', '(', ')', '!' }) >= 0)
                    throw new IOException("临时目录含有 CMD 不支持的字符，请设置普通的 TEMP 目录后重试。");

                DirectorySecurity security = new DirectorySecurity();
                security.SetAccessRuleProtection(true, false);
                InheritanceFlags inheritance = InheritanceFlags.ContainerInherit | InheritanceFlags.ObjectInherit;
                SecurityIdentifier system = new SecurityIdentifier(WellKnownSidType.LocalSystemSid, null);
                SecurityIdentifier admins = new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid, null);
                security.AddAccessRule(new FileSystemAccessRule(system, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
                security.AddAccessRule(new FileSystemAccessRule(admins, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
                using (WindowsIdentity identity = WindowsIdentity.GetCurrent())
                {
                    if (!new WindowsPrincipal(identity).IsInRole(WindowsBuiltInRole.Administrator))
                        security.AddAccessRule(new FileSystemAccessRule(identity.User, FileSystemRights.FullControl, inheritance, PropagationFlags.None, AccessControlType.Allow));
                }
                Directory.CreateDirectory(temporaryDirectory, security);
                scriptPath = Path.Combine(temporaryDirectory, "win10-optimization.bat");
                using (Stream payload = Assembly.GetExecutingAssembly().GetManifestResourceStream("Cz.Win10Optimization.Payload.bat"))
                {
                    if (payload == null) throw new IOException("安装文件不完整：缺少内嵌 BAT。");
                    using (FileStream output = new FileStream(scriptPath, FileMode.CreateNew, FileAccess.Write, FileShare.None))
                        payload.CopyTo(output);
                }

                // Keep the payload open for reading, denying replacement until cmd exits.
                using (FileStream lockedPayload = new FileStream(scriptPath, FileMode.Open, FileAccess.Read, FileShare.Read))
                {
                    using (SHA256 sha = SHA256.Create())
                    {
                        string actual = BitConverter.ToString(sha.ComputeHash(lockedPayload)).Replace("-", "");
                        if (!string.Equals(actual, PayloadInfo.Sha256, StringComparison.OrdinalIgnoreCase))
                            throw new IOException("内嵌 BAT 完整性检查失败，未执行。");
                    }
                    // ELEV handles NET FILE failing on a machine with the Server service off.
                    // :: skips the redundant mshta relaunch after the initial ELEV shift.
                    // Caller arguments are deliberately ignored.
                    string modeArgument = " ELEV ::";
                    ProcessStartInfo start = new ProcessStartInfo();
                    start.FileName = Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System), "cmd.exe");
                    start.Arguments = "/d /v:off /s /c \"\"" + scriptPath + "\"" + modeArgument + "\"";
                    start.WorkingDirectory = Environment.CurrentDirectory;
                    start.UseShellExecute = false;
                    start.CreateNoWindow = false;
                    using (Process child = Process.Start(start))
                    {
                        if (child == null) throw new IOException("Windows 未返回 BAT 进程。");
                        scriptStarted = true;
                        child.WaitForExit();
                        return child.ExitCode;
                    }
                }
            }
            catch (Exception exception)
            {
                Console.Error.WriteLine("Win10 优化工具未能正常执行：" + exception.Message);
                return 1;
            }
            finally
            {
                // The original BAT can relaunch through a UAC broker if its CACLS check
                // fails. That process is not necessarily a descendant we can wait for.
                // Retain the private payload after starting so it cannot disappear under
                // a relaunched CMD. Only failed, never-started extraction is removed.
                try
                {
                    if (!scriptStarted)
                    {
                        if (scriptPath != null && File.Exists(scriptPath)) File.Delete(scriptPath);
                        if (temporaryDirectory != null && Directory.Exists(temporaryDirectory)) Directory.Delete(temporaryDirectory, false);
                    }
                }
                catch (Exception cleanupError)
                {
                    Console.Error.WriteLine("临时封装文件未能移除：" + cleanupError.Message);
                }
            }
        }
    }
}
