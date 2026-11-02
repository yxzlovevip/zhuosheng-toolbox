using System.Diagnostics;
using System.Security.Cryptography;
using Toolbox.Core;

// This harmless fixture only creates waiting copies of itself, never real tools.
if(args.FirstOrDefault()=="--owner") {
 var owned=Launcher(args[1]);
 await owned.LaunchAsync(Item(args[1]));
 File.WriteAllText(Path.Combine(args[1],"owner-ready"),"ready");
 await Task.Delay(Timeout.Infinite);
 return 0;
}
if(Path.GetFileName(Environment.ProcessPath)=="ToolFixture.exe") {
 string folder=Environment.CurrentDirectory;
 string role=args.FirstOrDefault()??"parent";
 File.WriteAllText(Path.Combine(folder,role+".pid"),Environment.ProcessId.ToString());
 if(role!="grandchild") {
  var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,WorkingDirectory=folder,CreateNoWindow=true};
  start.ArgumentList.Add(role=="parent"?"child":"grandchild");
  using var child=Process.Start(start)!;
 }
 if(role=="parent"&&File.ReadAllText(Path.Combine(folder,"mode.txt"))=="orphan")return 0;
 await Task.Delay(Timeout.Infinite);
 return 0;
}

int passed=0,failed=0;
string root=Path.GetFullPath(".build/process-tests-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(root);
var processes=new List<Process>();
async Task WaitFor(Func<bool> condition,string reason) {
 var timer=Stopwatch.StartNew();
 while(!condition()){if(timer.Elapsed>TimeSpan.FromSeconds(12))throw new Exception(reason);await Task.Delay(20);}
}
async Task Test(string name,Func<Task> action) {
 try{await action();Console.WriteLine("PASS "+name);passed++;}
 catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}
 finally{foreach(var process in processes){try{if(!process.HasExited){process.Kill(true);process.WaitForExit(5000);}}catch(InvalidOperationException){}process.Dispose();}processes.Clear();}
}
string Fixture(string name,string mode="tree") {
 string folder=Path.Combine(root,name+" with spaces");Directory.CreateDirectory(folder);
 foreach(string file in Directory.GetFiles(AppContext.BaseDirectory))File.Copy(file,Path.Combine(folder,Path.GetFileName(file)));
 File.Copy(Path.Combine(folder,"Toolbox.ProcessTests.exe"),Path.Combine(folder,"ToolFixture.exe"));
 File.WriteAllText(Path.Combine(folder,"mode.txt"),mode);return folder;
}
async Task<Process[]> Tree(string folder) {
 await WaitFor(()=>File.Exists(Path.Combine(folder,"grandchild.pid")),"fixture did not create its descendants");
 var tree=new[]{"child","grandchild"}.Select(role=>Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(folder,role+".pid"))))).ToArray();
 processes.AddRange(tree);return tree;
}
void DisposeLauncher(ToolLauncher launcher){if((object)launcher is IDisposable owned)owned.Dispose();}
try {
 await Test("external single-instance tool is rejected without stopping the external tree",async()=>{
  string folder=Fixture("external-instance");
  var start=new ProcessStartInfo(Path.Combine(folder,"ToolFixture.exe")){UseShellExecute=false,WorkingDirectory=folder,CreateNoWindow=true};
  using var external=Process.Start(start)!;processes.Add(Process.GetProcessById(external.Id));var descendants=await Tree(folder);
  using var launcher=Launcher(folder);
  try{await launcher.LaunchAsync(Item(folder) with{InstanceExecutable="ToolFixture.exe"});throw new Exception("external instance accepted");}
  catch(InvalidOperationException ex)when(ex.Message.Contains("工具箱外运行")){}
  launcher.Dispose();if(external.HasExited||descendants.Any(p=>p.HasExited))throw new Exception("external process was stopped");
 });
 await Test("a tool already owned by this toolbox is not mistaken for an external instance",async()=>{
  string folder=Fixture("owned-instance");using var launcher=Launcher(folder);
  using var first=await launcher.LaunchAsync(Item(folder) with{InstanceExecutable="ToolFixture.exe"});processes.Add(Process.GetProcessById(first.Id));await Tree(folder);
  using var second=await launcher.LaunchAsync(Item(folder) with{InstanceExecutable="ToolFixture.exe"});processes.Add(Process.GetProcessById(second.Id));
  if(second.HasExited)throw new Exception("owned tool launch was rejected");launcher.Dispose();
  await WaitFor(()=>first.HasExited&&second.HasExited,"owned instance survived shutdown");
 });
 await Test("tool states follow descendants and remain isolated by tool id",async()=>{
  string folder=Fixture("status","orphan");using var launcher=Launcher(folder);
  var getStates=typeof(ToolLauncher).GetMethod("GetRunStates");
  if(getStates==null)throw new Exception("per-tool runtime states are missing");
  string Status(string id){var json=System.Text.Json.JsonSerializer.SerializeToElement(getStates.Invoke(launcher,null));return json.TryGetProperty(id,out var state)?state.GetProperty("Status").GetString()!:"";}
  if(Status("fixture")!="")throw new Exception("unlaunched tool has a runtime state");
  using var parent=await launcher.LaunchAsync(Item(folder));var descendants=await Tree(folder);await parent.WaitForExitAsync();
  if(Status("fixture")!="running")throw new Exception("living descendants were reported as exited");
  try{await launcher.LaunchAsync(Item(folder) with {Id="missing",RelativePath="missing.exe"});throw new Exception("missing file accepted");}catch(InvalidOperationException){}
  if(Status("missing")!="failed"||Status("fixture")!="running")throw new Exception("failure affected another tool state");
  foreach(var process in descendants){if(!process.HasExited)process.Kill(true);}
  await WaitFor(()=>Status("fixture")=="exited","tool did not transition to exited after all descendants ended");
  using var retried=await launcher.LaunchAsync(Item(folder) with {Id="missing"});processes.Add(Process.GetProcessById(retried.Id));
  if(Status("missing")!="running"||Status("fixture")!="exited")throw new Exception("retry did not independently recover to running");
 });
 await Test("disposing the tool launcher ends the tool and its child and grandchild",async()=>{
  string folder=Fixture("normal");var launcher=Launcher(folder);
  using var parent=await launcher.LaunchAsync(Item(folder));processes.Add(Process.GetProcessById(parent.Id));
  var descendants=await Tree(folder);
  DisposeLauncher(launcher);
  await WaitFor(()=>parent.HasExited&&descendants.All(p=>p.HasExited),"tool processes survived toolbox shutdown");
 });
 await Test("children are ended even after their original launcher has exited",async()=>{
  string folder=Fixture("orphan","orphan");var launcher=Launcher(folder);
  using var parent=await launcher.LaunchAsync(Item(folder));var descendants=await Tree(folder);
  await parent.WaitForExitAsync();
  if(!launcher.HasRunningTools)throw new Exception("update check missed surviving tool descendants");
  DisposeLauncher(launcher);
  await WaitFor(()=>descendants.All(p=>p.HasExited),"orphaned tool descendants survived shutdown");
  if(launcher.HasRunningTools)throw new Exception("closed session still reports running tools");
 });
 await Test("abrupt toolbox termination also ends every managed descendant",async()=>{
  string folder=Fixture("crash");var start=new ProcessStartInfo(Environment.ProcessPath!){UseShellExecute=false,CreateNoWindow=true};
  start.ArgumentList.Add("--owner");start.ArgumentList.Add(folder);
  using var owner=Process.Start(start)!;processes.Add(Process.GetProcessById(owner.Id));
  var descendants=await Tree(folder);processes.Add(Process.GetProcessById(int.Parse(File.ReadAllText(Path.Combine(folder,"parent.pid")))));
  await WaitFor(()=>File.Exists(Path.Combine(folder,"owner-ready")),"owner did not finish launch");
  owner.Kill();await owner.WaitForExitAsync();
  await WaitFor(()=>processes.All(p=>p.HasExited),"tool processes survived abrupt toolbox termination");
 });
 await Test("closing one toolbox session leaves another session's tools running",async()=>{
  string first=Fixture("session-one"),second=Fixture("session-two");var one=Launcher(first);var two=Launcher(second);
  using var parentOne=await one.LaunchAsync(Item(first));var treeOne=await Tree(first);
  using var parentTwo=await two.LaunchAsync(Item(second));processes.Add(Process.GetProcessById(parentOne.Id));processes.Add(Process.GetProcessById(parentTwo.Id));var treeTwo=await Tree(second);
  try{DisposeLauncher(one);await WaitFor(()=>parentOne.HasExited&&treeOne.All(p=>p.HasExited),"first session did not stop");if(parentTwo.HasExited||treeTwo.Any(p=>p.HasExited))throw new Exception("unrelated session was stopped");}
  finally{DisposeLauncher(two);}
 });
 await Test("shutdown during validation cannot launch an unmanaged tool",async()=>{
  string folder=Fixture("shutdown-race");var launcher=Launcher(folder);
  Task<Process> pending=launcher.LaunchAsync(Item(folder));DisposeLauncher(launcher);
  try{using var process=await pending;await WaitFor(()=>process.HasExited,"concurrent tool launch survived shutdown");}
  catch(ObjectDisposedException){}
  DisposeLauncher(launcher);
  try{using var unexpected=await launcher.LaunchAsync(Item(folder));processes.Add(Process.GetProcessById(unexpected.Id));throw new Exception("closed launcher accepted a new tool");}
  catch(ObjectDisposedException){}
 });
}finally{Directory.Delete(root,true);}
Console.WriteLine($"RESULT {passed} passed, {failed} failed");return failed==0?0:1;

static ToolLauncher Launcher(string folder){var auth=new AuthGate();auth.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")));return new(folder,auth,()=>true);}
static ToolItem Item(string folder)=>new(){Id="fixture",Name="Process fixture",RelativePath="ToolFixture.exe",Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(folder,"ToolFixture.exe"))))};
