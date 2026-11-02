using System.Diagnostics;
using System.IO.Compression;
using System.Reflection;
using System.Security.Cryptography;
using Toolbox.Setup;

int passed=0,failed=0;
void Test(string name,Action action){try{action();Console.WriteLine("PASS "+name);passed++;}catch(Exception error){Console.WriteLine("FAIL "+name+": "+error.Message);failed++;}}
void Assert(bool value){if(!value)throw new Exception("assertion failed");}
void Reject(Action action){try{action();}catch(InvalidOperationException){return;}throw new Exception("expected rejection");}
string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
string fixtures=Path.GetFullPath(".build/size-optimization/fixtures-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(fixtures);
byte[] helper=File.ReadAllBytes(".build/compression/7zr-26.05.exe");
byte[] Zip(params (string Path,string Content)[] files){using var bytes=new MemoryStream();using(var zip=new ZipArchive(bytes,ZipArchiveMode.Create,true)){foreach(var file in files){using var writer=new StreamWriter(zip.CreateEntry(file.Path,CompressionLevel.NoCompression).Open());writer.Write(file.Content);}}return bytes.ToArray();}
(byte[] Zip,byte[] Archive) Pack(byte[] zip){string folder=Path.Combine(fixtures,Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);File.WriteAllBytes(Path.Combine(folder,"payload.zip"),zip);var info=new ProcessStartInfo(Path.GetFullPath(".build/compression/7zr-26.05.exe")){UseShellExecute=false,CreateNoWindow=true,RedirectStandardOutput=true,WorkingDirectory=folder};foreach(string arg in new[]{"a","payload.7z","payload.zip","-mx=1","-bsp0"})info.ArgumentList.Add(arg);using var process=Process.Start(info)!;string output=process.StandardOutput.ReadToEnd();process.WaitForExit();if(process.ExitCode!=0)throw new Exception(output);return(zip,File.ReadAllBytes(Path.Combine(folder,"payload.7z")));}
void Install(byte[] archive,byte[] decoder,string archiveHash,string decoderHash,string zipHash,long zipLength,string root,Action? commit=null,Action<string>? status=null){
 var type=typeof(InstallEngine).Assembly.GetType("Toolbox.Setup.SolidPayload")??throw new Exception("SolidPayload installer is missing");
 var method=type.GetMethod("Install")!;
 using var input=new MemoryStream(archive);using var exe=new MemoryStream(decoder);
 try{method.Invoke(null,new object?[]{input,exe,archiveHash,decoderHash,zipHash,zipLength,root,status??(Action<string>)(_=>{}),commit});}catch(TargetInvocationException error){throw error.InnerException!;}
}
var valid=Pack(Zip(("CZToolbox.exe","new"),("tools/中文 空格/副本一.dll","same"),("tools/中文 空格/副本二.dll","same")));
string root=Path.Combine(fixtures,"中文 安装位置");
void Valid(Action? commit=null)=>Install(valid.Archive,helper,Hash(valid.Archive),Hash(helper),Hash(valid.Zip),valid.Zip.Length,root,commit);
void NoTemporary(){Assert(!Directory.GetDirectories(root,".solid-*").Any());}
try{
 Test("streaming installation never writes a full intermediate ZIP",()=>{
  bool committed=false;
  Install(valid.Archive,helper,Hash(valid.Archive),Hash(helper),Hash(valid.Zip),valid.Zip.Length,root,()=>committed=true,_=>{
   Assert(!Directory.GetFiles(root,"*.zip",SearchOption.AllDirectories).Any());
  });
  Assert(committed);Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();
 });
 Test("solid offline payload restores every file in Chinese and spaced paths",()=>{Valid();Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");Assert(File.ReadAllText(Path.Combine(root,"app/tools/中文 空格/副本一.dll"))=="same");Assert(File.ReadAllText(Path.Combine(root,"app/tools/中文 空格/副本二.dll"))=="same");NoTemporary();});
 Test("damaged compressed payload rejected before replacing old app",()=>{byte[] corrupt=(byte[])valid.Archive.Clone();corrupt[10]^=1;Reject(()=>Install(corrupt,helper,Hash(valid.Archive),Hash(helper),Hash(valid.Zip),valid.Zip.Length,root));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();});
 Test("changed embedded decoder rejected without executing it",()=>{Reject(()=>Install(valid.Archive,new byte[]{0,1,2},Hash(valid.Archive),Hash(helper),Hash(valid.Zip),valid.Zip.Length,root));NoTemporary();});
 Test("decoded ZIP hash mismatch leaves old app untouched",()=>{Reject(()=>Install(valid.Archive,helper,Hash(valid.Archive),Hash(helper),new string('0',64),valid.Zip.Length,root));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();});
 Test("late authentication failure preserves both current app and existing backup",()=>{
  string isolated=Path.Combine(fixtures,"late-authentication");Directory.CreateDirectory(Path.Combine(isolated,"app"));Directory.CreateDirectory(Path.Combine(isolated,".previous"));
  File.WriteAllText(Path.Combine(isolated,"app/CZToolbox.exe"),"current");File.WriteAllText(Path.Combine(isolated,".previous/CZToolbox.exe"),"older");bool committed=false;
  Reject(()=>Install(valid.Archive,helper,Hash(valid.Archive),Hash(helper),new string('0',64),valid.Zip.Length,isolated,()=>committed=true));
  Assert(!committed);Assert(File.ReadAllText(Path.Combine(isolated,"app/CZToolbox.exe"))=="current");Assert(File.ReadAllText(Path.Combine(isolated,".previous/CZToolbox.exe"))=="older");
  Assert(!Directory.GetDirectories(isolated,".stage-*").Any());Assert(!Directory.GetDirectories(isolated,".solid-*").Any());
 });
 Test("decoded length mismatch and oversized output rejected",()=>{foreach(long length in new[]{valid.Zip.Length-1L,valid.Zip.Length+1L,8L*1024*1024*1024})Reject(()=>Install(valid.Archive,helper,Hash(valid.Archive),Hash(helper),Hash(valid.Zip),length,root));NoTemporary();});
 Test("invalid 7z input with a valid transport hash is rejected and cleaned",()=>{byte[] broken=new byte[]{1,2,3,4};Reject(()=>Install(broken,helper,Hash(broken),Hash(helper),Hash(valid.Zip),valid.Zip.Length,root));NoTemporary();});
 Test("inner ZIP path checks still protect solid installations",()=>{var malicious=Pack(Zip(("CZToolbox.exe","bad"),("../escaped.txt","bad")));Reject(()=>Install(malicious.Archive,helper,Hash(malicious.Archive),Hash(helper),Hash(malicious.Zip),malicious.Zip.Length,root));Assert(!File.Exists(Path.Combine(root,"escaped.txt")));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();});
 Test("streaming rejects unsafe names and duplicate paths before replacing the app",()=>{
  foreach(string name in new[]{"C:/escape.exe","/escape.exe","x:stream","web/../escape","web//a","web/a.","cztoolbox.exe"}) {
   var bad=Pack(Zip(("CZToolbox.exe","bad"),(name,"bad")));
   Reject(()=>Install(bad.Archive,helper,Hash(bad.Archive),Hash(helper),Hash(bad.Zip),bad.Zip.Length,root));
   Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();
  }
 });
 Test("streaming checks the central directory for links and mismatched local records",()=>{
  int central=Enumerable.Range(0,valid.Zip.Length-4).First(i=>BitConverter.ToUInt32(valid.Zip,i)==0x02014b50);
  foreach(int kind in new[]{0,1,2,3}) {
   byte[] zip=(byte[])valid.Zip.Clone();
   if(kind==0)BitConverter.GetBytes(unchecked((int)0xA0000000)).CopyTo(zip,central+38);
   if(kind==1)zip[central+46]=(byte)'X';
   if(kind==2)zip[central+42]=1;
   if(kind==3)zip=zip.Concat(new byte[]{1}).ToArray();
   var bad=Pack(zip);Reject(()=>Install(bad.Archive,helper,Hash(bad.Archive),Hash(helper),Hash(bad.Zip),bad.Zip.Length,root));
   Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();
  }
 });
 Test("streaming handles empty directories and files across read buffer boundaries",()=>{
  string content=new string('x',131073)+"中文尾部";
  byte[] zip;using(var memory=new MemoryStream()) {
   using(var writer=new ZipArchive(memory,ZipArchiveMode.Create,true)) {
    writer.CreateEntry("empty/",CompressionLevel.NoCompression);
    writer.CreateEntry("empty/zero.txt",CompressionLevel.NoCompression);
    using(var text=new StreamWriter(writer.CreateEntry("CZToolbox.exe",CompressionLevel.NoCompression).Open()))text.Write(content);
   }
   zip=memory.ToArray();
  }
  var packed=Pack(zip);string isolated=Path.Combine(fixtures,"empty-and-buffers");
  Install(packed.Archive,helper,Hash(packed.Archive),Hash(helper),Hash(zip),zip.Length,isolated);
  Assert(Directory.Exists(Path.Combine(isolated,"app/empty")));Assert(new FileInfo(Path.Combine(isolated,"app/empty/zero.txt")).Length==0);
  Assert(File.ReadAllText(Path.Combine(isolated,"app/CZToolbox.exe"))==content);
 });
 Test("truncated ZIP structures and unsupported compression cannot commit",()=>{
  byte[] deflated;using(var memory=new MemoryStream()){using(var zip=new ZipArchive(memory,ZipArchiveMode.Create,true))using(var text=new StreamWriter(zip.CreateEntry("CZToolbox.exe",CompressionLevel.Optimal).Open()))text.Write("new");deflated=memory.ToArray();}
  foreach(byte[] zip in new[]{valid.Zip.Take(20).ToArray(),valid.Zip.Take(valid.Zip.Length-5).ToArray(),deflated}) {
   bool committed=false;var bad=Pack(zip);
   Reject(()=>Install(bad.Archive,helper,Hash(bad.Archive),Hash(helper),Hash(bad.Zip),bad.Zip.Length,root,()=>committed=true));
   Assert(!committed);Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();
  }
 });
 Test("solid upgrade preserves configuration and supports rollback",()=>{File.WriteAllText(Path.Combine(root,"app/update-settings.json"),"personal");File.WriteAllText(Path.Combine(root,"app/CZToolbox.exe"),"old");Reject(()=>Valid(()=>throw new InvalidOperationException("registration failed")));Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="old");Valid();Assert(File.ReadAllText(Path.Combine(root,"app/update-settings.json"))=="personal");Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="new");NoTemporary();});
 Test("temporary cleanup failure rolls back before committing installation",()=>{
  File.WriteAllText(Path.Combine(root,"app/CZToolbox.exe"),"old");bool committed=false;Task? release=null;
  try{
   Reject(()=>Install(valid.Archive,helper,Hash(valid.Archive),Hash(helper),Hash(valid.Zip),valid.Zip.Length,root,()=>committed=true,status=>{
    if(status!="正在安装…")return;
    string packed=Path.Combine(Directory.GetDirectories(root,".solid-*").Single(),"payload.7z");
    var locked=new FileStream(packed,FileMode.Open,FileAccess.Read,FileShare.Read);
    release=Task.Run(()=>{Thread.Sleep(3500);locked.Dispose();});
   }));
   Assert(!committed);Assert(File.ReadAllText(Path.Combine(root,"app/CZToolbox.exe"))=="old");NoTemporary();
  }finally{release?.GetAwaiter().GetResult();}
 });
}finally{Directory.Delete(fixtures,true);}
Console.WriteLine($"RESULT {passed} passed, {failed} failed");return failed==0?0:1;

