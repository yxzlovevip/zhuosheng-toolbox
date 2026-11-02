using System.Net;
using System.IO;
using System.Net.Http;
using System.Security.Cryptography;
using System.Text.Json;
using Toolbox.App;
using Toolbox.Core;
int pass=0,fail=0;
async Task Test(string name,Func<Task> test){try{await test();Console.WriteLine("PASS "+name);pass++;}catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);fail++;}}
void Assert(bool value,string reason="assertion failed"){if(!value)throw new Exception(reason);}
JsonElement Args(object value)=>JsonSerializer.SerializeToElement(value);
var auth=new AuthGate(); using var key=RSA.Create(2048); var wait=new TaskCompletionSource();
string reminderRoot=Path.Combine(Path.GetTempPath(),"cz-reminder-host-"+Guid.NewGuid());
var reminder=new NewSystemReminder(()=>DateTimeOffset.UtcNow.AddHours(-12),Path.Combine(reminderRoot,"state"));
var item=new ToolItem {Id="uac",Name="UAC",RelativePath="uac.reg",NeedsConfirmation=true};
bool holdSystemQuery=false;var queryStarted=new TaskCompletionSource();
var services=new AppServices {Auth=auth,DriverReminder=reminder,HardwareCache=new(Path.Combine(reminderRoot,"hardware.json"),()=>Task.FromResult<string?>("host-test-boot")),QuerySystemInfo=async(graphics,token)=>{if(holdSystemQuery){queryStarted.TrySetResult();await Task.Delay(20000,token);}return new SystemInfoReport {CollectedAt=DateTimeOffset.UtcNow,Sections=[new SystemInfoSection {Id=graphics?"graphics":"cpu",Title="设备"}]};},Catalog=new([item,item with {Id="simple",NeedsConfirmation=false}]),Launcher=new(Path.GetFullPath(".build/host-tools"),auth,()=>false),Updates=new(new UpdateSettings {ManifestUrl="https://updates.example.com/latest.json"},key.ExportSubjectPublicKeyInfoPem(),new Version(1,0,0),new HttpClient(new SlowHandler(wait))),ConfirmTool=_=>throw new Exception("legacy dialog should not open")};
using var controller=new WorkbenchController(services,_=>{});
await Test("runtime states reject locked sessions",async()=>{
 try{await controller.HandleAsync("getToolStates",Args(new{}));throw new Exception("locked query accepted");}
 catch(InvalidOperationException ex){Assert(ex.Message.Contains("登录"),ex.Message);}
});
await Test("bootstrap hides tools until authentication",async()=>{var result=JsonSerializer.SerializeToElement(await controller.HandleAsync("bootstrap",Args(new{})));Assert(!result.GetProperty("unlocked").GetBoolean());Assert(!result.TryGetProperty("tools",out _));});
await Test("startup notice must be accepted before login",async()=>{try{await controller.HandleAsync("login",Args(new{password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))}));throw new Exception("startup notice bypassed");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("使用提示"),ex.Message);}});
await Test("startup acceptance lasts only for the running host",async()=>{await controller.HandleAsync("acceptStartup",Args(new{}));var state=JsonSerializer.SerializeToElement(await controller.HandleAsync("bootstrap",Args(new{})));Assert(state.GetProperty("startupAccepted").GetBoolean());var freshAuth=new AuthGate();var freshServices=new AppServices {Auth=freshAuth,Catalog=services.Catalog,Launcher=new(Path.GetFullPath(".build/host-tools"),freshAuth,()=>false),Updates=new(new UpdateSettings(),key.ExportSubjectPublicKeyInfoPem(),new Version(1,0,0)),ConfirmTool=_=>false};using var fresh=new WorkbenchController(freshServices,_=>{});var next=JsonSerializer.SerializeToElement(await fresh.HandleAsync("bootstrap",Args(new{})));Assert(!next.GetProperty("startupAccepted").GetBoolean());});
await Test("protected command rejects unauthenticated request",async()=>{try{await controller.HandleAsync("requestLaunch",Args(new{toolId="uac"}));throw new Exception("accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("登录"));}});
await Test("software links require authentication",async()=>{try{await controller.HandleAsync("openSoftwareLink",Args(new{softwareId="anything",linkId="anything"}));throw new Exception("accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("登录"));}});
await Test("new system actions require authentication",async()=>{foreach(string action in new[]{"checkNewSystem","dismissNewSystem"})try{await controller.HandleAsync(action,Args(new{}));throw new Exception("accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("登录"));}});
await Test("hardware queries require authentication",async()=>{foreach(string action in new[]{"getSystemInfo","queryGraphicsInfo"})try{await controller.HandleAsync(action,Args(new{}));throw new Exception("accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("登录"));}});
await Test("invalid password is rejected and correct password exposes catalog",async()=>{try{await controller.HandleAsync("login",Args(new{password="bad"}));throw new Exception("accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("密码错误"));}var session=JsonSerializer.SerializeToElement(await controller.HandleAsync("login",Args(new{password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))})));Assert(session.GetProperty("tools").GetArrayLength()==2);});
await Test("version is read from build rather than fixed floor",async()=>{Assert(AppVersion.Normalize(new Version(2,3,4,9))==new Version(2,3,4));var session=JsonSerializer.SerializeToElement(await controller.HandleAsync("bootstrap",Args(new{})));string expected=args.FirstOrDefault()??"1.0.0";Assert(session.GetProperty("version").GetString()==expected,"build version not exposed");});
await Test("software links reject unknown entries rather than accepting caller URLs",async()=>{
 foreach(var request in new[]{new{softwareId="missing",linkId="missing",url="https://example.com"},new{softwareId="",linkId="",url="file:///C:/Windows/System32/calc.exe"}}){
  try{await controller.HandleAsync("openSoftwareLink",Args(request));throw new Exception("arbitrary link accepted");}
  catch(InvalidOperationException ex){Assert(ex.Message.Contains("下载入口不存在"),ex.Message);}
 }
});
await Test("software links resolve the bundled catalog and use the browser without executing downloads",async()=>{
 var property=typeof(AppServices).GetProperty("OpenSoftwareLink");Assert(property!=null,"browser opener missing");
 using var resource=typeof(AppServices).Assembly.GetManifestResourceStream("software-collection.json");Assert(resource!=null,"bundled software catalog missing");
 using var data=JsonDocument.Parse(resource!);var software=data.RootElement.GetProperty("items").EnumerateArray().Single(i=>i.GetProperty("name").GetString()=="Everything");
 var link=software.GetProperty("links")[0];string? opened=null;
 property!.SetValue(services,(Action<string>)(url=>opened=url));
 await controller.HandleAsync("openSoftwareLink",Args(new{softwareId=software.GetProperty("id").GetString(),linkId=link.GetProperty("id").GetString(),url="file:///C:/Windows/System32/calc.exe"}));
 Assert(opened==link.GetProperty("url").GetString(),"caller URL used instead of bundled link");
 try{await controller.HandleAsync("openSoftwareLink",Args(new{softwareId=software.GetProperty("id").GetString(),linkId="unknown"}));throw new Exception("unknown link accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("下载入口不存在"),ex.Message);}
 property.SetValue(services,(Action<string>)(_=>throw new InvalidOperationException("browser unavailable")));
 try{await controller.HandleAsync("openSoftwareLink",Args(new{softwareId=software.GetProperty("id").GetString(),linkId=link.GetProperty("id").GetString()}));throw new Exception("browser error swallowed");}catch(InvalidOperationException ex){Assert(ex.Message=="browser unavailable");}
});
await Test("hardware and GPU actions return their own device reports",async()=>{
 foreach(var (action,id) in new[]{("getSystemInfo","cpu"),("queryGraphicsInfo","graphics")}) {
  var report=(SystemInfoReport)await controller.HandleAsync(action,Args(new{}));Assert(report.Sections.Single().Id==id,"wrong hardware report");
 }
});
await Test("lock cancels hardware query and duplicate requests are rejected",async()=>{
 holdSystemQuery=true;var running=controller.HandleAsync("getSystemInfo",Args(new{refresh=true}));await queryStarted.Task.WaitAsync(TimeSpan.FromSeconds(2));
 try {await controller.HandleAsync("queryGraphicsInfo",Args(new{}));throw new Exception("duplicate accepted");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("查询"),ex.Message);}
 await controller.HandleAsync("lock",Args(new{}));bool cancelled=false;try {await running;}catch(OperationCanceledException){cancelled=true;}Assert(cancelled,"locked request returned identifiers");
 holdSystemQuery=false;await controller.HandleAsync("login",Args(new{password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))}));await controller.HandleAsync("getSystemInfo",Args(new{}));
});
await Test("risk tool launches go directly to validation without another confirmation",async()=>{try{await controller.HandleAsync("requestLaunch",Args(new{toolId="uac"}));throw new Exception("tool confirmation still returned");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("管理员"),ex.Message);}});
await Test("tool file check uses the same access checks as tool launch",async()=>{try{await controller.HandleAsync("checkToolFiles",Args(new{}));throw new Exception("admin gate bypassed");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("管理员"),"file check unavailable or bypassed access validation: "+ex.Message);}});
await Test("failed launch states expose the actual cause and survive relogin",async()=>{
 var states=Args(await controller.HandleAsync("getToolStates",Args(new{})));
 Assert(states.GetProperty("uac").GetProperty("Status").GetString()=="failed");
 Assert(states.GetProperty("uac").GetProperty("Message").GetString()!.Contains("管理员"));
 await controller.HandleAsync("lock",Args(new{}));
 var locked=Args(await controller.HandleAsync("bootstrap",Args(new{})));Assert(!locked.TryGetProperty("toolStates",out _));
 var session=Args(await controller.HandleAsync("login",Args(new{password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))})));
 Assert(session.GetProperty("toolStates").GetProperty("uac").GetProperty("Status").GetString()=="failed");
});
await Test("tool file check rejects locked sessions",async()=>{
 await controller.HandleAsync("lock",Args(new{}));
 try{await controller.HandleAsync("checkToolFiles",Args(new{}));throw new Exception("locked session checked tool files");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("登录"),ex.Message);}
 await controller.HandleAsync("login",Args(new{password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))}));
});
await Test("lock and login do not repeat the startup notice",async()=>{await controller.HandleAsync("lock",Args(new{}));var state=JsonSerializer.SerializeToElement(await controller.HandleAsync("bootstrap",Args(new{})));Assert(state.GetProperty("startupAccepted").GetBoolean());await controller.HandleAsync("login",Args(new{password=(System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))}));});
await Test("slow background update check does not block tool validation",async()=>{Task<object> checking=controller.HandleAsync("checkUpdate",Args(new{}));try{await controller.HandleAsync("requestLaunch",Args(new{toolId="simple"}));throw new Exception("admin gate bypassed");}catch(InvalidOperationException ex){Assert(ex.Message.Contains("管理员"),"background check blocked tool: "+ex.Message);}finally{wait.TrySetResult();try{await checking;}catch(HttpRequestException){}}});
await Test("new system status and dismissal use persistent host state",async()=>{
 var status=JsonSerializer.SerializeToElement(await controller.HandleAsync("checkNewSystem",Args(new{})));
 Assert(status.GetProperty("ShouldPrompt").GetBoolean()); Assert(status.GetProperty("InstalledAt").ValueKind==JsonValueKind.String);
 await controller.HandleAsync("dismissNewSystem",Args(new{}));
 status=JsonSerializer.SerializeToElement(await controller.HandleAsync("checkNewSystem",Args(new{})));
 Assert(!status.GetProperty("ShouldPrompt").GetBoolean());
});
await Test("Windows install timestamps parse both registry formats",()=>{
 var expected=DateTimeOffset.Parse("2026-10-20T08:30:00+08:00");
 Assert(WindowsInstallReader.Parse(expected.UtcDateTime.ToFileTimeUtc(),null)==expected);
 Assert(WindowsInstallReader.Parse(long.MaxValue,expected.ToUnixTimeSeconds())==expected);
 Assert(WindowsInstallReader.Parse(null,(int)expected.ToUnixTimeSeconds())==expected);
 Assert(WindowsInstallReader.Parse(null,null)==null); Assert(WindowsInstallReader.Parse(long.MaxValue,long.MaxValue)==null);
 return Task.CompletedTask;
});
await Test("real Windows installation time can be read without launching tools",()=>{
 var installed=WindowsInstallReader.Read();Assert(installed.HasValue,"installation time missing on current Windows");
 Console.WriteLine("Windows InstalledAt: "+installed.GetValueOrDefault().ToString("O"));return Task.CompletedTask;
});
if(Directory.Exists(reminderRoot))Directory.Delete(reminderRoot,true);
Console.WriteLine($"RESULT {pass} passed, {fail} failed");return fail==0?0:1;
sealed class SlowHandler(TaskCompletionSource wait):HttpMessageHandler {protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct){await wait.Task.WaitAsync(ct);return new(HttpStatusCode.ServiceUnavailable);}}
