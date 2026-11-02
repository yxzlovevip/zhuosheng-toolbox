namespace Toolbox.App;

public sealed record SystemInfoReport {
 public DateTimeOffset CollectedAt {get;init;}
 public SystemInfoSection[] Sections {get;init;}=[];
}
public sealed record SystemInfoSection {
 public string Id {get;init;}="";
 public string Title {get;init;}="";
 public SystemInfoDevice[] Devices {get;init;}=[];
 public string Error {get;init;}="";
 public string Notice {get;init;}="";
}
public sealed record SystemInfoDevice {
 public string Id {get;init;}="";
 public string Name {get;init;}="";
 public SystemInfoField[] Fields {get;init;}=[];
 public SystemInfoDevice[] Children {get;init;}=[];
 public long? CapacityBytes {get;init;}
 public long? FreeBytes {get;init;}
}
public sealed record SystemInfoField {
 public string Label {get;init;}="";
 public string Value {get;init;}="";
}
