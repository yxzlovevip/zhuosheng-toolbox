using System;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Reflection;
using System.Security.AccessControl;
using System.Security.Cryptography;
using System.Security.Principal;
using System.Threading.Tasks;
using System.Threading;
using System.Collections.Generic;
using System.Windows.Forms;
using Microsoft.Win32;
using Toolbox.Installation;

namespace Toolbox.Setup {
 static class Program {
  const string RegistryPath=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\InternalToolbox";
  static string Root=InstallPathPolicy.DefaultRoot;
  static string RepairSourceRoot;
  [STAThread] static void Main(string[] args) {
   Application.EnableVisualStyles(); Application.SetCompatibleTextRenderingDefault(false);
   if(!new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator)) {MessageBox.Show("需要管理员权限。","卓盛工具箱");return;}
   bool installed;
   try{string recorded=ReadRegisteredRoot();installed=recorded!=null;if(installed)Root=recorded;if(Array.IndexOf(args,"--upgrade")>=0&&!installed&&RepairSourceRoot==null)throw new InvalidOperationException("未找到已安装版本，请先正常运行安装程序。");}
   catch(Exception ex){MessageBox.Show(ex.Message,"安装目录异常");return;}
#if UNINSTALL
   if(!installed){MessageBox.Show("未找到有效安装记录，无法继续卸载。","卓盛工具箱");return;}
   bool removing=Array.IndexOf(args,"--remove")>=0;
   if(!removing){Uninstall(false);return;}
   WithInstallLock(()=>Uninstall(true));
#else
   WithInstallLock(()=>Application.Run(new InstallForm(args,installed)));
#endif
  }
  static string ReadRegisteredRoot() {
   using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64))
   using(var key=machine.OpenSubKey(RegistryPath)) {
    return InspectRegisteredRoot(key==null?null:key.GetValue("InstallLocation") as string);
   }
  }
  static string InspectRegisteredRoot(string value) {
   RepairSourceRoot=null;
   if(string.IsNullOrWhiteSpace(value))return null;
   string root=InstallPathPolicy.Normalize(value);
   if(Directory.Exists(root)&&!InstallPathPolicy.IsInstalledRoot(root,true))throw new InvalidOperationException("安装记录指向的文件夹不是有效工具箱目录，请检查或修复原安装。");
   if(Directory.Exists(root)) {
#if UNINSTALL
    InstallPathPolicy.EnsureProtectedAncestors(root,true);
#else
    try{InstallPathPolicy.EnsureProtectedAncestors(root,true);}
    catch(InvalidOperationException){RepairSourceRoot=root;return root;}
#endif
   }
   return root;
  }
  static string SuggestRepairRoot() {
   for(int i=0;i<100;i++) {
    string candidate=InstallPathPolicy.DefaultRoot+(i==0?"":"-Repair"+(i==1?"":i.ToString()));
    if(File.Exists(candidate)||Directory.Exists(candidate)&&Directory.GetFileSystemEntries(candidate).Length>0)continue;
    try{InstallPathPolicy.EnsureNoLinks(candidate);InstallPathPolicy.EnsureProtectedAncestors(candidate,false);return candidate;}catch(InvalidOperationException){}
   }
   throw new InvalidOperationException("未找到可用的默认修复位置，请联系维护人员检查 Program Files 权限。");
  }
  static void CommitInstallation() {
   string marker=Path.Combine(Root,InstallPathPolicy.MarkerName);byte[] old=File.Exists(marker)?File.ReadAllBytes(marker):null;
   File.WriteAllText(marker,InstallPathPolicy.ProductId);
   try{Register();}catch{if(old!=null)File.WriteAllBytes(marker,old);else if(File.Exists(marker))File.Delete(marker);throw;}
  }
  static void WithInstallLock(Action action) {
   var security=new MutexSecurity();
   foreach(var sid in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid})security.AddAccessRule(new MutexAccessRule(new SecurityIdentifier(sid,null),MutexRights.FullControl,AccessControlType.Allow));
   bool created;
   try {using(var mutex=new Mutex(false,@"Global\InternalToolbox-Setup-v1",out created,security)) {
    bool acquired=false;try{try{acquired=mutex.WaitOne(0);}catch(AbandonedMutexException){acquired=true;}
     if(!acquired){MessageBox.Show("已有安装或卸载任务正在运行。请完成或关闭该任务后重试。","卓盛工具箱");return;}action();
    }finally{if(acquired)mutex.ReleaseMutex();}
   }}catch(UnauthorizedAccessException){MessageBox.Show("无法取得安装任务锁，请检查权限或重启后重试。","卓盛工具箱");}
  }
  static void ProtectDirectory(string directory) {
   InstallEngine.NoLinks(directory);
   var security=new DirectorySecurity(); security.SetAccessRuleProtection(true,false);
   security.SetOwner(new SecurityIdentifier(WellKnownSidType.BuiltinAdministratorsSid,null));
   foreach(var sid in new[]{WellKnownSidType.BuiltinAdministratorsSid,WellKnownSidType.LocalSystemSid}) security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(sid,null),FileSystemRights.FullControl,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
   security.AddAccessRule(new FileSystemAccessRule(new SecurityIdentifier(WellKnownSidType.BuiltinUsersSid,null),FileSystemRights.ReadAndExecute,InheritanceFlags.ContainerInherit|InheritanceFlags.ObjectInherit,PropagationFlags.None,AccessControlType.Allow));
   Directory.CreateDirectory(directory,security);Directory.SetAccessControl(directory,security);
  }
  static void EnsureParentDirectories(string root) {
   var missing=new List<string>();string current=Path.GetDirectoryName(root);
   while(!Directory.Exists(current)){if(File.Exists(current))throw new InvalidOperationException("安装路径的上级被文件占用。");missing.Add(current);current=Path.GetDirectoryName(current);}
   for(int i=missing.Count-1;i>=0;i--){InstallPathPolicy.EnsureProtectedAncestors(missing[i],false);ProtectDirectory(missing[i]);}
  }
  static void ApplyDirectoryPermissions(string path,DirectorySecurity permissions) {
   // A descriptor read from disk has no modified sections; passing that object
   // straight back to SetAccessControl silently leaves the current ACL unchanged.
   // Mark only the DACL as modified, preserving the existing owner/group/SACL.
   var access=new DirectorySecurity();
   access.SetSecurityDescriptorBinaryForm(permissions.GetSecurityDescriptorBinaryForm(),AccessControlSections.Access);
   Directory.SetAccessControl(path,access);
  }
  static string ShortcutPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),"卓盛工具箱.lnk");}}
  static string LegacyShortcutPath {get{return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonPrograms),"内部工具箱.lnk");}}
  static void RemoveOwnedShortcut(string path) {
   if(!File.Exists(path))return;
   Type type=Type.GetTypeFromProgID("WScript.Shell");dynamic shell=Activator.CreateInstance(type);dynamic link=shell.CreateShortcut(path);
   try {string target=Convert.ToString(link.TargetPath);if(!string.IsNullOrEmpty(target)&&(Path.GetFullPath(target).Equals(Path.Combine(Root,"app","CZToolbox.exe"),StringComparison.OrdinalIgnoreCase)||RepairSourceRoot!=null&&Path.GetFullPath(target).Equals(Path.Combine(RepairSourceRoot,"app","CZToolbox.exe"),StringComparison.OrdinalIgnoreCase)))File.Delete(path);}
   finally {System.Runtime.InteropServices.Marshal.FinalReleaseComObject(link);System.Runtime.InteropServices.Marshal.FinalReleaseComObject(shell);}
  }
  static void Register() {
   const string registryPath=@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\InternalToolbox";
   bool hadShortcut=File.Exists(ShortcutPath);byte[] oldShortcut=hadShortcut?File.ReadAllBytes(ShortcutPath):null;
   byte[] oldLegacy=File.Exists(LegacyShortcutPath)?File.ReadAllBytes(LegacyShortcutPath):null;
   var oldValues=new Dictionary<string,object>();var oldKinds=new Dictionary<string,RegistryValueKind>();bool existed;
   using(var key=Registry.LocalMachine.OpenSubKey(registryPath)){existed=key!=null;if(key!=null)foreach(string name in key.GetValueNames()){oldValues[name]=key.GetValue(name,null,RegistryValueOptions.DoNotExpandEnvironmentNames);oldKinds[name]=key.GetValueKind(name);}}
   try{RegisterCore();RemoveOwnedShortcut(LegacyShortcutPath);}
   catch{
    try{
     if(existed){using(var key=Registry.LocalMachine.CreateSubKey(registryPath)){foreach(string name in key.GetValueNames())if(!oldValues.ContainsKey(name))key.DeleteValue(name,false);foreach(var pair in oldValues)key.SetValue(pair.Key,pair.Value,oldKinds[pair.Key]);}}
     else Registry.LocalMachine.DeleteSubKeyTree(registryPath,false);
     if(hadShortcut)File.WriteAllBytes(ShortcutPath,oldShortcut);else if(File.Exists(ShortcutPath))File.Delete(ShortcutPath);
     if(oldLegacy!=null)File.WriteAllBytes(LegacyShortcutPath,oldLegacy);
    }catch(Exception restore){try{File.AppendAllText(Path.Combine(Root,"install.log"),DateTime.Now.ToString("O")+" registration restore: "+restore.Message+Environment.NewLine);}catch{}}
    throw;
   }
  }
  static void RegisterCore() {
   Type type=Type.GetTypeFromProgID("WScript.Shell"); dynamic shell=Activator.CreateInstance(type); dynamic shortcut=shell.CreateShortcut(ShortcutPath); shortcut.TargetPath=Path.Combine(Root,"app","CZToolbox.exe");shortcut.WorkingDirectory=Path.Combine(Root,"app");shortcut.Description="卓盛工具箱";shortcut.Save();
   string uninstall=Path.Combine(Root,"app","Uninstall.exe");
   using(var key=Registry.LocalMachine.CreateSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\InternalToolbox")) {
    key.SetValue("DisplayName","卓盛工具箱");key.SetValue("DisplayVersion",PayloadInfo.Version);key.SetValue("Publisher","卓盛科技");key.SetValue("InstallLocation",Root);key.SetValue("DisplayIcon",Path.Combine(Root,"app","CZToolbox.exe"));key.SetValue("UninstallString","\""+uninstall+"\"");key.SetValue("NoModify",1,RegistryValueKind.DWord);key.SetValue("NoRepair",1,RegistryValueKind.DWord);
   }
  }
  static bool HasWebViewRuntime() {
   using(var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry32))
   using(var clients=machine.OpenSubKey(@"SOFTWARE\Microsoft\EdgeUpdate\Clients")) {
    if(clients==null)return false;
    foreach(string id in clients.GetSubKeyNames())using(var client=clients.OpenSubKey(id)) {
     string name=Convert.ToString(client.GetValue("name")),version=Convert.ToString(client.GetValue("pv"));Version parsed;
     if(name.IndexOf("WebView2",StringComparison.OrdinalIgnoreCase)>=0&&Version.TryParse(version,out parsed)&&parsed.Major>0)return true;
    }
   }
   return false;
  }
  static void EnsureWebViewRuntime() {
   if(HasWebViewRuntime())return;
   using(var process=Process.Start(new ProcessStartInfo(Path.Combine(Root,"app","WebView2Bootstrapper.exe"),"/silent /install"){UseShellExecute=false,CreateNoWindow=true})) {
    if(!process.WaitForExit(180000))throw new InvalidOperationException("WebView2 安装尚未完成，请稍后重试。");
    if(!HasWebViewRuntime())throw new InvalidOperationException("WebView2 运行时安装失败，请检查网络后重试。");
   }
  }
