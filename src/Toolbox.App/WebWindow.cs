using System.Diagnostics;
using System.IO;
using System.Runtime.InteropServices;
using System.Text.Json;
using System.Windows;
using System.Windows.Interop;
using System.Windows.Media;
using System.Windows.Shell;
using Microsoft.Web.WebView2.Core;
using Microsoft.Web.WebView2.Wpf;
namespace Toolbox.App;

public sealed class WebWindow : Window {
 readonly WebView2 browser=new();
 readonly WorkbenchController controller;
 const string Origin="https://toolbox.local";
 [DllImport("user32.dll")] static extern bool ReleaseCapture();
 [DllImport("user32.dll")] static extern IntPtr SendMessage(IntPtr window,uint message,IntPtr wParam,IntPtr lParam);
 public WebWindow(AppServices services) {
  Title="卓盛工具箱"; Width=1380; Height=860; MinWidth=980; MinHeight=650; WindowStartupLocation=WindowStartupLocation.CenterScreen; WindowStyle=WindowStyle.None; ResizeMode=ResizeMode.CanResize; Background=new SolidColorBrush(Color.FromRgb(11,18,34)); Content=browser;
  Icon=new System.Windows.Media.Imaging.BitmapImage(new Uri("pack://application:,,,/CZToolbox;component/toolbox.ico"));
  WindowChrome.SetWindowChrome(this,new WindowChrome {CaptionHeight=0,ResizeBorderThickness=new Thickness(6),GlassFrameThickness=new Thickness(0),CornerRadius=new CornerRadius(0)});
  controller=new(services,SendEvent);
  Loaded+=async(s,e)=>await InitializeBrowser();
  Closed+=(s,e)=>{try{controller.Dispose();}finally{browser.Dispose();}};
 }
 async Task InitializeBrowser() {
  try {
   var environment=await CoreWebView2Environment.CreateAsync(null,Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InternalToolbox","WebView"));
   await browser.EnsureCoreWebView2Async(environment);
   var web=browser.CoreWebView2; web.Settings.AreDevToolsEnabled=false; web.Settings.AreDefaultContextMenusEnabled=false; web.Settings.IsStatusBarEnabled=false; web.Settings.IsZoomControlEnabled=false; web.Settings.AreHostObjectsAllowed=false; web.Settings.AreBrowserAcceleratorKeysEnabled=false;
   web.Settings.IsGeneralAutofillEnabled=false; web.Settings.IsPasswordAutosaveEnabled=false;
   web.SetVirtualHostNameToFolderMapping("toolbox.local",Path.Combine(AppContext.BaseDirectory,"web"),CoreWebView2HostResourceAccessKind.DenyCors);
   web.NavigationStarting+=(s,e)=>{if(!IsLocal(e.Uri)) e.Cancel=true;}; web.NewWindowRequested+=(s,e)=>e.Handled=true;
   web.PermissionRequested+=(s,e)=>e.State=CoreWebView2PermissionState.Deny;
   web.DownloadStarting+=(s,e)=>e.Cancel=true;
   web.WebMessageReceived+=MessageReceived;
   web.Navigate(Origin+"/index.html");
  } catch(WebView2RuntimeNotFoundException) {MessageBox.Show(this,"缺少 Microsoft Edge WebView2 Runtime。请安装微软 WebView2 运行时后重试。\n官方下载：https://developer.microsoft.com/microsoft-edge/webview2/","运行环境缺失"); Close();}
  catch(Exception ex) {MessageBox.Show(this,"界面初始化失败："+ex.Message,"卓盛工具箱"); Close();}
 }
 static bool IsLocal(string url)=>Uri.TryCreate(url,UriKind.Absolute,out var uri)&&uri.Scheme=="https"&&uri.Host=="toolbox.local"&&uri.IsDefaultPort;
 async void MessageReceived(object? sender,CoreWebView2WebMessageReceivedEventArgs e) {
  if(!IsLocal(e.Source)) return;
  int id=0;
  try {
   if(e.WebMessageAsJson.Length>16384) return;
   using var document=JsonDocument.Parse(e.WebMessageAsJson); var root=document.RootElement; id=root.GetProperty("id").GetInt32(); string action=root.GetProperty("action").GetString()??"";
   object? result;
   if(action is "minimize" or "maximize" or "close" or "drag") {
    switch(action) {case "minimize": WindowState=WindowState.Minimized; break; case "maximize": WindowState=WindowState==WindowState.Maximized?WindowState.Normal:WindowState.Maximized; break; case "close": Close(); return; case "drag": ReleaseCapture(); SendMessage(new WindowInteropHelper(this).Handle,0x00A1,(IntPtr)2,IntPtr.Zero); break;}
    result=new { };
   } else result=await controller.HandleAsync(action,root.GetProperty("args"));
   browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new {id,ok=true,result}));
  } catch(Exception ex) {browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(new {id,ok=false,error=ex.Message}));}
 }
 void SendEvent(object payload)=>Dispatcher.BeginInvoke(()=>browser.CoreWebView2?.PostWebMessageAsJson(JsonSerializer.Serialize(payload)));
}

