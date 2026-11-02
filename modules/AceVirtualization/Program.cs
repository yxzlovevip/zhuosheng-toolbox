using System;
using System.IO;
using System.Reflection;
using System.Text;
using System.Windows.Forms;
[assembly:AssemblyTitle("ACE 虚拟化环境检测修复（脚本封装）")]
[assembly:AssemblyProduct("卓盛工具箱 · ACE 环境检测修复")]
[assembly:AssemblyCompany("卓盛科技")]
[assembly:AssemblyDescription("保留原四项检测、三项修复及 BIOS/UEFI 提示；默认只读检测。")]
[assembly:AssemblyVersion("1.0.0.0")]
[assembly:AssemblyFileVersion("1.0.0.0")]
namespace Cz.AceVirtualization {
    internal static class Program {
        [STAThread]public static int Main(string[] args) {
            if(args.Length==1&&args[0]=="--check") {
                try {var result=new PayloadRunner().Check();using(var writer=new StreamWriter(Console.OpenStandardOutput(),new UTF8Encoding(false)))writer.WriteLine(result.Details);return 0;}
                catch(Exception ex){using(var writer=new StreamWriter(Console.OpenStandardError(),new UTF8Encoding(false)))writer.WriteLine(ex.Message);return 1;}
            }
            if(args.Length!=0)return 87;
            Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
            Application.Run(new MainForm());return 0;
        }
    }
}
