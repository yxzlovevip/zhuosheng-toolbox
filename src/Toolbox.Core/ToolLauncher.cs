using System.Diagnostics;
using System.Security.Cryptography;
namespace Toolbox.Core;
public sealed class ToolFileChangedException(string message):InvalidOperationException(message);
public sealed record ToolRunState(string Status,string Message="");
public sealed class ToolLauncher(string root,AuthGate auth,Func<bool> isAdmin) : IDisposable {
 readonly string basePath=Path.GetFullPath(root);
 readonly object gate=new();
 readonly Dictionary<string,ToolProcessGroup> processes=new();
 readonly Dictionary<string,ToolRunState> states=new();
 bool disposed;
 public bool HasRunningTools {get{lock(gate)return processes.Values.Any(group=>group.HasRunningProcesses);}}
 public IReadOnlyDictionary<string,ToolRunState> GetRunStates() {
  lock(gate) {
   foreach(var id in states.Keys.ToArray()) {
    if(states[id].Status=="running"&&(!processes.TryGetValue(id,out var group)||!group.HasRunningProcesses)) {
     states[id]=new("exited");
     if(processes.Remove(id,out var ended))ended.Dispose();
    }
   }
   return new Dictionary<string,ToolRunState>(states);
  }
 }
 public void Dispose() {
  lock(gate) {
   if(disposed)return;disposed=true;
   List<Exception> errors=[];
   foreach(var group in processes.Values)try{group.Dispose();}catch(Exception ex){errors.Add(ex);}
   processes.Clear();
   foreach(var id in states.Keys.ToArray())if(states[id].Status is "opening" or "running")states[id]=new("exited");
   if(errors.Count>0)throw new AggregateException("部分工具进程未能结束。",errors);
  }
 }
 public async Task<string> ValidateAsync(ToolItem item,CancellationToken cancellationToken=default) {
  cancellationToken.ThrowIfCancellationRequested();
  RequireAccess();
  string path=Resolve(item.RelativePath);
  try {
   using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
   await CheckHash(file,item.Sha256,cancellationToken);
   await ToolPayloadVerifier.VerifyAsync(basePath,item.PayloadManifest,item.PayloadManifestSha256,cancellationToken);
  } catch(Exception ex) when(ex is FileNotFoundException or DirectoryNotFoundException) {
   throw new InvalidOperationException($"工具文件缺失：{item.RelativePath}。请使用包含 tools 文件夹的完整工具箱，或重新安装。",ex);
  } catch(IOException ex) { throw new InvalidOperationException("工具文件被占用或不可读取。请关闭占用程序后重试。",ex); }
  cancellationToken.ThrowIfCancellationRequested();RequireAccess();return path;
 }
 public async Task<ToolFilesReport> CheckFilesAsync(IEnumerable<ToolItem> items,CancellationToken cancellationToken=default) {
  RequireAccess();var results=new List<ToolFileCheck>();
  foreach(var item in items) {
   cancellationToken.ThrowIfCancellationRequested();
   string status="ready",message="文件完整";
   try {await ValidateAsync(item,cancellationToken);}
   catch(ToolFileChangedException) {status="changed";message="文件已变化，请重新安装完整工具箱。";}
   catch(InvalidOperationException ex) when(ex.InnerException is FileNotFoundException or DirectoryNotFoundException) {status="missing";message="文件缺失，请重新安装完整工具箱。";}
   catch(Exception ex) when(ex is InvalidOperationException or UnauthorizedAccessException or IOException) {status="unavailable";message="无法检查："+ex.Message;}
   cancellationToken.ThrowIfCancellationRequested();RequireAccess();
   results.Add(new(item.Id,item.Name,item.Category,status,message));
  }
  cancellationToken.ThrowIfCancellationRequested();return new(DateTimeOffset.UtcNow,results.AsReadOnly());
 }
 void RequireAccess() {
  if(!auth.IsUnlocked) throw new InvalidOperationException("请先输入口令解锁工具箱。");
  if(!isAdmin()) throw new InvalidOperationException("需要以管理员身份运行工具箱。");
 }
 string Resolve(string relative)=>ToolPayloadVerifier.Resolve(basePath,relative);
 static async Task CheckHash(FileStream file,string expected,CancellationToken cancellationToken=default) {
  string hash=Convert.ToHexString(await SHA256.HashDataAsync(file,cancellationToken));
  if(!hash.Equals(expected,StringComparison.OrdinalIgnoreCase)) throw new ToolFileChangedException("工具文件已变化，完整性检查未通过。请重新安装，勿手动替换文件。");
 }
 public async Task<Process> LaunchAsync(ToolItem item) {
  lock(gate) {ObjectDisposedException.ThrowIf(disposed,this);states[item.Id]=new("opening");}
  try {
   RequireAccess();
   string path=Resolve(item.RelativePath);
   try {
    using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read);
    await CheckHash(file,item.Sha256);
    await ToolPayloadVerifier.VerifyAsync(basePath,item.PayloadManifest,item.PayloadManifestSha256);
    RequireAccess();
    lock(gate) {
     ObjectDisposedException.ThrowIf(disposed,this);
     CheckExistingInstance(item);
     if(!processes.TryGetValue(item.Id,out var group))processes[item.Id]=group=new();
     var process=group.Start(CreateStartInfo(path,item.Arguments));states[item.Id]=new("running");return process;
    }
   } catch(Exception ex) when(ex is FileNotFoundException or DirectoryNotFoundException) {
    throw new InvalidOperationException($"工具文件缺失：{item.RelativePath}。请使用包含 tools 文件夹的完整工具箱，或重新安装。",ex);
   } catch(Exception ex) when(ex is IOException or System.ComponentModel.Win32Exception or UnauthorizedAccessException) { throw new InvalidOperationException("工具未能启动，可能被安全软件拦截或文件不可访问。"+ex.Message,ex); }
  } catch(Exception ex) {
   lock(gate) {
    if(!disposed)states[item.Id]=processes.TryGetValue(item.Id,out var group)&&group.HasRunningProcesses?new("running","本次启动失败："+ex.Message):new("failed",ex.Message);
   }
   throw;
  }
 }
 void CheckExistingInstance(ToolItem item) {
  if(string.IsNullOrEmpty(item.InstanceExecutable))return;
  string executable=Resolve(item.InstanceExecutable);
  foreach(var process in Process.GetProcessesByName(Path.GetFileNameWithoutExtension(executable))) {
   using(process) {
    try {
     if(!process.HasExited&&!processes.Values.Any(group=>group.ContainsProcess(process)))throw new InvalidOperationException("原工具已在工具箱外运行。请先退出原工具，再从工具箱打开；工具箱不会结束你原来打开的程序。");
    }catch(System.ComponentModel.Win32Exception ex){throw new InvalidOperationException("无法确认已有原工具是否由工具箱管理，请先退出原工具后重试。",ex);}
   }
  }
 }
 public static ProcessStartInfo CreateStartInfo(string path,IReadOnlyList<string>? arguments=null) {
  string extension=Path.GetExtension(path).ToLowerInvariant();
  if(extension is not (".exe" or ".reg")) throw new InvalidOperationException("不支持该工具类型。");
  if(arguments?.Any(a=>a is null||a.Length>28000||a.Contains('\0'))==true||arguments?.Sum(a=>(long)a.Length+3)>28000)throw new InvalidOperationException("工具启动参数无效或路径总长度过长，请分批处理。");
  if(extension==".reg"&&arguments is {Count:>0})throw new InvalidOperationException("注册表工具不允许附加参数。");
  var start=new ProcessStartInfo {FileName=path,WorkingDirectory=Path.GetDirectoryName(path)!,UseShellExecute=false};
  if(extension==".reg") {start.FileName=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.Windows),"regedit.exe"); start.ArgumentList.Add(path);}
  else if(arguments!=null)foreach(string argument in arguments)start.ArgumentList.Add(argument);
  return start;
 }
}
