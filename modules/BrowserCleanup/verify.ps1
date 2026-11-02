$ErrorActionPreference = 'Stop'
$projectRoot = Split-Path (Split-Path $PSScriptRoot -Parent) -Parent
$exe = Join-Path $projectRoot '工具\重装后工具\浏览器配置与推广清理.exe'
$original = Join-Path $projectRoot '工具\重装后工具\12.一键去浏览器配置推广(使用流氓PE安装后必须使用).bat'
$assembly = [Reflection.Assembly]::LoadFile($exe)
$stream = $assembly.GetManifestResourceStream('Cz.BrowserCleanup.Payload.bat')
if ($null -eq $stream) { throw '成品缺少原始 BAT 资源' }
$memory = New-Object IO.MemoryStream
try {
    $stream.CopyTo($memory)
    $payload = $memory.ToArray()
} finally { $memory.Dispose(); $stream.Dispose() }
$originalBytes = [IO.File]::ReadAllBytes($original)
if ([Convert]::ToBase64String($payload) -ne [Convert]::ToBase64String($originalBytes)) { throw '成品内嵌 BAT 与原文件不一致' }
$sha = [Security.Cryptography.SHA256]::Create()
try { $payloadHash = [BitConverter]::ToString($sha.ComputeHash($payload)).Replace('-', '') } finally { $sha.Dispose() }
if ($payloadHash -ne 'A920497432039F668047863973684FD9247DFDE40049BB0B9286A7556498A88B') { throw '原始 BAT 哈希不符合审查版本' }

# LoadLibraryEx with AS_DATAFILE reads PE resources without running the EXE entry point.
if (-not ('CzBrowserResourceReader' -as [type])) {
    Add-Type -TypeDefinition @'
using System;
using System.Runtime.InteropServices;
public static class CzBrowserResourceReader {
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr LoadLibraryEx(string path, IntPtr file, uint flags);
 [DllImport("kernel32.dll", CharSet=CharSet.Unicode, SetLastError=true)] public static extern IntPtr FindResource(IntPtr module, IntPtr name, IntPtr type);
 [DllImport("kernel32.dll")] public static extern uint SizeofResource(IntPtr module, IntPtr resource);
 [DllImport("kernel32.dll")] public static extern IntPtr LoadResource(IntPtr module, IntPtr resource);
 [DllImport("kernel32.dll")] public static extern IntPtr LockResource(IntPtr resource);
 [DllImport("kernel32.dll")] public static extern bool FreeLibrary(IntPtr module);
}
'@
}
$moduleHandle = [CzBrowserResourceReader]::LoadLibraryEx($exe, [IntPtr]::Zero, 2)
if ($moduleHandle -eq [IntPtr]::Zero) { throw '无法以数据方式读取 EXE' }
try {
    $resource = [CzBrowserResourceReader]::FindResource($moduleHandle, [IntPtr]1, [IntPtr]24)
    if ($resource -eq [IntPtr]::Zero) { throw '成品缺少 Windows 程序清单' }
    $size = [CzBrowserResourceReader]::SizeofResource($moduleHandle, $resource)
    $manifestBytes = New-Object byte[] $size
    $pointer = [CzBrowserResourceReader]::LockResource([CzBrowserResourceReader]::LoadResource($moduleHandle, $resource))
    [Runtime.InteropServices.Marshal]::Copy($pointer, $manifestBytes, 0, $size)
    $manifest = [xml][Text.Encoding]::UTF8.GetString($manifestBytes)
    $level = $manifest.SelectSingleNode('//*[local-name()="requestedExecutionLevel"]')
    if ($null -eq $level -or $level.level -ne 'requireAdministrator') { throw '成品未声明管理员权限' }
} finally { [void][CzBrowserResourceReader]::FreeLibrary($moduleHandle) }
$catalog = @(Get-Content -LiteralPath (Join-Path $projectRoot 'assets\catalog.json') -Raw | ConvertFrom-Json)
$entries = @($catalog | Where-Object Id -eq 'browser-cleanup')
if ($entries.Count -ne 1) { throw '主软件入口缺失或重复' }
$exeHash = (Get-FileHash -LiteralPath $exe -Algorithm SHA256).Hash
if ($entries[0].Sha256 -ne $exeHash) { throw '主软件登记的成品哈希已过期' }
Write-Output "PASS: production payload byte-for-byte verified ($($payload.Length) bytes), administrator manifest and catalog hash verified; production EXE NOT executed."
Write-Output "EXE SHA256: $exeHash"
