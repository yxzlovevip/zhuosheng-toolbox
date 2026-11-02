using System;
using System.IO;
using System.Threading;
using Cz.AceVirtualization;
internal static class RunnerTests {
    static int n;
    static void Check(bool ok,string m){if(!ok)throw new Exception(m);n++;}
    public static int Main(){try{
        var runner=new PayloadRunner();
        var detected=runner.Check();
        Check(detected.Cpu=="On"&&detected.Dma=="On"&&detected.Hypervisor=="Auto"&&detected.Vbs=="Running","真实封装未读取无修改检测替身的输出");
        foreach(var action in new[]{RepairChoice.Hypervisor,RepairChoice.Vbs,RepairChoice.Combined}) {
            var result=runner.Repair(action);
            int want=action==RepairChoice.Hypervisor?3010:action==RepairChoice.Vbs?37:0;
            Check(result.ExitCode==want,"脚本退出码丢失："+action);
            Check(File.Exists(result.LogPath)&&File.ReadAllText(result.LogPath).Contains("FIXTURE="+action),"实际子脚本输出未保留到日志");
            Check(result.Output.Contains("STDERR-FIXTURE"),"标准错误输出丢失");
            Check(result.RequiresRestart==(want==3010),"失败退出码被误报为需要重启");
            Check(result.CommandCompleted==(want==0||want==3010),"失败脚本被误报为完成");
        }
        string marker="SCRIPT=";var finalResult=runner.Repair(RepairChoice.Combined);
        string script=null;foreach(string line in finalResult.Output.Split(new[]{'\r','\n'},StringSplitOptions.RemoveEmptyEntries))if(line.StartsWith(marker))script=line.Substring(marker.Length);
        Check(script!=null&&!File.Exists(script)&&!Directory.Exists(Path.GetDirectoryName(script)),"运行结束后私有提取目录未清理");
        bool rejected=false;try{runner.Repair((RepairChoice)999);}catch(ArgumentException){rejected=true;}
        Check(rejected,"未知动作被执行");
        Console.WriteLine("RESULT runner "+n+" passed, 0 failed; only harmless fixture scripts executed");return 0;
    }catch(Exception e){Console.WriteLine("FAIL "+e);return 1;}}
}
