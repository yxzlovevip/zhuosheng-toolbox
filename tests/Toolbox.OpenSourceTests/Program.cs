using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Toolbox.Core;

if(args.Contains("--payload-only")||args.Contains("--smoke"))return await PayloadChecks.Run(args.Contains("--smoke"));

int passed=0,failed=0;
async Task Test(string name,Func<Task> run){try{await run();Console.WriteLine("PASS "+name);passed++;}catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}}
void Assert(bool value,string message="assertion failed"){if(!value)throw new Exception(message);}
async Task Reject(Func<Task> run){try{await run();}catch(Exception ex)when(ex is InvalidOperationException or IOException){return;}throw new Exception("unsafe payload accepted");}
string Hash(byte[] bytes)=>Convert.ToHexString(SHA256.HashData(bytes));
string root=Path.Combine(Path.GetTempPath(),"cz-opensource-tests-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(Path.Combine(root,"app"));
try{
 byte[] binary=Encoding.UTF8.GetBytes("entry fixture"),dependency=Encoding.UTF8.GetBytes("dependency fixture");
 await File.WriteAllBytesAsync(Path.Combine(root,"entry.exe"),binary);
 await File.WriteAllBytesAsync(Path.Combine(root,"app/library.dll"),dependency);
 var auth=new AuthGate();auth.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")));using var launcher=new ToolLauncher(root,auth,()=>true);
 ToolItem WriteManifest(object[] files){byte[] manifest=JsonSerializer.SerializeToUtf8Bytes(files);File.WriteAllBytes(Path.Combine(root,"app/payload.json"),manifest);return new ToolItem{Id="oss",Name="工具",RelativePath="entry.exe",Sha256=Hash(binary),PayloadManifest="app/payload.json",PayloadManifestSha256=Hash(manifest),Arguments=["--setting","空 格\"路径"]};}
 object Entry(string path="library.dll",long? size=null,string? hash=null)=>new{Path=path,Size=size??dependency.Length,Sha256=hash??Hash(dependency)};
 await Test("fixed arguments preserve Unicode spaces and quotes without a shell",()=>{var start=ToolLauncher.CreateStartInfo(Path.Combine(root,"entry.exe"),["--setting","空 格\"路径"]);Assert(!start.UseShellExecute);Assert(start.ArgumentList.SequenceEqual(new[]{"--setting","空 格\"路径"}));return Task.CompletedTask;});
 await Test("batch operations accept hundreds of short paths",()=>{var paths=Enumerable.Range(0,200).Select(i=>$"C:\\资料\\{i}.png").ToArray();var start=ToolLauncher.CreateStartInfo(Path.Combine(root,"entry.exe"),paths);Assert(start.ArgumentList.SequenceEqual(paths));return Task.CompletedTask;});
 await Test("oversized batch is rejected before process creation",async()=>{await Reject(()=>Task.FromResult(ToolLauncher.CreateStartInfo(Path.Combine(root,"entry.exe"),[new string('a',28001)])));});
 await Test("registry import cannot gain silent arguments",async()=>{await Reject(()=>Task.FromResult(ToolLauncher.CreateStartInfo("C:\\test.reg",["/s"])));});
 await Test("full payload verifies",async()=>{await launcher.ValidateAsync(WriteManifest([Entry()]));});
 await Test("dependency tamper rejected even when entry exe unchanged",async()=>{var item=WriteManifest([Entry()]);await File.WriteAllTextAsync(Path.Combine(root,"app/library.dll"),"changed");await Reject(()=>launcher.ValidateAsync(item));await File.WriteAllBytesAsync(Path.Combine(root,"app/library.dll"),dependency);});
 await Test("missing dependency rejected",async()=>{await Reject(()=>launcher.ValidateAsync(WriteManifest([Entry("missing.dll")])));});
 await Test("payload manifest itself is pinned",async()=>{var item=WriteManifest([Entry()]);await File.AppendAllTextAsync(Path.Combine(root,"app/payload.json")," ");await Reject(()=>launcher.ValidateAsync(item));});
 await Test("manifest traversal and streams rejected",async()=>{foreach(string path in new[]{"../entry.exe","sub/../../entry.exe","library.dll:stream","/entry.exe","C:/entry.exe","sub/../library.dll"})await Reject(()=>launcher.ValidateAsync(WriteManifest([Entry(path)])));});
 await Test("manifest cannot escape via directory junction",async()=>{string link=Path.Combine(root,"app/link");try{var start=new System.Diagnostics.ProcessStartInfo("powershell.exe"){UseShellExecute=false,CreateNoWindow=true,ArgumentList={"-NoProfile","-Command","$ErrorActionPreference='Stop'; New-Item -ItemType Junction -Path $env:CZ_LINK -Target $env:CZ_TARGET | Out-Null"}};start.Environment["CZ_LINK"]=link;start.Environment["CZ_TARGET"]=root;using var p=System.Diagnostics.Process.Start(start)!;await p.WaitForExitAsync();Assert(p.ExitCode==0,"junction fixture creation failed");await Reject(()=>launcher.ValidateAsync(WriteManifest([Entry("link/entry.exe",binary.Length,Hash(binary))])));}finally{if(Directory.Exists(link))Directory.Delete(link);}});
 await Test("null and missing dependency paths rejected",async()=>{await Reject(()=>launcher.ValidateAsync(WriteManifest([new{Size=1,Sha256=Hash(dependency)}])));await Reject(()=>launcher.ValidateAsync(WriteManifest([null!])));});
 await Test("duplicate and empty manifests rejected",async()=>{await Reject(()=>launcher.ValidateAsync(WriteManifest([Entry(),Entry()])));await Reject(()=>launcher.ValidateAsync(WriteManifest([])));});
 await Test("invalid length and SHA-256 rejected",async()=>{await Reject(()=>launcher.ValidateAsync(WriteManifest([Entry(size:-1)])));await Reject(()=>launcher.ValidateAsync(WriteManifest([Entry(hash:"bad")])));});
 await Test("cancellation interrupts dependency verification",async()=>{var item=WriteManifest([Entry()]);using var cancelled=new CancellationTokenSource();cancelled.Cancel();try{await launcher.ValidateAsync(item,cancelled.Token);throw new Exception("cancellation ignored");}catch(OperationCanceledException){}});
}finally{Directory.Delete(root,true);}
Console.WriteLine($"RESULT {passed} passed, {failed} failed");return failed==0?0:1;
