using Microsoft.Win32;
using Toolbox.Installation;
namespace Toolbox.App;
public static class InstalledLocation {
 public static string Resolve(string applicationDirectory) {
  using var machine=RegistryKey.OpenBaseKey(RegistryHive.LocalMachine,RegistryView.Registry64);
  using var key=machine.OpenSubKey(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Uninstall\InternalToolbox");
  string root=InstallPathPolicy.ResolveInstalledRoot(key?.GetValue("InstallLocation") as string??"",applicationDirectory);
  InstallPathPolicy.EnsureProtectedAncestors(root,true);
  return root;
 }
}
