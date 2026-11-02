using System.Diagnostics;
using System.IO;
using System.Text.Json;
using Toolbox.App;

int passed=0,failed=0;
async Task Test(string name,Func<Task> test) {try {await test();Console.WriteLine("PASS "+name);passed++;} catch(Exception ex) {Console.WriteLine("FAIL "+name+": "+ex.Message);failed++;}}
void Assert(bool value,string message) {if(!value)throw new Exception(message);}
const string fixture="""
{"CollectedAt":"2026-10-21T12:00:00Z","Sections":[
 {"Id":"memory","Title":"内存","Devices":[{"Id":"DIMM0","Name":"内存 1","Fields":[{"Label":"序列号","Value":"ABC-1"}]},{"Id":"DIMM1","Name":"内存 2","Fields":[{"Label":"序列号","Value":"ABC-2"}]}]},
 {"Id":"disks","Title":"磁盘与分区","Devices":[{"Id":"disk0","Name":"SSD","CapacityBytes":1000000000000,"Children":[{"Id":"efi","Name":"EFI 系统分区","CapacityBytes":104857600,"Fields":[{"Label":"盘符","Value":"无盘符"}]}]}]},
 {"Id":"graphics","Title":"显卡","Devices":[],"Notice":"未检测到显卡"},
 {"Id":"bios","Title":"BIOS","Devices":[],"Error":"访问被拒绝"}]}
""";
await Test("multiple memory sticks and serial numbers survive query parsing",()=>{
 var report=SystemInfoReader.Parse(fixture);var rows=report.Sections.Single(s=>s.Id=="memory").Devices;
 Assert(rows.Length==2&&rows[0].Fields[0].Value=="ABC-1"&&rows[1].Fields[0].Value=="ABC-2","memory instances collapsed");return Task.CompletedTask;
});
await Test("hidden partition remains a child of the right physical disk",()=>{
 var disk=SystemInfoReader.Parse(fixture).Sections.Single(s=>s.Id=="disks").Devices.Single();
 Assert(disk.CapacityBytes==1000000000000&&disk.Children.Single().CapacityBytes==104857600,"capacity or parent relationship lost");
 Assert(disk.Children.Single().Fields.Single().Value=="无盘符","EFI partition dropped");return Task.CompletedTask;
});
await Test("empty hardware and failed sections remain distinct",()=>{
 var sections=SystemInfoReader.Parse(fixture).Sections;Assert(sections[2].Devices.Length==0&&sections[2].Notice=="未检测到显卡"&&sections[2].Error=="","empty hardware became an error");
 Assert(sections[3].Error=="访问被拒绝","section failure hidden");return Task.CompletedTask;
});
await Test("corrupt and incomplete responses cannot look like successful empty scans",()=>{
 foreach(string json in new[]{"garbage","null","{}","{\"CollectedAt\":\"2026-10-21T12:00:00Z\",\"Sections\":[]}"}) {
  bool rejected=false;try {SystemInfoReader.Parse(json);}catch(InvalidOperationException){rejected=true;}
  Assert(rejected,"invalid report accepted: "+json);
 }return Task.CompletedTask;
});
await Test("PowerShell input and output preserve Chinese and quotes without temporary scripts",async()=>{
 string output=await WindowsQueryRunner.RunAsync("[Console]::Out.Write('内存 \"DDR5\"')",TimeSpan.FromSeconds(10),CancellationToken.None);
 Assert(output=="内存 \"DDR5\"","UTF-8 or quoting corrupted");
});
await Test("a hung owned query is stopped at its timeout",async()=>{
 var clock=Stopwatch.StartNew();bool timedOut=false;
 try {await WindowsQueryRunner.RunAsync("Start-Sleep -Seconds 20",TimeSpan.FromSeconds(1),CancellationToken.None);}catch(TimeoutException){timedOut=true;}
 Assert(timedOut&&clock.Elapsed<TimeSpan.FromSeconds(8),"query timeout did not stop the owned process");
});
await Test("caller cancellation stops the query promptly",async()=>{
 using var cancel=new CancellationTokenSource(TimeSpan.FromMilliseconds(600));bool cancelled=false;
 try {await WindowsQueryRunner.RunAsync("Start-Sleep -Seconds 20",TimeSpan.FromSeconds(10),cancel.Token);}catch(OperationCanceledException){cancelled=true;}
 Assert(cancelled,"cancelled process did not exit");
});
await Test("Storage CIM keeps EFI type, ownership and full volumes with zero free bytes",async()=>{
 const string cimFixture="""
function Get-CimInstance {
 param($ClassName,$Namespace,$OperationTimeoutSec,$ErrorAction)
 switch ($ClassName) {
 'Win32_DiskDrive' { [pscustomobject]@{Index=0;DeviceID='PHYSICALDRIVE0';Model='Fixture SSD';Size=1000000000000}; [pscustomobject]@{Index=1;DeviceID='PHYSICALDRIVE1';Model='Empty SSD';Size=500000000000} }
 'MSFT_Disk' { [pscustomobject]@{Number=0;PartitionStyle=2}; [pscustomobject]@{Number=1;PartitionStyle=0} }
 'MSFT_Partition' {
  [pscustomobject]@{DiskNumber=0;PartitionNumber=1;GptType='{c12a7328-f81f-11d2-ba4b-00a0c93ec93b}';MbrType=0;DriveLetter=[char]0;AccessPaths=@('efi-volume');Offset=1048576;Size=104857600}
  [pscustomobject]@{DiskNumber=0;PartitionNumber=2;GptType='{ebd0a0a2-b9e5-4433-87c0-68b6b72699c7}';MbrType=0;DriveLetter=[char]'C';AccessPaths=@('c-volume');Offset=105906176;Size=999000000000}
 }
 'MSFT_Volume' {
  [pscustomobject]@{Path='efi-volume';DriveLetter=[char]0;FileSystem='FAT32';Size=104857600;SizeRemaining=1000000}
  [pscustomobject]@{Path='c-volume';DriveLetter=[char]'C';FileSystem='NTFS';FileSystemLabel='Full';Size=998000000000;SizeRemaining=0}
 }
 'Win32_DiskPartition' { throw 'Should not use legacy queries when Storage returns partitions' }
 }
}
function Get-ItemProperty { param($LiteralPath,$Name,$ErrorAction); throw 'fixture has no registry hardware identifiers' }
function Confirm-SecureBootUEFI { param($ErrorAction); return $false }
$QueryMode='system'
""";
 using var stream=typeof(SystemInfoReader).Assembly.GetManifestResourceStream("SystemInformation.ps1")!;using var reader=new StreamReader(stream);
 var report=SystemInfoReader.Parse(await WindowsQueryRunner.RunAsync(cimFixture+"\n"+reader.ReadToEnd(),TimeSpan.FromSeconds(10),CancellationToken.None));
 var disks=report.Sections.Single(s=>s.Id=="disks").Devices;
 Assert(disks.Length==2&&disks[0].Children.Length==2&&disks[1].Children.Length==0,"Storage partition attached to wrong disk");
 Assert(disks[0].Children[0].Fields.Single(f=>f.Label=="盘符").Value=="无盘符","char16 null drive letter became a mounted drive");
 Assert(disks[0].Children[0].Fields.Single(f=>f.Label=="分区类型").Value=="EFI 系统分区","EFI GptType not decoded");
 Assert(disks[0].Children[0].Fields.Single(f=>f.Label=="文件系统").Value=="FAT32","EFI volume mapping lost");
 Assert(disks[0].Children[1].FreeBytes==0&&disks[0].Children[1].Fields.Single(f=>f.Label=="卷可用空间").Value=="0 MiB","zero free space became unknown");
 Assert(disks[0].Children[1].Fields.Single(f=>f.Label=="卷容量").Value!="未提供","separate volume capacity lost");
});
if(args.Contains("--live")) {
 await Test("live Windows scan returns hardware, hidden partitions and all device categories",async()=>{
  var report=await new SystemInfoReader().ReadAsync(CancellationToken.None);
  string[] expected={"computer","cpu","memory","board","bios","graphics","disks","network","devices","identifiers"};
  Assert(expected.All(id=>report.Sections.Any(s=>s.Id==id)),"required category missing");
  foreach(var section in report.Sections) Console.WriteLine($"  {section.Id}: {section.Devices.Length} devices / {section.Devices.Sum(d=>d.Children.Length)} children / {(section.Error.Length>0?"partial failure":"ok")}");
  Assert(report.Sections.Single(s=>s.Id=="cpu").Devices.Length>0,"CPU query empty on this Windows host");
  Assert(report.Sections.Single(s=>s.Id=="disks").Devices.Any(d=>d.Children.Length>0),"no disk partition relationships");
  Assert(report.Sections.Single(s=>s.Id=="identifiers").Devices.Any(d=>d.Fields.Length>0),"machine identifiers missing");
 });
 await Test("live GPU query returns generic adapters and NVIDIA availability without modifying settings",async()=>{
  var report=await new SystemInfoReader().ReadGraphicsAsync(CancellationToken.None);
  Assert(report.Sections.Any(s=>s.Id=="graphics")&&report.Sections.Any(s=>s.Id=="nvidia"),"GPU result omitted support status");
  foreach(var section in report.Sections)Console.WriteLine($"  {section.Id}: {section.Devices.Length} devices; {section.Notice}; {section.Error}");
 });
}
Console.WriteLine($"RESULT {passed} passed, {failed} failed");return failed==0?0:1;