#if UNINSTALL
  static void Uninstall(bool removing) {
   if(!removing&&MessageBox.Show("卸载卓盛工具箱及内置工具？\n不会恢复第三方工具修改过的系统设置。","确认卸载",MessageBoxButtons.OKCancel,MessageBoxIcon.Question)!=DialogResult.OK)return;
   try {
    InstallEngine.NoLinks(Root);
    string source=Assembly.GetExecutingAssembly().Location;
    if(!removing&&!source.Equals(Path.Combine(Root,"app","Uninstall.exe"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("卸载程序与安装记录不一致，请从 Windows 已安装的应用中卸载。");
    // Run an independent, fixed-purpose remover outside the installation directory.
    string removalRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),"InternalToolbox-Removal");
    ProtectDirectory(removalRoot);
    string copy=Path.Combine(removalRoot,"Remove-"+Guid.NewGuid().ToString("N")+".exe");
    if(source.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) {File.Copy(source,copy);Process.Start(new ProcessStartInfo(copy,"--remove") {UseShellExecute=true});return;}
    foreach(var process in Process.GetProcessesByName("CZToolbox")) {try{if(process.MainModule.FileName.StartsWith(Root+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请先关闭工具箱。");}catch(System.ComponentModel.Win32Exception){throw new InvalidOperationException("无法确认工具箱运行状态。");}}
    OwnedFiles.Remove(Path.Combine(Root,"app"));
    OwnedFiles.Remove(Path.Combine(Root,".previous"));
    OwnedFiles.RemoveDownloads(Path.Combine(Root,".updates"));
    string marker=Path.Combine(Root,InstallPathPolicy.MarkerName);if(File.Exists(marker))File.Delete(marker);
    string installLog=Path.Combine(Root,"install.log");if(File.Exists(installLog))File.Delete(installLog);
    RemoveOwnedShortcut(ShortcutPath);RemoveOwnedShortcut(LegacyShortcutPath);Registry.LocalMachine.DeleteSubKeyTree(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\InternalToolbox",false);
    if(Directory.Exists(Root)&&Directory.GetFileSystemEntries(Root).Length==0)Directory.Delete(Root); MessageBox.Show("卸载完成。个人设置、日志以及另行放入的文件予以保留。","卓盛工具箱");
   }catch(Exception ex){MessageBox.Show("卸载未完成："+ex.Message,"卓盛工具箱");}
  }
#endif
  sealed class InstallForm:Form {
   readonly Label status=new Label(),hint=new Label(),permissionPaths=new Label();
   readonly Button install=new Button(),browse=new Button();readonly ProgressBar progress=new ProgressBar();readonly TextBox location=new TextBox();
   readonly RadioButton overwrite=new RadioButton(),newLocation=new RadioButton();readonly CheckBox permissionConsent=new CheckBox();
   readonly string[] args;readonly bool installed;readonly string existingRoot;string newPath;string[] repairPaths=new string[0];bool working,done,refreshing,showingOld;
   public InstallForm(string[] arguments,bool hasInstallation) {
    args=arguments;installed=hasInstallation;existingRoot=installed?Root:null;showingOld=installed;newPath=installed?null:Root;
    Text="卓盛工具箱 · 安装";ClientSize=new Size(580,535);StartPosition=FormStartPosition.CenterScreen;FormBorderStyle=FormBorderStyle.FixedDialog;MaximizeBox=false;Font=new Font("Microsoft YaHei UI",10);BackColor=Color.FromArgb(248,248,248);
    var title=new Label{Text="卓盛工具箱",Font=new Font("Microsoft YaHei UI",23,FontStyle.Bold),AutoSize=true,Location=new Point(30,24)};
    var desc=new Label{Text="系统维护与网络诊断",Location=new Point(33,80),Size=new Size(510,24),ForeColor=Color.FromArgb(85,85,85)};
    var detected=new Label{Text=installed?"检测到已安装："+existingRoot:"未检测到已安装版本",Location=new Point(33,113),Size=new Size(513,26)};
    overwrite.SetBounds(33,148,202,28);overwrite.Text="覆盖原安装（保留配置）";overwrite.Visible=installed;overwrite.Checked=installed;
    newLocation.SetBounds(257,148,230,28);newLocation.Text="安装到新位置";newLocation.Visible=installed;
    var pathLabel=new Label{Text="安装位置",Location=new Point(33,188),AutoSize=true};
    location.SetBounds(33,214,407,30);location.Text=Root;
    browse.SetBounds(452,212,94,33);browse.Text="浏览…";browse.Click+=(s,e)=>ChooseDirectory();
    hint.SetBounds(33,258,513,42);hint.Font=new Font("Microsoft YaHei UI",9);hint.ForeColor=Color.FromArgb(90,90,90);
    permissionConsent.SetBounds(33,310,513,26);permissionConsent.Text="同意修复以下上级目录的防替换权限后安装";permissionConsent.Font=new Font("Microsoft YaHei UI",9);
    permissionPaths.SetBounds(33,340,513,45);permissionPaths.Font=new Font("Microsoft YaHei UI",9);permissionPaths.ForeColor=Color.FromArgb(140,90,20);
    status.SetBounds(33,398,510,43);status.Font=new Font("Microsoft YaHei UI",9);
    progress.SetBounds(33,455,513,5);progress.Style=ProgressBarStyle.Marquee;progress.Visible=false;
    install.SetBounds(340,478,206,40);install.FlatStyle=FlatStyle.Flat;install.BackColor=Color.FromArgb(97,64,218);install.ForeColor=Color.White;
    install.Click+=async(s,e)=>{if(done){LaunchApp();Close();return;}await Install();};
    Controls.AddRange(new Control[]{title,desc,detected,overwrite,newLocation,pathLabel,location,browse,hint,permissionConsent,permissionPaths,status,progress,install});
    overwrite.CheckedChanged+=(s,e)=>RefreshMode();newLocation.CheckedChanged+=(s,e)=>RefreshMode();location.TextChanged+=(s,e)=>RefreshMode();permissionConsent.CheckedChanged+=(s,e)=>RefreshMode();
    FormClosing+=(s,e)=>{if(working)e.Cancel=true;};RefreshMode();
    Shown+=async(s,e)=>{if(Array.IndexOf(args,"--upgrade")>=0&&installed&&repairPaths.Length==0)await Install();};
   }
   void RefreshMode() {
    if(refreshing||working||done)return;refreshing=true;
    try {
     bool useOld=installed&&overwrite.Checked;
     if(showingOld!=useOld){if(showingOld){if(newPath==null){try{newPath=SuggestRepairRoot();}catch{newPath=InstallPathPolicy.DefaultRoot;}}location.Text=newPath;}else{newPath=location.Text;location.Text=existingRoot;}showingOld=useOld;}
     location.ReadOnly=useOld;browse.Enabled=!useOld;install.Text=useOld?"覆盖安装":"安装";
     hint.Text=useOld?"更新到原位置，保留更新配置和上一版本备份。请先关闭工具箱及已启动的工具。":"缺少的文件夹将自动创建。原安装目录及其中的文件会保留。";
     try {
      string selected=InstallPathPolicy.Normalize(location.Text);var paths=InstallPathPolicy.GetRepairableAncestors(selected);
      if(string.Join("|",paths)!=string.Join("|",repairPaths))permissionConsent.Checked=false;
      repairPaths=paths;permissionConsent.Visible=paths.Length>0;permissionPaths.Text=string.Join("；",paths);
      install.Enabled=paths.Length==0||permissionConsent.Checked;
      status.Text=paths.Length>0?"上级目录允许替换程序；确认修复权限后即可继续。":"v"+PayloadInfo.Version+" · 全部工具内置 · 安装时需约 4 GB 可用空间，临时文件自动清理";
     }catch(Exception error){repairPaths=new string[0];permissionConsent.Visible=false;permissionPaths.Text="";status.Text=error.Message;install.Enabled=false;}
    }finally{refreshing=false;}
   }
   void ChooseDirectory() {
    string start=location.Text;while(!Directory.Exists(start)&&!string.IsNullOrEmpty(start))start=Path.GetDirectoryName(start);
    using(var dialog=new FolderBrowserDialog {Description="选择位置，将在其中创建“卓盛工具箱”文件夹。",SelectedPath=start??"",ShowNewFolderButton=true})if(dialog.ShowDialog(this)==DialogResult.OK){string leaf=Path.GetFileName(dialog.SelectedPath.TrimEnd('\\'));location.Text=leaf=="卓盛工具箱"||leaf=="内部工具箱"||leaf=="InternalToolbox"?dialog.SelectedPath:Path.Combine(dialog.SelectedPath,"卓盛工具箱");}
   }
   void LaunchApp(){Process.Start(new ProcessStartInfo(Path.Combine(Root,"app","CZToolbox.exe")){UseShellExecute=true,WorkingDirectory=Path.Combine(Root,"app")});}
   async Task Install() {
    if(working||done)return;
    string selected;bool replacing=installed&&overwrite.Checked;
    try {
     selected=InstallPathPolicy.Normalize(location.Text);InstallPathPolicy.EnsureNoLinks(selected);
     if(!replacing&&Directory.Exists(selected)&&InstallPathPolicy.IsInstalledRoot(selected,true)) {
      if(MessageBox.Show(this,"这个位置已安装工具箱，是否覆盖安装并保留配置？","检测到已安装版本",MessageBoxButtons.YesNo,MessageBoxIcon.Question)!=DialogResult.Yes)return;
      replacing=true;
     }
     var currentPlan=InstallPathPolicy.GetRepairableAncestors(selected);
     if(string.Join("|",currentPlan)!=string.Join("|",repairPaths)){RefreshMode();throw new InvalidOperationException("目录状态已变化，请重新确认后安装。");}
     if(currentPlan.Length>0&&!permissionConsent.Checked)throw new InvalidOperationException("请先勾选同意修复上级目录权限，或选择其他位置。");
     // Reject unrelated files before any permission or directory mutation.
     if(Directory.Exists(selected)&&Directory.GetFileSystemEntries(selected).Length>0&&(!replacing||!InstallPathPolicy.IsInstalledRoot(selected,true)))throw new InvalidOperationException("所选位置已有其他文件，请选择空文件夹。");
    }catch(Exception error){status.Text=error.Message;return;}
    working=true;install.Enabled=false;progress.Visible=true;location.Enabled=browse.Enabled=overwrite.Enabled=newLocation.Enabled=permissionConsent.Enabled=false;
    var previousPermissions=new List<KeyValuePair<string,DirectorySecurity>>();
    try {
     await Task.Run(()=>{
      int index=Array.IndexOf(args,"--wait-pid"),pid;if(index>=0&&index+1<args.Length&&int.TryParse(args[index+1],out pid)){try{using(var process=Process.GetProcessById(pid)){if(!process.WaitForExit(60000))throw new InvalidOperationException("工具箱尚未退出，请关闭后重试。");}}catch(ArgumentException){}}
      for(int i=repairPaths.Length-1;i>=0;i--){string path=repairPaths[i];InstallPathPolicy.EnsureNoLinks(path);var before=new DirectoryInfo(path).GetAccessControl();var repaired=InstallPathPolicy.ProtectedAncestorSecurity(before);previousPermissions.Add(new KeyValuePair<string,DirectorySecurity>(path,before));ApplyDirectoryPermissions(path,repaired);}
      InstallPathPolicy.ValidateNewTarget(selected,replacing);EnsureParentDirectories(selected);if(installed)RepairSourceRoot=existingRoot;Root=selected;ProtectDirectory(Root);
      using(var payload=Assembly.GetExecutingAssembly().GetManifestResourceStream("payload.7z"))
      using(var decoder=Assembly.GetExecutingAssembly().GetManifestResourceStream("7zr.exe")) {
       if(payload==null||decoder==null)throw new InvalidOperationException("安装包资源缺失。");
       SolidPayload.Install(payload,decoder,PayloadInfo.Sha256,PayloadInfo.DecoderSha256,PayloadInfo.ZipSha256,PayloadInfo.ZipSize,Root,text=>BeginInvoke(new Action(()=>status.Text=text)),()=>{EnsureWebViewRuntime();CommitInstallation();});
      }
     });
     done=true;status.Text="安装完成。原文件已按安装规则保留。";install.Text="启动";
     if(Array.IndexOf(args,"--upgrade")>=0&&repairPaths.Length==0&&replacing){working=false;LaunchApp();Close();}
    }catch(Exception error){
     string rollback="";for(int i=previousPermissions.Count-1;i>=0;i--)try{ApplyDirectoryPermissions(previousPermissions[i].Key,previousPermissions[i].Value);}catch(Exception restore){rollback="\n权限恢复未完成："+restore.Message;}
     status.Text="安装未完成："+error.Message+rollback;install.Text="重试";
    }finally{working=false;install.Enabled=true;progress.Visible=false;location.Enabled=overwrite.Enabled=newLocation.Enabled=permissionConsent.Enabled=!done;browse.Enabled=!done&&!(installed&&overwrite.Checked);}
   }
  }
 }
}


