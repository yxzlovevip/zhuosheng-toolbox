using System.Diagnostics;
using System.IO;
using System.Security.Principal;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using System.Windows.Media;
using Toolbox.Core;
namespace Toolbox.App;
public partial class MainWindow : Window {
 readonly AppServices services;
 readonly List<Process> children=[];
 CancellationTokenSource? updateCancellation;
 UpdateInfo? available;
 string category="";
 bool launching,updating,unlocking,themeReady;
 public MainWindow(AppServices services) {
  this.services=services; ThemeManager.Initialize(); InitializeComponent();
  ThemePicker.SelectedIndex=ThemeManager.Mode=="light"?1:ThemeManager.Mode=="dark"?2:0; themeReady=true;
  Loaded+=(s,e)=>PasswordInput.Focus();
  Closed+=(s,e)=>{updateCancellation?.Cancel(); services.Auth.Lock(); try{services.Launcher.Dispose();}finally{services.Updates.Dispose();foreach(var p in children)p.Dispose();}};
  AdminStatus.Text=new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)?"管理员权限":"测试宿主 · 未提升";
 }
 void Password_KeyDown(object s,KeyEventArgs e) {if(e.Key==Key.Enter) Unlock_Click(s,new());}
 void PasswordVisibility_Changed(object s,RoutedEventArgs e) {
  if(VisiblePassword==null) return;
  if(ShowPassword.IsChecked==true) {VisiblePassword.Text=PasswordInput.Password; PasswordInput.Password=""; PasswordInput.Visibility=Visibility.Collapsed; VisiblePassword.Visibility=Visibility.Visible; VisiblePassword.Focus(); VisiblePassword.CaretIndex=VisiblePassword.Text.Length;}
  else {PasswordInput.Password=VisiblePassword.Text; VisiblePassword.Clear(); VisiblePassword.Visibility=Visibility.Collapsed; PasswordInput.Visibility=Visibility.Visible; PasswordInput.Focus();}
 }
 async void Unlock_Click(object s,RoutedEventArgs e) {
  if(unlocking) return;
  string password=ShowPassword.IsChecked==true?VisiblePassword.Text:PasswordInput.Password;
  unlocking=true; UnlockButton.IsEnabled=false; UnlockButton.Content="验证中…";
  bool accepted;
  try {accepted=await Task.Run(()=>services.Auth.TryUnlock(password));}
  finally {unlocking=false; UnlockButton.IsEnabled=true; UnlockButton.Content="登录";}
  if(!IsVisible) {services.Auth.Lock(); return;}
  if(!accepted) {LoginError.Text=services.Auth.RemainingSeconds>0?$"请 {services.Auth.RemainingSeconds} 秒后重试。":"密码错误。"; PasswordInput.Clear(); VisiblePassword.Clear(); return;}
  PasswordInput.Clear(); VisiblePassword.Clear(); ShowPassword.IsChecked=false; LoginError.Text="";
  LoginPage.Visibility=Visibility.Collapsed; Workspace.Visibility=Visibility.Visible;
  ShowTools(); RefreshTools(); _=CheckForUpdatesAsync();
 }
 void Category_Click(object s,RoutedEventArgs e) {category=(string)((Button)s).Tag; ShowTools(); RefreshTools();}
 void ShowTools() {
  ToolsPage.Visibility=Visibility.Visible; HelpPage.Visibility=UpdatePage.Visibility=Visibility.Collapsed;
  PageTitle.Text=category.Length==0?"全部工具":category; PageSubtitle.Text="找到需要的工具，查看说明后再打开。"; SearchInput.IsEnabled=true;
  SelectNavigation(category);
 }
 void SelectNavigation(string selected) {
  foreach(Button button in Navigation.Children.OfType<Button>()) {
   string? id=button.Tag as string??System.Windows.Automation.AutomationProperties.GetName(button);
   if(id==selected)button.SetResourceReference(BackgroundProperty,"NavSelected");else button.Background=Brushes.Transparent;
  }
 }
 void Help_Click(object s,RoutedEventArgs e) {if(!services.Auth.IsUnlocked) return; ToolsPage.Visibility=UpdatePage.Visibility=Visibility.Collapsed; HelpPage.Visibility=Visibility.Visible; PageTitle.Text="使用说明"; PageSubtitle.Text="了解使用步骤、权限和功能范围。"; SearchInput.IsEnabled=false;SelectNavigation("使用说明");}
 void Updates_Click(object s,RoutedEventArgs e)=>ShowUpdatePage();
 void Lock_Click(object s,RoutedEventArgs e)=>LockWorkspace();
 void Search_Changed(object s,TextChangedEventArgs e) {if(ToolList!=null) RefreshTools();}
 void RefreshTools() {
  var selected=ToolList.SelectedItem as ToolItem;
  var items=services.Catalog.Search(SearchInput.Text,category).ToArray(); ToolList.ItemsSource=items;
  ToolCount.Text=$"{items.Length} 个工具";
  ToolList.SelectedItem=items.FirstOrDefault(i=>i.Id==selected?.Id)??items.FirstOrDefault();
   if(items.Length==0) {DetailSections.Visibility=Visibility.Collapsed;DetailIcon.Kind="search";DetailName.Text="无匹配工具"; DetailMeta.Text="请调整关键词或分类。"; DetailPurpose.Text=DetailPreparation.Text=DetailSteps.Text=DetailImpact.Text=DetailRestart.Text=DetailResult.Text=DetailEvidence.Text=""; LaunchButton.IsEnabled=false;}
 }
 void Tool_Changed(object s,SelectionChangedEventArgs e) {
  if(ToolList.SelectedItem is not ToolItem item) {LaunchButton.IsEnabled=false; return;}
  DetailSections.Visibility=Visibility.Visible;
  DetailName.Text=item.Name; DetailMeta.Text=$"{item.Category}  /  {item.Risk}"; DetailPurpose.Text=item.Purpose;
  DetailIcon.Kind=(string)new ToolIconConverter().Convert(item.Id,typeof(string),null!,System.Globalization.CultureInfo.CurrentCulture);
  DetailPreparation.Text=item.Preparation; DetailSteps.Text=string.Join("\n",item.Steps.Select((step,index)=>$"{index+1}. {step}"));
  DetailImpact.Text=item.Impact; DetailRestart.Text="重启要求："+item.Restart; DetailResult.Text=item.Result; DetailEvidence.Text="验证状态："+item.Evidence;
  LaunchButton.IsEnabled=!launching; LaunchButton.Content=item.Id=="uac"?"查看确认并导入":"打开工具";
 }
 async void Launch_Click(object s,RoutedEventArgs e) {
  if(!services.Auth.IsUnlocked||launching||ToolList.SelectedItem is not ToolItem item) return;
  if(item.NeedsConfirmation&&!services.ConfirmTool(item)) {StatusText.Text="已取消。"; return;}
  launching=true; LaunchButton.IsEnabled=false; StatusText.Text="正在校验工具文件，请稍候…";
  try {var process=await services.Launcher.LaunchAsync(item); children.Add(process); StatusText.Text=$"已启动：{item.Name}"; WriteLog(item.Id,"已启动");}
  catch(Exception ex) {StatusText.Text="未能启动："+ex.Message; WriteLog(item.Id,ex.Message);}
  finally {launching=false; LaunchButton.IsEnabled=services.Auth.IsUnlocked&&ToolList.SelectedItem!=null;}
 }
 static string LogDirectory=>Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InternalToolbox","Logs");
 static void WriteLog(string id,string message) {try {Directory.CreateDirectory(LogDirectory); File.AppendAllText(Path.Combine(LogDirectory,DateTime.Now.ToString("yyyy-MM")+".log"),$"{DateTimeOffset.Now:O} v1.0.0 [{id}] {message.Replace('\n',' ')}{Environment.NewLine}");} catch(IOException) {} catch(UnauthorizedAccessException) {}}
 void Logs_Click(object s,RoutedEventArgs e) {try {Directory.CreateDirectory(LogDirectory); Process.Start(new ProcessStartInfo {FileName="explorer.exe",ArgumentList={LogDirectory},UseShellExecute=false});} catch(Exception ex) {StatusText.Text="日志目录无法打开："+ex.Message;}}
 void CheckUpdate_Click(object s,RoutedEventArgs e)=>_=CheckForUpdatesAsync();
 async Task CheckForUpdatesAsync() {
  if(!services.Auth.IsUnlocked||updating) return;
  available=null; InstallUpdateButton.Visibility=Visibility.Collapsed;
  if(services.SettingsError.Length>0) {UpdateStatus.Text="更新配置有误"; UpdateNotes.Text=services.SettingsError; return;}
  updating=true; CheckUpdateButton.IsEnabled=false; UpdateStatus.Text="正在检查更新…"; updateCancellation=new();
  try {
   var result=await services.Updates.CheckAsync(updateCancellation.Token);
   switch(result.State) {
    case UpdateState.NotConfigured: UpdateStatus.Text="尚未配置更新服务器"; UpdateNotes.Text="当前工具可正常使用。请维护人员按随附《更新发布说明》配置 HTTPS 更新地址后启用在线更新。"; break;
    case UpdateState.Current: UpdateStatus.Text="当前已是最新版本"; UpdateNotes.Text="服务器检查成功，暂时没有新版。"; break;
    case UpdateState.Available: available=result.Info; UpdateStatus.Text="发现新版本 "+available!.Version; UpdateNotes.Text=available.Notes; InstallUpdateButton.Visibility=Visibility.Visible; Notice.Text="发现工具箱新版，可前往“软件更新”查看说明并下载安装。"; break;
   }
  } catch(OperationCanceledException) {UpdateStatus.Text="检查已取消或超时"; UpdateNotes.Text="可稍后重新检查，现有工具不受影响。";}
  catch(Exception ex) {UpdateStatus.Text="检查更新失败"; UpdateNotes.Text=ex.Message+"\n现有工具可继续使用，请稍后重试。"; WriteLog("update",ex.Message);}
  finally {updating=false; CheckUpdateButton.IsEnabled=true; updateCancellation.Dispose(); updateCancellation=null;}
 }
 bool HasRunningTools()=>services.Launcher.HasRunningTools;
 async void InstallUpdate_Click(object s,RoutedEventArgs e) {
  if(!services.Auth.IsUnlocked||updating||available is not UpdateInfo info) return;
  if(launching||HasRunningTools()) {UpdateStatus.Text="请先关闭已打开的工具"; UpdateNotes.Text="工具仍在运行。关闭后再安装，避免更新覆盖使用中的文件。"; return;}
  if(MessageBox.Show(this,$"准备更新到 {info.Version}。\n请保存工作，并关闭第三方工具。下载校验完成后，工具箱将退出并打开安装程序。","确认更新",MessageBoxButton.OKCancel,MessageBoxImage.Information)!=MessageBoxResult.OK) return;
  updating=true; CheckUpdateButton.IsEnabled=InstallUpdateButton.IsEnabled=false; CancelUpdateButton.Visibility=UpdateProgress.Visibility=Visibility.Visible; UpdateProgress.Value=0; updateCancellation=new();
  string? target=null;
  try {
   string protectedRoot=InstalledLocation.Resolve(services.InstallDirectory);
   string cache=Path.Combine(protectedRoot,".updates"); Directory.CreateDirectory(cache);
   if((File.GetAttributes(cache)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("更新目录异常，请重新安装。");
   target=Path.Combine(cache,"Setup-"+Guid.NewGuid().ToString("N")+".exe"); UpdateStatus.Text="正在下载并校验新版…";
   await services.Updates.DownloadAsync(info,target,new Progress<double>(value=>{UpdateProgress.Value=value*100; UpdateStatus.Text=$"正在下载并校验… {value:P0}";}),updateCancellation.Token);
   if(!services.Auth.IsUnlocked||launching||HasRunningTools()) throw new InvalidOperationException("当前已锁定或有工具正在运行，请关闭工具后重新安装。");
   var start=new ProcessStartInfo(target) {UseShellExecute=false,WorkingDirectory=Path.GetDirectoryName(target)!}; start.ArgumentList.Add("--upgrade"); start.ArgumentList.Add("--wait-pid"); start.ArgumentList.Add(Environment.ProcessId.ToString());
   Process.Start(start); Application.Current.Shutdown();
  } catch(OperationCanceledException) {UpdateStatus.Text="已取消下载";}
  catch(Exception ex) {UpdateStatus.Text="更新未安装"; UpdateNotes.Text=ex.Message; WriteLog("update",ex.Message);}
  finally {
   if(target!=null&&!Application.Current.Dispatcher.HasShutdownStarted) {try {File.Delete(target);} catch(IOException) {} catch(UnauthorizedAccessException) {}}
   updating=false; CheckUpdateButton.IsEnabled=InstallUpdateButton.IsEnabled=true; CancelUpdateButton.Visibility=UpdateProgress.Visibility=Visibility.Collapsed; updateCancellation?.Dispose(); updateCancellation=null;
  }
 }
 void CancelUpdate_Click(object s,RoutedEventArgs e)=>updateCancellation?.Cancel();
 void Theme_Changed(object s,SelectionChangedEventArgs e) {if(themeReady&&ThemePicker.SelectedItem is ComboBoxItem item) ThemeManager.Set((string)item.Tag);}
 public void ShowUpdatePage() {if(!services.Auth.IsUnlocked) return; ToolsPage.Visibility=HelpPage.Visibility=Visibility.Collapsed; UpdatePage.Visibility=Visibility.Visible; PageTitle.Text="软件更新"; PageSubtitle.Text="查看版本、更新说明与安装进度。"; SearchInput.IsEnabled=false;SelectNavigation("软件更新");}
 public void LockWorkspace() {
  services.Auth.Lock(); updateCancellation?.Cancel(); Workspace.Visibility=Visibility.Collapsed; LoginPage.Visibility=Visibility.Visible;
  PasswordInput.Clear(); VisiblePassword.Clear(); ShowPassword.IsChecked=false; LoginError.Text=""; PasswordInput.Focus();
 }
}
