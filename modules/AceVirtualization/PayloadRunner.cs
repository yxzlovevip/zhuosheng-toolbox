using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Text;
using System.Threading.Tasks;

namespace Cz.AceVirtualization {
    public interface IAceService {
        DetectionSnapshot Check();
        ScriptResult Repair(RepairChoice action);
    }
    public sealed class ScriptResult {
        public int ExitCode;
        public string Output, LogPath;
        public bool RequiresRestart { get { return ExitCode==3010; } }
        public bool CommandCompleted { get { return ExitCode==0||ExitCode==3010; } }
    }
    public sealed class PayloadRunner : IAceService {
        public DetectionSnapshot Check() {
            ScriptResult r=Run("Check.ps1",true);
            if(r.ExitCode!=0)throw new IOException("检测未完成，退出码 "+r.ExitCode+"。日志："+r.LogPath+Environment.NewLine+r.Output);
            DetectionSnapshot state=ReportParser.Parse(r.Output);
            state.LogPath=r.LogPath;
            state.Details+="\r\n检测日志："+r.LogPath;
            return state;
        }
        public ScriptResult Repair(RepairChoice action) { return Run(RepairPlanner.ScriptName(action),false); }
        private static string SystemFile(string path) {
            return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),path);
        }
        private static string Hash(Stream stream) {
            using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(stream)).Replace("-","");
        }
        private static string NewPrivateDirectory() {
            string path=Path.Combine(Path.GetTempPath(),"CzAce-"+Guid.NewGuid().ToString("N"));
            // CMD expands percent/delayed variables even within some quoted paths.
            if(path.IndexOfAny(new[]{'%', '!', '"', '\r', '\n', '&', '|', '<', '>', '^', '(', ')'})>=0)
                throw new IOException("临时路径含有批处理不支持的字符，请设置普通 TEMP 路径。");
            var acl=new DirectorySecurity();acl.SetAccessRuleProtection(true,false);
            var inheritance=InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit;
            foreach(var sid in new[]{new SecurityIdentifier(WellKnownSidType.LocalSystemSid,null),new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null)})
                acl.AddAccessRule(new FileSystemAccessRule(sid,FileSystemRights.FullControl,inheritance,PropagationFlags.None,AccessControlType.Allow));
            using(var user=WindowsIdentity.GetCurrent())
                acl.AddAccessRule(new FileSystemAccessRule(user.User,FileSystemRights.FullControl,inheritance,PropagationFlags.None,AccessControlType.Allow));
            Directory.CreateDirectory(path,acl);return path;
        }
        private static ScriptResult Run(string entry,bool powershell) {
            string temporary=null;
            var locks=new List<FileStream>();var created=new List<string>();
            string logDirectory=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"CzToolbox","AceVirtualization","Logs");
            Directory.CreateDirectory(logDirectory);
            string logPath=Path.Combine(logDirectory,DateTime.Now.ToString("yyyyMMdd-HHmmss-fff")+"-"+Guid.NewGuid().ToString("N")+".log");
            using(var log=new StreamWriter(new FileStream(logPath,FileMode.CreateNew,FileAccess.Write,FileShare.Read),new UTF8Encoding(false))) {
                log.AutoFlush=true;log.WriteLine("ACE 环境检测修复 · "+DateTime.Now.ToString("o"));log.WriteLine("入口："+entry);
                try {
                    temporary=NewPrivateDirectory();
                    foreach(var file in PayloadHashes.Files) {
                        string relative=file.Key.Replace('/',Path.DirectorySeparatorChar);
                        if(Path.IsPathRooted(relative)||relative.Contains("..")||relative.IndexOf(':')>=0)throw new IOException("内嵌路径异常。");
                        string path=Path.Combine(temporary,relative);Directory.CreateDirectory(Path.GetDirectoryName(path));
                        using(var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("Ace.Payload."+file.Key.Replace('\\','/'))) {
                            if(resource==null)throw new IOException("缺少内嵌文件："+file.Key);
                            using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None)){created.Add(path);resource.CopyTo(output);}
                        }
                        var locked=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);locks.Add(locked);
                        if(Hash(locked)!=file.Value)throw new IOException("内嵌文件校验失败："+file.Key);
                    }
                    string target=Path.Combine(temporary,entry);
                    if(!created.Contains(target))throw new IOException("未知执行入口。");
                    var start=new ProcessStartInfo();
                    start.FileName=SystemFile(powershell?"WindowsPowerShell\\v1.0\\powershell.exe":"cmd.exe");
                    start.Arguments=powershell?"-NoProfile -ExecutionPolicy Bypass -File \""+target+"\"":"/d /v:off /s /c \"\""+target+"\"\"";
                    start.WorkingDirectory=temporary;start.UseShellExecute=false;start.CreateNoWindow=true;
                    start.RedirectStandardOutput=true;start.RedirectStandardError=true;start.RedirectStandardInput=true;
                    start.StandardOutputEncoding=powershell?Encoding.UTF8:Encoding.GetEncoding(936);
                    start.StandardErrorEncoding=start.StandardOutputEncoding;
                    // Limit execution policy to child processes. No registry policy is set.
                    start.EnvironmentVariables["PSExecutionPolicyPreference"]="Bypass";
                    start.EnvironmentVariables["PATH"]=SystemFile("")+";"+SystemFile("WindowsPowerShell\\v1.0");
                    start.EnvironmentVariables["ComSpec"]=SystemFile("cmd.exe");
                    using(var process=Process.Start(start)) {
                        if(process==null)throw new IOException("系统未返回脚本进程。");
                        process.StandardInput.Close();
                        Task<string> stdout=process.StandardOutput.ReadToEndAsync(),stderr=process.StandardError.ReadToEndAsync();
                        // Repair may edit boot files: wait instead of killing it on an arbitrary timer.
                        process.WaitForExit();Task.WaitAll(stdout,stderr);
                        string output=stdout.Result+Environment.NewLine+stderr.Result;
                        log.WriteLine(output);log.WriteLine("退出码："+process.ExitCode);
                        log.WriteLine("进程退出不证明修复已生效；请重启并重新检测，最终以系统和目标应用为准。");
                        return new ScriptResult{ExitCode=process.ExitCode,Output=output,LogPath=logPath};
                    }
                }catch(Exception ex){log.WriteLine(ex.ToString());throw new IOException("操作未能完成。日志："+logPath+Environment.NewLine+ex.Message,ex);}
                finally {
                    foreach(var held in locks)held.Dispose();
                    foreach(string file in created)try{File.Delete(file);}catch(IOException){}catch(UnauthorizedAccessException){}
                    // Only remove known, now-empty directories; never recursively delete a computed path.
                    if(temporary!=null){
                        var directories=new HashSet<string>();foreach(string file in created){string dir=Path.GetDirectoryName(file);while(dir!=temporary&&dir.StartsWith(temporary+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)){directories.Add(dir);dir=Path.GetDirectoryName(dir);}}
                        var sorted=new List<string>(directories);sorted.Sort(delegate(string a,string b){return b.Length.CompareTo(a.Length);});sorted.Add(temporary);
                        foreach(string dir in sorted)try{Directory.Delete(dir,false);}catch(IOException){}catch(UnauthorizedAccessException){}
                    }
                }
            }
        }
    }
}
