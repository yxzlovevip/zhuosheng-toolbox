using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Net;
using System.Net.Sockets;
using System.Threading;
using System.Windows.Forms;
using Cz.NetworkTools;

internal static class UiTests
{
    private static void Assert(bool value, string message) { if (!value) throw new Exception(message); }
    private static T Find<T>(Form form, string name) where T : Control { return (T)form.Controls.Find(name, true)[0]; }
    private static void Wait(Func<bool> predicate)
    {
        var clock=Stopwatch.StartNew();
        while(!predicate()) { Application.DoEvents(); Thread.Sleep(20); if(clock.ElapsedMilliseconds>10000) throw new Exception("UI 等待超时"); }
        Application.DoEvents();
    }
    [STAThread]
    public static int Main(string[] args)
    {
        try
        {
            Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
            using(var form=new MainForm())
            {
                form.StartPosition=FormStartPosition.Manual; form.Location=new Point(-20000,-20000); form.Show(); Application.DoEvents();
                SynchronizationContext.SetSynchronizationContext(new WindowsFormsSynchronizationContext());
                Control.CheckForIllegalCrossThreadCalls=true;
                var address=Find<TextBox>(form,"AddressInput"); var port=Find<NumericUpDown>(form,"PortInput");
                var start=Find<Button>(form,"StartButton"); var cancel=Find<Button>(form,"CancelButton"); var copy=Find<Button>(form,"CopyButton");
                var summary=Find<Label>(form,"SummaryLabel");
                Assert(start.Enabled&&!cancel.Enabled&&!copy.Enabled,"初始按钮状态错误");
                using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size)); image.Save(Path.Combine(args[0],"initial-window.png")); }
                start.PerformClick(); Assert(summary.Text.Contains("请填写"),"空地址没有说明");
                Console.WriteLine("PASS UI initial state and empty address feedback");
                var listener=new TcpListener(IPAddress.Loopback,0);listener.Start();
                var server=new Thread(delegate(){try{using(var peer=listener.AcceptTcpClient()){var stream=peer.GetStream();var b=new byte[512];stream.Read(b,0,b.Length);Thread.Sleep(700);}}catch(SocketException){} });server.IsBackground=true;server.Start();
                address.Text="127.0.0.1";port.Value=((IPEndPoint)listener.LocalEndpoint).Port;
                start.PerformClick(); Assert(!start.Enabled&&cancel.Enabled&&!address.Enabled,"运行时输入未锁定");
                Wait(delegate{return Find<Label>(form,"State2").Text=="检测中";});cancel.PerformClick();
                Wait(delegate{return start.Enabled;});listener.Stop();server.Join(2000);
                Assert(summary.Text.Contains("取消")&&copy.Enabled&&!cancel.Enabled,"取消状态错误");
                Assert(Find<Label>(form,"State1").Text=="通过"&&Find<Label>(form,"State2").Text=="已取消","取消丢失已完成结果");
                Console.WriteLine("PASS UI starts background test, stays responsive, cancels and retains completed steps");
                var unused=new TcpListener(IPAddress.Loopback,0);unused.Start();int closed=((IPEndPoint)unused.LocalEndpoint).Port;unused.Stop();
                port.Value=closed;start.PerformClick();Wait(delegate{return start.Enabled;});
                Assert(Find<Label>(form,"State1").Text=="失败"&&summary.Text.Contains("端口连接失败"),"失败结果错误: "+summary.Text+" / "+Find<Label>(form,"State1").Text+" / "+Find<Label>(form,"Detail1").Text);
                Assert(Find<Label>(form,"State2").Text=="未检测","未完成协议检查被误报");
                Assert(Find<Label>(form,"State0").Text=="通过"&&Find<Label>(form,"Detail2").Text.Length>0,"最终结果仍被异步进度覆盖");
                Console.WriteLine("PASS UI retry displays failed TCP and skipped SMB");
                IDataObject oldClipboard=Clipboard.GetDataObject(); var snapshot=new DataObject();
                if(oldClipboard!=null)foreach(string format in oldClipboard.GetFormats(false)){try{snapshot.SetData(format,oldClipboard.GetData(format,false));}catch(System.Runtime.InteropServices.ExternalException){}}
                try{copy.PerformClick();Assert(Clipboard.GetText().Contains("端口连接失败")&&Clipboard.GetText().Contains("尚未验证"),"复制报告失败");}
                finally{Clipboard.SetDataObject(snapshot,true);}
                Console.WriteLine("PASS UI copy report contains result and validation boundaries; clipboard restored");
                using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size)); image.Save(Path.Combine(args[0],"result-window.png")); }
                form.ClientSize=new Size(805,690);Application.DoEvents();
                using(var image=new Bitmap(form.Width,form.Height)) { form.DrawToBitmap(image,new Rectangle(Point.Empty,image.Size)); image.Save(Path.Combine(args[0],"small-window.png")); }
                form.Close();
            }
            Console.WriteLine("RESULT UI tests passed"); return 0;
        }
        catch(Exception ex){Console.WriteLine("FAIL UI "+ex);return 1;}
    }
}
