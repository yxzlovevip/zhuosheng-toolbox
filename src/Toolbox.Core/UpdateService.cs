using System.Net;
using System.Security.Cryptography;
using System.Text.Json;
namespace Toolbox.Core;

public sealed class UpdateService(UpdateSettings settings,string publicKey,Version current,HttpClient? client=null) : IDisposable {
 readonly HttpClient http=client??new HttpClient(new HttpClientHandler {AllowAutoRedirect=false}) {Timeout=TimeSpan.FromMinutes(15)};
 readonly bool ownsClient=client==null;
 static readonly JsonSerializerOptions JsonOptions=new() {PropertyNameCaseInsensitive=true};
 static Uri Https(string url) {
  if(!Uri.TryCreate(url,UriKind.Absolute,out var uri)||uri.Scheme!="https"||!string.IsNullOrEmpty(uri.UserInfo)||!string.IsNullOrEmpty(uri.Fragment)||uri.IsLoopback) throw new InvalidOperationException("更新地址必须为有效的 HTTPS 公网地址。");
  return uri;
 }
 Uri ValidateInfo(UpdateInfo info) {
  if(!Version.TryParse(info.Version,out _)||info.Size is <=0 or >1073741824||info.Sha256.Length!=64||info.Sha256.Any(c=>!Uri.IsHexDigit(c))) throw new InvalidOperationException("更新清单的版本、大小或校验值无效。");
  var uri=Https(info.Url); var manifest=Https(settings.ManifestUrl);
  if(uri.Host!=manifest.Host||uri.Port!=manifest.Port||!uri.AbsolutePath.EndsWith(".exe",StringComparison.OrdinalIgnoreCase)) throw new InvalidOperationException("更新包必须与清单位于同一 HTTPS 站点并且为 EXE 文件。");
  return uri;
 }
 public async Task<UpdateResult> CheckAsync(CancellationToken ct=default) {
  if(string.IsNullOrWhiteSpace(settings.ManifestUrl)) return new(UpdateState.NotConfigured);
  using var timeout=CancellationTokenSource.CreateLinkedTokenSource(ct); timeout.CancelAfter(TimeSpan.FromSeconds(20));
  using var response=await http.GetAsync(Https(settings.ManifestUrl),HttpCompletionOption.ResponseHeadersRead,timeout.Token);
  RequireSuccess(response);
  byte[] data=await ReadLimited(response.Content,1024*1024,timeout.Token);
  using var json=JsonDocument.Parse(data);
  byte[] payload=Convert.FromBase64String(json.RootElement.GetProperty("payload").GetString()??"");
  byte[] signature=Convert.FromBase64String(json.RootElement.GetProperty("signature").GetString()??"");
  using var rsa=RSA.Create(); rsa.ImportFromPem(publicKey);
  if(!rsa.VerifyData(payload,signature,HashAlgorithmName.SHA256,RSASignaturePadding.Pkcs1)) throw new CryptographicException("更新清单签名无效，已阻止更新。");
  var info=JsonSerializer.Deserialize<UpdateInfo>(payload,JsonOptions)??throw new InvalidOperationException("更新清单为空。");
  ValidateInfo(info);
  return Version.Parse(info.Version)>current ? new(UpdateState.Available,info):new(UpdateState.Current);
 }
 static void RequireSuccess(HttpResponseMessage response) {
  if((int)response.StatusCode is >=300 and <400) throw new InvalidOperationException("更新服务器返回重定向，请维护者使用最终 HTTPS 地址。");
  response.EnsureSuccessStatusCode();
 }
 static async Task<byte[]> ReadLimited(HttpContent content,int limit,CancellationToken ct) {
  using var stream=await content.ReadAsStreamAsync(ct); using var memory=new MemoryStream(); var buffer=new byte[8192]; int n;
  while((n=await stream.ReadAsync(buffer,ct))>0) { if(memory.Length+n>limit) throw new InvalidOperationException("更新清单过大。"); await memory.WriteAsync(buffer.AsMemory(0,n),ct); }
  return memory.ToArray();
 }
 public async Task DownloadAsync(UpdateInfo info,string target,IProgress<double>? progress=null,CancellationToken ct=default) {
  Uri uri=ValidateInfo(info);
  if(Version.Parse(info.Version)<=current) throw new InvalidOperationException("拒绝安装相同版本或旧版本。");
  string partial=target+".part";
  try {
   using var response=await http.GetAsync(uri,HttpCompletionOption.ResponseHeadersRead,ct); RequireSuccess(response);
   if(response.Content.Headers.ContentLength is long length&&length!=info.Size) throw new InvalidOperationException("更新包大小与签名清单不一致。");
   await using(var source=await response.Content.ReadAsStreamAsync(ct))
   await using(var destination=new FileStream(partial,FileMode.CreateNew,FileAccess.Write,FileShare.None,81920,true)) {
    var buffer=new byte[81920]; long total=0; int n;
    while((n=await source.ReadAsync(buffer,ct))>0) { total+=n; if(total>info.Size) throw new InvalidOperationException("更新包超出声明大小。"); await destination.WriteAsync(buffer.AsMemory(0,n),ct); progress?.Report((double)total/info.Size); }
    if(total!=info.Size) throw new InvalidOperationException("更新包下载不完整。");
   }
   await using(var file=File.OpenRead(partial)) if(!Convert.ToHexString(await SHA256.HashDataAsync(file,ct)).Equals(info.Sha256,StringComparison.OrdinalIgnoreCase)) throw new CryptographicException("更新包校验失败，已阻止安装。");
   File.Move(partial,target,true);
  } finally { if(File.Exists(partial)) File.Delete(partial); }
 }
 public void Dispose() { if(ownsClient) http.Dispose(); }
}
