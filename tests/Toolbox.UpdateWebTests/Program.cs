using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Toolbox.App;

static class Program {
 [STAThread] static int Main() {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ThemeManager.Initialize();
  int result=1;
  var window=new WebWindow(AppServices.Create()){ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
  window.Loaded+=async(_,_)=>{
   try{
    var browser=(WebView2)typeof(WebWindow).GetField("browser",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    async Task Wait(string expression){for(int i=0;i<350;i++){if(browser.CoreWebView2!=null&&await browser.CoreWebView2.ExecuteScriptAsync(expression)=="true")return;await Task.Delay(100);}throw new Exception("Timed out: "+expression);}
    await Wait("!!document.querySelector('#startup-title')");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.confirm-dialog .primary').click()");
    await Wait("!!document.querySelector('#password')");
    await browser.CoreWebView2.ExecuteScriptAsync("var i=document.querySelector('#password');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(i,"+System.Text.Json.JsonSerializer.Serialize((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")))+");i.dispatchEvent(new Event('input',{bubbles:true}));");
    await Task.Delay(150);await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('form').requestSubmit()");
    await Wait("!!document.querySelector('.workspace')");
    await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('.sidebar button')].find(b=>b.textContent.trim()==='软件更新').click()");
    await Wait("document.querySelector('.update-message h3')?.textContent==='当前已是最新版本'");
    await Wait($"document.querySelector('.version')?.textContent.includes('{AppVersion.Display}')");
    Console.WriteLine("PASS real native bridge automatically checks public signed release and shows current version "+AppVersion.Display);
    await Wait("document.querySelector('.update-surface')&&getComputedStyle(document.querySelector('.update-surface')).opacity==='1'");
    string path=Path.GetFullPath("artifacts/theme-update/native-updates.png");Directory.CreateDirectory(Path.GetDirectoryName(path)!);
    await using(var output=File.Create(path))await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
    Console.WriteLine("PASS native update page screenshot captured; no installer or third-party tool launched");
    result=0;
   }catch(Exception error){Console.WriteLine("FAIL native updater "+error);}
   finally{window.Close();app.Shutdown();}
  };
  app.Run(window);return result;
 }
}
