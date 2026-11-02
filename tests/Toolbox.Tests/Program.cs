using System.Net;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Toolbox.Core;

int passed = 0, failed = 0;
async Task Test(string name, Func<Task> test) { try { await test(); Console.WriteLine("PASS " + name); passed++; } catch(Exception ex) { Console.WriteLine("FAIL " + name + ": " + ex.Message); failed++; } }
void Assert(bool value, string message = "assertion failed") { if(!value) throw new Exception(message); }
void Throws(Action action) { try { action(); } catch(InvalidOperationException) { return; } throw new Exception("expected rejection"); }
async Task ThrowsAsync(Func<Task> action) { try { await action(); } catch(Exception ex) when(ex is InvalidOperationException or HttpRequestException or CryptographicException) { return; } throw new Exception("expected rejection"); }

await Test("password wrong, correct, relock", () => { var g=new AuthGate(); Assert(!g.TryUnlock("1398")); Assert(!g.IsUnlocked); Assert(g.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")))); Assert(g.IsUnlocked); g.Lock(); Assert(!g.IsUnlocked); Assert(!g.TryUnlock((" " + (System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))))); return Task.CompletedTask; });
await Test("five failed attempts cause cooldown", () => { var time=new TestTime(); var g=new AuthGate(time); for(int i=0;i<5;i++) Assert(!g.TryUnlock("bad")); Assert(!g.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")))); Assert(g.RemainingSeconds>0); time.Advance(31); Assert(g.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")))); return Task.CompletedTask; });

