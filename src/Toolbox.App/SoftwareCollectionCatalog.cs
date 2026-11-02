using System.IO;
using System.Text.Json;
namespace Toolbox.App;

// Only identifiers cross the UI bridge. The target always comes from the bundled catalog.
internal static class SoftwareCollectionCatalog {
 static readonly Lazy<Dictionary<(string Software,string Link),string>> links=new(Load);
 static Dictionary<(string,string),string> Load() {
  using var stream=typeof(SoftwareCollectionCatalog).Assembly.GetManifestResourceStream("software-collection.json")??throw new InvalidOperationException("软件合集目录缺失。");
  using var data=JsonDocument.Parse(stream);
  var result=new Dictionary<(string,string),string>();
  foreach(var item in data.RootElement.GetProperty("items").EnumerateArray()) {
   string id=item.GetProperty("id").GetString()??"";
   foreach(var link in item.GetProperty("links").EnumerateArray()) {
    string linkId=link.GetProperty("id").GetString()??"",url=link.GetProperty("url").GetString()??"";
    if(id.Length==0||linkId.Length==0||!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme is not ("https" or "http")||uri.UserInfo.Length>0||!result.TryAdd((id,linkId),uri.AbsoluteUri))throw new InvalidOperationException("软件合集目录包含无效入口。");
   }
  }
  return result;
 }
 internal static string Resolve(string softwareId,string linkId)=>links.Value.TryGetValue((softwareId,linkId),out var url)?url:throw new InvalidOperationException("软件合集下载入口不存在，请重新打开软件合集。");
}
