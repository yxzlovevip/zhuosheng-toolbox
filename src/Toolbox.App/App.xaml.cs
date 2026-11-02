using System.Security.Principal;
using System.Windows;
namespace Toolbox.App;
public partial class App : Application {
 protected override void OnStartup(StartupEventArgs e) {
  base.OnStartup(e);
  DispatcherUnhandledException+=(s,args)=>{ MessageBox.Show("发生错误："+args.Exception.Message+"\n请关闭后重试；如果问题重复出现，请联系维护人员。","卓盛工具箱"); args.Handled=true; };
  if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) { MessageBox.Show("工具箱必须以管理员身份运行。请右键程序选择“以管理员身份运行”。","需要管理员权限"); Shutdown(1); return; }
  try { ThemeManager.Initialize(); MainWindow=new WebWindow(AppServices.Create()); MainWindow.Show(); }
  catch(Exception ex) { MessageBox.Show("启动失败："+ex.Message,"卓盛工具箱"); Shutdown(1); }
 }
}
