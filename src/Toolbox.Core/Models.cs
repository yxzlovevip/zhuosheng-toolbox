namespace Toolbox.Core;

public sealed record ToolItem {
 public string Id {get;init;}="";
 public string Name {get;init;}="";
 public string Category {get;init;}="";
 public string Group {get;init;}="";
 public string RelativePath {get;init;}="";
 public string Sha256 {get;init;}="";
 public string[] Arguments {get;init;}=[];
 public string PayloadManifest {get;init;}="";
 public string PayloadManifestSha256 {get;init;}="";
 public string InstanceExecutable {get;init;}="";
 public string Summary {get;init;}="";
 public string ToolPassword {get;init;}="";
 public string IconPath {get;init;}="";
 public string Purpose {get;init;}="";
 public string[] SearchAliases {get;init;}=[];
 public string Preparation {get;init;}="";
 public string[] Steps {get;init;}=[];
 public string Impact {get;init;}="";
 public string Result {get;init;}="";
 public string Evidence {get;init;}="";
 public string Risk {get;init;}="常规";
 public bool NeedsConfirmation {get;init;}
 public string Restart {get;init;}="以工具提示为准";
}
public sealed record UpdateSettings { public string ManifestUrl {get;init;}=""; }
public sealed record ToolFileCheck(string Id,string Name,string Category,string Status,string Message);
public sealed record ToolFilesReport(DateTimeOffset CheckedAt,IReadOnlyList<ToolFileCheck> Items);
public sealed record UpdateInfo { public string Version {get;init;}=""; public string Url {get;init;}=""; public string Sha256 {get;init;}=""; public long Size {get;init;} public string Notes {get;init;}=""; }
public enum UpdateState { NotConfigured,Current,Available }
public sealed record UpdateResult(UpdateState State,UpdateInfo? Info=null);
