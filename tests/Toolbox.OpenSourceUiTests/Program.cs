using System.Diagnostics;
using System.IO;
using System.Security.Cryptography;
using System.Text.Json;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using Toolbox.Core;
using Toolbox.OpenSourceTools;

static class Program {
 [STAThread] static int Main(){
  var app=new Application{ShutdownMode=ShutdownMode.OnExplicitShutdown};int passed=0,failed=0;
  string root=Path.GetFullPath("."),folder=Path.Combine(Path.GetTempPath(),"cz-real-file-tools-"+Guid.NewGuid().ToString("N"));Directory.CreateDirectory(folder);
  var entries=JsonSerializer.Deserialize<LaunchEntry[]>(File.ReadAllText("assets/opensource-launchers.json"))!;
  async Task Test(string name,Func<Task> run){try{await run();Console.WriteLine("PASS "+name);passed++;}catch(Exception ex){Console.WriteLine("FAIL "+name+": "+ex);failed++;}}
  void Assert(bool value,string message){if(!value)throw new Exception(message);}
  app.Startup+=async(_,_)=>{
   try{
    string image=Path.Combine(folder,"测试 空格.jpg");var bitmap=BitmapSource.Create(16,8,96,96,PixelFormats.Bgr24,null,Enumerable.Repeat((byte)120,16*8*3).ToArray(),16*3);var encoder=new JpegBitmapEncoder();encoder.Frames.Add(BitmapFrame.Create(bitmap));using(var file=File.Create(image))encoder.Save(file);byte[] original=SHA256.HashData(File.ReadAllBytes(image));
    await Test("Chinese conversion window produces a real PNG and preserves the source",async()=>{
     var window=new FileToolWindow(entries.Single(e=>e.Id=="oss-fileconverter"),root){ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
     try{window.Show();window.SetFiles([image]);var combo=Descendants<ComboBox>(window).Single();combo.SelectedItem=combo.Items.Cast<ComboBoxItem>().Single(i=>(string)i.Tag=="To Png");var pending=window.ExecuteAsync();string output=Path.Combine(folder,"测试 空格_converted.png");var timer=Stopwatch.StartNew();while(!File.Exists(output)&&timer.Elapsed<TimeSpan.FromSeconds(25)&&!pending.IsCompleted)await Task.Delay(100);Assert(File.Exists(output),"conversion result missing");await Task.Delay(300);using(var input=File.OpenRead(output)){var decoded=BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Assert(decoded.Frames[0].PixelWidth==16&&decoded.Frames[0].PixelHeight==8,"incorrect converted dimensions");}Assert(SHA256.HashData(File.ReadAllBytes(image)).SequenceEqual(original),"original changed");window.Close();await pending.WaitAsync(TimeSpan.FromSeconds(5));}
     finally{window.Close();}
    });
    await Test("PowerToys Image Resizer creates a real resized copy",async()=>{
     string destination=Path.Combine(folder,"resized");Directory.CreateDirectory(destination);var entry=entries.Single(e=>e.Id=="pt-image-resizer");string exe=ToolPayloadVerifier.Resolve(root,entry.RelativePath.Replace("PowerToys.ImageResizer.exe","PowerToys.ImageResizerCLI.exe"));
     var start=ToolLauncher.CreateStartInfo(exe,["--width","8","--height","8","--fit","Fit","--destination",destination,image]);start.RedirectStandardOutput=true;start.RedirectStandardError=true;start.CreateNoWindow=true;using var process=Process.Start(start)!;
     try{var stdout=process.StandardOutput.ReadToEndAsync();var stderr=process.StandardError.ReadToEndAsync();await process.WaitForExitAsync().WaitAsync(TimeSpan.FromSeconds(20));Assert(process.ExitCode==0,await stderr);await stdout;string output=Directory.GetFiles(destination).Single();using var input=File.OpenRead(output);var decoded=BitmapDecoder.Create(input,BitmapCreateOptions.None,BitmapCacheOption.OnLoad);Assert(decoded.Frames[0].PixelWidth==8&&decoded.Frames[0].PixelHeight==4,"incorrect resize dimensions");Assert(SHA256.HashData(File.ReadAllBytes(image)).SequenceEqual(original),"original changed");}finally{if(!process.HasExited)process.Kill(true);}
    });
    await Test("Chinese File Locksmith window queries a genuinely locked file without ending its owner",async()=>{
     string file=Path.Combine(folder,"被占用的文件.txt");File.WriteAllText(file,"fixture");using var locked=new FileStream(file,FileMode.Open,FileAccess.ReadWrite,FileShare.None);
     var window=new FileToolWindow(entries.Single(e=>e.Id=="pt-file-locksmith"),root){ShowActivated=false,ShowInTaskbar=false,WindowStartupLocation=WindowStartupLocation.Manual,Left=-20000,Top=-20000};
     try{window.Show();window.SetFiles([file]);await window.ExecuteAsync().WaitAsync(TimeSpan.FromSeconds(20));string result=string.Join("\n",Descendants<TextBox>(window).Select(t=>t.Text));Assert(result.Contains(Environment.ProcessId.ToString()),"locking process PID absent: "+result);Assert(locked.CanRead,"locking process or file was stopped");}finally{window.Close();}
    });
   }finally{Directory.Delete(folder,true);Console.WriteLine($"RESULT {passed} passed, {failed} failed");app.Shutdown(failed==0?0:1);}
  };
  return app.Run();
 }
 static IEnumerable<T> Descendants<T>(DependencyObject root) where T:DependencyObject{for(int i=0;i<VisualTreeHelper.GetChildrenCount(root);i++){var child=VisualTreeHelper.GetChild(root,i);if(child is T typed)yield return typed;foreach(var nested in Descendants<T>(child))yield return nested;}}
}
