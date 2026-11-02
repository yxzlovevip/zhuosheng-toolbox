using System.ComponentModel;
using System.Diagnostics;
using System.Runtime.InteropServices;
using System.Text;
using Microsoft.Win32.SafeHandles;

namespace Toolbox.Core;

// Each toolbox owns an unnamed, non-inheritable job. Children cannot break away.
// The OS closes it on a crash too, so cleanup does not depend on the UI callback.
internal sealed class ToolProcessGroup : IDisposable {
 readonly object gate=new();
 JobHandle? job;
 bool disposed;
 public bool HasRunningProcesses {get{lock(gate)return !disposed&&job!=null&&ActiveProcesses(job)>0;}}
 public bool ContainsProcess(Process process) {
  lock(gate) {
   if(disposed||job is null||process.HasExited)return false;
   if(!IsProcessInJob(process.Handle,job,out bool belongs))throw new Win32Exception(Marshal.GetLastWin32Error());
   return belongs;
  }
 }

 public Process Start(ProcessStartInfo start) {
  lock(gate) {
   ObjectDisposedException.ThrowIf(disposed,this);
   job??=CreateGroup();
   using var attributes=new JobAttributes(job);
   var startup=new ExtendedStartupInfo {Startup=new StartupInfo {Size=Marshal.SizeOf<ExtendedStartupInfo>()},Attributes=attributes.List};
   var command=new StringBuilder(Quote(start.FileName)+string.Concat(start.ArgumentList.Select(a=>" "+Quote(a))));
   // Assignment is part of creation, including if the host dies during this call.
   // No suspended, unassigned process can survive a crash between API calls.
   if(!CreateProcess(start.FileName,command,IntPtr.Zero,IntPtr.Zero,false,0x80004,IntPtr.Zero,start.WorkingDirectory,ref startup,out var native))throw new Win32Exception(Marshal.GetLastWin32Error());
   Process? process=null;
   try {
    process=Process.GetProcessById((int)native.ProcessId);
    if(ResumeThread(native.Thread)==uint.MaxValue)throw new Win32Exception(Marshal.GetLastWin32Error());
    return process;
   }catch {
    TerminateProcess(native.Process,1);WaitForSingleObject(native.Process,5000);
    process?.Dispose();throw;
   }finally{CloseHandle(native.Thread);CloseHandle(native.Process);GC.KeepAlive(job);}
  }
 }

