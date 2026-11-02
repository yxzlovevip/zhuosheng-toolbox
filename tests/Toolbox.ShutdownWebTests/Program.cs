using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Wpf;
using Toolbox.App;
using Toolbox.Core;

class ShutdownWebTests {
 [STAThread] static int Main(string[] args) {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};int result=1;
  app.Startup+=async(_,_)=>{
   WebWindow? window=null;var tracked=new List<Process>();ToolLauncher? launcher=null;
   try {
    string fixture=Path.GetFullPath(args[0]),root=Path.GetDirectoryName(fixture)!;
    var auth=new AuthGate();auth.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")));launcher=new(root,auth,()=>true);
    var item=new ToolItem {Id="fixture",Name="无副作用的进程测试",RelativePath=Path.GetFileName(fixture),Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture)))};
    var services=new AppServices {Auth=auth,Launcher=launcher,Catalog=new([item]),Updates=new(new UpdateSettings(),"",new Version(1,3,5)),ConfirmTool=_=>false};
    window=new WebWindow(services){Left=-20000,Top=-20000,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual};
    var controller=(WorkbenchController)typeof(WebWindow).GetField("controller",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    await controller.HandleAsync("acceptStartup",JsonSerializer.SerializeToElement(new{}));
    window.Show();var browser=(WebView2)window.Content;
    await Wait(()=>browser.CoreWebView2!=null);
    await WaitAsync(async()=>await browser.CoreWebView2.ExecuteScriptAsync("document.readyState==='complete'&&!!document.querySelector('#root')")=="true");
    await WaitAsync(async()=>await browser.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('[data-section=\"全部工具\"]')")=="true");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-section=\"全部工具\"]').click()");
    // Exercise the real WebView message bridge, rather than calling Dispose directly.
    await browser.CoreWebView2.ExecuteScriptAsync("window.chrome.webview.postMessage({id:991,action:'requestLaunch',args:{toolId:'fixture'}})");
    await Wait(()=>File.Exists(Path.Combine(root,"grandchild.pid")));
    foreach(string role in new[]{"parent","child","grandchild"})tracked.Add(Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(root,role+".pid")))));
    await WaitAsync(async()=>await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.detail-panel .tool-run-state')?.textContent==='运行中'")=="true");
    Console.WriteLine("PASS actual native bridge launches a managed test tool and two descendants");
    tracked[0].Kill();await tracked[0].WaitForExitAsync();
    await Task.Delay(1200);
    if(await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.detail-panel .tool-run-state')?.textContent==='运行中'")!="true")throw new Exception("native UI lost running descendants");
    browser.CoreWebView2.Reload();
    await WaitAsync(async()=>await browser.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('[data-section=\"全部工具\"]')")=="true");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-section=\"全部工具\"]').click()");
    await WaitAsync(async()=>await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.detail-panel .tool-run-state')?.textContent==='运行中'")=="true");
    foreach(var process in tracked.Skip(1)){process.Kill();await process.WaitForExitAsync();}
    await WaitAsync(async()=>await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.detail-panel .tool-run-state')?.textContent==='已退出'")=="true");
    Console.WriteLine("PASS native UI tracks surviving descendants, restores states after reload, and shows exited");
    foreach(string role in new[]{"parent","child","grandchild"})File.Delete(Path.Combine(root,role+".pid"));
    await browser.CoreWebView2.ExecuteScriptAsync("window.chrome.webview.postMessage({id:993,action:'requestLaunch',args:{toolId:'fixture'}})");
    await Wait(()=>File.Exists(Path.Combine(root,"grandchild.pid")));
    foreach(string role in new[]{"parent","child","grandchild"})tracked.Add(Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(root,role+".pid")))));
    using var external=Process.Start(new ProcessStartInfo(fixture,"external"){UseShellExecute=false,WorkingDirectory=root})!;
    tracked.Add(Process.GetProcessById(external.Id));
    await Wait(()=>File.Exists(Path.Combine(root,"external.pid")));
    await browser.CoreWebView2.ExecuteScriptAsync("window.chrome.webview.postMessage({id:992,action:'close',args:{}})");
    await Wait(()=>!window.IsVisible&&tracked.Take(6).All(p=>p.HasExited));
    if(launcher.HasRunningTools)throw new Exception("closed window still owns active tools");
    Console.WriteLine("PASS closing the actual native window ends the tool and all test descendants");
    if(external.HasExited)throw new Exception("unmanaged external process was stopped");
    Console.WriteLine("PASS unrelated process survives the toolbox window closing");
    Console.WriteLine("RESULT 4 passed, 0 failed; no real maintenance tools were executed");result=0;
   }catch(Exception error){Console.WriteLine("FAIL "+error);}
   finally {
    window?.Close();launcher?.Dispose();
    foreach(var process in tracked){try{if(!process.HasExited){process.Kill(true);process.WaitForExit(5000);}}catch(InvalidOperationException){}process.Dispose();}
    app.Shutdown();
   }
  };
  app.Run();return result;
 }
 static async Task Wait(Func<bool> condition)=>await WaitAsync(()=>Task.FromResult(condition()));
 static async Task WaitAsync(Func<Task<bool>> condition) {var timer=Stopwatch.StartNew();while(!await condition()){if(timer.Elapsed>TimeSpan.FromSeconds(20))throw new Exception("native shutdown test timed out");await Task.Delay(25);}}
}
