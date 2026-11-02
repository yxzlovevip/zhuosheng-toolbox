using System.Diagnostics;
using System.IO;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Xml.Linq;
using Microsoft.Win32;
using Toolbox.Core;

namespace Toolbox.OpenSourceTools;
public sealed class FileToolWindow : Window {
 readonly LaunchEntry entry;
 readonly string root;
 readonly TextBox selection=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,Height=105};
 readonly TextBox result=new(){IsReadOnly=true,TextWrapping=TextWrapping.Wrap,VerticalScrollBarVisibility=ScrollBarVisibility.Auto,MinHeight=100,BorderThickness=new(0),Background=Brushes.Transparent};
 readonly ComboBox presets=new(){MinWidth=240,Margin=new(0,8,0,12)};
 readonly Button choose=new(){Content="选择文件…",Padding=new(18,9,18,9),HorizontalAlignment=HorizontalAlignment.Left};
 readonly Button run=new(){Padding=new(22,10,22,10),HorizontalAlignment=HorizontalAlignment.Right,IsEnabled=false};
 readonly CancellationTokenSource lifetime=new();
 readonly List<Process> owned=[];
 string[] files=[];
 bool closing;
 public FileToolWindow(LaunchEntry entry,string root) {
  this.entry=entry;this.root=root;
  Title=entry.Name;Width=680;Height=590;MinWidth=560;MinHeight=460;WindowStartupLocation=WindowStartupLocation.CenterScreen;FontFamily=new("Microsoft YaHei UI");FontSize=14;Background=new SolidColorBrush(Color.FromRgb(247,248,251));
  var panel=new DockPanel{Margin=new(26)};Content=panel;
  var header=new StackPanel{Margin=new(0,0,0,16)};DockPanel.SetDock(header,Dock.Top);panel.Children.Add(header);
  header.Children.Add(new TextBlock{Text=entry.Name,FontSize=24,FontWeight=FontWeights.SemiBold,Margin=new(0,0,0,10)});
  header.Children.Add(new TextBlock{Text=entry.Mode switch{"convert"=>"先选文件，再选目标格式。转换结果另存为新文件，原文件保留。","rename"=>"选择要改名的文件，进入 PowerRename 预览后再应用。",_=>"选择删除不了或正在使用的文件，查询占用它的程序。这里只查询，不结束进程。"},TextWrapping=TextWrapping.Wrap,LineHeight=24});
  var footer=new StackPanel{Margin=new(0,16,0,0)};DockPanel.SetDock(footer,Dock.Bottom);panel.Children.Add(footer);footer.Children.Add(run);
  footer.Children.Add(new TextBlock{Text="关闭工具箱会结束由它启动的工具。正在处理文件时，请等待完成后再关闭。",FontSize=12,Foreground=Brushes.DimGray,TextWrapping=TextWrapping.Wrap,Margin=new(0,12,0,0)});
  var body=new StackPanel();panel.Children.Add(body);body.Children.Add(choose);body.Children.Add(selection);selection.Margin=new(0,10,0,12);
  if(entry.Mode=="convert") {
   body.Children.Add(new TextBlock{Text="转换为"});body.Children.Add(presets);
   string path=ToolPayloadVerifier.Resolve(root,Path.Combine(Path.GetDirectoryName(entry.RelativePath)!,"Settings.default.xml"));
   var preferred=new[]{"To Png","To Jpg","To Webp","To Avif","To Pdf","To Ico","To Mp4","To Webm","To Gif","To Mp3","To Wav","To Flac"};
   var available=XDocument.Load(path).Root!.Elements("ConversionPreset").Where(p=>preferred.Contains((string?)p.Attribute("Name"))).ToArray();
   foreach(var preset in available){string key=(string)preset.Attribute("Name")!;presets.Items.Add(new ComboBoxItem{Content=key[3..].ToUpperInvariant(),Tag=key,ToolTip="支持："+string.Join("、",preset.Elements("InputTypes").Select(x=>x.Value))});}
   presets.SelectedIndex=0;run.Content="开始转换";
  }else run.Content=entry.Mode=="rename"?"打开改名预览":"查询文件占用";
  body.Children.Add(result);
  choose.Click+=(_,_)=>{var dialog=new OpenFileDialog{Multiselect=true,CheckFileExists=true,Title="选择文件",Filter="所有文件|*.*"};if(dialog.ShowDialog(this)==true)SetFiles(dialog.FileNames);};
  run.Click+=async(_,_)=>await ExecuteAsync();
  Closing+=(_,_)=>{closing=true;lifetime.Cancel();foreach(var process in owned)try{if(!process.HasExited)process.Kill(entireProcessTree:true);}catch(InvalidOperationException){}catch(System.ComponentModel.Win32Exception){};};
  Closed+=(_,_)=>{foreach(var process in owned)process.Dispose();lifetime.Dispose();};
 }
 public void SetFiles(IEnumerable<string> paths){files=paths.Select(Path.GetFullPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();selection.Text=string.Join(Environment.NewLine,files);run.IsEnabled=files.Length>0;result.Text=$"已选择 {files.Length} 个文件。";}
 public async Task ExecuteAsync() {
  if(files.Length==0||!run.IsEnabled)return;
  run.IsEnabled=choose.IsEnabled=false;result.Text="正在检查工具文件…";
  try {
   await ToolPayloadVerifier.VerifyAsync(root,entry.PayloadManifest,entry.PayloadManifestSha256,lifetime.Token);
   lifetime.Token.ThrowIfCancellationRequested();
   if(files.Any(file=>!File.Exists(file)))throw new InvalidOperationException("部分所选文件已经不存在，请重新选择。");
   var arguments=new List<string>();
   if(entry.Mode=="convert"){arguments.Add("--conversion-preset");arguments.Add((string)((ComboBoxItem)presets.SelectedItem).Tag);}
   if(entry.Mode=="lock")arguments.Add("--json");
   arguments.AddRange(files);
   if(arguments.Sum(a=>a.Length+3)>28000)throw new InvalidOperationException("所选文件过多或路径太长，请分批处理。");
   var start=ToolLauncher.CreateStartInfo(ToolPayloadVerifier.Resolve(root,entry.RelativePath),arguments);
   if(entry.Mode=="lock"){start.RedirectStandardOutput=true;start.RedirectStandardError=true;start.StandardOutputEncoding=System.Text.Encoding.UTF8;start.CreateNoWindow=true;}
   var process=Process.Start(start)??throw new InvalidOperationException("工具未能启动。");owned.Add(process);
   result.Text=entry.Mode=="rename"?"已打开 PowerRename。请在原工具中预览后确认改名。":"工具正在处理，请稍候…";
   var stdout=start.RedirectStandardOutput?process.StandardOutput.ReadToEndAsync(lifetime.Token):Task.FromResult("");
   var stderr=start.RedirectStandardError?process.StandardError.ReadToEndAsync(lifetime.Token):Task.FromResult("");
   await process.WaitForExitAsync(lifetime.Token);
   string output=await stdout,error=await stderr;
   if(process.ExitCode!=0)throw new InvalidOperationException("原工具返回错误（"+process.ExitCode+"）。"+error);
   if(entry.Mode=="lock"){try{using var json=JsonDocument.Parse(output);result.Text=json.RootElement.ValueKind==JsonValueKind.Array&&json.RootElement.GetArrayLength()==0?"没有查询到占用进程。":JsonSerializer.Serialize(json,new JsonSerializerOptions{WriteIndented=true,Encoder=System.Text.Encodings.Web.JavaScriptEncoder.UnsafeRelaxedJsonEscaping});}catch(JsonException){result.Text=output;}}
   else result.Text=entry.Mode=="convert"?"转换进程已结束，请在原文件所在文件夹中检查新生成的文件；具体成功或失败以原工具显示为准。":"PowerRename 已关闭，请检查文件名。";
  }catch(OperationCanceledException)when(closing){}
  catch(Exception ex){if(!closing)result.Text="未完成："+ex.Message;}
  finally{if(!closing)run.IsEnabled=choose.IsEnabled=true;}
 }
}
