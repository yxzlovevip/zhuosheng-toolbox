using System.IO;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Threading;
using Toolbox.App;
using Toolbox.Core;
static class Program {
 [STAThread] static int Main() {
  System.Windows.Media.RenderOptions.ProcessRenderMode=System.Windows.Interop.RenderMode.SoftwareOnly;
  // Use the real styles and window without running production administrator startup.
  var app=new Application {ShutdownMode=ShutdownMode.OnExplicitShutdown};
  var xaml=System.Xml.Linq.XDocument.Load("src/Toolbox.App/App.xaml");
  System.Xml.Linq.XNamespace ns="http://schemas.microsoft.com/winfx/2006/xaml/presentation";
  var resources=new System.Xml.Linq.XElement(ns+"ResourceDictionary",xaml.Root!.Attributes().Where(a=>a.IsNamespaceDeclaration),xaml.Root.Element(ns+"Application.Resources")!.Nodes());
  resources.SetAttributeValue(System.Xml.Linq.XNamespace.Xmlns+"local","clr-namespace:Toolbox.App;assembly=CZToolbox");
  app.Resources=(ResourceDictionary)System.Windows.Markup.XamlReader.Parse(resources.ToString().Replace("clr-namespace:Toolbox.App\"","clr-namespace:Toolbox.App;assembly=CZToolbox\""));
  SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(Dispatcher.CurrentDispatcher));
  int failed=0,passed=0,confirmations=0;
  void Assert(bool value,string message) {if(!value) throw new Exception(message);}
  void Test(string name,Action action) {try {action(); Console.WriteLine("PASS "+name); passed++;} catch(Exception ex) {Console.WriteLine("FAIL "+name+": "+ex.Message); failed++;}}
  Test("built app contains every registered tool with its release hash",()=>{
   using var embedded=typeof(AppServices).Assembly.GetManifestResourceStream("catalog.json")!;
   var packagedCatalog=ToolCatalog.Load(embedded);
   var payloadAuth=new AuthGate(); payloadAuth.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")));
   var launcher=new ToolLauncher(Path.Combine(AppContext.BaseDirectory,"tools"),payloadAuth,()=>true);
   var errors=new List<string>();
   foreach(var item in packagedCatalog.Items) {
    try {Task.Run(()=>launcher.ValidateAsync(item)).GetAwaiter().GetResult();}
    catch(Exception ex) {errors.Add(item.Name+": "+ex.Message);}
   }
   Assert(errors.Count==0,string.Join("; ",errors));
  });
  using var catalogFile=File.OpenRead("assets/catalog.json"); var auth=new AuthGate();
  var services=new AppServices {Catalog=ToolCatalog.Load(catalogFile),Auth=auth,Launcher=new(Path.GetFullPath(".build/ui-tools"),auth,()=>false),Updates=new(new UpdateSettings(),File.ReadAllText("assets/update-public.pem"),new Version(1,0,0)),ConfirmTool=item=>{confirmations++; return false;}};
  var window=new MainWindow(services) {ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000}; app.MainWindow=window; window.Show(); Pump();
  T Find<T>(string name) where T:FrameworkElement=>(T)window.FindName(name);
  void Click(string name)=>Find<Button>(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Directory.CreateDirectory("artifacts/screenshots");
  Test("login hides all tools",()=>{Assert(Find<Grid>("Workspace").Visibility==Visibility.Collapsed,"workspace exposed"); Capture(window,"login");});
  Test("wrong password stays on login",()=>{Find<PasswordBox>("PasswordInput").Password="0000"; Click("UnlockButton"); WaitFor(()=>Find<TextBlock>("LoginError").Text.Length>0); Assert(!auth.IsUnlocked,"wrong accepted"); Assert(Find<TextBlock>("LoginError").Text.Length>0,"missing error");});
  Test("correct password exposes tool catalog",()=>{Find<PasswordBox>("PasswordInput").Password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")); Click("UnlockButton"); WaitFor(()=>auth.IsUnlocked); Pump(); Assert(auth.IsUnlocked,"unlock failed"); Assert(Find<ListBox>("ToolList").Items.Count>=20,"catalog missing integrated tools"); Assert(Find<PasswordBox>("PasswordInput").Password=="","password retained"); Capture(window,"tools");});
  Test("search finds UAC with true impact",()=>{Find<TextBox>("SearchInput").Text="UAC"; Pump(); var list=Find<ListBox>("ToolList"); Assert(list.Items.Count==1,"search mismatch"); list.SelectedIndex=0; Assert(Find<TextBlock>("DetailImpact").Text.Contains("EnableLUA=0"),"uac impact absent"); Capture(window,"uac");});
  Test("cancel confirmation never starts UAC",()=>{Click("LaunchButton"); Pump(); Assert(confirmations==1,"confirmation not requested"); Assert(Find<TextBlock>("StatusText").Text.Contains("取消"),"cancel not reported");});
  Test("clear search restores integrated tools",()=>{Find<TextBox>("SearchInput").Text=""; Pump(); var ids=Find<ListBox>("ToolList").Items.Cast<ToolItem>().Select(i=>i.Id).ToArray(); Assert(ids.Contains("smb-connection-test")&&ids.Contains("browser-cleanup"),"integration lost");});
  Test("unconfigured update is disclosed",()=>{window.ShowUpdatePage(); Pump(); Assert(Find<TextBlock>("UpdateStatus").Text.Contains("尚未配置"),"not configured hidden"); Capture(window,"updates");});
  Test("dark and light palettes apply immediately",()=>{ThemeManager.Set("dark",false); Pump(); Assert(((SolidColorBrush)window.Background).Color==Color.FromRgb(17,17,17),"dark palette not applied"); Capture(window,"dark"); ThemeManager.Set("light",false); Pump(); Assert(((SolidColorBrush)window.Background).Color==Color.FromRgb(245,245,245),"light palette not applied"); Capture(window,"light");});
  Test("primary button glyphs have readable contrast in both themes",()=>{foreach(string mode in new[]{"dark","light"}) {ThemeManager.Set(mode,false); Pump(); var button=Find<Button>("LaunchButton"); button.ApplyTemplate(); var text=FindDescendant<TextBlock>(button)??throw new Exception("button text missing"); double contrast=Contrast(((SolidColorBrush)text.Foreground).Color,((SolidColorBrush)button.Background).Color); Assert(contrast>=7,$"{mode}: text contrast {contrast:F2}"); var combo=Find<ComboBox>("ThemePicker"); combo.ApplyTemplate(); var selected=FindDescendant<TextBlock>(combo)??throw new Exception("combo text missing"); Assert(Contrast(((SolidColorBrush)selected.Foreground).Color,((SolidColorBrush)combo.Background).Color)>=4.5,"combo contrast low");} ThemeManager.Set("light",false);});
  Button Nav(string name)=>Find<StackPanel>("Navigation").Children.OfType<Button>().Single(b=>System.Windows.Automation.AutomationProperties.GetName(b)==name);
  void Navigate(string name)=>Nav(name).RaiseEvent(new RoutedEventArgs(Button.ClickEvent));
  Test("navigation switches categories and shows matching detail icon",()=>{Navigate("磁盘工具");Pump();Assert(Find<ListBox>("ToolList").Items.Count==2,"disk tools lost");Assert(Find<ToolIcon>("DetailIcon").Kind=="disk","detail icon mismatched");});
  Test("help and updates each select only their navigation item",()=>{foreach(string name in new[]{"使用说明","软件更新"}) {Navigate(name);Pump();var selected=Find<StackPanel>("Navigation").Children.OfType<Button>().Where(b=>b.Background==app.FindResource("NavSelected")).ToArray();Assert(selected.Length==1&&selected[0]==Nav(name),"navigation selection stale");}});
  Test("empty search hides obsolete details and recovers",()=>{Navigate("全部工具");Find<TextBox>("SearchInput").Text="no-match-xyz";Pump();Assert(Find<StackPanel>("DetailSections").Visibility==Visibility.Collapsed&&!Find<Button>("LaunchButton").IsEnabled,"stale details shown");Capture(window,"empty");Find<TextBox>("SearchInput").Clear();Pump();Assert(Find<StackPanel>("DetailSections").Visibility==Visibility.Visible&&Find<Button>("LaunchButton").IsEnabled,"details did not recover");});
  Test("tool workspace renders in both themes at normal and minimum size",()=>{foreach(string mode in new[]{"light","dark"}) {ThemeManager.Set(mode,false);Pump();Capture(window,"tools-"+mode);window.Width=window.MinWidth;window.Height=window.MinHeight;Pump();var launch=Find<Button>("LaunchButton");var origin=launch.TransformToAncestor((Visual)window.Content).Transform(new Point(0,0));Assert(origin.X>=0&&origin.Y>=0&&origin.X+launch.ActualWidth<=((FrameworkElement)window.Content).ActualWidth&&origin.Y+launch.ActualHeight<=((FrameworkElement)window.Content).ActualHeight,"launch button clipped");Capture(window,"minimum-"+mode);window.Width=1240;window.Height=840;Pump();}ThemeManager.Set("light",false);});
  Test("relock clears access",()=>{window.LockWorkspace(); Assert(!auth.IsUnlocked,"gate still open"); Assert(Find<Grid>("Workspace").Visibility==Visibility.Collapsed,"workspace visible");Capture(window,"login-light");Find<PasswordBox>("PasswordInput").Password="sample";Find<CheckBox>("ShowPassword").IsChecked=true;Assert(Find<TextBox>("VisiblePassword").Text=="sample"&&Find<PasswordBox>("PasswordInput").Password=="","show password broken");Find<CheckBox>("ShowPassword").IsChecked=false;Assert(Find<PasswordBox>("PasswordInput").Password=="sample"&&Find<TextBox>("VisiblePassword").Text=="","hide password broken");});
  window.Close(); app.Shutdown(); Console.WriteLine($"RESULT {passed} passed, {failed} failed"); return failed==0?0:1;
 }
 static void Pump() {var frame=new DispatcherFrame(); var timer=new DispatcherTimer(DispatcherPriority.Background) {Interval=TimeSpan.FromMilliseconds(200)}; timer.Tick+=(s,e)=>{timer.Stop(); frame.Continue=false;}; timer.Start(); Dispatcher.PushFrame(frame);}
 static void WaitFor(Func<bool> condition) {for(int i=0;i<25&&!condition();i++) Pump();}
 static T? FindDescendant<T>(DependencyObject root) where T:DependencyObject {for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i); if(child is T value) return value; var nested=FindDescendant<T>(child); if(nested!=null) return nested;} return null;}
 static double Contrast(Color a,Color b) {double L(Color c){double F(byte v){double x=v/255d;return x<=0.04045?x/12.92:Math.Pow((x+0.055)/1.055,2.4);}return .2126*F(c.R)+.7152*F(c.G)+.0722*F(c.B);}double x=L(a),y=L(b);return (Math.Max(x,y)+.05)/(Math.Min(x,y)+.05);}
 static void Capture(Window window,string name) {window.UpdateLayout(); Pump(); var element=(FrameworkElement)window.Content; var dpi=VisualTreeHelper.GetDpi(element); var bmp=new RenderTargetBitmap((int)Math.Ceiling(element.ActualWidth*dpi.DpiScaleX),(int)Math.Ceiling(element.ActualHeight*dpi.DpiScaleY),dpi.PixelsPerInchX,dpi.PixelsPerInchY,PixelFormats.Pbgra32); bmp.Render(element); var encoder=new PngBitmapEncoder(); encoder.Frames.Add(BitmapFrame.Create(bmp)); using var output=File.Create($"artifacts/screenshots/{name}.png"); encoder.Save(output);}
}
