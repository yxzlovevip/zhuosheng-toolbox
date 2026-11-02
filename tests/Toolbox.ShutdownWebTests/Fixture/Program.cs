using System;
using System.Diagnostics;
using System.IO;
using System.Threading;
class WaitingToolFixture {
 static void Main(string[] args) {
  string role=args.Length>0?args[0]:"parent";
  File.WriteAllText(Path.Combine(Environment.CurrentDirectory,role+".pid"),Process.GetCurrentProcess().Id.ToString());
  if(role=="parent"||role=="child")using(var child=Process.Start(new ProcessStartInfo(Process.GetCurrentProcess().MainModule.FileName,role=="parent"?"child":"grandchild"){UseShellExecute=false,WorkingDirectory=Environment.CurrentDirectory})){}
  Thread.Sleep(Timeout.Infinite);
 }
}
