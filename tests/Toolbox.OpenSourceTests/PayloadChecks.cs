using System.Diagnostics;
using System.Security.Cryptography;
using System.Text.Json;
using Toolbox.Core;

static class PayloadChecks {
 public static async Task<int> Run(bool smoke) {
  int passed=0,failed=0;string root=Path.GetFullPath(".");
  using var data=JsonDocument.Parse(File.ReadAllText("assets/opensource-launchers.json"));
  var launchers=data.RootElement.EnumerateArray().Select(e=>e.Clone()).ToArray();
  var distinct=launchers.DistinctBy(e=>e.GetProperty("PayloadManifest").GetString()).ToArray();
  if(distinct.Length!=8||launchers.Length!=14){Console.WriteLine("FAIL expected eight projects and fourteen functional entries");return 1;}
  foreach(var item in distinct) {
   string id=item.GetProperty("Id").GetString()!,manifest=item.GetProperty("PayloadManifest").GetString()!;
   try{
    await ToolPayloadVerifier.VerifyAsync(root,manifest,item.GetProperty("PayloadManifestSha256").GetString()!);
    string executable=ToolPayloadVerifier.Resolve(root,item.GetProperty("RelativePath").GetString()!);
    if(!File.Exists(executable))throw new Exception("real executable missing");
    string license=Path.Combine(Path.GetDirectoryName(ToolPayloadVerifier.Resolve(root,manifest))!,"UPSTREAM-LICENSE.txt");
    if(!File.Exists(license)||new FileInfo(license).Length<100)throw new Exception("upstream license missing");
    Console.WriteLine("PASS complete official payload and license "+id);passed++;
   }catch(Exception ex){Console.WriteLine("FAIL payload "+id+": "+ex.Message);failed++;}
  }
  if(smoke&&failed==0){
   using var catalogStream=File.OpenRead("assets/catalog.json");var catalog=ToolCatalog.Load(catalogStream);
   foreach(var entry in distinct){
    string id=entry.GetProperty("Id").GetString()!,relative=entry.GetProperty("RelativePath").GetString()!;
    string executable=ToolPayloadVerifier.Resolve(root,relative),processName=Path.GetFileNameWithoutExtension(executable);
    var prior=FindProcesses(processName,executable);
    if(prior.Count>0){prior.ForEach(p=>p.Dispose());Console.WriteLine("FAIL smoke "+id+": an external instance is already running; left untouched");failed++;continue;}
    var auth=new AuthGate();auth.TryUnlock((System.Environment.GetEnvironmentVariable("CZ_TEST_PASSWORD") ?? throw new System.InvalidOperationException("Set CZ_TEST_PASSWORD before running native login tests.")));using var launcher=new ToolLauncher(root,auth,()=>true);
    Process? wrapper=null;List<Process> actual=[];
    try{
     var tool=catalog.Items.Single(t=>t.Id==id);
     // Open only the native settings window for File Converter; no conversion or shell installation.
     if(id=="oss-fileconverter")tool=tool with{RelativePath=relative,Sha256=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(executable))),Arguments=["--settings"]};
     wrapper=await launcher.LaunchAsync(tool);
     var timer=Stopwatch.StartNew();
     while(actual.Count==0&&timer.Elapsed<TimeSpan.FromSeconds(20)){await Task.Delay(250);actual=FindProcesses(processName,executable);}
     if(actual.Count==0)throw new Exception("native program never started");
     await Task.Delay(2500);
     if(actual.All(p=>p.HasExited))throw new Exception("native program exited during startup");
     Console.WriteLine("STARTED "+id+": "+string.Join(", ",actual.Where(p=>!p.HasExited).Select(p=>$"pid={p.Id}, title={p.MainWindowTitle}")));
     launcher.Dispose();
     timer.Restart();while((!wrapper.HasExited||actual.Any(p=>!p.HasExited))&&timer.Elapsed<TimeSpan.FromSeconds(8))await Task.Delay(100);
     if(!wrapper.HasExited||actual.Any(p=>!p.HasExited))throw new Exception("owned process survived toolbox disposal");
     await ToolPayloadVerifier.VerifyAsync(root,entry.GetProperty("PayloadManifest").GetString()!,entry.GetProperty("PayloadManifestSha256").GetString()!);
     Console.WriteLine("PASS native startup and managed shutdown "+id);passed++;
    }catch(Exception ex){Console.WriteLine("FAIL smoke "+id+": "+ex.Message);failed++;}
    finally{launcher.Dispose();wrapper?.Dispose();actual.ForEach(p=>p.Dispose());}
   }
  }
  Console.WriteLine($"RESULT {passed} passed, {failed} failed");return failed==0?0:1;
 }
 static List<Process> FindProcesses(string name,string executable){var found=new List<Process>();foreach(var p in Process.GetProcessesByName(name)){try{if(p.MainModule?.FileName.Equals(executable,StringComparison.OrdinalIgnoreCase)==true){found.Add(p);continue;}}catch(System.ComponentModel.Win32Exception){}catch(InvalidOperationException){}p.Dispose();}return found;}
}
