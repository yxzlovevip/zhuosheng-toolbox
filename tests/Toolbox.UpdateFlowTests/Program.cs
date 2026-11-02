using System.Diagnostics;
using System.IO;
using System.Net;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Toolbox.App;
using Toolbox.Core;

if(Path.GetFileNameWithoutExtension(Environment.ProcessPath)=="UpdateWaitFixture") {await Task.Delay(Timeout.Infinite);return 0;}
int passed=0,failed=0;
string root=Path.GetFullPath(".build/update-flow-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(Path.Combine(root,"tools"));
// This fixture only waits; it never runs a maintenance command.
// No bundled maintenance tool, real installer, installed directory or registry writes.
foreach(string file in Directory.GetFiles(AppContext.BaseDirectory))File.Copy(file,Path.Combine(root,"tools",Path.GetFileName(file)));
string fixture=Path.Combine(root,"tools","UpdateWaitFixture.exe");
File.Copy(Path.Combine(AppContext.BaseDirectory,"Toolbox.UpdateFlowTests.exe"),fixture);
string fixtureHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(fixture)));
byte[] installer=Encoding.UTF8.GetBytes("inert download fixture, never executed");
using var key=RSA.Create(2048);
var info=new UpdateInfo {Version="9.0.0",Url="https://updates.example.com/setup.exe",Size=installer.Length,Sha256=Convert.ToHexString(SHA256.HashData(installer))};
byte[] payload=JsonSerializer.SerializeToUtf8Bytes(info);
byte[] envelope=JsonSerializer.SerializeToUtf8Bytes(new {payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1))});
JsonElement Args(object value)=>JsonSerializer.SerializeToElement(value);
void Assert(bool value,string message){if(!value)throw new Exception(message);}
async Task Test(string name,Func<Task> test){try{await test();Console.WriteLine("PASS "+name);passed++;}catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}}

async Task<(WorkbenchController Controller,AppServices Services,DownloadHandler Handler)> Create(){
 var auth=new AuthGate();
 var item=new ToolItem {Id="wait",Name="等待测试工具",RelativePath="UpdateWaitFixture.exe",Sha256=fixtureHash};
 var handler=new DownloadHandler(envelope,installer);
 var services=new AppServices {Auth=auth,Catalog=new([item,item with {Id="second",Name="第二个等待工具"}]),Launcher=new(Path.Combine(root,"tools"),auth,()=>true),Updates=new(new UpdateSettings {ManifestUrl="https://updates.example.com/latest.json"},key.ExportSubjectPublicKeyInfoPem(),new Version(1,4,4),new HttpClient(handler)),ConfirmTool=_=>false};
 // The locator is an IO boundary: test installation paths never consult the real registry.
 typeof(AppServices).GetProperty("ResolveInstallRoot")?.SetValue(services,(Func<string,string>)(_=>root));
 var controller=new WorkbenchController(services,_=>{});
 await controller.HandleAsync("acceptStartup",Args(new{}));await controller.HandleAsync("login",Args(new{password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))}));
 await controller.HandleAsync("checkUpdate",Args(new{}));
 return(controller,services,handler);
}
await Test("an active tool allows a full validated download and blocks install with its name",async()=>{
 var context=await Create();using var controller=context.Controller;
 await controller.HandleAsync("requestLaunch",Args(new{toolId="wait"}));
 Assert(context.Services.Launcher.HasRunningTools,"waiting fixture exited too early");
 context.Handler.Release.TrySetResult();
 await controller.HandleAsync("downloadUpdate",Args(new{}));
 Assert(context.Handler.Downloads==1,"no actual download");
 Assert(Directory.GetFiles(Path.Combine(root,".updates"),"Setup-*.exe").Any(file=>File.ReadAllBytes(file).SequenceEqual(installer)),"download was not validated and committed");
 try{await controller.HandleAsync("installUpdate",Args(new{}));throw new Exception("installation started with a live tool");}
 catch(InvalidOperationException ex){Assert(ex.Message.Contains("等待测试工具"),"blocking error omits running tool name: "+ex.Message);}
});
await Test("starting another tool during a held download is allowed while duplicate downloads are refused",async()=>{
 var context=await Create();using var controller=context.Controller;
 Task<object> downloading=controller.HandleAsync("downloadUpdate",Args(new{}));
 try{
  await context.Handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
  await controller.HandleAsync("requestLaunch",Args(new{toolId="second"}));
  Assert(context.Services.Launcher.GetRunStates()["second"].Status=="running","tool did not start during download");
  try{await controller.HandleAsync("downloadUpdate",Args(new{}));throw new Exception("duplicate download accepted");}catch(InvalidOperationException){}
 }finally{context.Handler.Release.TrySetResult();await downloading;}
});
await Test("locking cancels a held download without committing an installer",async()=>{
 var context=await Create();using var controller=context.Controller;
 Task<object> downloading=controller.HandleAsync("downloadUpdate",Args(new{}));
 try{
  await context.Handler.Started.Task.WaitAsync(TimeSpan.FromSeconds(3));
  await controller.HandleAsync("lock",Args(new{}));
  try{await downloading;throw new Exception("locked download completed");}catch(OperationCanceledException){}
  Assert(!Directory.GetFiles(Path.Combine(root,".updates"),"*.part").Any(),"partial file survived cancellation");
 }finally{context.Handler.Release.TrySetResult();try{await downloading;}catch(OperationCanceledException){}}
});
Console.WriteLine($"RESULT {passed} passed, {failed} failed; isolated evidence: {root}");return failed==0?0:1;

sealed class DownloadHandler(byte[] manifest,byte[] installer):HttpMessageHandler {
 public TaskCompletionSource Started {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public TaskCompletionSource Release {get;}=new(TaskCreationOptions.RunContinuationsAsynchronously);
 public int Downloads {get;private set;}
 protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){
  if(request.RequestUri!.AbsolutePath.EndsWith(".json"))return new(HttpStatusCode.OK){Content=new ByteArrayContent(manifest)};
  Downloads++;Started.TrySetResult();await Release.Task.WaitAsync(ct);
  return new(HttpStatusCode.OK){Content=new ByteArrayContent(installer)};
 }
}
