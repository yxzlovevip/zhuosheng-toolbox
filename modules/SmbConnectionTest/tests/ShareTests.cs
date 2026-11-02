using System;
using System.Diagnostics;
using System.IO;
using System.Text;
using System.Threading;
using Cz.NetworkTools;

internal static class ShareTests
{
    private static void Assert(bool condition, string message) { if (!condition) throw new Exception(message); }
    public static int Main(string[] args)
    {
        // A deliberately stalled native-call boundary, isolated to the test executable.
        if (args.Length == 2 && args[0] == "--share-check")
        {
            string path = Encoding.UTF8.GetString(Convert.FromBase64String(args[1]));
            string pidFile = Environment.GetEnvironmentVariable("CZ_SMB_TEST_PID");
            File.WriteAllText(pidFile, Process.GetCurrentProcess().Id.ToString());
            if (path == @"\\blocked.invalid\media") { Thread.Sleep(30000); return 0; }
            if (path == @"\\denied.invalid\media") return 5;
            return ShareAccess.ReadDirectory(path);
        }
        string marker=Path.Combine(Path.GetDirectoryName(typeof(ShareTests).Assembly.Location),"worker-pid.txt");
        Environment.SetEnvironmentVariable("CZ_SMB_TEST_PID",marker);
        try
        {
            Assert(ShareAccess.ReadDirectory(@"C:\Windows")==87,"worker允许了本地路径");
            Assert(ShareAccess.ReadDirectory(@"\\?\C:\Windows")==87,"worker允许了设备路径");
            Assert(ShareAccess.ReadDirectory(@"\\nas")==87,"worker允许了未填共享名");
            Console.WriteLine("PASS share worker rejects local/device paths and absent share");
            try { ShareAccess.Check(SmbTarget.Parse("denied.invalid",445,"media"),CancellationToken.None); throw new Exception("拒绝访问被报为通过"); }
            catch(IOException ex){ Assert(ex.Message.Contains("凭据")&&ex.Message.Contains("5"),"权限错误没有解释"); }
            Console.WriteLine("PASS share access denial keeps error code and credential guidance");
            var clock=Stopwatch.StartNew();
            using(var cancellation=new CancellationTokenSource())
            {
                cancellation.CancelAfter(350);
                bool cancelled=false; try{ShareAccess.Check(SmbTarget.Parse("blocked.invalid",445,"media"),cancellation.Token);}catch(OperationCanceledException){cancelled=true;}
                Assert(cancelled&&clock.ElapsedMilliseconds<2500,"取消没有及时结束共享子进程");
            }
            AssertChildStopped(marker);Console.WriteLine("PASS cancellation terminates stalled share worker");
            clock.Restart();bool timeout=false;
            try{ShareAccess.Check(SmbTarget.Parse("blocked.invalid",445,"media"),CancellationToken.None);}catch(TimeoutException){timeout=true;}
            Assert(timeout&&clock.ElapsedMilliseconds>=8000&&clock.ElapsedMilliseconds<11000,"共享超时界限错误");
            AssertChildStopped(marker);Console.WriteLine("PASS 8 second timeout terminates stalled share worker");
            Console.WriteLine("RESULT share worker tests passed");return 0;
        }
        catch(Exception ex){Console.WriteLine("FAIL share worker "+ex);return 1;}
        finally{Environment.SetEnvironmentVariable("CZ_SMB_TEST_PID",null);if(File.Exists(marker))File.Delete(marker);}
    }
    private static void AssertChildStopped(string marker)
    {
        int pid=int.Parse(File.ReadAllText(marker));
        try{using(Process child=Process.GetProcessById(pid)){Assert(child.HasExited,"共享子进程仍在运行");}}
        catch(ArgumentException){}
    }
}
