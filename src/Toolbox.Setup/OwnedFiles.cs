using System;
using System.IO;
using System.Collections.Generic;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
namespace Toolbox.Setup {
 // Only unchanged payload files belong to the installer. Unknown/modified files stay in place.
 public static class OwnedFiles {
  [DataContract] public sealed class Entry {
   [DataMember(IsRequired=true)] public string Path;
   [DataMember(IsRequired=true)] public long Size;
   [DataMember(IsRequired=true)] public string Sha256;
  }
  public static void Remove(string folder) {
   if(!Directory.Exists(folder))return;InstallEngine.NoLinks(folder);
   string manifest=System.IO.Path.Combine(folder,"payload-files.json");
   if(!File.Exists(manifest))return;InstallEngine.NoLinks(manifest);
   if(new FileInfo(manifest).Length>4194304)throw new InvalidOperationException("安装文件清单过大，已保留原文件。");
   Entry[] entries;
   try{using(var stream=new MemoryStream(Encoding.UTF8.GetBytes(File.ReadAllText(manifest))))entries=(Entry[])new DataContractJsonSerializer(typeof(Entry[])).ReadObject(stream);}
   catch(Exception ex){throw new InvalidOperationException("安装文件清单无效，已保留原文件。",ex);}
   if(entries==null||entries.Length>10000)throw new InvalidOperationException("安装文件清单无效。");
   var targets=new List<string>();var unique=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var entry in entries){
    if(entry==null||string.IsNullOrWhiteSpace(entry.Path)||System.IO.Path.IsPathRooted(entry.Path)||entry.Size<0||entry.Sha256==null||!System.Text.RegularExpressions.Regex.IsMatch(entry.Sha256,@"\A[0-9a-fA-F]{64}\z"))throw new InvalidOperationException("安装文件清单无效。");
    foreach(string part in entry.Path.Replace('\\','/').Split('/'))if(string.IsNullOrWhiteSpace(part)||part=="."||part==".."||part.TrimEnd(' ','.')!=part||part.IndexOfAny(System.IO.Path.GetInvalidFileNameChars())>=0)throw new InvalidOperationException("安装文件清单路径无效。");
    string target=System.IO.Path.GetFullPath(System.IO.Path.Combine(folder,entry.Path));
    if(!target.StartsWith(System.IO.Path.GetFullPath(folder).TrimEnd('\\')+"\\",StringComparison.OrdinalIgnoreCase)||!unique.Add(target))throw new InvalidOperationException("安装文件清单路径越界或重复。");
    InstallEngine.NoLinks(target);targets.Add(target);
   }
   var directories=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   for(int i=0;i<entries.Length;i++){
    string target=targets[i];if(!File.Exists(target))continue;
    // Hold the file against concurrent replacement until its identity is checked.
    bool owned;using(var input=new FileStream(target,FileMode.Open,FileAccess.Read,FileShare.Read))using(var hash=SHA256.Create())owned=input.Length==entries[i].Size&&BitConverter.ToString(hash.ComputeHash(input)).Replace("-","").Equals(entries[i].Sha256,StringComparison.OrdinalIgnoreCase);
    if(!owned)continue;File.Delete(target);
    string parent=System.IO.Path.GetDirectoryName(target);while(parent!=null&&parent.Length>=folder.Length){directories.Add(parent);if(parent.Equals(folder,StringComparison.OrdinalIgnoreCase))break;parent=System.IO.Path.GetDirectoryName(parent);}
   }
   File.Delete(manifest);directories.Add(folder);
   var ordered=new List<string>(directories);ordered.Sort((a,b)=>b.Length.CompareTo(a.Length));
   foreach(string directory in ordered)if(Directory.Exists(directory)&&Directory.GetFileSystemEntries(directory).Length==0)Directory.Delete(directory);
  }
  public static void RemoveDownloads(string folder) {
   if(!Directory.Exists(folder))return;InstallEngine.NoLinks(folder);
   foreach(string file in Directory.GetFiles(folder))if(System.Text.RegularExpressions.Regex.IsMatch(System.IO.Path.GetFileName(file),@"\ASetup-[0-9a-fA-F]{32}\.exe(?:\.part)?\z")){InstallEngine.NoLinks(file);File.Delete(file);}
   if(Directory.GetFileSystemEntries(folder).Length==0)Directory.Delete(folder);
  }
 }
}
