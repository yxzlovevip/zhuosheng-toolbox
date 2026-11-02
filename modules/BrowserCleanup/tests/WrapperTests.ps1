$ErrorActionPreference = 'Stop'
$moduleRoot = Split-Path $PSScriptRoot -Parent
$checks = 0
function Assert-True([bool]$Condition, [string]$Message) {
    if (-not $Condition) { throw "FAIL: $Message" }
    $script:checks++
}

# These tests run the real wrapper with a harmless embedded BAT, never the cleanup BAT.
# They catch incorrect first-argument forwarding, changed working directories,
# swallowed child exit codes, modified payload bytes and leftover extraction files.
Assert-True (Test-Path -LiteralPath (Join-Path $moduleRoot 'Program.cs')) '尚未实现 EXE 封装入口'
Assert-True (Test-Path -LiteralPath (Join-Path $moduleRoot 'build.ps1')) '尚未实现可重复构建'
$testRoot = Join-Path $moduleRoot ('test-output\session-' + [Guid]::NewGuid().ToString('N'))
New-Item -ItemType Directory -Path $testRoot -Force | Out-Null
$fixturePath = Join-Path $testRoot 'harmless.bat'
$fixture = "@echo off`r`necho ARG=[%1]`r`necho CWD=[%CD%]`r`necho SCRIPT=[%~f0]`r`nset /p reply=`r`necho INPUT=[%reply%]`r`nexit /b 37`r`n"
[IO.File]::WriteAllText($fixturePath, $fixture, [Text.Encoding]::ASCII)
$testExe = Join-Path $testRoot 'wrapper-fixture.exe'
& (Join-Path $moduleRoot 'build.ps1') -PayloadPath $fixturePath -OutputPath $testExe -ManifestPath (Join-Path $PSScriptRoot 'asInvoker.manifest')

foreach ($case in @(
    @{Args=''; Want='ARG=[]'},
    @{Args='/S'; Want='ARG=[/S]'},
    @{Args='/s'; Want='ARG=[/s]'},
    @{Args='/S ignored'; Want='ARG=[/S]'},
    @{Args='unknown /S'; Want='ARG=[]'},
    @{Args='"x&exit 99"'; Want='ARG=[]'}
)) {
    $info = New-Object Diagnostics.ProcessStartInfo
    $info.FileName = $testExe
    $info.Arguments = $case.Args
    $info.WorkingDirectory = $testRoot
    $info.UseShellExecute = $false
    $info.CreateNoWindow = $true
    $info.RedirectStandardOutput = $true
    $info.RedirectStandardError = $true
    $info.RedirectStandardInput = $true
    $process = [Diagnostics.Process]::Start($info)
    $process.StandardInput.WriteLine('wrapper-input-check')
    $process.StandardInput.Close()
    $output = $process.StandardOutput.ReadToEnd()
    $errors = $process.StandardError.ReadToEnd()
    $process.WaitForExit()
    Assert-True ($process.ExitCode -eq 37) "子脚本退出码未透传：$($case.Args) / $errors"
    Assert-True ($output.Contains($case.Want)) "首参数转换错误：$($case.Args) / $output"
    Assert-True ($output.Contains("CWD=[$testRoot]")) '封装改变了调用方工作目录'
    Assert-True ($output.Contains('INPUT=[wrapper-input-check]')) '交互输入没有传递给 BAT'
    $scriptMatch = [regex]::Match($output, 'SCRIPT=\[(.+)\]')
    Assert-True ($scriptMatch.Success) '测试脚本没有执行'
    $extracted = $scriptMatch.Groups[1].Value.TrimEnd("`r")
    Assert-True (-not (Test-Path -LiteralPath $extracted)) '临时脚本没有清理'
    Assert-True (-not (Test-Path -LiteralPath (Split-Path $extracted -Parent))) '临时专用目录没有清理'
}

$assembly = [Reflection.Assembly]::LoadFile($testExe)
$stream = $assembly.GetManifestResourceStream('Cz.BrowserCleanup.Payload.bat')
Assert-True ($null -ne $stream) '嵌入资源不存在'
$memory = New-Object IO.MemoryStream
$stream.CopyTo($memory)
$stream.Dispose()
Assert-True ([Convert]::ToBase64String($memory.ToArray()) -eq [Convert]::ToBase64String([IO.File]::ReadAllBytes($fixturePath))) '嵌入 BAT 字节发生变化'
$memory.Dispose()
Write-Output "PASS: $checks assertions; only harmless BAT executed."
