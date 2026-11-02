using System;
using System.IO;
using System.Security.AccessControl;
using System.Security.Principal;
using System.Collections.Generic;
namespace Toolbox.Installation {
 public static class InstallPathPolicy {
  public const string ProductId="InternalToolbox-8EC7EC9F-76BC-45E9-83A8-535ED136E8E6";
  public const string MarkerName="installation.id";
  public static readonly string DefaultRoot=Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),"InternalToolbox");
  public static string Normalize(string path) {
   if(string.IsNullOrWhiteSpace(path)||path.Length<4||!char.IsLetter(path[0])||path[1]!=':'||(path[2]!='\\'&&path[2]!='/'))throw new InvalidOperationException("请选择本地磁盘中的完整安装路径。");
   string value=path.TrimEnd('\\','/');
   foreach(string part in value.Substring(3).Split(new[]{'\\','/'},StringSplitOptions.None))if(string.IsNullOrWhiteSpace(part)||part=="."||part==".."||part.TrimEnd(' ','.')!=part||part.IndexOfAny(Path.GetInvalidFileNameChars())>=0)throw new InvalidOperationException("安装路径包含无效目录名。");
   try{value=Path.GetFullPath(value);}catch(Exception ex){throw new InvalidOperationException("安装路径无效。",ex);}
   if(value.Length>140||value.Equals(Path.GetPathRoot(value),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请选择较短的独立文件夹，不能直接安装到磁盘根目录。");
   string windows=Environment.GetFolderPath(Environment.SpecialFolder.Windows);
   if(value.Equals(windows,StringComparison.OrdinalIgnoreCase)||value.StartsWith(windows+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("不能安装到 Windows 系统目录。");
   foreach(var special in new[]{Environment.SpecialFolder.ProgramFiles,Environment.SpecialFolder.ProgramFilesX86,Environment.SpecialFolder.CommonApplicationData,Environment.SpecialFolder.UserProfile,Environment.SpecialFolder.DesktopDirectory,Environment.SpecialFolder.MyDocuments,Environment.SpecialFolder.LocalApplicationData,Environment.SpecialFolder.ApplicationData,Environment.SpecialFolder.CommonDesktopDirectory,Environment.SpecialFolder.CommonDocuments}) {
    string critical=Environment.GetFolderPath(special);if(!string.IsNullOrEmpty(critical)&&value.Equals(critical,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("请选择独立的工具箱文件夹，不能直接使用系统或个人资料目录。");
   }
   return value;
  }
  public static void EnsureNoLinks(string path) {
   string current=Path.GetFullPath(path);
   while(!string.IsNullOrEmpty(current)){if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("安装路径不能包含链接或重定向目录。");current=Path.GetDirectoryName(current)??string.Empty;}
  }
  public static void ValidateNewTarget(string path,bool registered) {
   string root=Normalize(path);EnsureNoLinks(root);
   var drive=new DriveInfo(Path.GetPathRoot(root)??string.Empty);
   if(!drive.IsReady||drive.DriveType!=DriveType.Fixed||(drive.DriveFormat!="NTFS"&&drive.DriveFormat!="ReFS"))throw new InvalidOperationException("请选择支持 Windows 权限保护的本地 NTFS 或 ReFS 磁盘。");
   if(File.Exists(root))throw new InvalidOperationException("安装路径已被同名文件占用。");
   if(Directory.Exists(root)&&Directory.GetFileSystemEntries(root).Length>0&&(!registered||!IsInstalledRoot(root,true)))throw new InvalidOperationException("该文件夹已有其他文件，请选择空文件夹或本软件的原安装目录。");
   EnsureProtectedAncestors(root,registered);
  }
  static bool Trusted(SecurityIdentifier sid) {return sid!=null&&(sid.IsWellKnown(WellKnownSidType.BuiltinAdministratorsSid)||sid.IsWellKnown(WellKnownSidType.LocalSystemSid)||sid.Value=="S-1-5-80-956008885-3418522649-1831038044-1853292631-2271478464");}
  public static bool IsProtectedAncestor(DirectorySecurity security,bool volumeRoot) {
   var owner=security.GetOwner(typeof(SecurityIdentifier));if(owner==null||!Trusted((SecurityIdentifier)owner))return false;
   long dangerous=(long)(FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership|FileSystemRights.DeleteSubdirectoriesAndFiles)|0x10000000L;
   if(!volumeRoot)dangerous|=(long)FileSystemRights.Delete;
   foreach(FileSystemAccessRule rule in security.GetAccessRules(true,true,typeof(SecurityIdentifier)))if(rule.AccessControlType==AccessControlType.Allow&&(rule.PropagationFlags&PropagationFlags.InheritOnly)==0&&!Trusted((SecurityIdentifier)rule.IdentityReference)&&(((long)rule.FileSystemRights&dangerous)!=0))return false;
   return true;
  }
  public static void EnsureProtectedAncestors(string root,bool includeRoot) {
   string current=includeRoot?root:Path.GetDirectoryName(root)??string.Empty;
   while(!Directory.Exists(current)&&!string.IsNullOrEmpty(current)){if(File.Exists(current))throw new InvalidOperationException("安装路径的上级被同名文件占用。");current=Path.GetDirectoryName(current)??string.Empty;}
   while(!string.IsNullOrEmpty(current)){
    if(!IsProtectedAncestor(new DirectoryInfo(current).GetAccessControl(),current.Equals(Path.GetPathRoot(current),StringComparison.OrdinalIgnoreCase)))throw new InvalidOperationException("该位置的上级目录允许普通用户替换，请选择磁盘根目录下的独立文件夹或管理员管理的目录。");
    current=Path.GetDirectoryName(current)??string.Empty;
   }
  }
  public static string[] GetRepairableAncestors(string root) {
   EnsureNoLinks(root);var result=new List<string>();string current=Path.GetDirectoryName(root);
   while(!string.IsNullOrEmpty(current)) {
    if(File.Exists(current))throw new InvalidOperationException("安装路径的上级被同名文件占用。");
    if(Directory.Exists(current)) {
     bool volume=current.Equals(Path.GetPathRoot(current),StringComparison.OrdinalIgnoreCase);
     var security=new DirectoryInfo(current).GetAccessControl();
     if(!IsProtectedAncestor(security,volume)) {
      if(volume||!Trusted((SecurityIdentifier)security.GetOwner(typeof(SecurityIdentifier))))throw new InvalidOperationException("此位置需要调整整个磁盘或其他账户的权限，请选择其他安装位置。");
      result.Add(current);
     }
    }
    current=Path.GetDirectoryName(current);
   }
   return result.ToArray();
  }
  public static DirectorySecurity ProtectedAncestorSecurity(DirectorySecurity original) {
   if(!Trusted((SecurityIdentifier)original.GetOwner(typeof(SecurityIdentifier))))throw new InvalidOperationException("不能自动接管其他账户的目录。");
   // Preserve the raw ACEs: RemoveAccessRuleSpecific cannot remove inherited rules
   // in memory, even after SetAccessRuleProtection(true, true). Generic inheritance
   // masks also cannot be recreated through FileSystemAccessRule's public ctor.
   var descriptor=new RawSecurityDescriptor(original.GetSecurityDescriptorBinaryForm(),0);
   descriptor.SetFlags(descriptor.ControlFlags|ControlFlags.DiscretionaryAclProtected);
   if(descriptor.DiscretionaryAcl==null)throw new InvalidOperationException("目录缺少明确的权限规则，请选择其他安装位置。");
   var acl=new RawAcl(descriptor.DiscretionaryAcl.Revision,descriptor.DiscretionaryAcl.Count);
   uint dangerous=(uint)(FileSystemRights.ChangePermissions|FileSystemRights.TakeOwnership|FileSystemRights.DeleteSubdirectoriesAndFiles|FileSystemRights.Delete);
   foreach(GenericAce entry in descriptor.DiscretionaryAcl) {
    var rule=entry as QualifiedAce;
    if(rule!=null&&rule.AceQualifier==AceQualifier.AccessAllowed&&!Trusted(rule.SecurityIdentifier)&&(rule.AceFlags&AceFlags.InheritOnly)==0) {
     uint rights=unchecked((uint)rule.AccessMask);
     if((rights&0x10000000U)!=0)rights=(rights&~0x10000000U)|(uint)FileSystemRights.FullControl;
     uint allowed=rights&~dangerous;if(allowed==0)continue;
     rule.AccessMask=unchecked((int)allowed);
    }
    acl.InsertAce(acl.Count,entry);
   }
   descriptor.DiscretionaryAcl=acl;var bytes=new byte[descriptor.BinaryLength];descriptor.GetBinaryForm(bytes,0);
   var security=new DirectorySecurity();security.SetSecurityDescriptorBinaryForm(bytes);
   if(!IsProtectedAncestor(security,false))throw new InvalidOperationException("无法自动修复此目录的权限，请选择其他安装位置。");
   return security;
  }
  public static bool IsInstalledRoot(string path,bool allowLegacy) {
   string root=Normalize(path);EnsureNoLinks(root);EnsureNoLinks(Path.Combine(root,"app"));
   string marker=Path.Combine(root,MarkerName);
   if(File.Exists(marker)){EnsureNoLinks(marker);return new FileInfo(marker).Length<256&&File.ReadAllText(marker).Trim()==ProductId;}
   return allowLegacy&&root.Equals(DefaultRoot,StringComparison.OrdinalIgnoreCase)&&File.Exists(Path.Combine(root,"app","CZToolbox.exe"));
  }
  public static string ResolveInstalledRoot(string recorded,string applicationDirectory) {
   if(string.IsNullOrWhiteSpace(recorded))throw new InvalidOperationException("未找到安装记录，请先使用安装包安装软件。");
   string root=Normalize(recorded);
   if(!IsInstalledRoot(root,true)||!Path.GetFullPath(applicationDirectory).TrimEnd('\\','/').Equals(Path.Combine(root,"app"),StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("当前程序与安装记录不一致，请通过安装包修复后再更新。");
   return root;
  }
 }
}