 static JobHandle CreateGroup() {
  var handle=CreateJobObject(IntPtr.Zero,null);
  if(handle.IsInvalid){int error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error);}
  var limits=new ExtendedLimits();limits.Basic.LimitFlags=0x2000; // JOB_OBJECT_LIMIT_KILL_ON_JOB_CLOSE
  if(!SetInformationJobObject(handle,9,ref limits,(uint)Marshal.SizeOf<ExtendedLimits>())){int error=Marshal.GetLastWin32Error();handle.Dispose();throw new Win32Exception(error);}
  return handle;
 }
 static uint ActiveProcesses(JobHandle handle) {
  if(!QueryInformationJobObject(handle,1,out var accounting,(uint)Marshal.SizeOf<Accounting>(),IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error());
  return accounting.ActiveProcesses;
 }
 public void Dispose() {
  lock(gate) {
   if(disposed)return;disposed=true;
   if(job==null)return;
   try {
    if(!TerminateJobObject(job,1))throw new Win32Exception(Marshal.GetLastWin32Error());
    var timer=Stopwatch.StartNew();
    while(ActiveProcesses(job)>0){if(timer.Elapsed>TimeSpan.FromSeconds(10))throw new InvalidOperationException("工具进程尚未完全结束，请检查工具箱日志。");Thread.Sleep(10);}
   }finally{job.Dispose();job=null;}
  }
 }
 static string Quote(string value) {
  var text=new StringBuilder("\"");int slashes=0;
  foreach(char c in value){if(c=='\\'){slashes++;continue;}text.Append('\\',c=='"'?slashes*2+1:slashes);text.Append(c);slashes=0;}
  return text.Append('\\',slashes*2).Append('"').ToString();
 }
 sealed class JobHandle : SafeHandleZeroOrMinusOneIsInvalid {
  public JobHandle():base(true){}
  protected override bool ReleaseHandle()=>CloseHandle(handle);
 }
 sealed class JobAttributes : IDisposable {
  public IntPtr List {get;private set;}
  IntPtr handles;
  bool initialized;
  public JobAttributes(JobHandle job) {
   UIntPtr size=UIntPtr.Zero;
   InitializeProcThreadAttributeList(IntPtr.Zero,1,0,ref size);
   try {
    List=Marshal.AllocHGlobal(checked((nint)size.ToUInt64()));
    if(!InitializeProcThreadAttributeList(List,1,0,ref size))throw new Win32Exception(Marshal.GetLastWin32Error());
    initialized=true;handles=Marshal.AllocHGlobal(IntPtr.Size);Marshal.WriteIntPtr(handles,job.DangerousGetHandle());
    // PROC_THREAD_ATTRIBUTE_JOB_LIST, supported by Windows 10 and later.
    if(!UpdateProcThreadAttribute(List,0,(UIntPtr)0x2000D,handles,(UIntPtr)IntPtr.Size,IntPtr.Zero,IntPtr.Zero))throw new Win32Exception(Marshal.GetLastWin32Error(),"无法监管工具进程，已阻止启动。");
   }catch{Dispose();throw;}
  }
  public void Dispose(){if(initialized){DeleteProcThreadAttributeList(List);initialized=false;}if(List!=IntPtr.Zero){Marshal.FreeHGlobal(List);List=IntPtr.Zero;}if(handles!=IntPtr.Zero){Marshal.FreeHGlobal(handles);handles=IntPtr.Zero;}}
 }
 [StructLayout(LayoutKind.Sequential)] struct ExtendedStartupInfo {public StartupInfo Startup;public IntPtr Attributes;}
 [StructLayout(LayoutKind.Sequential)] struct StartupInfo {
  public int Size;public IntPtr Reserved,Desktop,Title;
  public uint X,Y,XSize,YSize,XCountChars,YCountChars,FillAttribute,Flags;
  public ushort ShowWindow,ReservedSize;public IntPtr ReservedBytes,StdInput,StdOutput,StdError;
 }
 [StructLayout(LayoutKind.Sequential)] struct ProcessInformation {public IntPtr Process,Thread;public uint ProcessId,ThreadId;}
 [StructLayout(LayoutKind.Sequential)] struct BasicLimits {
  public long PerProcessUserTimeLimit,PerJobUserTimeLimit;public uint LimitFlags;
  public UIntPtr MinimumWorkingSetSize,MaximumWorkingSetSize;public uint ActiveProcessLimit;public UIntPtr Affinity;public uint PriorityClass,SchedulingClass;
 }
 [StructLayout(LayoutKind.Sequential)] struct IoCounters {public ulong ReadOperations,WriteOperations,OtherOperations,ReadBytes,WriteBytes,OtherBytes;}
 [StructLayout(LayoutKind.Sequential)] struct ExtendedLimits {public BasicLimits Basic;public IoCounters Io;public UIntPtr ProcessMemoryLimit,JobMemoryLimit,PeakProcessMemoryUsed,PeakJobMemoryUsed;}
 [StructLayout(LayoutKind.Sequential)] struct Accounting {public long TotalUserTime,TotalKernelTime,ThisPeriodUserTime,ThisPeriodKernelTime;public uint PageFaultCount,TotalProcesses,ActiveProcesses,TotalTerminatedProcesses;}
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] static extern JobHandle CreateJobObject(IntPtr security,string? name);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool SetInformationJobObject(JobHandle job,int type,ref ExtendedLimits information,uint size);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool QueryInformationJobObject(JobHandle job,int type,out Accounting information,uint size,IntPtr returnedLength);
 [DllImport("kernel32.dll",CharSet=CharSet.Unicode,SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool CreateProcess(string application,StringBuilder command,IntPtr processSecurity,IntPtr threadSecurity,[MarshalAs(UnmanagedType.Bool)]bool inheritHandles,uint flags,IntPtr environment,string directory,ref ExtendedStartupInfo startup,out ProcessInformation information);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool InitializeProcThreadAttributeList(IntPtr list,int count,uint flags,ref UIntPtr size);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool UpdateProcThreadAttribute(IntPtr list,uint flags,UIntPtr attribute,IntPtr value,UIntPtr size,IntPtr previousValue,IntPtr returnSize);
 [DllImport("kernel32.dll")] static extern void DeleteProcThreadAttributeList(IntPtr list);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool TerminateJobObject(JobHandle job,uint exitCode);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool IsProcessInJob(IntPtr process,JobHandle job,[MarshalAs(UnmanagedType.Bool)]out bool result);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool TerminateProcess(IntPtr process,uint exitCode);
 [DllImport("kernel32.dll",SetLastError=true)] static extern uint ResumeThread(IntPtr thread);
 [DllImport("kernel32.dll",SetLastError=true)] static extern uint WaitForSingleObject(IntPtr handle,uint milliseconds);
 [DllImport("kernel32.dll",SetLastError=true)] [return:MarshalAs(UnmanagedType.Bool)] static extern bool CloseHandle(IntPtr handle);
}
