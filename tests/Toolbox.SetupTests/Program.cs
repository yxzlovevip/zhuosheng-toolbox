using System.IO.Compression;
using Toolbox.Setup;
int pass=0,fail=0;
void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);pass++;}catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);fail++;}}
void Assert(bool value){if(!value)throw new Exception("assertion failed");}
void Reject(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("expected rejection");}
MemoryStream Zip(params (string,string)[] files){var ms=new MemoryStream();using(var archive=new ZipArchive(ms,ZipArchiveMode.Create,true)){foreach(var file in files){using var writer=new StreamWriter(archive.CreateEntry(file.Item1).Open());writer.Write(file.Item2);}}ms.Position=0;return ms;}
string root=Path.Combine(Path.GetTempPath(),"cz-install-tests-"+Guid.NewGuid()); Directory.CreateDirectory(root);
try{
 Test("registered multi-application payload supports 3 GiB while keeping a 4 GiB expansion bound",()=>{
  var method=typeof(InstallEngine).GetMethod("AddExpandedSize",System.Reflection.BindingFlags.Static|System.Reflection.BindingFlags.Public);
  Assert(method!=null);
  long Add(long current,long next){try{return (long)method!.Invoke(null,new object[]{current,next})!;}catch(System.Reflection.TargetInvocationException ex){throw ex.InnerException!;}}
  Assert(Add(1073741824L,2147483648L)==3221225472L);Assert(Add(4294967295L,1)==4294967296L);
  Reject(()=>Add(4294967296L,1));Reject(()=>Add(long.MaxValue,1));Reject(()=>Add(0,-1));
 });
 Test("large legitimate owned-file manifests remove owned files and preserve personal files",()=>{
  string folder=Path.Combine(root,"large-manifest");Directory.CreateDirectory(folder);
  string owned=Path.Combine(folder,"CZToolbox.exe");File.WriteAllText(owned,"owned");File.WriteAllText(Path.Combine(folder,"personal.txt"),"keep");
  string hash=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(File.ReadAllBytes(owned)));
  var entries=Enumerable.Range(0,6000).Select(i=>new OwnedFiles.Entry{Path="tools/"+new string('x',60)+"/file-"+i.ToString("D5")+".dll",Size=0,Sha256=new string('0',64)}).Append(new OwnedFiles.Entry{Path="CZToolbox.exe",Size=new FileInfo(owned).Length,Sha256=hash}).ToArray();
  string manifest=Path.Combine(folder,"payload-files.json");using(var output=File.Create(manifest))new System.Runtime.Serialization.Json.DataContractJsonSerializer(typeof(OwnedFiles.Entry[])).WriteObject(output,entries);
  Assert(new FileInfo(manifest).Length>1048576);OwnedFiles.Remove(folder);Assert(!File.Exists(owned)&&!File.Exists(manifest));Assert(File.ReadAllText(Path.Combine(folder,"personal.txt"))=="keep");
 });
 Test("captured inherited ancestor permissions can be repaired without mutating the source descriptor",()=>{var original=new System.Security.AccessControl.DirectorySecurity();original.SetSecurityDescriptorSddlForm(File.ReadAllText("tests/Toolbox.SetupUiTests/inherited-ancestor.sddl").Trim());string before=original.GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All);Assert(original.GetAccessRules(true,true,typeof(System.Security.Principal.SecurityIdentifier)).Cast<System.Security.AccessControl.FileSystemAccessRule>().Any(r=>r.IsInherited));var repaired=Toolbox.Installation.InstallPathPolicy.ProtectedAncestorSecurity(original);Assert(Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(repaired,false));Assert(original.GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All)==before);});
 Test("missing intermediate directories are validated from an existing protected ancestor",()=>{string target=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"Toolbox-Missing-"+Guid.NewGuid(),"nested","app-root");Toolbox.Installation.InstallPathPolicy.ValidateNewTarget(target,false);Assert(!Directory.Exists(Path.GetDirectoryName(target)));});
 Test("explicit ancestor repair removes replacement rights and preserves ordinary writing",()=>{var acl=new System.Security.AccessControl.DirectorySecurity();var admins=new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid,null);var users=new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.AuthenticatedUserSid,null);acl.SetOwner(admins);acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(admins,System.Security.AccessControl.FileSystemRights.FullControl,System.Security.AccessControl.AccessControlType.Allow));acl.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(users,System.Security.AccessControl.FileSystemRights.Modify,System.Security.AccessControl.AccessControlType.Allow));string before=acl.GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All);var repaired=Toolbox.Installation.InstallPathPolicy.ProtectedAncestorSecurity(acl);Assert(Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(repaired,false));Assert(acl.GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All)==before);Assert(repaired.GetAccessRules(true,true,typeof(System.Security.Principal.SecurityIdentifier)).Cast<System.Security.AccessControl.FileSystemAccessRule>().Any(r=>r.IdentityReference==users&&(r.FileSystemRights&System.Security.AccessControl.FileSystemRights.WriteData)!=0));});
 Test("install publishes all payload files",()=>{using var zip=Zip(("CZToolbox.exe","app1"),("web/index.html","page1"));InstallEngine.Install(zip,root,_=>{});Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="app1");Assert(File.Exists(Path.Combine(root,"app/web/index.html")));});
 Test("traversal archive leaves previous version untouched",()=>{using var zip=Zip(("../escaped.exe","bad"),("CZToolbox.exe","app2"));Reject(()=>InstallEngine.Install(zip,root,_=>{}));Assert(!File.Exists(Path.Combine(root,"escaped.exe")));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="app1");});
 Test("absolute and alternate stream entries rejected",()=>{foreach(string name in new[]{"C:/escape.exe","/escape.exe","CZToolbox.exe:evil","web/../CZToolbox.exe","web//a"}){using var zip=Zip((name,"bad"));Reject(()=>InstallEngine.Install(zip,root,_=>{}));}});
 Test("missing application rejects incomplete update",()=>{using var zip=Zip(("web/index.html","page2"));Reject(()=>InstallEngine.Install(zip,root,_=>{}));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="app1");});
 Test("locked application prevents replacement without data loss",()=>{using var locked=new FileStream(Path.Combine(root,"app/CZToolbox.exe"),FileMode.Open,FileAccess.Read,FileShare.Read);using var zip=Zip(("CZToolbox.exe","app2"));Reject(()=>InstallEngine.Install(zip,root,_=>{}));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="app1");});
 Test("installer inherited working directory does not lock the old app folder",()=>{
  string isolated=Path.Combine(root,"inherited-cwd"),original=Environment.CurrentDirectory;
  using(var initial=Zip(("CZToolbox.exe","old"),("web/index.html","old page")))InstallEngine.Install(initial,isolated,_=>{});
  Environment.CurrentDirectory=Path.Combine(isolated,"app","web");
  try {
   using var update=Zip(("CZToolbox.exe","new"),("web/index.html","new page"));
   InstallEngine.Install(update,isolated,_=>{});
   Assert(File.ReadAllText(Path.Combine(isolated,"app/CZToolbox.exe"))=="new");
   Assert(File.ReadAllText(Path.Combine(isolated,".previous/CZToolbox.exe"))=="old");
  }finally{Environment.CurrentDirectory=original;}
 });
 Test("upgrade preserves update configuration",()=>{File.WriteAllText(Path.Combine(root,"app/update-settings.json"),"configured");using var zip=Zip(("CZToolbox.exe","app2"),("update-settings.json","empty"));InstallEngine.Install(zip,root,_=>{});Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="app2");Assert(File.ReadAllText(Path.Combine(root,"app/update-settings.json"))=="configured");});
 Test("temporary nested directory lock is waited out without losing the old version",()=>{
  string isolated=Path.Combine(root,"transient-directory-lock");
  using(var initial=Zip(("CZToolbox.exe","old"),("web/index.html","old")))InstallEngine.Install(initial,isolated,_=>{});
  using var locked=DirectoryProbe.Lock(Path.Combine(isolated,"app/web"));
  // File probes all succeed: this lock applies to a directory, not a payload file.
  using(var probe=new FileStream(Path.Combine(isolated,"app/web/index.html"),FileMode.Open,FileAccess.ReadWrite,FileShare.None)){}
  var release=Task.Run(()=>{Thread.Sleep(700);locked.Dispose();});
  try {
   using var payload=Zip(("CZToolbox.exe","new"),("web/index.html","new"));InstallEngine.Install(payload,isolated,_=>{});
   Assert(File.ReadAllText(Path.Combine(isolated,"app/CZToolbox.exe"))=="new");
   Assert(File.ReadAllText(Path.Combine(isolated,".previous/CZToolbox.exe"))=="old");
  }finally{release.GetAwaiter().GetResult();}
 });
 Test("case insensitive duplicates rejected",()=>{using var zip=Zip(("CZToolbox.exe","one"),("cztoolbox.exe","two"));Reject(()=>InstallEngine.Install(zip,root,_=>{}));});
 Test("persistent directory lock preserves the old app and records the failing operation",()=>{
  string isolated=Path.Combine(root,"persistent-directory-lock");
  using(var initial=Zip(("CZToolbox.exe","old"),("web/index.html","old")))InstallEngine.Install(initial,isolated,_=>{});
  using var locked=DirectoryProbe.Lock(Path.Combine(isolated,"app/web"));
  using var payload=Zip(("CZToolbox.exe","new"));Reject(()=>InstallEngine.Install(payload,isolated,_=>{}));
  Assert(File.ReadAllText(Path.Combine(isolated,"app/CZToolbox.exe"))=="old");
  Assert(File.Exists(Path.Combine(isolated,"install.log")));
  string log=File.ReadAllText(Path.Combine(isolated,"install.log"));
  Assert(log.Contains(Path.Combine(isolated,"app"))&&(log.Contains("IOException")||log.Contains("UnauthorizedAccessException")));
 });
 Test("failed registration rolls back published update",()=>{using var zip=Zip(("CZToolbox.exe","app3"));Reject(()=>InstallEngine.Install(zip,root,_=>{},()=>throw new InvalidOperationException("registration failed")));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="app2");});
 Test("failed registration removes incomplete first install",()=>{string fresh=Path.Combine(root,"fresh");using var zip=Zip(("CZToolbox.exe","app1"));Reject(()=>InstallEngine.Install(zip,fresh,_=>{},()=>throw new InvalidOperationException("registration failed")));Assert(!Directory.Exists(Path.Combine(fresh,"app")));});
 Test("custom drive and Chinese folder retain chosen location",()=>{Assert(Toolbox.Installation.InstallPathPolicy.Normalize(@"D:\软件\内部工具箱\")==@"D:\软件\内部工具箱");});
 Test("root, relative, network and traversal paths are rejected",()=>{foreach(string path in new[]{@"D:\",@"relative\box",@"D:box",@"\\nas\share\box",@"D:\Tools\..\box",@"D:\Tools\bad.\box"})Reject(()=>Toolbox.Installation.InstallPathPolicy.Normalize(path));});
 Test("critical Windows directories cannot be installation targets",()=>{foreach(string path in new[]{Environment.GetFolderPath(Environment.SpecialFolder.Windows),Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),Environment.GetFolderPath(Environment.SpecialFolder.UserProfile)})Reject(()=>Toolbox.Installation.InstallPathPolicy.Normalize(path));});
 Test("fresh installation does not take over existing user files",()=>{string data=Path.Combine(root,"personal");Directory.CreateDirectory(data);File.WriteAllText(Path.Combine(data,"keep.txt"),"user-data");Reject(()=>Toolbox.Installation.InstallPathPolicy.ValidateNewTarget(data,false));Assert(File.ReadAllText(Path.Combine(data,"keep.txt"))=="user-data");});
 Test("registered custom installation resolves updates to its own root",()=>{string custom=Path.Combine(root,"custom");Directory.CreateDirectory(custom);File.WriteAllText(Path.Combine(custom,Toolbox.Installation.InstallPathPolicy.MarkerName),Toolbox.Installation.InstallPathPolicy.ProductId);Assert(Toolbox.Installation.InstallPathPolicy.ResolveInstalledRoot(custom,Path.Combine(custom,"app"))==custom);Reject(()=>Toolbox.Installation.InstallPathPolicy.ResolveInstalledRoot(custom,Path.Combine(root,"other","app")));});
 Test("unmarked custom folder is not treated as an installation",()=>{Reject(()=>Toolbox.Installation.InstallPathPolicy.ResolveInstalledRoot(root,Path.Combine(root,"app")));});
 Test("uninstall removes owned files and preserves added and modified files",()=>{string folder=Path.Combine(root,"owned");Directory.CreateDirectory(Path.Combine(folder,"tools"));File.WriteAllText(Path.Combine(folder,"owned.txt"),"owned");File.WriteAllText(Path.Combine(folder,"changed.txt"),"changed");File.WriteAllText(Path.Combine(folder,"tools/extra.zip"),"personal");File.WriteAllText(Path.Combine(folder,"payload-files.json"),System.Text.Json.JsonSerializer.Serialize(new[]{new{Path="owned.txt",Size=5,Sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("owned")))},new{Path="changed.txt",Size=3,Sha256=Convert.ToHexString(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("old")))}}));OwnedFiles.Remove(folder);Assert(!File.Exists(Path.Combine(folder,"owned.txt")));Assert(File.ReadAllText(Path.Combine(folder,"changed.txt"))=="changed");Assert(File.ReadAllText(Path.Combine(folder,"tools/extra.zip"))=="personal");});
 Test("invalid ownership manifest cannot delete outside installation",()=>{string folder=Path.Combine(root,"bad-manifest");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"payload-files.json"),"[{\"Path\":\"../keep.txt\",\"Size\":0,\"Sha256\":\""+new string('0',64)+"\"}]");File.WriteAllText(Path.Combine(root,"keep.txt"),"keep");Reject(()=>OwnedFiles.Remove(folder));Assert(File.ReadAllText(Path.Combine(root,"keep.txt"))=="keep");});
 Test("missing ownership manifest preserves legacy folder",()=>{string folder=Path.Combine(root,"legacy");Directory.CreateDirectory(folder);File.WriteAllText(Path.Combine(folder,"extra.txt"),"personal");OwnedFiles.Remove(folder);Assert(File.Exists(Path.Combine(folder,"extra.txt")));});
 Test("repeated upgrades retain unknown files from older backups",()=>{string folder=Path.Combine(root,"preserve-upgrades");using(var zip=Zip(("CZToolbox.exe","one")))InstallEngine.Install(zip,folder,_=>{});File.WriteAllText(Path.Combine(folder,"app/extra.zip"),"personal");using(var zip=Zip(("CZToolbox.exe","two")))InstallEngine.Install(zip,folder,_=>{});using(var zip=Zip(("CZToolbox.exe","three")))InstallEngine.Install(zip,folder,_=>{});string preserved=Directory.GetDirectories(folder,".preserved-*").Single();Assert(File.ReadAllText(Path.Combine(preserved,"extra.zip"))=="personal");Assert(File.ReadAllText(Path.Combine(folder,"app/CZToolbox.exe"))=="three");});
 Test("standard Program Files parent passes actual Windows ACL check",()=>{Toolbox.Installation.InstallPathPolicy.EnsureProtectedAncestors(Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"InternalToolbox-AclProbe"),false);});
 Test("user-owned existing parent is rejected without changing its permissions",()=>{string folder=Path.Combine(root,"personal-parent");Directory.CreateDirectory(folder);var before=new DirectoryInfo(folder).GetAccessControl().GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All);Reject(()=>Toolbox.Installation.InstallPathPolicy.EnsureProtectedAncestors(Path.Combine(folder,"toolbox"),false));Assert(new DirectoryInfo(folder).GetAccessControl().GetSecurityDescriptorSddlForm(System.Security.AccessControl.AccessControlSections.All)==before);});
 Test("ancestor ACL rejects user replacement while accepting secure drive roots",()=>{var security=new System.Security.AccessControl.DirectorySecurity();var admins=new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.BuiltinAdministratorsSid,null);var users=new System.Security.Principal.SecurityIdentifier(System.Security.Principal.WellKnownSidType.AuthenticatedUserSid,null);security.SetOwner(admins);security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(admins,System.Security.AccessControl.FileSystemRights.FullControl,System.Security.AccessControl.AccessControlType.Allow));security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(users,System.Security.AccessControl.FileSystemRights.Modify,System.Security.AccessControl.AccessControlType.Allow));Assert(Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(security,true));Assert(!Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(security,false));security.AddAccessRule(new System.Security.AccessControl.FileSystemAccessRule(users,System.Security.AccessControl.FileSystemRights.DeleteSubdirectoriesAndFiles,System.Security.AccessControl.AccessControlType.Allow));Assert(!Toolbox.Installation.InstallPathPolicy.IsProtectedAncestor(security,true));});
}finally{Directory.Delete(root,true);}
Console.WriteLine($"RESULT {pass} passed, {fail} failed");return fail==0?0:1;

static class DirectoryProbe {
 [System.Runtime.InteropServices.DllImport("kernel32.dll",CharSet=System.Runtime.InteropServices.CharSet.Unicode,SetLastError=true)]
 static extern Microsoft.Win32.SafeHandles.SafeFileHandle CreateFile(string path,uint access,uint share,IntPtr security,uint mode,uint flags,IntPtr template);
 public static Microsoft.Win32.SafeHandles.SafeFileHandle Lock(string path){var handle=CreateFile(path,0,3,IntPtr.Zero,3,0x02000000,IntPtr.Zero);if(handle.IsInvalid)throw new System.ComponentModel.Win32Exception(System.Runtime.InteropServices.Marshal.GetLastWin32Error());return handle;}
}
