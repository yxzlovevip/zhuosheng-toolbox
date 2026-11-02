using System.Security.Cryptography;
using System.Text.Json;

namespace Toolbox.Core;

public static class ToolPayloadVerifier {
 public static string Resolve(string root,string relative) {
  if(string.IsNullOrWhiteSpace(relative))throw new InvalidOperationException("工具路径不能为空。");
  string basePath=Path.GetFullPath(root);
  var parts=relative.Replace('\\','/').Split('/');
  if(Path.IsPathRooted(relative)||parts.Any(p=>string.IsNullOrWhiteSpace(p)||p is "." or ".."||p.Contains(':')))throw new InvalidOperationException("工具路径不符合发布清单要求。");
  string path=Path.GetFullPath(Path.Combine(basePath,relative));
  if(!path.StartsWith(basePath+Path.DirectorySeparatorChar,StringComparison.OrdinalIgnoreCase))throw new InvalidOperationException("工具路径超出安装目录。");
  for(string? component=path;component!=null;component=Path.GetDirectoryName(component))
   if((File.Exists(component)||Directory.Exists(component))&&(File.GetAttributes(component)&FileAttributes.ReparsePoint)!=0)throw new InvalidOperationException("工具路径包含链接或重定向目录，已阻止启动。");
  return path;
 }
 public static async Task VerifyAsync(string root,string manifestRelative,string expectedHash,CancellationToken cancellationToken=default) {
  cancellationToken.ThrowIfCancellationRequested();
  if(string.IsNullOrEmpty(manifestRelative)&&string.IsNullOrEmpty(expectedHash))return;
  if(!IsHash(expectedHash))throw new InvalidOperationException("工具依赖清单缺少有效的完整性信息。");
  string manifestPath=Resolve(root,manifestRelative);
  try {
   using var manifest=new FileStream(manifestPath,FileMode.Open,FileAccess.Read,FileShare.Read,65536,true);
   if(manifest.Length>16*1024*1024)throw new InvalidOperationException("工具依赖清单过大。");
   if(!Convert.ToHexString(await SHA256.HashDataAsync(manifest,cancellationToken)).Equals(expectedHash,StringComparison.OrdinalIgnoreCase))throw new ToolFileChangedException("工具依赖清单已变化，请重新安装完整工具箱。");
   manifest.Position=0;
   var entries=await JsonSerializer.DeserializeAsync<PayloadFile[]>(manifest,cancellationToken:cancellationToken);
   if(entries is null||entries.Length==0||entries.Length>50000)throw new InvalidOperationException("工具依赖清单为空或超出限制。");
   var seen=new HashSet<string>(StringComparer.OrdinalIgnoreCase);
   foreach(var entry in entries) {
    cancellationToken.ThrowIfCancellationRequested();
    if(entry is null)throw new InvalidOperationException("工具依赖清单包含空记录。");
    string path=Resolve(Path.GetDirectoryName(manifestPath)!,entry.Path);
    if(!seen.Add(path)||entry.Size<0||!IsHash(entry.Sha256))throw new InvalidOperationException("工具依赖清单包含重复路径或无效信息。");
    using var file=new FileStream(path,FileMode.Open,FileAccess.Read,FileShare.Read,65536,true);
    if(file.Length!=entry.Size||!Convert.ToHexString(await SHA256.HashDataAsync(file,cancellationToken)).Equals(entry.Sha256,StringComparison.OrdinalIgnoreCase))throw new ToolFileChangedException("工具依赖文件已变化："+entry.Path+"。请重新安装完整工具箱。");
   }
  }catch(Exception ex)when(ex is FileNotFoundException or DirectoryNotFoundException){throw new InvalidOperationException("工具依赖文件缺失，请重新安装完整工具箱。"+ex.Message,ex);}
  catch(JsonException ex){throw new InvalidOperationException("工具依赖清单格式无效。",ex);}
 }
 static bool IsHash(string? value)=>value is {Length:64}&&value.All(Uri.IsHexDigit);
 sealed record PayloadFile(string Path,long Size,string Sha256);
}
