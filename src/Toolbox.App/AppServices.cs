using System.IO;
using System.Diagnostics;
using System.Security.Principal;
using System.Text.Json;
using Toolbox.Core;
namespace Toolbox.App;
public sealed class AppServices {
 public required ToolCatalog Catalog {get;init;}
 public required AuthGate Auth {get;init;}
 public required ToolLauncher Launcher {get;init;}
 public required UpdateService Updates {get;init;}
 public required Func<ToolItem,bool> ConfirmTool {get;init;}
 public string SettingsError {get;init;}="";
 public NewSystemReminder? DriverReminder {get;init;}
 public Func<bool,CancellationToken,Task<SystemInfoReport>> QuerySystemInfo {get;init;}=static(graphics,token)=>graphics?new SystemInfoReader().ReadGraphicsAsync(token):new SystemInfoReader().ReadAsync(token);
 public SystemInfoCache HardwareCache {get;init;}=SystemInfoCache.CreateDefault();
 public string InstallDirectory {get;init;}=AppContext.BaseDirectory;
 public Func<string,string> ResolveInstallRoot {get;init;}=InstalledLocation.Resolve;
 public Action<string> OpenSoftwareLink {get;init;}=static url=>Process.Start(new ProcessStartInfo(url){UseShellExecute=true});
 public static AppServices Create() {
  var assembly=typeof(AppServices).Assembly;
  using var catalog=assembly.GetManifestResourceStream("catalog.json")!;
  using var key=new StreamReader(assembly.GetManifestResourceStream("update-public.pem")!);
  var auth=new AuthGate(); var settings=new UpdateSettings(); string error="";
  try { settings=JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"update-settings.json")))??new(); }
  catch(Exception ex) when(ex is IOException or JsonException or UnauthorizedAccessException) {error="更新配置无法读取："+ex.Message;}
  return new() {Catalog=ToolCatalog.Load(catalog),Auth=auth,Launcher=new(Path.Combine(AppContext.BaseDirectory,"tools"),auth,()=>new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)),Updates=new(settings,key.ReadToEnd(),AppVersion.Current),SettingsError=error,DriverReminder=new(WindowsInstallReader.Read,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"InternalToolbox","driver-reminder.txt")),ConfirmTool=item=>new ConfirmWindow(item) {Owner=System.Windows.Application.Current.MainWindow}.ShowDialog()==true};
 }
}
