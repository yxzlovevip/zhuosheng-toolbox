using System;
using System.IO;
using System.IO.Compression;
using System.Collections.Generic;
using System.Diagnostics;
using System.Threading;
using System.Text;
namespace Toolbox.Setup {
 public static class InstallEngine {
  public static void Install(Stream payload,string root,Action<string> status) {Install(payload,root,status,null);}
  public static void Install(Stream payload,string root,Action<string> status,Action commit) {
   Run(root,status,delegate(string stage) {
    status("正在解压文件…");
    using(var zip=new ZipArchive(payload,ZipArchiveMode.Read,true)) {
     var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long expanded=0;
     foreach(var entry in zip.Entries) {
      bool directory;string destination=EntryPath(stage,entry.FullName,entry.Length,entry.ExternalAttributes,names,ref expanded,out directory);
      if(directory){Directory.CreateDirectory(destination);continue;}
      Directory.CreateDirectory(Path.GetDirectoryName(destination));
      using(var input=entry.Open())using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None))input.CopyTo(output);
     }
    }
   },commit);
  }
  // The stored ZIP can be expanded as 7zr produces it. Authentication must finish
  // inside extract, before Run probes or moves the existing application.
  public static void InstallStored(Stream payload,string root,Action<string> status,Action verified,Action commit) {
   Run(root,status,delegate(string stage){ExtractStored(payload,stage);verified();},commit);
  }
  static void Run(string root,Action<string> status,Action<string> extract,Action commit) {
   root=Path.GetFullPath(root); Directory.CreateDirectory(root); NoLinks(root);
   string current=Path.Combine(root,"app"),stage=Path.Combine(root,".stage-"+Guid.NewGuid().ToString("N")),backup=Path.Combine(root,".previous");
   // Windows locks the process working directory even after the old app exits.
   // Older clients launch setup from app, so release our own directory handle.
   string working=Path.GetFullPath(Environment.CurrentDirectory).TrimEnd(Path.DirectorySeparatorChar);
   if(working.Equals(current,StringComparison.OrdinalIgnoreCase)||working.StartsWith(current+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))Environment.CurrentDirectory=root;
   Directory.CreateDirectory(stage); bool movedOld=false,published=false;
   try {
    Record(root,"开始安装；工作目录="+Environment.CurrentDirectory+"；暂存目录="+stage);
    extract(stage);
    if(!File.Exists(Path.Combine(stage,"CZToolbox.exe"))) throw new InvalidOperationException("安装包缺少主程序。");
    if(Directory.Exists(current)) {
     NoLinks(current);
     foreach(string file in Directory.GetFiles(current,"*",SearchOption.AllDirectories)) {
      NoLinks(file);
      try {using(var probe=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None)) {}}
      catch(IOException ex) {throw new InvalidOperationException("程序或工具仍在运行，请全部关闭后重试。",ex);}
      catch(UnauthorizedAccessException ex) {throw new InvalidOperationException("程序文件不可替换，请关闭工具并检查权限。",ex);}
     }
     string config=Path.Combine(current,"update-settings.json"); if(File.Exists(config)) File.Copy(config,Path.Combine(stage,"update-settings.json"),true);
    }
    if(Directory.Exists(backup)) {
     OwnedFiles.Remove(backup);
     if(Directory.Exists(backup))MoveDirectory(root,backup,Path.Combine(root,".preserved-"+Guid.NewGuid().ToString("N")),status);
    }
    status("正在安装…");
    if(Directory.Exists(current)) {MoveDirectory(root,current,backup,status);movedOld=true;}
    MoveDirectory(root,stage,current,status); published=true;
    if(commit!=null)commit();
    status("安装完成");
    Record(root,"安装完成");
   } catch(Exception ex) {
    Record(root,"安装失败；工作目录="+Environment.CurrentDirectory+Environment.NewLine+ex);
    if(published&&Directory.Exists(current)) DeleteInside(root,current);
    if(movedOld&&!Directory.Exists(current)&&Directory.Exists(backup)) MoveDirectory(root,backup,current,status);
    if(ex is InvalidOperationException) throw;
    throw new InvalidOperationException("安装失败，原版本已保留："+ex.Message,ex);
   } finally {if(Directory.Exists(stage)) DeleteInside(root,stage);}
  }
  static string EntryPath(string stage,string fullName,long length,int attributes,HashSet<string> names,ref long expanded,out bool directory) {
   string name=fullName.Replace('\\','/');directory=name.EndsWith("/",StringComparison.Ordinal);string clean=name.TrimEnd('/');
   if(Path.IsPathRooted(clean)||string.IsNullOrEmpty(clean))throw new InvalidOperationException("安装包包含无效路径。");
   foreach(string part in clean.Split('/'))if(string.IsNullOrWhiteSpace(part)||part=="."||part==".."||part.Contains(":")||part.TrimEnd(' ','.')!=part)throw new InvalidOperationException("安装包路径越界或含有无效字符。");
   if(!names.Add(clean)||((attributes>>16)&0xF000)==0xA000)throw new InvalidOperationException("安装包包含重复路径或链接。");
   expanded=AddExpandedSize(expanded,length);string destination=Path.GetFullPath(Path.Combine(stage,clean));Bounded(stage,destination);return destination;
  }
  struct StoredEntry {public uint Size,Crc;public long Offset;public ushort Flags;}
  static byte[] Bytes(BinaryReader reader,int count) {byte[] bytes=reader.ReadBytes(count);if(bytes.Length!=count)throw new EndOfStreamException();return bytes;}
  static string Name(BinaryReader reader,int length,ushort flags) {
   byte[] bytes=Bytes(reader,length);
   if((flags&2048)!=0)return new UTF8Encoding(false,true).GetString(bytes);
   foreach(byte value in bytes)if(value>127)throw new InvalidOperationException("安装包文件名编码无效。");
   return Encoding.ASCII.GetString(bytes);
  }
  static void ExtractStored(Stream payload,string stage) {
   // Build emits seekable, uncompressed ZIPs below 4 GiB. Reject other ZIP
   // features explicitly rather than allowing a decoder to write archive paths.
   var reader=new BinaryReader(payload);var files=new Dictionary<string,StoredEntry>(StringComparer.Ordinal);
   var names=new HashSet<string>(StringComparer.OrdinalIgnoreCase);long expanded=0,offset=0;byte[] buffer=new byte[131072];
   try {
    uint signature=reader.ReadUInt32();
    while(signature==0x04034b50) {
     reader.ReadUInt16();ushort flags=reader.ReadUInt16(),method=reader.ReadUInt16();reader.ReadUInt32();uint crc=reader.ReadUInt32(),packed=reader.ReadUInt32(),size=reader.ReadUInt32();int nameLength=reader.ReadUInt16(),extra=reader.ReadUInt16();
     if((flags&~2048)!=0||method!=0||packed!=size||size==uint.MaxValue)throw new InvalidOperationException("安装包不是受支持的流式 ZIP 格式。");
     string name=Name(reader,nameLength,flags);Bytes(reader,extra);bool directory;
     string destination=EntryPath(stage,name,size,0,names,ref expanded,out directory);
     files.Add(name,new StoredEntry{Size=size,Crc=crc,Offset=offset,Flags=flags});
     if(directory){if(size!=0)throw new InvalidOperationException("安装包目录数据无效。");Directory.CreateDirectory(destination);}
     else {
      Directory.CreateDirectory(Path.GetDirectoryName(destination));
      using(var output=new FileStream(destination,FileMode.CreateNew,FileAccess.Write,FileShare.None)) {
       long remaining=size;while(remaining>0){int count=reader.Read(buffer,0,(int)Math.Min(remaining,buffer.Length));if(count==0)throw new EndOfStreamException();output.Write(buffer,0,count);remaining-=count;}
      }
     }
     offset+=30L+nameLength+extra+size;signature=reader.ReadUInt32();
    }
    long centralStart=offset;var indexed=new HashSet<string>(StringComparer.Ordinal);
    while(signature==0x02014b50) {
     reader.ReadUInt16();reader.ReadUInt16();ushort flags=reader.ReadUInt16(),method=reader.ReadUInt16();reader.ReadUInt32();uint crc=reader.ReadUInt32(),packed=reader.ReadUInt32(),size=reader.ReadUInt32();int nameLength=reader.ReadUInt16(),extra=reader.ReadUInt16(),comment=reader.ReadUInt16(),disk=reader.ReadUInt16();reader.ReadUInt16();uint attributes=reader.ReadUInt32(),localOffset=reader.ReadUInt32();
     string name=Name(reader,nameLength,flags);Bytes(reader,extra);Bytes(reader,comment);StoredEntry entry;
     if(disk!=0||method!=0||packed!=size||!files.TryGetValue(name,out entry)||!indexed.Add(name)||entry.Flags!=flags||entry.Crc!=crc||entry.Size!=size||entry.Offset!=localOffset||((attributes>>16)&0xF000)==0xA000)throw new InvalidOperationException("安装包文件索引不一致或包含链接。");
     offset+=46L+nameLength+extra+comment;signature=reader.ReadUInt32();
    }
    if(signature!=0x06054b50||reader.ReadUInt16()!=0||reader.ReadUInt16()!=0)throw new InvalidOperationException("安装包文件索引无效。");
    int entries=reader.ReadUInt16(),total=reader.ReadUInt16();uint centralSize=reader.ReadUInt32(),centralOffset=reader.ReadUInt32();int endComment=reader.ReadUInt16();Bytes(reader,endComment);
    if(entries!=total||total!=files.Count||indexed.Count!=files.Count||centralSize!=offset-centralStart||centralOffset!=centralStart||payload.ReadByte()!=-1)throw new InvalidOperationException("安装包文件索引不完整。");
   }catch(EndOfStreamException error){throw new InvalidOperationException("安装文件不完整，请重新获取安装包。",error);}
  }
  public static long AddExpandedSize(long current,long next) {
   const long limit=4294967296L;
   if(current<0||next<0||current>limit||next>limit-current)throw new InvalidOperationException("安装包展开内容过大。");
   return current+next;
  }
  static void MoveDirectory(string root,string source,string target,Action<string> status) {
   Bounded(root,source);Bounded(root,target);
   var elapsed=Stopwatch.StartNew();bool waiting=false;
   while(true) {
    try {Directory.Move(source,target);Record(root,"目录替换完成："+source+" -> "+target);return;}
    catch(Exception error) {
     int code=error.HResult&0xffff;
     if(!(error is IOException||error is UnauthorizedAccessException)||(code!=5&&code!=32&&code!=33))throw;
     if(!waiting){Record(root,"目录暂时无法替换："+source+" -> "+target+Environment.NewLine+error);waiting=true;status("正在等待 Windows 释放程序目录，请稍候…");}
     if(elapsed.Elapsed>=TimeSpan.FromSeconds(15))throw new InvalidOperationException("等待后仍无法替换目录："+source+"。原始原因："+error.Message+" 详细记录："+Path.Combine(root,"install.log"),error);
     Thread.Sleep(200);
    }
   }
  }
  static void Record(string root,string message) {
   try {string path=Path.Combine(root,"install.log");NoLinks(path);File.AppendAllText(path,DateTimeOffset.Now.ToString("O")+" "+message+Environment.NewLine);}catch{}
  }
  static void Bounded(string root,string target) {if(!Path.GetFullPath(target).StartsWith(Path.GetFullPath(root).TrimEnd(Path.DirectorySeparatorChar)+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("目标超出安装目录。");}
  public static void NoLinks(string path) {string current=Path.GetFullPath(path);while(current!=null) {if((Directory.Exists(current)||File.Exists(current))&&(File.GetAttributes(current)&FileAttributes.ReparsePoint)!=0) throw new InvalidOperationException("安装路径包含链接。");current=Path.GetDirectoryName(current);}}
  public static void DeleteInside(string root,string target) {
   Bounded(root,target);
   if((File.GetAttributes(target)&FileAttributes.ReparsePoint)!=0) {Directory.Delete(target);return;}
   foreach(string file in Directory.GetFiles(target)) {File.SetAttributes(file,FileAttributes.Normal);File.Delete(file);}
   foreach(string folder in Directory.GetDirectories(target)) DeleteInside(root,folder);
   Directory.Delete(target);
  }
 }
}
