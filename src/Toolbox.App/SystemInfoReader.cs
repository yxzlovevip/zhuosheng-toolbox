using System.IO;
using System.Text.Json;

namespace Toolbox.App;

public sealed class SystemInfoReader {
 public Task<SystemInfoReport> ReadAsync(CancellationToken token)=>Read("system",token);
 public Task<SystemInfoReport> ReadGraphicsAsync(CancellationToken token)=>Read("graphics",token);
 static async Task<SystemInfoReport> Read(string mode,CancellationToken token) {
  using var resource=typeof(SystemInfoReader).Assembly.GetManifestResourceStream("SystemInformation.ps1")??throw new InvalidOperationException("系统查询模块缺失，请重新安装。");
  using var reader=new StreamReader(resource);
  string script="$QueryMode = '"+mode+"'\n"+await reader.ReadToEndAsync(token);
  return Parse(await WindowsQueryRunner.RunAsync(script,TimeSpan.FromSeconds(35),token));
 }
 public static SystemInfoReport Parse(string json) {
  try {
   var report=JsonSerializer.Deserialize<SystemInfoReport>(json);
   Validate(report);
   return report!;
  }catch(JsonException ex) {throw new InvalidOperationException("查询返回的数据无法读取，请刷新重试。",ex);}
 }
 public static void Validate(SystemInfoReport? report) {
  if(report==null||report.CollectedAt==default||report.Sections==null||report.Sections.Length==0||report.Sections.Any(s=>s==null||string.IsNullOrWhiteSpace(s.Id)||s.Devices==null||s.Devices.Any(d=>!ValidDevice(d,0))))throw new InvalidOperationException("查询返回的数据无法读取，请刷新重试。");
 }
 static bool ValidDevice(SystemInfoDevice? device,int depth)=>device!=null&&depth<8&&device.Fields!=null&&device.Children!=null&&device.Fields.All(f=>f!=null)&&device.Children.All(d=>ValidDevice(d,depth+1));
}
