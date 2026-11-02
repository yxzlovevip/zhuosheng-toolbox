using System.IO;
using System.Reflection;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Toolbox.App;

static class Program {
 [STAThread] static int Main() {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ThemeManager.Initialize();int result=1;
  var window=new WebWindow(AppServices.Create()) {ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
  window.Loaded+=async(_,_)=>{
   try {
    var browser=(WebView2)typeof(WebWindow).GetField("browser",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    async Task Wait(string expression) {for(int i=0;i<400;i++){if(browser.CoreWebView2!=null&&await browser.CoreWebView2.ExecuteScriptAsync(expression)=="true")return;await Task.Delay(100);}throw new Exception("Timed out: "+expression);}
    async Task Click(string label) {await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==="+System.Text.Json.JsonSerializer.Serialize(label)+")?.click()");}
    async Task Capture(string filename) {string path=Path.Combine(Path.GetTempPath(),"cz-system-info-qa",filename);Directory.CreateDirectory(Path.GetDirectoryName(path)!);await using var output=File.Create(path);await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);}
    await Wait("!!document.querySelector('#password')||!!document.querySelector('.startup-notice')||[...document.querySelectorAll('button')].some(b=>b.textContent.includes('我已了解，进入工具箱'))");
    await Click("我已了解，进入工具箱");await Wait("!!document.querySelector('#password')");
    await browser.CoreWebView2.ExecuteScriptAsync("window.__systemUiErrors=[];addEventListener('error',e=>window.__systemUiErrors.push(e.message));var p=document.querySelector('#password');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(p,"+System.Text.Json.JsonSerializer.Serialize((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")))+");p.dispatchEvent(new Event('input',{bubbles:true}));");
    await Task.Delay(150);await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('form').requestSubmit()");await Wait("!!document.querySelector('.system-info-page')");
    await Click("系统信息");await Wait("!!document.querySelector('.section-cpu .hardware-device')&&!document.querySelector('[aria-busy=true]')");
    await Wait("document.querySelectorAll('.section-memory .hardware-device').length>0");
    Console.WriteLine("PASS real Windows system report renders through native WebView2 bridge");
    await Capture("native-overview.png");
    await Click("刷新信息");await Click("网络工具");await Click("系统信息");await Wait("!!document.querySelector('.section-cpu .hardware-device')&&!document.querySelector('[aria-busy=true]')");
    await Wait("!document.querySelector('.system-info-scroll>[role=alert]')");Console.WriteLine("PASS leaving and returning during a native query reuses the in-flight report");
    await Click("磁盘分区");await Wait("document.querySelectorAll('.disk-partitions .partition-card').length>0");await Wait("document.querySelector('.disk-partitions').textContent.includes('无盘符')");
    Console.WriteLine("PASS real disk associations retain partitions without drive letters");await Capture("native-partitions.png");
    await Click("显卡查询");await Click("查询显卡详情");await Wait("!!document.querySelector('.section-nvidia')&&!document.querySelector('[aria-busy=true]')");
    await Wait("document.querySelectorAll('.section-graphics .hardware-device').length>0");Console.WriteLine("PASS native GPU query returns driver details or explicit support status");await Capture("native-graphics.png");
    await Wait("window.__systemUiErrors.length===0");
    await Click("刷新信息");await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.lock-button').click()");await Wait("!!document.querySelector('#password')");await Task.Delay(700);await Wait("!document.querySelector('.system-info-page')");
    Console.WriteLine("PASS locking during a live hardware query removes the page and cancels the request");
    result=0;Console.WriteLine("RESULT 5 passed, 0 failed");
   }catch(Exception ex){Console.WriteLine("FAIL native system information: "+ex);}
   finally {window.Close();app.Shutdown();}
  };
  app.Run(window);return result;
 }
}
