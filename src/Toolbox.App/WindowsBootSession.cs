using System.ComponentModel;
using System.Runtime.InteropServices;
using System.Xml.Linq;
using Microsoft.Win32.SafeHandles;

namespace Toolbox.App;

public static class WindowsBootSession {
 [StructLayout(LayoutKind.Sequential)]
 struct BootEnvironment {public Guid Identifier;public uint FirmwareType;public ulong Flags;}
 [DllImport("ntdll.dll")] static extern int NtQuerySystemInformation(int informationClass,out BootEnvironment information,int size,out int returnedLength);
 [DllImport("wevtapi.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern EventHandle EvtQuery(IntPtr session,string path,string query,int flags);
 [DllImport("wevtapi.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool EvtNext(EventHandle result,int size,[Out] IntPtr[] events,int timeout,int flags,out int returned);
 [DllImport("wevtapi.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool EvtRender(IntPtr context,EventHandle fragment,int flags,int size,IntPtr buffer,out int used,out int count);
 [DllImport("wevtapi.dll")] [return:MarshalAs(UnmanagedType.Bool)] static extern bool EvtClose(IntPtr handle);
 sealed class EventHandle:SafeHandleZeroOrMinusOneIsInvalid {
  public EventHandle():base(true){}
  public EventHandle(IntPtr handle):base(true){SetHandle(handle);}
  protected override bool ReleaseHandle()=>EvtClose(handle);
 }
 static readonly Lazy<Task<string?>> current=new(ReadAsync);
 public static Task<string?> CurrentAsync()=>current.Value;
 public static Task<string?> ReadAsync()=>Task.Run(Read);
 static string? Read() {
  // A hybrid shutdown can restore the kernel; combine its GUID with the boot
  // event. Sleep/resume retains both and therefore the same hardware snapshot.
  try {
   if(NtQuerySystemInformation(90,out var environment,Marshal.SizeOf<BootEnvironment>(),out _)!=0||environment.Identifier==Guid.Empty)return null;
   const string filter="*[System[(Provider[@Name='Microsoft-Windows-Kernel-Boot'] and EventID=27) or (Provider[@Name='Microsoft-Windows-Kernel-General'] and EventID=12)]]";
   using var query=EvtQuery(IntPtr.Zero,"System",filter,0x201);
   if(query.IsInvalid)return null;
   var events=new IntPtr[1];
   if(!EvtNext(query,1,events,1000,0,out int returned)||returned!=1)return null;
   using var record=new EventHandle(events[0]);
   EvtRender(IntPtr.Zero,record,1,0,IntPtr.Zero,out int required,out _);
   if(Marshal.GetLastWin32Error()!=122||required<=0||required>64*1024)return null;
   IntPtr buffer=Marshal.AllocHGlobal(required);
   try {
    if(!EvtRender(IntPtr.Zero,record,1,required,buffer,out _,out _))return null;
    var xml=XDocument.Parse(Marshal.PtrToStringUni(buffer)??"");
    XNamespace ns="http://schemas.microsoft.com/win/2004/08/events/event";
    var system=xml.Root?.Element(ns+"System");
    string? id=system?.Element(ns+"EventRecordID")?.Value;
    string? time=system?.Element(ns+"TimeCreated")?.Attribute("SystemTime")?.Value;
    return string.IsNullOrEmpty(id)||string.IsNullOrEmpty(time)?null:environment.Identifier.ToString("N")+":"+id+":"+time;
   } finally {Marshal.FreeHGlobal(buffer);}
  } catch(Exception ex) when(ex is Win32Exception or DllNotFoundException or EntryPointNotFoundException or System.Xml.XmlException) {return null;}
 }
}
