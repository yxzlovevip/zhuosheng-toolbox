using System.Diagnostics;
using System.IO;
using System.Text;

namespace Toolbox.App;

public static class WindowsQueryRunner {
 public static async Task<string> RunAsync(string script,TimeSpan timeout,CancellationToken cancellationToken) {
  cancellationToken.ThrowIfCancellationRequested();
  const string entry="[Console]::InputEncoding = New-Object System.Text.UTF8Encoding($false); [Console]::OutputEncoding = New-Object System.Text.UTF8Encoding($false); $ErrorActionPreference='Stop'; try { & ([scriptblock]::Create([Console]::In.ReadToEnd())) } catch { [Console]::Error.Write($_.Exception.Message); exit 1 }";
  var start=new ProcessStartInfo(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.System),@"WindowsPowerShell\v1.0\powershell.exe")) {
   UseShellExecute=false,CreateNoWindow=true,RedirectStandardInput=true,RedirectStandardOutput=true,RedirectStandardError=true,
   StandardInputEncoding=new UTF8Encoding(false),StandardOutputEncoding=Encoding.UTF8,StandardErrorEncoding=Encoding.UTF8
  };
  foreach(string argument in new[]{"-NoLogo","-NoProfile","-NonInteractive","-EncodedCommand",Convert.ToBase64String(Encoding.Unicode.GetBytes(entry))}) start.ArgumentList.Add(argument);
  using var process=new Process {StartInfo=start};
  using var deadline=new CancellationTokenSource(timeout);
  using var linked=CancellationTokenSource.CreateLinkedTokenSource(cancellationToken,deadline.Token);
  try {
   if(!process.Start())throw new InvalidOperationException("无法启动本机信息查询。");
   var output=process.StandardOutput.ReadToEndAsync(linked.Token);
   var error=process.StandardError.ReadToEndAsync(linked.Token);
   await process.StandardInput.WriteAsync(script.AsMemory(),linked.Token);
   process.StandardInput.Close();
   await process.WaitForExitAsync(linked.Token);
   string result=await output;string errors=await error;
   cancellationToken.ThrowIfCancellationRequested();
   if(process.ExitCode!=0)throw new InvalidOperationException("本机查询失败："+(string.IsNullOrWhiteSpace(errors)?"Windows 未返回有效结果。":errors.Trim()[..Math.Min(errors.Trim().Length,500)]));
   if(result.Length>4*1024*1024)throw new InvalidOperationException("查询结果过大，请关闭部分虚拟设备后重试。");
   return result;
  } catch(OperationCanceledException) when(!cancellationToken.IsCancellationRequested&&deadline.IsCancellationRequested) {
   throw new TimeoutException("信息查询超时，请刷新重试。其他功能可以继续使用。");
  } finally {
   // Only the process tree created for this query belongs to this reader.
   try {if(process.Id>0&&!process.HasExited) {process.Kill(entireProcessTree:true);await process.WaitForExitAsync();}}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){}
  }
 }
}
