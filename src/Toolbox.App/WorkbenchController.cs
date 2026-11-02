using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Toolbox.Core;
namespace Toolbox.App;

public sealed class WorkbenchController(AppServices services,Action<object> sendEvent) : IDisposable {
 readonly List<Process> processes=[];
 bool startupAccepted;
 UpdateInfo? update;
 string? downloaded;
 CancellationTokenSource? cancellation;
 CancellationTokenSource? hardwareCancellation;
 CancellationTokenSource? toolCheckCancellation;
 bool launching,loggingIn,updating,checking,installing;
 object Session()=>new {unlocked=services.Auth.IsUnlocked,startupAccepted,tools=services.Catalog.Items,toolStates=services.Launcher.GetRunStates(),theme=ThemeManager.Mode,version=AppVersion.Display};
 void RequireLogin() {if(!services.Auth.IsUnlocked) throw new InvalidOperationException("请先登录。");}
 static string String(JsonElement args,string key)=>args.TryGetProperty(key,out var value)&&value.ValueKind==JsonValueKind.String?value.GetString()??"":"";
 public async Task<object> HandleAsync(string action,JsonElement args) {
  switch(action) {
   case "bootstrap": return services.Auth.IsUnlocked?Session():new {unlocked=false,startupAccepted,theme=ThemeManager.Mode,version=AppVersion.Display};
   case "acceptStartup": startupAccepted=true; return new { };
   case "theme": ThemeManager.Set(String(args,"mode"),reportFailure:true); return new { };
   case "login":
    if(!startupAccepted) throw new InvalidOperationException("请先阅读并确认使用提示。");
    if(loggingIn) throw new InvalidOperationException("正在验证，请稍候。");
    loggingIn=true; try {string password=String(args,"password"); bool ok=await Task.Run(()=>services.Auth.TryUnlock(password)); if(!ok) throw new InvalidOperationException(services.Auth.RemainingSeconds>0?$"请 {services.Auth.RemainingSeconds} 秒后重试。":"密码错误。"); return Session();} finally {loggingIn=false;}
   case "lock": services.Auth.Lock(); cancellation?.Cancel(); hardwareCancellation?.Cancel(); toolCheckCancellation?.Cancel(); update=null; downloaded=null; return new { };
  }
  RequireLogin();
  if(!startupAccepted) throw new InvalidOperationException("请先阅读并确认使用提示。");
  switch(action) {
   case "getToolStates": return services.Launcher.GetRunStates();
   case "checkToolFiles": {
    if(toolCheckCancellation!=null)throw new InvalidOperationException("工具文件检查正在进行，请稍候。");
    using var checkCancellation=new CancellationTokenSource(TimeSpan.FromMinutes(2));toolCheckCancellation=checkCancellation;
    try {var report=await services.Launcher.CheckFilesAsync(services.Catalog.Items,checkCancellation.Token);checkCancellation.Token.ThrowIfCancellationRequested();RequireLogin();return report;}
    catch(OperationCanceledException) {throw new InvalidOperationException("工具文件检查已取消或超时，请重试。");}
    finally {toolCheckCancellation=null;}
   }
   case "openSoftwareLink":
    services.OpenSoftwareLink(SoftwareCollectionCatalog.Resolve(String(args,"softwareId"),String(args,"linkId")));
    return new { };
   case "getSystemInfo":
   case "queryGraphicsInfo": {
    if(hardwareCancellation!=null)throw new InvalidOperationException("信息查询正在进行，请稍候。");
    using var queryCancellation=new CancellationTokenSource();hardwareCancellation=queryCancellation;
    bool refresh=args.TryGetProperty("refresh",out var force)&&force.ValueKind==JsonValueKind.True;
    try {
     var report=action=="queryGraphicsInfo"
      ?await services.QuerySystemInfo(true,queryCancellation.Token)
      :await services.HardwareCache.GetAsync(token=>services.QuerySystemInfo(false,token),refresh,queryCancellation.Token);
     queryCancellation.Token.ThrowIfCancellationRequested();RequireLogin();return report;
    }
    finally {hardwareCancellation=null;}
   }
   case "checkNewSystem": return services.DriverReminder is {} reminder?await reminder.CheckAsync():new NewSystemStatus(null,false);
   case "dismissNewSystem":
    if(services.DriverReminder is {} dismiss) await dismiss.DismissAsync();
    return new { };
   case "requestLaunch": {
    var item=services.Catalog.Items.SingleOrDefault(t=>t.Id==String(args,"toolId"))??throw new InvalidOperationException("工具不存在。");
    await Launch(item); return new {confirmation=false,toolStates=services.Launcher.GetRunStates()};
   }
   case "checkUpdate": {
    if(updating||checking) throw new InvalidOperationException("更新任务正在运行。");
    if(services.SettingsError.Length>0) throw new InvalidOperationException(services.SettingsError);
    checking=true; update=null; downloaded=null; cancellation=new(); try {var result=await services.Updates.CheckAsync(cancellation.Token); cancellation.Token.ThrowIfCancellationRequested(); RequireLogin(); update=result.Info; return result;} finally {checking=false; cancellation.Dispose(); cancellation=null;}
   }
   case "cancelUpdate": cancellation?.Cancel(); return new { };
   case "downloadUpdate": {
    if(updating||checking||installing||update==null) throw new InvalidOperationException("请先检查更新，或等待当前更新任务完成。");
    string root=InstallRoot(); string cache=Path.Combine(root,".updates"); Directory.CreateDirectory(cache);
    EnsureProtectedDirectory(cache);
    string target=Path.Combine(cache,"Setup-"+Guid.NewGuid().ToString("N")+".exe");
    updating=true; cancellation=new(); try {await services.Updates.DownloadAsync(update,target,new Progress<double>(value=>sendEvent(new { @event="updateProgress",value})),cancellation.Token); RequireLogin(); downloaded=target; return new {ready=true};} finally {updating=false; cancellation.Dispose(); cancellation=null;}
   }
   case "installUpdate": {
    if(toolCheckCancellation!=null)throw new InvalidOperationException("请等待工具文件检查完成后再安装更新。");
    if(updating||checking||installing||downloaded==null||update==null) throw new InvalidOperationException("请先完成更新下载。");
    if(HasRunningTools()||launching) {
     var states=services.Launcher.GetRunStates();
     var names=services.Catalog.Items.Where(item=>states.TryGetValue(item.Id,out var state)&&state.Status is "opening" or "running").Select(item=>item.Name).ToArray();
     throw new InvalidOperationException("请先保存工作并关闭以下工具："+(names.Length>0?string.Join("、",names):"正在打开的工具")+"。关闭后可重试安装。");
    }
    installing=true;
    try {
    string root=InstallRoot(); EnsureProtectedDirectory(Path.Combine(root,".updates"));
    using var file=new FileStream(downloaded,FileMode.Open,FileAccess.Read,FileShare.Read);
    if(!Convert.ToHexString(await SHA256.HashDataAsync(file)).Equals(update.Sha256,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("更新包已变化，请重新下载。");
    RequireLogin(); var start=new ProcessStartInfo(downloaded) {UseShellExecute=false,WorkingDirectory=Path.Combine(root,".updates")}; start.ArgumentList.Add("--upgrade"); start.ArgumentList.Add("--wait-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
    Process.Start(start); _=Application.Current.Dispatcher.BeginInvoke(()=>Application.Current.Shutdown()); return new { };
    } finally {installing=false;}
   }
   case "openLogs": Directory.CreateDirectory(LogDirectory); Process.Start(new ProcessStartInfo("explorer.exe") {UseShellExecute=false,ArgumentList={LogDirectory}}); return new { };
   default: throw new InvalidOperationException("不支持的操作。");
  }
 }
 string InstallRoot() {
  return services.ResolveInstallRoot(services.InstallDirectory);
 }
 static void EnsureProtectedDirectory(string directory) {string? current=Path.GetFullPath(directory); while(current!=null) {if((File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("更新目录包含链接，请重新安装。"); current=Path.GetDirectoryName(current);}}
 bool HasRunningTools()=>services.Launcher.HasRunningTools;
 async Task Launch(ToolItem item) {
  if(launching||installing) throw new InvalidOperationException("另一项工具正在打开或更新正在安装，请稍候。");
  launching=true;
  try {var process=await services.Launcher.LaunchAsync(item); processes.Add(process); Log(item.Id,"已启动");}
  catch(Exception ex) {Log(item.Id,ex.Message); throw;}
  finally {launching=false;}
 }
 static string LogDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InternalToolbox","Logs");
 static void Log(string id,string text) {try {Directory.CreateDirectory(LogDirectory); File.AppendAllText(Path.Combine(LogDirectory,DateTime.Now.ToString("yyyy-MM")+".log"),$"{DateTimeOffset.Now:O} v{AppVersion.Display} [{id}] {text.Replace('\n',' ')}{Environment.NewLine}");}catch(IOException){}catch(UnauthorizedAccessException){}}
 public void Dispose() {cancellation?.Cancel(); hardwareCancellation?.Cancel(); toolCheckCancellation?.Cancel(); services.Auth.Lock(); try{services.Launcher.Dispose();}finally{services.Updates.Dispose();foreach(var p in processes)p.Dispose();}}
}
