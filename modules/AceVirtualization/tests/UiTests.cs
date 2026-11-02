using System;
using System.Collections.Generic;
using System.Drawing;
using System.IO;
using System.Threading;
using System.Windows.Forms;
using Cz.AceVirtualization;
internal sealed class FakeService:IAceService {
    public int Checks;public List<RepairChoice> Repairs=new List<RepairChoice>();
    public DetectionSnapshot Check(){Checks++;return new DetectionSnapshot{Cpu="On",Dma="Off",Hypervisor="Auto",Vbs="Running",Details="fixture detection"};}
    public ScriptResult Repair(RepairChoice action){Repairs.Add(action);return new ScriptResult{ExitCode=action==RepairChoice.Vbs?37:3010,Output="fixture output",LogPath=""};}
}
internal static class UiTests {
    static int n;static void Check(bool v,string s){if(!v)throw new Exception(s);n++;}
    static T Find<T>(Form f,string name)where T:Control{return (T)f.Controls.Find(name,true)[0];}
    static void Wait(Func<bool> done){var start=DateTime.UtcNow;while(!done()){Application.DoEvents();Thread.Sleep(10);if((DateTime.UtcNow-start).TotalSeconds>8)throw new Exception("UI wait timeout");}Application.DoEvents();}
    [STAThread]public static int Main(string[] args){try{
        Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
        var service=new FakeService();
        using(var form=new MainForm(service)){
            form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.Show();
            var detect=Find<Button>(form,"DetectButton");Wait(()=>service.Checks==1&&detect.Enabled);
            Check(service.Repairs.Count==0,"窗口打开自动执行了修复");
            Check(Find<Label>(form,"DmaState").Text.Contains("已关闭"),"DMA 检测功能丢失");
            Check(Find<Label>(form,"CpuState").Text.Contains("已启用"),"CPU 检测功能丢失");
            Check(Find<Label>(form,"HypervisorState").Text.Contains("已启用"),"Hypervisor 检测功能丢失");
            Check(Find<Label>(form,"VbsState").Text.Contains("正在运行"),"VBS 检测功能丢失");
            Check(Find<TextBox>(form,"Guidance").Text.Contains("BIOS"),"DMA 异常没有 BIOS 操作建议");
            var choices=Find<ComboBox>(form,"RepairChoice");Check(choices.Items.Count==3,"三种原修复功能未全部保留");
            var repair=Find<Button>(form,"RepairButton");
            for(int i=0;i<3;i++){
                choices.SelectedIndex=i;repair.PerformClick();Wait(()=>repair.Enabled);
                Check(service.Repairs[service.Repairs.Count-1]==new[]{RepairChoice.Combined,RepairChoice.Hypervisor,RepairChoice.Vbs}[i],"界面调用错误修复功能");
            }
            Check(Find<Label>(form,"Summary").Text.Contains("37"),"失败结果未保留退出码");
            Check(Find<TextBox>(form,"Output").Text.Contains("fixture output"),"原始输出没有显示");
            Check(Find<TextBox>(form,"Guidance").Text.Contains("F3")&&Find<TextBox>(form,"Guidance").Text.Contains("PIN"),"原 UEFI 与登录准备提示缺失");
            form.ClientSize=new Size(850,780);Application.DoEvents();
            using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(Point.Empty,bitmap.Size));bitmap.Save(Path.Combine(args[0],"ace-window.png"));}
            form.Close();
        }
        Console.WriteLine("RESULT UI "+n+" passed, 0 failed; only fixture service used");return 0;
    }catch(Exception ex){Console.WriteLine("FAIL UI "+ex);return 1;}}
}
