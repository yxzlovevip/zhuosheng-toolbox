namespace Toolbox.Core;

public sealed record NewSystemStatus(DateTimeOffset? InstalledAt, bool ShouldPrompt, string Message = "");

public sealed class NewSystemReminder(Func<DateTimeOffset?> readInstalledAt, string statePath, TimeProvider? clock = null) {
 readonly TimeProvider time=clock??TimeProvider.System;
 readonly Task<DateTimeOffset?> installation=Task.Run(readInstalledAt);
 public async Task<NewSystemStatus> CheckAsync() {
  DateTimeOffset? installed;
  try {installed=await installation;}
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or System.Security.SecurityException or InvalidOperationException) {
   return new(null,false,"无法读取系统安装时间，可手动打开驱动总裁检查驱动。");
  }
  if(installed==null) return new(null,false,"系统安装时间不可用，可手动打开驱动总裁检查驱动。");
  var age=time.GetUtcNow()-installed.Value;
  if(age<TimeSpan.Zero) return new(installed,false,"系统安装时间晚于当前时间，请检查电脑时间。");
  if(age>TimeSpan.FromDays(3)) return new(installed,false);
  try {
   string dismissed=File.Exists(statePath)?await File.ReadAllTextAsync(statePath):"";
   return new(installed,dismissed!=Key(installed.Value));
  } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
   return new(installed,false,"无法读取驱动提醒记录，可手动打开驱动总裁检查驱动。");
  }
 }
 public async Task DismissAsync() {
  var status=await CheckAsync();
  if(!status.ShouldPrompt||status.InstalledAt==null) return;
  Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(statePath))!);
  string temporary=statePath+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {await File.WriteAllTextAsync(temporary,Key(status.InstalledAt.Value)); File.Move(temporary,statePath,true);}
  finally {if(File.Exists(temporary)) File.Delete(temporary);}
 }
 static string Key(DateTimeOffset installed)=>installed.UtcTicks.ToString(System.Globalization.CultureInfo.InvariantCulture);
}
