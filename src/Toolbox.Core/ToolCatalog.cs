using System.Text.Json;
namespace Toolbox.Core;
public sealed class ToolCatalog {
 public IReadOnlyList<ToolItem> Items {get;}
 public ToolCatalog(IEnumerable<ToolItem> items) {
  var list=items.ToArray();
  if(list.Any(i=>string.IsNullOrWhiteSpace(i.Id)||string.IsNullOrWhiteSpace(i.Name))||list.Select(i=>i.Id).Distinct(StringComparer.Ordinal).Count()!=list.Length) throw new InvalidOperationException("工具清单存在空名称或重复标识。");
  Items=Array.AsReadOnly(list);
 }
 public static ToolCatalog Load(Stream stream)=>new(JsonSerializer.Deserialize<ToolItem[]>(stream,new JsonSerializerOptions {PropertyNameCaseInsensitive=true})??throw new InvalidOperationException("工具清单为空。"));
 public IEnumerable<ToolItem> Search(string query,string? category) {
  string[] terms=(query??"").Split((char[]?)null,StringSplitOptions.RemoveEmptyEntries);
  return Items.Where(i=>{
   if(!string.IsNullOrEmpty(category)&&i.Category!=category)return false;
   string text=$"{i.Name} {i.Summary} {i.Category} {i.Group} {i.Purpose} {string.Join(' ',i.SearchAliases??[])}";
   return terms.All(term=>text.Contains(term,StringComparison.OrdinalIgnoreCase));
  });
 }
}