string root=Path.Combine(Path.GetTempPath(),"cz-tests-"+Guid.NewGuid()); Directory.CreateDirectory(root);
await Test("planned maintenance tools remain available in the shipped catalog", () => {
  using var stream=File.OpenRead("assets/catalog.json");var catalog=ToolCatalog.Load(stream);
  foreach(var (file,category) in new[]{("13.卸载工具.exe","常用工具"),("3.系统必备运行库修复.exe","重装维护")}) {
    var entry=catalog.Items.SingleOrDefault(t=>Path.GetFileName(t.RelativePath)==file)??throw new Exception("missing planned tool: "+file);
    Assert(entry.Category==category);
    Assert(catalog.Search(entry.Name,category).Any(t=>t.Id==entry.Id));
    using var original=File.OpenRead(entry.RelativePath);Assert(Convert.ToHexString(SHA256.HashData(original))==entry.Sha256,"tool bytes do not match catalog: "+file);
  }
  return Task.CompletedTask;
});
await Test("removed maintenance tools are absent and ACE uses the new script wrapper", () => {
  using var stream=File.OpenRead("assets/catalog.json");var catalog=ToolCatalog.Load(stream);
  Assert(!catalog.Items.Any(t=>t.Id is "activation" or "security-update-control"),"removed tool entry is still available");
  foreach(string removed in new[]{"重装/系统激活工具.exe","工具/重装后工具/1.关杀毒更新.exe"})Assert(!File.Exists(removed),"removed source still present: "+removed);
  var ace=catalog.Items.Single(t=>t.Id=="ace-virtualization");
  using var current=File.OpenRead(ace.RelativePath);using var candidate=File.OpenRead("modules/AceVirtualization/build-output/ACE虚拟化环境检测修复.exe");
  Assert(Convert.ToHexString(SHA256.HashData(current))==Convert.ToHexString(SHA256.HashData(candidate)),"ACE is not the new wrapper");
  Assert(catalog.Items.Any(t=>t.Id=="heu-kms")&&catalog.Items.Any(t=>t.Id=="updates"),"unrelated activation/update tools were removed");
  return Task.CompletedTask;
});
try {
  string path=Path.Combine(root,"safe.exe"); await File.WriteAllTextAsync(path,"fixture");
  var item=new ToolItem { Id="safe",Name="测试",Category="网络工具",RelativePath="safe.exe",Sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("fixture"))),Summary="网络测速",Steps=["查看说明"] };
  await Test("launch requires login and administrator", async () => { var g=new AuthGate(); var l=new ToolLauncher(root,g,()=>true); await ThrowsAsync(()=>l.ValidateAsync(item)); g.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))); await ThrowsAsync(()=>new ToolLauncher(root,g,()=>false).ValidateAsync(item)); });
  var auth=new AuthGate(); auth.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests."))); var launcher=new ToolLauncher(root,auth,()=>true);
  await Test("original bytes accepted", async () => { Assert(await launcher.ValidateAsync(item)==path); });
  await Test("file check distinguishes intact, missing, changed and invalid paths without running tools",async()=>{
    string changed=Path.Combine(root,"changed.exe");await File.WriteAllTextAsync(changed,"different bytes");
    var report=await launcher.CheckFilesAsync([item,item with {Id="missing",RelativePath="missing.exe"},item with {Id="changed",RelativePath="changed.exe"},item with {Id="invalid",RelativePath="../escape.exe"}]);
    Assert(report.Items.Select(i=>i.Status).SequenceEqual(new[]{"ready","missing","changed","unavailable"}));
    Assert(report.Items.Select(i=>i.Id).SequenceEqual(new[]{"safe","missing","changed","invalid"}));
    Assert(File.ReadAllText(path)=="fixture"&&File.ReadAllText(changed)=="different bytes","check modified tool files");
    Assert(!launcher.HasRunningTools,"file check launched a process");
  });
  await Test("file check honors cancellation",async()=>{
    using var cancelled=new CancellationTokenSource();cancelled.Cancel();
    try {await launcher.CheckFilesAsync([item],cancelled.Token);throw new Exception("cancelled check returned results");}catch(OperationCanceledException){}
  });
  await Test("an unreadable tool does not prevent checking the remaining tools",async()=>{
    string lockedPath=Path.Combine(root,"locked.exe");await File.WriteAllTextAsync(lockedPath,"fixture");
    using var held=new FileStream(lockedPath,FileMode.Open,FileAccess.Read,FileShare.None);
    var report=await launcher.CheckFilesAsync([item with {Id="locked",RelativePath="locked.exe"},item]);
    Assert(report.Items.Select(i=>i.Status).SequenceEqual(new[]{"unavailable","ready"}));
  });
  await Test("changed tool rejected", async () => { await File.WriteAllTextAsync(path,"changed"); await ThrowsAsync(()=>launcher.ValidateAsync(item)); await File.WriteAllTextAsync(path,"fixture"); });
  await Test("path traversal and absolute path rejected", async () => { foreach(var relative in new[]{"../escape.exe",path,"sub/../../safe.exe","safe.exe:stream","sub/../safe.exe"}) await ThrowsAsync(()=>launcher.ValidateAsync(item with {RelativePath=relative})); });
  await Test("missing file rejected", async () => { await ThrowsAsync(()=>launcher.ValidateAsync(item with {RelativePath="missing.exe"})); });
  await Test("launch reports missing payload directory accurately", async () => {
    var missing=item with {RelativePath="not-packaged/gpu.exe"};
    try { await launcher.LaunchAsync(missing); throw new Exception("missing payload was accepted"); }
    catch(InvalidOperationException ex) { Assert(ex.InnerException is DirectoryNotFoundException); Assert(ex.Message.Contains("工具文件缺失"),ex.Message); Assert(ex.Message.Contains(missing.RelativePath),"missing relative path not disclosed"); }
  });
  await Test("reparse directory rejected", async () => { string link=Path.Combine(root,"link"); var psi=new System.Diagnostics.ProcessStartInfo("powershell.exe") {UseShellExecute=false,CreateNoWindow=true}; psi.ArgumentList.Add("-NoProfile"); psi.ArgumentList.Add("-Command"); psi.ArgumentList.Add("New-Item -ItemType Junction -Path $env:CZ_TEST_LINK -Target $env:CZ_TEST_ROOT | Out-Null"); psi.Environment["CZ_TEST_LINK"]=link; psi.Environment["CZ_TEST_ROOT"]=root; using var p=System.Diagnostics.Process.Start(psi)!; await p.WaitForExitAsync(); Assert(p.ExitCode==0); await ThrowsAsync(()=>launcher.ValidateAsync(item with {RelativePath="link/safe.exe"})); Directory.Delete(link); });
  await Test("search matches summary and category", () => { var c=new ToolCatalog([item,item with {Id="uac",Name="关闭 UAC 提示",Category="系统设置",Summary="管理员提示"}]); Assert(c.Search("测速",null).Single().Id=="safe"); Assert(!c.Search("测速","系统设置").Any()); Assert(c.Search("uac",null).Single().Id=="uac"); return Task.CompletedTask; });
  await Test("tool search uses purpose aliases, all query terms and category boundaries", () => {
    using var data=new MemoryStream(Encoding.UTF8.GetBytes("[{\"Id\":\"space\",\"Name\":\"SpaceSniffer\",\"Category\":\"磁盘工具\",\"SearchAliases\":[\"找大文件\",\"分析磁盘空间\"]},{\"Id\":\"rename\",\"Name\":\"PowerRename\",\"Category\":\"效率工具\",\"SearchAliases\":[\"批量改名\"]}]"));
    var searchable=ToolCatalog.Load(data);
    Assert(searchable.Search("  找大文件  ",null).Single().Id=="space");
    Assert(searchable.Search("spacesniffer 找大文件",null).Single().Id=="space");
    Assert(searchable.Search("批量改名","效率工具").Single().Id=="rename");
    Assert(!searchable.Search("找大文件","效率工具").Any());
    Assert(!searchable.Search("找大文件 不存在",null).Any());return Task.CompletedTask;
  });
  await Test("duplicate ids rejected", () => { Throws(()=>new ToolCatalog([item,item])); return Task.CompletedTask; });
  await Test("regedit invocation requires visible confirmation", () => { var start=ToolLauncher.CreateStartInfo("C:\\tools\\uac.reg"); Assert(start.FileName.EndsWith("regedit.exe",StringComparison.OrdinalIgnoreCase)); Assert(start.ArgumentList.Count==1); Assert(start.ArgumentList[0]=="C:\\tools\\uac.reg"); return Task.CompletedTask; });
  await Test("app manifest requests administrator", () => { string manifest=File.ReadAllText(Path.GetFullPath("src/Toolbox.App/app.manifest")); Assert(manifest.Contains("level=\"requireAdministrator\"")); return Task.CompletedTask; });

  using var key=RSA.Create(3072); string pub=key.ExportSubjectPublicKeyInfoPem();
  var info=new UpdateInfo {Version="1.1.0",Url="https://updates.example.com/setup.exe",Sha256=Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("installer"))),Size=9,Notes="测试新版"};
  byte[] Envelope(UpdateInfo i,bool corrupt=false) { var payload=JsonSerializer.SerializeToUtf8Bytes(i); var sig=key.SignData(payload,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1); if(corrupt) sig[0]^=1; return JsonSerializer.SerializeToUtf8Bytes(new { payload=Convert.ToBase64String(payload),signature=Convert.ToBase64String(sig) }); }
  UpdateService Service(byte[] response,HttpStatusCode status=HttpStatusCode.OK) => new(new UpdateSettings {ManifestUrl="https://updates.example.com/latest.json"},pub,new Version(1,0,0),new HttpClient(new ReplyHandler(response,status)));
  await Test("missing endpoint is distinct from latest", async () => { var s=new UpdateService(new UpdateSettings(),pub,new Version(1,0,0)); Assert((await s.CheckAsync()).State==UpdateState.NotConfigured); });
  await Test("valid signed newer manifest accepted", async () => { var r=await Service(Envelope(info)).CheckAsync(); Assert(r.State==UpdateState.Available); Assert(r.Info?.Version=="1.1.0"); });
  await Test("same version is latest", async () => { Assert((await Service(Envelope(info with {Version="1.0.0"})).CheckAsync()).State==UpdateState.Current); });
  await Test("invalid signature rejected", async () => { await ThrowsAsync(()=>Service(Envelope(info,true)).CheckAsync()); });
  await Test("http installer and cross host rejected", async () => { await ThrowsAsync(()=>Service(Envelope(info with {Url="http://updates.example.com/a.exe"})).CheckAsync()); await ThrowsAsync(()=>Service(Envelope(info with {Url="https://evil.example.com/a.exe"})).CheckAsync()); });
  await Test("redirect never interpreted as no update", async () => { await ThrowsAsync(()=>Service([],HttpStatusCode.Redirect).CheckAsync()); });
  await Test("offline returns failure", async () => { var s=new UpdateService(new UpdateSettings {ManifestUrl="https://updates.example.com/latest.json"},pub,new Version(1,0,0),new HttpClient(new ReplyHandler([],HttpStatusCode.ServiceUnavailable))); await ThrowsAsync(()=>s.CheckAsync()); });
  await Test("corrupt download rejected and removed", async () => { var s=Service(Encoding.UTF8.GetBytes("different")); string target=Path.Combine(root,"update.exe"); await ThrowsAsync(()=>s.DownloadAsync(info,target)); Assert(!File.Exists(target)); Assert(!File.Exists(target+".part")); });
  await Test("verified download is published", async () => { var s=Service(Encoding.UTF8.GetBytes("installer")); string target=Path.Combine(root,"update.exe"); await s.DownloadAsync(info,target); Assert(File.ReadAllText(target)=="installer"); });
  await Test("oversize and bad version manifests rejected", async () => { await ThrowsAsync(()=>Service(Envelope(info with {Size=0})).CheckAsync()); await ThrowsAsync(()=>Service(Envelope(info with {Version="garbage"})).CheckAsync()); });
  await Test("new system reminder covers 72 hours and rejects future or unknown dates", async () => {
    var time=new TestTime(); var now=time.GetUtcNow();
    foreach(var (installed,expected) in new (DateTimeOffset?,bool)[]{(now,true),(now.AddHours(-71),true),(now.AddHours(-72),true),(now.AddHours(-72).AddSeconds(-1),false),(now.AddSeconds(1),false),(null,false)}) {
      var reminder=new NewSystemReminder(()=>installed,Path.Combine(root,"boundary-"+Guid.NewGuid()),time);
      Assert((await reminder.CheckAsync()).ShouldPrompt==expected,$"wrong decision for {installed}");
    }
  });
  await Test("dismissal survives restart but a different Windows installation prompts again", async () => {
    var time=new TestTime(); var installed=time.GetUtcNow().AddDays(-1); string state=Path.Combine(root,"reminder/state.txt");
    var first=new NewSystemReminder(()=>installed,state,time);
    Assert((await first.CheckAsync()).ShouldPrompt); await first.DismissAsync();
    Assert(!(await first.CheckAsync()).ShouldPrompt);
    Assert(!(await new NewSystemReminder(()=>installed,state,time).CheckAsync()).ShouldPrompt);
    Assert((await new NewSystemReminder(()=>installed.AddHours(1),state,time).CheckAsync()).ShouldPrompt);
  });
  await Test("installation read failure is disclosed without a driver prompt", async () => {
    var reminder=new NewSystemReminder(()=>throw new IOException("unavailable"),Path.Combine(root,"unreadable"));
    var status=await reminder.CheckAsync(); Assert(!status.ShouldPrompt); Assert(status.InstalledAt==null); Assert(status.Message.Length>0);
  });
  await Test("reminder persistence failure is disclosed and not acknowledged", async () => {
    string blocked=Path.Combine(root,"blocked"); await File.WriteAllTextAsync(blocked,"not a directory");
    var reminder=new NewSystemReminder(()=>DateTimeOffset.UtcNow.AddHours(-1),Path.Combine(blocked,"state"));
    try {await reminder.DismissAsync(); throw new Exception("save failure hidden");} catch(IOException) {}
    Assert((await reminder.CheckAsync()).ShouldPrompt);
  });
} finally { Directory.Delete(root,true); }
Console.WriteLine($"RESULT {passed} passed, {failed} failed"); return failed==0?0:1;

sealed class TestTime : TimeProvider { DateTimeOffset now=DateTimeOffset.UtcNow; public override DateTimeOffset GetUtcNow()=>now; public void Advance(int s)=>now=now.AddSeconds(s); }
sealed class ReplyHandler(byte[] data,HttpStatusCode status):HttpMessageHandler { protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct)=>Task.FromResult(new HttpResponseMessage(status){Content=new ByteArrayContent(data),RequestMessage=request}); }
