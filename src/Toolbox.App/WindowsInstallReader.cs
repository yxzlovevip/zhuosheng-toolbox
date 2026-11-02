using Microsoft.Win32;
namespace Toolbox.App;

public static class WindowsInstallReader {
 public static DateTimeOffset? Read() {
  using var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
  using var current=machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion");
  return Parse(current?.GetValue("InstallTime"),current?.GetValue("InstallDate"));
 }
 public static DateTimeOffset? Parse(object? installTime,object? installDate) {
  if(installTime is long fileTime&&fileTime>0) {
   try {return new DateTimeOffset(DateTime.FromFileTimeUtc(fileTime));}catch(ArgumentOutOfRangeException) {}
  }
  long seconds=installDate switch {int value=>(uint)value,long value=>value,_=>0};
  if(seconds<=0) return null;
  try {return DateTimeOffset.FromUnixTimeSeconds(seconds);}catch(ArgumentOutOfRangeException) {return null;}
 }
}
