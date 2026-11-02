using System.Reflection;
using System.Security.Cryptography;
using System.Text.Json;
using Toolbox.Setup;
using Toolbox.Core;
string installer=Path.GetFullPath(args.FirstOrDefault()??"dist/内部工具箱-安装-v1.0.0.exe");
string scratch=Path.GetFullPath(Environment.GetEnvironmentVariable("CZ_PACKAGE_TEST_TEMP_ROOT")??".build");
string root=Path.Combine(scratch,"package-verify-"+Guid.NewGuid().ToString("N"));
try {
 bool upgrading=args.Length>1;string oldHash=null;
 if(upgrading){var oldAssembly=Assembly.LoadFile(Path.GetFullPath(args[1]));using var oldPayload=oldAssembly.GetManifestResourceStream("payload.zip")!;InstallEngine.Install(oldPayload,root,_=>{});oldHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,"app/CZToolbox.exe"))));Console.WriteLine("PASS actual previous ZIP installer installs before upgrade");}
 var assembly=Assembly.LoadFile(installer);var info=assembly.GetType("Toolbox.Setup.PayloadInfo")!;string expected=(string)info.GetField("Sha256")!.GetRawConstantValue()!;
 using var payload=assembly.GetManifestResourceStream("payload.7z")??assembly.GetManifestResourceStream("payload.zip")!;
 if(Convert.ToHexString(SHA256.HashData(payload))!=expected)throw new Exception("embedded payload hash mismatch");Console.WriteLine("PASS installer payload resource SHA-256");payload.Position=0;
 using(var decoder=assembly.GetManifestResourceStream("7zr.exe")){
  if(decoder==null)InstallEngine.Install(payload,root,_=>{});
  else SolidPayload.Install(payload,decoder,expected,(string)info.GetField("DecoderSha256")!.GetRawConstantValue()!,(string)info.GetField("ZipSha256")!.GetRawConstantValue()!,(long)info.GetField("ZipSize")!.GetRawConstantValue()!,root,_=>{},null);
 }
 Console.WriteLine("PASS real installer resource installs into isolated target");
 if(Directory.GetDirectories(root,".solid-*").Length!=0)throw new Exception("compressed installation retained temporary files");
 if(upgrading){string backupHash=Convert.ToHexString(SHA256.HashData(File.ReadAllBytes(Path.Combine(root,".previous/CZToolbox.exe"))));if(backupHash!=oldHash)throw new Exception("upgrade did not preserve the old version");Console.WriteLine("PASS real previous-version overwrite preserves the old app backup");}
 string app=Path.Combine(root,"app");using var manifest=JsonDocument.Parse(File.ReadAllText(Path.Combine(app,"payload-files.json")));
 int files=0;foreach(var file in manifest.RootElement.EnumerateArray()){string path=Path.GetFullPath(Path.Combine(app,file.GetProperty("Path").GetString()!));if(!path.StartsWith(app+Path.DirectorySeparatorChar))throw new Exception("path escaped");if(!File.Exists(path))throw new Exception("missing "+path);using var input=File.OpenRead(path);if(Convert.ToHexString(SHA256.HashData(input))!=file.GetProperty("Sha256").GetString())throw new Exception("hash mismatch "+path);files++;}
 Console.WriteLine("PASS all "+files+" installed payload files match build manifest");
 if(!File.Exists(Path.Combine(app,"web/index.html")))throw new Exception("React UI missing");if(File.Exists(Path.Combine(app,"web/__test/bridge.js")))throw new Exception("test bridge shipped");
 string[] forbidden={"update-private.pem","CPU-Z-v2.15.0-CN.exe","EncryptSynaptics.com","系统激活工具.exe","1.关杀毒更新.exe"};foreach(string file in Directory.GetFiles(app,"*",SearchOption.AllDirectories))if(forbidden.Contains(Path.GetFileName(file)))throw new Exception("unexpected private/deferred/removed file "+file);
 Console.WriteLine("PASS production UI present; private key and deferred tools absent");
 if(!BundledCatalog.Read(Path.Combine(app,"CZToolbox.exe")).SequenceEqual(File.ReadAllBytes("assets/catalog.json")))throw new Exception("actual bundled app catalog differs from reviewed catalog");
 Console.WriteLine("PASS actual single-file app embeds the reviewed catalog, without executing app code");
 using(var catalog=JsonDocument.Parse(File.ReadAllText("assets/catalog.json"))){
  var entries=catalog.RootElement.EnumerateArray().ToArray();
  foreach(string restored in new[]{"13.卸载工具.exe","3.系统必备运行库修复.exe"})if(!entries.Any(t=>Path.GetFileName(t.GetProperty("RelativePath").GetString())==restored))throw new Exception("retained catalog entry missing: "+restored);
  if(entries.Any(t=>t.GetProperty("Id").GetString() is "activation" or "security-update-control"))throw new Exception("removed tool still registered");
  string acePath=Path.Combine(app,"tools","工具","系统设置","ACE虚拟化环境检测修复.exe");
  using(var bundled=File.OpenRead(acePath))using(var candidate=File.OpenRead("modules/AceVirtualization/build-output/ACE虚拟化环境检测修复.exe"))if(Convert.ToHexString(SHA256.HashData(bundled))!=Convert.ToHexString(SHA256.HashData(candidate)))throw new Exception("installer contains old ACE instead of the new script wrapper");
  foreach(var tool in entries){string path=Path.Combine(app,"tools",tool.GetProperty("RelativePath").GetString()!);using var bytes=File.OpenRead(path);if(Convert.ToHexString(SHA256.HashData(bytes))!=tool.GetProperty("Sha256").GetString())throw new Exception("installed catalog tool hash mismatch: "+path);}
  foreach(var tool in entries.Where(t=>t.TryGetProperty("PayloadManifest",out _)).DistinctBy(t=>t.GetProperty("PayloadManifest").GetString()))
   await ToolPayloadVerifier.VerifyAsync(Path.Combine(app,"tools"),tool.GetProperty("PayloadManifest").GetString()!,tool.GetProperty("PayloadManifestSha256").GetString()!);
  string openSource=Path.Combine(app,"tools","工具","开源工具");
  if(Directory.GetFiles(openSource,"*.log",SearchOption.AllDirectories).Length>0||File.Exists(Path.Combine(openSource,"sharex","PersonalPath.cfg")))throw new Exception("runtime log or developer personal path shipped");
  Console.WriteLine("PASS installed open-source dependency manifests; runtime logs and personal paths excluded");
  Console.WriteLine("PASS catalog and all "+entries.Length+" installed tool files match, including restored maintenance tools");
 }
 using(var logos=JsonDocument.Parse(File.ReadAllText("assets/software-logos.json"))) {
  int count=0;foreach(var logo in logos.RootElement.GetProperty("items").EnumerateObject()){
   var record=logo.Value;string image=Path.Combine(app,"web",record.GetProperty("path").GetString()!);
   if(!File.Exists(image))throw new Exception("bundled software logo missing: "+logo.Name);
   using var stream=File.OpenRead(image);if(Convert.ToHexString(SHA256.HashData(stream))!=record.GetProperty("sha256").GetString()!.ToUpperInvariant())throw new Exception("bundled software logo changed: "+logo.Name);
   count++;
  }
  Console.WriteLine("PASS all "+count+" bundled software logos match their source manifest");
 }
 File.WriteAllText(Path.Combine(app,"tools/personal.zip"),"personal-data");File.WriteAllText(Path.Combine(app,"update-settings.json"),"custom-settings");OwnedFiles.Remove(app);
 if(File.Exists(Path.Combine(app,"CZToolbox.exe"))||File.ReadAllText(Path.Combine(app,"tools/personal.zip"))!="personal-data"||File.ReadAllText(Path.Combine(app,"update-settings.json"))!="custom-settings")throw new Exception("manifest-based removal lost personal files or retained unchanged payload");
 Console.WriteLine("PASS real manifest removes owned payload and preserves added files and changed settings");Console.WriteLine("RESULT "+(upgrading?11:9)+" passed, 0 failed");return 0;
} catch(Exception ex){Console.WriteLine("FAIL package "+ex);return 1;} finally {if(Directory.Exists(root))InstallEngine.DeleteInside(Path.GetDirectoryName(root),root);}
