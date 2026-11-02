using System.IO;
using System.Reflection;
using System.Diagnostics;
using System.Text.Json;
using System.Windows;
using Toolbox.Core;

namespace Toolbox.OpenSourceTools;
public sealed record LaunchEntry(string Id,string Name,string RelativePath,string PayloadManifest,string PayloadManifestSha256,string Mode,string[]? Arguments=null);
public static class Program {
 [STAThread] public static int Main(string[] args) {
  try {
   using var resource=Assembly.GetExecutingAssembly().GetManifestResourceStream("launchers.json")!;
   var entries=JsonSerializer.Deserialize<LaunchEntry[]>(resource)!;
   var entry=args.Length==1?entries.SingleOrDefault(e=>e.Id==args[0]):null;
   if(entry is null)throw new InvalidOperationException("请从工具箱选择需要使用的工具。");
   string root=Path.GetFullPath(Path.Combine(AppContext.BaseDirectory,"../.."));
   if(entry.Mode=="launch")return Launch(entry,root).GetAwaiter().GetResult();
   var app=new Application();
   return app.Run(new FileToolWindow(entry,root));
  }catch(Exception ex){MessageBox.Show(ex.Message,"工具未能打开",MessageBoxButton.OK,MessageBoxImage.Information);return 1;}
 }
 static async Task<int> Launch(LaunchEntry entry,string root) {
  await ToolPayloadVerifier.VerifyAsync(root,entry.PayloadManifest,entry.PayloadManifestSha256);
  string executable=ToolPayloadVerifier.Resolve(root,entry.RelativePath);
  if(entry.Id=="oss-sharex") {
   string data=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InternalToolbox","OpenSource","ShareX");
   Directory.CreateDirectory(data);
   // The config file belongs to this bundled copy; existing standalone ShareX settings are untouched.
   await File.WriteAllTextAsync(Path.Combine(Path.GetDirectoryName(executable)!,"PersonalPath.cfg"),data);
  }
  using var process=Process.Start(ToolLauncher.CreateStartInfo(executable,entry.Arguments))??throw new InvalidOperationException("原工具未能启动。");
  await process.WaitForExitAsync();
  if(process.ExitCode!=0)throw new InvalidOperationException($"{entry.Name} 已退出，原工具返回代码 {process.ExitCode}。请查看原工具提示。");
  return 0;
 }
}
