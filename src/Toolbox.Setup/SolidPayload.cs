using System;
using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Threading;

namespace Toolbox.Setup {
 // The caller protects the installation root before invoking this method.
 // 7zr only writes stdout; InstallEngine validates and stages the stored ZIP.
 public static class SolidPayload {
  const long MaximumZip=4294967295L;
  public static void Install(Stream archive,Stream decoder,string archiveHash,string decoderHash,string zipHash,long zipLength,string root,Action<string> status,Action commit) {
   if(archive==null||decoder==null||!archive.CanSeek||archive.Length<=0||archive.Length>4294967296L||zipLength<=0||zipLength>MaximumZip)throw new InvalidOperationException("安装包压缩信息无效。");
   root=Path.GetFullPath(root);Directory.CreateDirectory(root);InstallEngine.NoLinks(root);
   long required=archive.Length+zipLength+67108864L;
   var drive=new DriveInfo(Path.GetPathRoot(root));
   if(drive.AvailableFreeSpace<required)throw new InvalidOperationException("安装位置空间不足，请至少保留 "+Math.Ceiling(required/1073741824.0)+" GB 可用空间（含临时解压文件）。");
   string workspace=Path.Combine(root,".solid-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(workspace);
   try {
    status("正在校验并准备压缩文件…");
    string packed=Path.Combine(workspace,"payload.7z"),exe=Path.Combine(workspace,"7zr.exe");
    SaveChecked(archive,packed,archiveHash,4294967296L);SaveChecked(decoder,exe,decoderHash,16777216L);
    status("正在解压并安装离线工具…");
    var start=new ProcessStartInfo(exe,"x \""+packed+"\" payload.zip -so -y -bd -bb0") {UseShellExecute=false,CreateNoWindow=true,WorkingDirectory=workspace,RedirectStandardOutput=true,RedirectStandardError=true};
    using(var process=new Process {StartInfo=start}) {
     if(!process.Start())throw new InvalidOperationException("无法启动安装解压程序。");
     var errors=process.StandardError.ReadToEndAsync();
     try {
      using(var timeout=new Timer(delegate {try{if(!process.HasExited)process.Kill();}catch{}},null,600000,Timeout.Infinite)) {
       int lastPercent=-1;
       using(var input=new CheckedReadStream(process.StandardOutput.BaseStream,zipLength,delegate(long bytes) {
        int percent=(int)(bytes*100/zipLength);if(percent==lastPercent)return;lastPercent=percent;
        status("正在解压离线工具（"+percent+"%）…");
       }))using(var buffered=new BufferedStream(input,131072))InstallEngine.InstallStored(buffered,root,status,delegate {
        process.WaitForExit();string detail=errors.GetAwaiter().GetResult();
        if(process.ExitCode!=0)throw new InvalidOperationException("安装文件解压失败，请重新获取安装包。"+(detail.Length==0?"":" "+detail.Substring(0,Math.Min(detail.Length,300))));
        input.Verify(zipHash);
       },delegate {
        // Still inside the transaction: cleanup failure must roll back the app.
        RemoveWorkspace(root,workspace);if(commit!=null)commit();
       });
      }
     }finally{if(!process.HasExited){process.Kill();process.WaitForExit();}}
    }
   }finally{RemoveWorkspace(root,workspace);}
  }
  static void SaveChecked(Stream input,string path,string expected,long limit) {
   using(var hash=SHA256.Create())using(var output=new FileStream(path,FileMode.CreateNew,FileAccess.Write,FileShare.None,131072)) {
    byte[] buffer=new byte[131072];long total=0;int count;
    while((count=input.Read(buffer,0,buffer.Length))>0){if(count>limit-total)throw new InvalidOperationException("安装包数据超过预期大小。");output.Write(buffer,0,count);hash.TransformBlock(buffer,0,count,buffer,0);total+=count;}
    hash.TransformFinalBlock(new byte[0],0,0);
    if(!EqualHash(hash,expected))throw new InvalidOperationException("安装包或解压程序完整性校验失败。");
   }
  }
  static bool EqualHash(HashAlgorithm hash,string expected){var actual=hash.Hash;return actual!=null&&string.Equals(BitConverter.ToString(actual).Replace("-",""),expected,StringComparison.OrdinalIgnoreCase);}
  sealed class CheckedReadStream:Stream {
   readonly Stream input;readonly long length;readonly Action<long> progress;readonly HashAlgorithm hash=SHA256.Create();long total;bool ended;
   public CheckedReadStream(Stream input,long length,Action<long> progress){this.input=input;this.length=length;this.progress=progress;}
   public override int Read(byte[] buffer,int offset,int count) {
    if(count==0||ended)return 0;int read=input.Read(buffer,offset,count);
    if(read==0){ended=true;hash.TransformFinalBlock(new byte[0],0,0);if(total!=length)throw new InvalidOperationException("解压后的安装文件长度不符。");return 0;}
    if(read>length-total)throw new InvalidOperationException("安装包解压输出超过预期大小。");
    hash.TransformBlock(buffer,offset,read,buffer,offset);total+=read;progress(total);return read;
   }
   public void Verify(string expected){if(!ended||total!=length||!EqualHash(hash,expected))throw new InvalidOperationException("解压后的安装文件校验失败。");}
   protected override void Dispose(bool disposing){if(disposing)hash.Dispose();base.Dispose(disposing);}
   public override bool CanRead{get{return true;}}public override bool CanSeek{get{return false;}}public override bool CanWrite{get{return false;}}
   public override long Length{get{throw new NotSupportedException();}}public override long Position{get{throw new NotSupportedException();}set{throw new NotSupportedException();}}
   public override void Flush(){}public override long Seek(long offset,SeekOrigin origin){throw new NotSupportedException();}public override void SetLength(long value){throw new NotSupportedException();}public override void Write(byte[] buffer,int offset,int count){throw new NotSupportedException();}
  }
  static void RemoveWorkspace(string root,string workspace) {
   var elapsed=Stopwatch.StartNew();
   while(Directory.Exists(workspace)) {
    try{InstallEngine.DeleteInside(root,workspace);return;}
    catch(Exception error){int code=error.HResult&0xffff;if(!(error is IOException||error is UnauthorizedAccessException)||(code!=5&&code!=32&&code!=33)||elapsed.ElapsedMilliseconds>=3000)throw;}
    // After a forced exit, Windows can briefly retain the executable image mapping.
    Thread.Sleep(50);
   }
  }
 }
}
