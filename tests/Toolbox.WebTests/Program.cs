using System.IO;
using System.Reflection;
using System.Text.Json;
using System.Windows;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
using Toolbox.App;
static class Program {
 [STAThread] static int Main() {
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown}; ThemeManager.Initialize(); int result=1; string? favoriteBackup=null; bool restoreFavorites=false;
  var services=AppServices.Create();int expectedCount=services.Catalog.Items.Count;
  var window=new WebWindow(services) {ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
  window.Loaded+=async(s,e)=>{
   try {
    var browser=(WebView2)typeof(WebWindow).GetField("browser",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;
    async Task Wait(string js,int attempts=150) {for(int i=0;i<attempts;i++){if(browser.CoreWebView2!=null&&await browser.CoreWebView2.ExecuteScriptAsync(js)=="true")return;await Task.Delay(100);}throw new Exception("Timed out: "+js);}
    await Wait("!!document.querySelector('#startup-title')");
    if(await browser.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('#password')")!="false")throw new Exception("login visible before startup acceptance");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.confirm-dialog .primary').click()");
    await Wait("!!document.querySelector('#password')"); Console.WriteLine("PASS native WebView2 shows startup notice before login");
    await browser.CoreWebView2.ExecuteScriptAsync("var i=document.querySelector('#password');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(i,"+System.Text.Json.JsonSerializer.Serialize((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")))+");i.dispatchEvent(new Event('input',{bubbles:true}));");
    await Task.Delay(200); await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('form').requestSubmit()");
    await Wait("!!document.querySelector('.system-info-page')");
    await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('.sidebar button')].find(b=>b.textContent.trim()==='全部工具').click()");
    await Wait("document.querySelectorAll('.tool-card').length==="+expectedCount); Console.WriteLine("PASS real native bridge authenticates and returns "+expectedCount+" tools");
    foreach(var (query,name) in new[]{("找大文件","SpaceSniffer 空间分析"),("查文件占用","文件占用查询"),("批量改名","批量改文件名")}) {
     await browser.CoreWebView2.ExecuteScriptAsync("var search=document.querySelector('[aria-label=\"搜索工具\"]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(search,"+JsonSerializer.Serialize(query)+");search.dispatchEvent(new Event('input',{bubbles:true}));");
     await Wait("document.querySelectorAll('.tool-card').length===1&&document.querySelector('.tool-card').textContent.includes("+JsonSerializer.Serialize(name)+")");
    }
    await browser.CoreWebView2.ExecuteScriptAsync("var search=document.querySelector('[aria-label=\"搜索工具\"]');Object.getOwnPropertyDescriptor(HTMLInputElement.prototype,'value').set.call(search,'');search.dispatchEvent(new Event('input',{bubbles:true}));");
    await Wait("document.querySelectorAll('.tool-card').length==="+expectedCount);
    Console.WriteLine("PASS native purpose search resolves all three requested queries through the real catalog bridge");
    favoriteBackup=JsonSerializer.Deserialize<string?>(await browser.CoreWebView2.ExecuteScriptAsync("localStorage.getItem('favorite-tools')"));restoreFavorites=true;
    await browser.CoreWebView2.ExecuteScriptAsync("localStorage.removeItem('favorite-tools')");browser.CoreWebView2.Reload();await Wait("!!document.querySelector('.workspace')");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-section=\"常用工具\"]').click()");await Wait("!!document.querySelector('.favorites-empty')");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-section=\"全部工具\"]').click()");await Wait("!!document.querySelector('.favorite-button')");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.favorite-button').click()");await Wait("document.querySelector('.favorite-button').getAttribute('aria-pressed')==='true'");
    browser.CoreWebView2.Reload();await Wait("!!document.querySelector('.workspace')");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('[data-section=\"常用工具\"]').click()");await Wait("document.querySelectorAll('.tool-card').length===1");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.favorite-button').click()");await Wait("!!document.querySelector('.favorites-empty')");
    Console.WriteLine("PASS native favorites start empty, persist after reload and remove immediately without launching tools");
    await browser.CoreWebView2.ExecuteScriptAsync("[...document.querySelectorAll('.sidebar button')].find(b=>b.textContent.trim()==='使用说明').click();");
    await Wait("!!document.querySelector('.tool-check-heading button')");
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.tool-check-heading button').click();");
    await Wait("document.querySelector('.tool-check-summary strong')?.textContent==='"+expectedCount+" / "+expectedCount+" 项文件完整'",1500);
    Console.WriteLine("PASS real native file check validates all "+expectedCount+" bundled tools without running them");
    await Task.Delay(1000);string path=Path.GetFullPath("artifacts/react-ui/native-host.png");Directory.CreateDirectory(Path.GetDirectoryName(path)!);await using(var output=File.Create(path)) await browser.CoreWebView2.CapturePreviewAsync(CoreWebView2CapturePreviewImageFormat.Png,output);
    await browser.CoreWebView2.ExecuteScriptAsync("document.querySelector('.lock-button').click()");await Wait("!!document.querySelector('#password')");
    browser.CoreWebView2.Reload();await Wait("!!document.querySelector('#password')");
    if(await browser.CoreWebView2.ExecuteScriptAsync("!!document.querySelector('#startup-title')")!="false")throw new Exception("startup notice repeated after reload");
    Console.WriteLine("PASS native relock clears tool screen without another startup notice");
    bool blocked=false; browser.CoreWebView2.NavigationStarting+=(sender,args)=>{if(args.Uri=="https://example.com/")blocked=args.Cancel;};browser.CoreWebView2.Navigate("https://example.com/");await Task.Delay(300);if(!blocked)throw new Exception("external navigation accepted");Console.WriteLine("PASS external navigation is blocked");
    result=0;Console.WriteLine("RESULT 7 passed, 0 failed");
   }catch(Exception ex){Console.WriteLine("FAIL native host "+ex);}
   finally{try{if(restoreFavorites){var browser=(WebView2)typeof(WebWindow).GetField("browser",BindingFlags.Instance|BindingFlags.NonPublic)!.GetValue(window)!;await browser.CoreWebView2.ExecuteScriptAsync(favoriteBackup is null?"localStorage.removeItem('favorite-tools')":"localStorage.setItem('favorite-tools',"+JsonSerializer.Serialize(favoriteBackup)+")");}}finally{window.Close();app.Shutdown();}}
  };
  app.Run(window);return result;
 }
}
