using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Toolbox.App;

static class Program {
 [STAThread] static int Main() {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};ThemeManager.Initialize();int result=1;
  var services=AppServices.Create();string? opened=null;bool browserError=false;
  typeof(AppServices).GetProperty("OpenSoftwareLink")!.SetValue(services,(Action<string>)(url=>{if(browserError)throw new InvalidOperationException("测试浏览器打开失败");opened=url;}));
  var window=new WebWindow(services){ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
  window.Loaded+=async(_,_)=>{
   try {
    var browser=(WebView2)typeof(WebWindow).GetField("browser",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    async Task Wait(string expression){for(int i=0;i<250;i++){if(browser.CoreWebView2!=null&&await browser.CoreWebView2.ExecuteScriptAsync(expression)=="true")return;await Task.Delay(100);}throw new Exception("Timed out: "+expression);}
    async Task Click(string label)=>await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('button')].find(b=>b.textContent.trim()==="+JsonSerializer.Serialize(label)+")?.click()");
    async Task Input(string selector,string value){await browser.CoreWebView2.ExecuteScriptAsync("var input=document.querySelector("+JsonSerializer.Serialize(selector)+");Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(input,"+JsonSerializer.Serialize(value)+");input.dispatchEvent(new Event('input',{bubbles:true}));");await Task.Delay(150);}
    await Wait("!!document.querySelector('#password')||[...document.querySelectorAll('button')].some(b=>b.textContent.includes('我已了解，进入工具箱'))");
    await Click("我已了解，进入工具箱");await Wait("!!document.querySelector('#password')");await Input("#password",(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")));
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('form').requestSubmit()");await Wait("!!document.querySelector('.workspace')");
    await Click("软件合集");await Wait("document.querySelectorAll('.software-card').length>100");
    if(!browser.Source.Host.Equals("toolbox.local"))throw new Exception("unexpected WebView origin");
    Console.WriteLine("PASS bundled collection renders in the real native WebView2 host");
    await browser.CoreWebView2.ExecuteScriptAsync("window.__logosVerified=false;Promise.all([...document.querySelectorAll('.software-logo')].map(image=>{image.loading='eager';return image.decode()})).then(()=>window.__logosVerified=true)");
    await Wait("window.__logosVerified===true&&document.querySelectorAll('.software-logo').length===222");
    Console.WriteLine("PASS all 222 local software logos decode in native WebView2");
    await Input("[aria-label='搜索软件']","Everything");await Wait("document.querySelectorAll('.software-card').length===1&&document.querySelector('.software-card').textContent.includes('Everything')");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.software-card .secondary').click()");
    await Wait("document.querySelector('.toast')?.textContent.includes('已在浏览器打开')===true");
    if(opened==null||new Uri(opened).Host!="www.voidtools.com")throw new Exception("native link resolved to wrong website");
    Console.WriteLine("PASS real bridge resolves bundled identifiers to the expected website without downloading or executing software");
    await Wait("!document.querySelector('.toast')");browserError=true;
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.software-card .secondary').click()");await Wait("document.querySelector('.toast')?.textContent.includes('测试浏览器打开失败')===true");
    Console.WriteLine("PASS native browser errors reach the visible error message");
    await Wait("!document.querySelector('.toast')");await Input("[aria-label='搜索软件']","");await Wait("document.querySelectorAll('.software-card').length>100");
    string target=Path.Combine(Path.GetTempPath(),"cz-software-collection-qa","native-collection.png");Directory.CreateDirectory(Path.GetDirectoryName(target)!);
    await using(var output=File.Create(target))await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.lock-button').click()");await Wait("!!document.querySelector('#password')&&!document.querySelector('.software-collection')");
    Console.WriteLine("PASS locking removes the software collection from the native window");
    result=0;Console.WriteLine("RESULT 5 passed, 0 failed");
   }catch(Exception ex){Console.WriteLine("FAIL native software collection: "+ex);}
   finally{window.Close();app.Shutdown();}
  };
  app.Run(window);return result;
 }
}
