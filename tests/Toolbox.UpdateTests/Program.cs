using System.Net;
using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Toolbox.Core;
using Toolbox.Setup;

// Exercises real release bytes and the install engine in an isolated directory.
string release=Path.GetFullPath(args.FirstOrDefault()??"artifacts/update-releases/1.3.0");
bool online=args.Contains("--public");
string output=Path.GetFullPath(".build/update-e2e-"+Guid.NewGuid().ToString("N"));
Directory.CreateDirectory(output);
string key=File.ReadAllText("assets/update-public.pem");
string? manifestArgument=args.FirstOrDefault(a=>a.StartsWith("--manifest-url="));
var settings=new UpdateSettings {ManifestUrl=manifestArgument?["--manifest-url=".Length..]??"https://zskj.top/cz-updates/latest.json"};
using var http=online?null:new HttpClient(new ReleaseHandler(release));
using var oldClient=new UpdateService(settings,key,new Version(1,1,1),http);
try {
 var result=await oldClient.CheckAsync();
 var info=result.Info??throw new Exception("old client did not find release");
 if(result.State!=UpdateState.Available)throw new Exception("unexpected release");
 var expectedVersion=Version.Parse(info.Version);
 Console.WriteLine($"PASS 1.1.1 discovers signed {info.Version} release"+(online?" through public HTTPS":" from release bytes"));
 using(var current=new UpdateService(settings,key,expectedVersion,http))
  if((await current.CheckAsync()).State!=UpdateState.Current)throw new Exception("current client offered same release");
 Console.WriteLine($"PASS {info.Version} reports current without an install offer");
 string installer=Path.Combine(output,"downloaded.exe");
 double progress=0;
 await oldClient.DownloadAsync(info,installer,new CaptureProgress(v=>progress=v));
 if(progress!=1||new FileInfo(installer).Length!=info.Size)throw new Exception("download incomplete");
 Console.WriteLine("PASS real installer download completes and verifies size and SHA-256");
 string root=Path.Combine(output,"installation");
 void Install(string path) {
  var assembly=Assembly.Load(File.ReadAllBytes(Path.GetFullPath(path)));
  string expected=(string)assembly.GetType("Toolbox.Setup.PayloadInfo")!.GetField("Sha256")!.GetRawConstantValue()!;
  using var payload=assembly.GetManifestResourceStream("payload.zip")!;
  if(Convert.ToHexString(SHA256.HashData(payload))!=expected)throw new Exception("installer payload hash mismatch");
  payload.Position=0;InstallEngine.Install(payload,root,_=>{});
 }
 Install("dist/内部工具箱-安装-v1.1.1.exe");
 string app=Path.Combine(root,"app"),config=Path.Combine(app,"update-settings.json");
 File.WriteAllText(config,JsonSerializer.Serialize(settings));
 File.WriteAllText(Path.Combine(app,"personal-note.txt"),"preserve this file");
 Install(installer);
 var version=System.Diagnostics.FileVersionInfo.GetVersionInfo(Path.Combine(app,"CZToolbox.exe"));
 if(version.FileMajorPart!=expectedVersion.Major||version.FileMinorPart!=expectedVersion.Minor||version.FileBuildPart!=expectedVersion.Build)throw new Exception("upgraded executable version mismatch");
 string preserved=Directory.GetFiles(root,"personal-note.txt",SearchOption.AllDirectories).Single();
 if(File.ReadAllText(preserved)!="preserve this file"||JsonSerializer.Deserialize<UpdateSettings>(File.ReadAllText(config))?.ManifestUrl!=settings.ManifestUrl)throw new Exception("upgrade lost custom file or settings");
 Console.WriteLine($"PASS real 1.1.1 payload upgrades to {info.Version} preserving files and update settings");
 using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(app,"payload-files.json")));
 int count=0;foreach(var file in manifest.RootElement.EnumerateArray()) {
  string relative=file.GetProperty("Path").GetString()!;
  if(relative=="update-settings.json")continue; // Deliberately preserved above.
  using var input=File.OpenRead(Path.Combine(app,relative));
  if(Convert.ToHexString(SHA256.HashData(input))!=file.GetProperty("Sha256").GetString())throw new Exception("installed hash mismatch: "+relative);
  count++;
 }
 Console.WriteLine($"PASS {count} upgraded payload files match build manifest");
 Console.WriteLine("RESULT 5 passed, 0 failed; isolated engine upgrade, no registry or desktop installation changed");
 return 0;
}catch(Exception error){Console.WriteLine("FAIL "+error);return 1;}
finally{InstallEngine.DeleteInside(Path.GetDirectoryName(output)!,output);}

sealed class ReleaseHandler(string directory):HttpMessageHandler {
 protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request,CancellationToken ct) {
  string name=Path.GetFileName(request.RequestUri!.AbsolutePath);
  return Task.FromResult(new HttpResponseMessage(HttpStatusCode.OK){Content=new StreamContent(File.OpenRead(Path.Combine(directory,name)))});
 }
}
sealed class CaptureProgress(Action<double> capture):IProgress<double> {public void Report(double value)=>capture(value);}
