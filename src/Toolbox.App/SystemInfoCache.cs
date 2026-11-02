using System.IO;
using System.Text.Json;

namespace Toolbox.App;

// This cache contains a snapshot, never live hardware state. It is per Windows user.
public sealed class SystemInfoCache(string path,Func<Task<string?>> bootIdentity) {
 const int Schema=1;
 readonly SemaphoreSlim gate=new(1,1);
 SystemInfoReport? memory;
 string? memoryBoot;
 long generation;
 sealed record Stored(int Schema,string BootSession,SystemInfoReport Report);

 public static SystemInfoCache CreateDefault()=>new(
  Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),"InternalToolbox","Cache","hardware-v1.json"),
  WindowsBootSession.CurrentAsync);

 public async Task<SystemInfoReport> GetAsync(Func<CancellationToken,Task<SystemInfoReport>> query,bool refresh,CancellationToken token) {
  long observed=Interlocked.Read(ref generation);
  await gate.WaitAsync(token).ConfigureAwait(false);
  try {
   string? boot=await bootIdentity().WaitAsync(token).ConfigureAwait(false);
   token.ThrowIfCancellationRequested();
   if(memory!=null&&memoryBoot==boot&&(!refresh||observed!=generation))return memory;
   // Independent toolbox processes must recheck the shared snapshot while
   // holding the same file lock, rather than both collecting on a cache miss.
   using var sharedLock=boot!=null?await AcquireSharedLock(token).ConfigureAwait(false):null;
   if(!refresh&&boot!=null) {
    var stored=ReadStored(boot);
    if(stored!=null){memory=stored;memoryBoot=boot;Interlocked.Increment(ref generation);return stored;}
   }
   var report=await query(token).ConfigureAwait(false);
   SystemInfoReader.Validate(report);
   token.ThrowIfCancellationRequested();
   memory=report;memoryBoot=boot;Interlocked.Increment(ref generation);
   if(boot!=null)Save(boot,report);
   return report;
  } finally {gate.Release();}
 }
 async Task<FileStream?> AcquireSharedLock(CancellationToken token) {
  try{Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);}
  catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return null;}
  while(true) {
   token.ThrowIfCancellationRequested();
   try{return new FileStream(path+".lock",FileMode.OpenOrCreate,FileAccess.ReadWrite,FileShare.None);}
   catch(IOException ex) when((ex.HResult&0xffff) is 32 or 33){await Task.Delay(60,token).ConfigureAwait(false);}
   catch(Exception ex) when(ex is IOException or UnauthorizedAccessException){return null;}
  }
 }
 SystemInfoReport? ReadStored(string boot) {
  try {
   var file=new FileInfo(path);
   if(!file.Exists||file.Length>8*1024*1024)return null;
   var stored=JsonSerializer.Deserialize<Stored>(File.ReadAllText(path));
   if(stored==null||stored.Schema!=Schema||stored.BootSession!=boot||stored.Report==null)return null;
   SystemInfoReader.Validate(stored.Report);
   return stored.Report;
  } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException or JsonException or InvalidOperationException) {return null;}
 }
 void Save(string boot,SystemInfoReport report) {
  string temporary=path+"."+Guid.NewGuid().ToString("N")+".tmp";
  try {
   Directory.CreateDirectory(Path.GetDirectoryName(Path.GetFullPath(path))!);
   File.WriteAllText(temporary,JsonSerializer.Serialize(new Stored(Schema,boot,report)));
   File.Move(temporary,path,true);
  } catch(Exception ex) when(ex is IOException or UnauthorizedAccessException) {
   // A blocked cache folder must not prevent a successful read-only query.
  } finally {
   try{if(File.Exists(temporary))File.Delete(temporary);}catch(IOException){}catch(UnauthorizedAccessException){}
  }
 }
}
