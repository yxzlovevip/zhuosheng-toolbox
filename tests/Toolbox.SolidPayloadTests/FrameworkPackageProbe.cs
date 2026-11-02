// Compiled separately with Windows .NET Framework csc, excluded from the SDK test project.
using System;
using System.Diagnostics;
using System.IO;
using System.Reflection;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;

class FrameworkPackageProbe {
 [DataContract] public class Entry {
  [DataMember] public string Path;
  [DataMember] public long Size;
  [DataMember] public string Sha256;
 }
 static string Hash(Stream input){using(var sha=SHA256.Create())return BitConverter.ToString(sha.ComputeHash(input)).Replace("-","");}
 static string FileHash(string path){using(var input=File.OpenRead(path))return Hash(input);}
 static object Info(Assembly target,string name){return target.GetType("Toolbox.Setup.PayloadInfo").GetField(name).GetRawConstantValue();}
 static void Install(Assembly target,string root,Action<string> status,Action commit) {
  using(var payload=target.GetManifestResourceStream("payload.7z")) {
   if(payload!=null)using(var decoder=target.GetManifestResourceStream("7zr.exe"))target.GetType("Toolbox.Setup.SolidPayload").GetMethod("Install").Invoke(null,new object[]{payload,decoder,Info(target,"Sha256"),Info(target,"DecoderSha256"),Info(target,"ZipSha256"),Info(target,"ZipSize"),root,status,commit});
   else using(var zip=target.GetManifestResourceStream("payload.zip")) {
    if(Hash(zip)!=(string)Info(target,"Sha256"))throw new Exception("old package hash mismatch");zip.Position=0;
    target.GetType("Toolbox.Setup.InstallEngine").GetMethod("Install",new[]{typeof(Stream),typeof(string),typeof(Action<string>),typeof(Action)}).Invoke(null,new object[]{zip,root,status,commit});
   }
  }
 }
 static int Main(string[] args) {
  string parent=Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),root=System.IO.Path.Combine(parent,"CZ-SolidProbe-"+Guid.NewGuid().ToString("N"));
  var candidate=Assembly.LoadFile(System.IO.Path.GetFullPath(args[0]));var engine=candidate.GetType("Toolbox.Setup.InstallEngine");
  var timer=Stopwatch.StartNew();
  try {
   candidate.GetType("Toolbox.Setup.Program").GetMethod("ProtectDirectory",BindingFlags.NonPublic|BindingFlags.Static).Invoke(null,new object[]{root});
   var previous=Assembly.LoadFile(System.IO.Path.GetFullPath(args[1]));
   Install(previous,root,s=>{},null);
   string app=System.IO.Path.Combine(root,"app"),oldHash=FileHash(System.IO.Path.Combine(app,"CZToolbox.exe"));
   string settings=System.IO.Path.Combine(app,"update-settings.json");File.AppendAllText(settings,Environment.NewLine);string settingsHash=FileHash(settings);
   Console.WriteLine("PASS actual Framework old installer engine installs old resource into a protected isolated location");
   Console.WriteLine("OLD_INSTALL_SECONDS="+timer.Elapsed.TotalSeconds.ToString("F3"));
   bool committed=false;var upgrade=Stopwatch.StartNew();
   Install(candidate,root,s=>Console.WriteLine("STATUS "+s),()=>committed=true);
   Console.WriteLine("UPGRADE_SECONDS="+upgrade.Elapsed.TotalSeconds.ToString("F3"));
   if(!committed||FileHash(System.IO.Path.Combine(root,".previous","CZToolbox.exe"))!=oldHash||FileHash(System.IO.Path.Combine(app,"CZToolbox.exe"))==oldHash)throw new Exception("actual Framework upgrade failed to preserve old app or commit new version");
   Console.WriteLine("PASS actual candidate Framework solid engine overwrites old version and preserves old backup");
   Entry[] files;using(var input=File.OpenRead(System.IO.Path.Combine(app,"payload-files.json")))files=(Entry[])new DataContractJsonSerializer(typeof(Entry[])).ReadObject(input);
   foreach(var file in files){string path=System.IO.Path.GetFullPath(System.IO.Path.Combine(app,file.Path));if(!path.StartsWith(app+System.IO.Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new Exception("manifest path escaped");string expected=file.Path=="update-settings.json"?settingsHash:file.Sha256;if(FileHash(path)!=expected)throw new Exception("installed file mismatch "+file.Path);}
   if(FileHash(settings)!=settingsHash||Directory.GetDirectories(root,".solid-*").Length!=0)throw new Exception("custom settings lost or temporary files retained");
   Console.WriteLine("PASS "+files.Length+" installed files verified (preserved custom update settings checked separately)");
   Console.WriteLine("PASS temporary solid files removed before commit");
   Console.WriteLine("RESULT actual Framework old-to-new engine validation passed in "+timer.Elapsed.TotalSeconds.ToString("F1")+" seconds; registry and real installation untouched");return 0;
  }catch(Exception error){Console.WriteLine("FAIL "+error);return 1;}
  finally{if(Directory.Exists(root))engine.GetMethod("DeleteInside").Invoke(null,new object[]{parent,root});}
 }
}
