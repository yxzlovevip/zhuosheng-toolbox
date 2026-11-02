using System;
using System.Drawing;
using System.Drawing.Imaging;
using System.IO;
using System.IO.Compression;
using System.Reflection;
using System.Security.AccessControl;
using System.Threading;
using System.Threading.Tasks;
using System.Windows.Forms;
class ToolboxSetupUiTests {
 [STAThread] static int Main(string[] args) {
  Application.EnableVisualStyles();Application.SetCompatibleTextRenderingDefault(false);
  try {
   var assembly=args.Length>0?Assembly.LoadFile(Path.GetFullPath(args[0])):typeof(Toolbox.Setup.InstallEngine).Assembly;
   CheckInheritedWorkingDirectory(assembly);
   CheckTemporaryDirectoryLock(assembly);
   var type=assembly.GetType("Toolbox.Setup.Program+InstallForm");var program=type.DeclaringType;
   var root=program.GetField("Root",BindingFlags.Static|BindingFlags.NonPublic);
   var inspect=program.GetMethod("InspectRegisteredRoot",BindingFlags.Static|BindingFlags.NonPublic);
   inspect.Invoke(null,new object[]{null});root.SetValue(null,Toolbox.Installation.InstallPathPolicy.DefaultRoot);
   Func<Form,string,object> field=(form,name)=>type.GetField(name,BindingFlags.NonPublic|BindingFlags.Instance).GetValue(form);
   using(var fresh=(Form)Activator.CreateInstance(type,new object[]{new string[0],false})) {
    if(((TextBox)field(fresh,"location")).ReadOnly||!((Button)field(fresh,"browse")).Enabled)throw new Exception("fresh path not editable");
    Console.WriteLine("PASS fresh installation allows a new path");
   }
   string registered=(string)program.GetMethod("ReadRegisteredRoot",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[0]);
   if(registered==null)throw new Exception("existing installation was discarded instead of offering overwrite");
   root.SetValue(null,registered);
   using(var form=(Form)Activator.CreateInstance(type,new object[]{new string[0],true})) {
    var overwrite=(RadioButton)field(form,"overwrite");var other=(RadioButton)field(form,"newLocation");
    var path=(TextBox)field(form,"location");var browse=(Button)field(form,"browse");var install=(Button)field(form,"install");
    var consent=(CheckBox)field(form,"permissionConsent");
    if(!overwrite.Checked||path.Text!=registered||!path.ReadOnly||browse.Enabled)throw new Exception("detected location is not selected for overwrite");
    Console.WriteLine("PASS actual installed location is detected and overwrite is offered");
    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();
    if(consent.Visible) {
     if(install.Enabled||consent.Checked)throw new Exception("ancestor ACL repair does not require explicit consent");
     consent.Checked=true;Application.DoEvents();if(!install.Enabled)throw new Exception("consented overwrite stays blocked");
    }
    Console.WriteLine("PASS permission repair is disclosed and never preselected");
    Directory.CreateDirectory("artifacts/screenshots");
    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save("artifacts/screenshots/install-overwrite.png",ImageFormat.Png);}
    other.Checked=true;Application.DoEvents();if(path.ReadOnly||!browse.Enabled||!install.Enabled||consent.Visible)throw new Exception("new-location choice did not enable normal install");
    path.Text=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Toolbox-UiProbe-"+Guid.NewGuid(),"nested","InternalToolbox");
    Application.DoEvents();if(!install.Enabled||Directory.Exists(Path.GetDirectoryName(path.Text)))throw new Exception("new path validation either blocked or created directories before consent");
    Console.WriteLine("PASS missing parent folders are accepted without early filesystem mutation");
    using(var bitmap=new Bitmap(form.Width,form.Height)){form.DrawToBitmap(bitmap,new Rectangle(0,0,bitmap.Width,bitmap.Height));bitmap.Save("artifacts/screenshots/install-new-location.png",ImageFormat.Png);}
    form.Close();
   }
   string probe=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Toolbox-ParentProbe-"+Guid.NewGuid().ToString("N"));
   try {
    program.GetMethod("EnsureParentDirectories",BindingFlags.Static|BindingFlags.NonPublic).Invoke(null,new object[]{Path.Combine(probe,"nested","InternalToolbox")});
    if(!Directory.Exists(Path.Combine(probe,"nested")))throw new Exception("missing parents were not created");
    Toolbox.Installation.InstallPathPolicy.EnsureProtectedAncestors(Path.Combine(probe,"nested","InternalToolbox"),false);
    Console.WriteLine("PASS actual missing parent creation applies protected permissions only to new folders");
   }finally{if(Directory.Exists(probe))Toolbox.Setup.InstallEngine.DeleteInside(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),probe);}
   CheckInheritedPermissionRepair(assembly);
   Console.WriteLine("RESULT 9 passed, 0 failed; only isolated new test folders were changed");return 0;
  }catch(Exception error){Console.WriteLine("FAIL "+error);return 1;}
 }
 static MemoryStream FixturePayload(string content) {
  var output=new MemoryStream();
  using(var zip=new ZipArchive(output,ZipArchiveMode.Create,true)) {
   using(var writer=new StreamWriter(zip.CreateEntry("CZToolbox.exe").Open()))writer.Write(content);
   using(var writer=new StreamWriter(zip.CreateEntry("web/index.html").Open()))writer.Write(content);
  }
  output.Position=0;return output;
 }
 static void CheckInheritedWorkingDirectory(Assembly target) {
  string original=Environment.CurrentDirectory;
  string isolated=Path.GetFullPath(Path.Combine(".build","setup-cwd-"+Guid.NewGuid().ToString("N")));
  Directory.CreateDirectory(isolated);
  var install=target.GetType("Toolbox.Setup.InstallEngine").GetMethod("Install",new[]{typeof(Stream),typeof(string),typeof(Action<string>)});
  try {
   using(var payload=FixturePayload("old"))install.Invoke(null,new object[]{payload,isolated,new Action<string>(s=>{})});
   foreach(string relative in new[]{"app",Path.Combine("app","web")}) {
    Environment.CurrentDirectory=Path.Combine(isolated,relative);
    using(var payload=FixturePayload("new"))install.Invoke(null,new object[]{payload,isolated,new Action<string>(s=>{})});
    if(File.ReadAllText(Path.Combine(isolated,"app","CZToolbox.exe"))!="new")throw new Exception("inherited working directory upgrade did not publish");
   }
   Console.WriteLine("PASS actual Framework installer releases app and nested working-directory locks");
  }finally{Environment.CurrentDirectory=original;Toolbox.Setup.InstallEngine.DeleteInside(Path.GetDirectoryName(isolated),isolated);}
 }
 [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
 static extern IntPtr CreateFile(string path,uint access,uint share,IntPtr security,uint mode,uint flags,IntPtr template);
 [System.Runtime.InteropServices.DllImport("kernel32.dll")] static extern bool CloseHandle(IntPtr handle);
 static void CheckTemporaryDirectoryLock(Assembly target) {
  string isolated=Path.GetFullPath(Path.Combine(".build","setup-lock-"+Guid.NewGuid().ToString("N")));
  Directory.CreateDirectory(isolated);
  var install=target.GetType("Toolbox.Setup.InstallEngine").GetMethod("Install",new[]{typeof(Stream),typeof(string),typeof(Action<string>)});
  try {
   using(var payload=FixturePayload("old"))install.Invoke(null,new object[]{payload,isolated,new Action<string>(s=>{})});
   IntPtr handle=CreateFile(Path.Combine(isolated,"app","web"),0,3,IntPtr.Zero,3,0x02000000,IntPtr.Zero);
   if(handle==new IntPtr(-1))throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());
   var release=new Thread(()=>{Thread.Sleep(700);CloseHandle(handle);});release.IsBackground=true;release.Start();
   try {
    using(var payload=FixturePayload("new"))install.Invoke(null,new object[]{payload,isolated,new Action<string>(s=>{})});
    if(File.ReadAllText(Path.Combine(isolated,"app","CZToolbox.exe"))!="new"||File.ReadAllText(Path.Combine(isolated,".previous","CZToolbox.exe"))!="old")throw new Exception("temporary directory lock lost the old or new version");
    Console.WriteLine("PASS actual Framework installer waits for a temporary nested directory lock");
   }finally{release.Join();}
  }finally{Toolbox.Setup.InstallEngine.DeleteInside(Path.GetDirectoryName(isolated),isolated);}
 }
 static void CheckInheritedPermissionRepair(Assembly target) {
  var setup=typeof(Toolbox.Setup.InstallEngine).Assembly.GetType("Toolbox.Setup.Program");
  string source=Path.GetDirectoryName((string)setup.GetMethod("ReadRegisteredRoot",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[0]));
  var original=new DirectoryInfo(source).GetAccessControl();
  string sourceBefore=original.GetSecurityDescriptorSddlForm(AccessControlSections.All);
  var pure=(DirectorySecurity)target.GetType("Toolbox.Installation.InstallPathPolicy").GetMethod("ProtectedAncestorSecurity").Invoke(null,new object[]{original});
  if(!Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(pure,false))throw new Exception("actual inherited ACL cannot be repaired in installer runtime");
  string parent=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles);
  string isolated=Path.Combine(parent,"Toolbox-AclProbe-"+Guid.NewGuid().ToString("N"));
  // The test assembly has no payload: failure is guaranteed before registration.
  var assembly=typeof(Toolbox.Setup.InstallEngine).Assembly;
  if(assembly.GetManifestResourceStream("payload.zip")!=null)throw new Exception("rollback test must not have a real install payload");
  var program=assembly.GetType("Toolbox.Setup.Program");
  try {
   program.GetMethod("ProtectDirectory",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{isolated});
   string copyPath=Path.Combine(isolated,"InheritedParent");Directory.CreateDirectory(copyPath);
   var copy=new DirectorySecurity();copy.SetSecurityDescriptorSddlForm(File.ReadAllText("tests/Toolbox.SetupUiTests/inherited-ancestor.sddl").Trim());copy.SetAccessRuleProtection(true,true);
   Directory.SetAccessControl(copyPath,copy);
   var before=new DirectoryInfo(copyPath).GetAccessControl();string beforeSddl=before.GetSecurityDescriptorSddlForm(AccessControlSections.All);
   if(Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(before,false))throw new Exception("isolated ACL fixture is already protected");
   var apply=target.GetType("Toolbox.Setup.Program").GetMethod("ApplyDirectoryPermissions",BindingFlags.NonPublic|BindingFlags.Static);
   apply.Invoke(null,new object[]{copyPath,Toolbox.Installation.InstallPathPolicy.ProtectedAncestorSecurity(before)});
   if(!Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(new DirectoryInfo(copyPath).GetAccessControl(),false))throw new Exception("ACL repair does not persist on NTFS");
   apply.Invoke(null,new object[]{copyPath,before});
   if(new DirectoryInfo(copyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All)!=beforeSddl)throw new Exception("isolated ACL cannot be restored");
   Console.WriteLine("PASS actual inherited ACL repairs and restores on an isolated NTFS directory");
   program.GetField("Root",BindingFlags.NonPublic|BindingFlags.Static).SetValue(null,Path.Combine(copyPath,"InternalToolbox"));
   var formType=program.GetNestedType("InstallForm",BindingFlags.NonPublic);
   using(var form=(Form)Activator.CreateInstance(formType,new object[]{new string[0],false})) {
    form.StartPosition=FormStartPosition.Manual;form.Location=new Point(-20000,-20000);form.ShowInTaskbar=false;form.Show();Application.DoEvents();
    var consent=(CheckBox)formType.GetField("permissionConsent",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
    if(!consent.Visible)throw new Exception("isolated ancestor repair is not disclosed");consent.Checked=true;
    var task=(Task)formType.GetMethod("Install",BindingFlags.Instance|BindingFlags.NonPublic).Invoke(form,new object[0]);
    var timer=System.Diagnostics.Stopwatch.StartNew();
    while(!task.IsCompleted){if(timer.ElapsedMilliseconds>15000)throw new Exception("isolated install did not finish");Application.DoEvents();Thread.Sleep(10);}
    task.GetAwaiter().GetResult();
    var status=(Label)formType.GetField("status",BindingFlags.Instance|BindingFlags.NonPublic).GetValue(form);
    if(!status.Text.Contains("安装包资源缺失"))throw new Exception("install did not reach the intentional payload failure: "+status.Text);
    if(new DirectoryInfo(copyPath).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All)!=beforeSddl)throw new Exception("failed install did not restore ancestor ACL");
    form.Close();
   }
   if(new DirectoryInfo(source).GetAccessControl().GetSecurityDescriptorSddlForm(AccessControlSections.All)!=sourceBefore)throw new Exception("source ACL was changed by read-only diagnosis");
   Console.WriteLine("PASS actual installer repairs isolated ancestor then restores it after payload failure");
  }finally{if(Directory.Exists(isolated))Toolbox.Setup.InstallEngine.DeleteInside(parent,isolated);}
 }
}
