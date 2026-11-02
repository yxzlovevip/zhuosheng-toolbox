using System;
using System.Reflection;
using System.Text;
using System.Threading;
using System.Windows.Forms;

[assembly: AssemblyTitle("SMB 连接测试")]
[assembly: AssemblyDescription("内部维护工具箱 · 网络工具 · SMB 连接诊断")]
[assembly: AssemblyCompany("内部维护工具箱")]
[assembly: AssemblyProduct("SMB 连接测试")]
[assembly: AssemblyVersion("1.0.0.0")]
[assembly: AssemblyFileVersion("1.0.0.0")]

namespace Cz.NetworkTools
{
    internal static class Program
    {
        [STAThread]
        public static int Main(string[] args)
        {
            if (args.Length > 0 && args[0] == "--share-check")
            {
                if (args.Length != 2) return 87;
                try { return ShareAccess.ReadDirectory(Encoding.UTF8.GetString(Convert.FromBase64String(args[1]))); }
                catch (Exception) { return 87; }
            }
            if (args.Length > 0 && args[0] == "--diagnose")
            {
                try
                {
                    if (args.Length < 2 || args.Length > 4) return 87;
                    int selectedPort = args.Length >= 3 ? int.Parse(args[2]) : 445;
                    var target = SmbTarget.Parse(args[1], selectedPort, args.Length >= 4 ? args[3] : "");
                    var result = new DiagnosticRunner().Run(target, null, CancellationToken.None);
                    using (var output = new System.IO.StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))) output.Write(result.ToText());
                    return result.Steps.Exists(delegate(CheckResult s) { return s.State == "失败" || s.State == "已取消"; }) ? 1 : 0;
                }
                catch (Exception ex)
                {
                    try { using (var output = new System.IO.StreamWriter(Console.OpenStandardOutput(), new UTF8Encoding(false))) output.WriteLine("无法完成检测：" + ex.Message); }
                    catch (System.IO.IOException) { }
                    return 87;
                }
            }
            if (args.Length != 0) return 87;
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm()); return 0;
        }
    }
}
