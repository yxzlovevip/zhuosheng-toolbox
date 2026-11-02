@echo off
title 系统优化
color 0A
mode con cols=80 lines=40
CLS
echo ---------------------------------------------------------------------
echo 本批处理将自动以管理员权限运行
echo ---------------------------------------------------------------------
rem --------------------------以下是代码段--------------------------------
:init
setlocal DisableDelayedExpansion
set "batchPath=%~0"
for %%k in (%0) do set batchName=%%~nk
set "vbsGetPrivileges=%temp%\OEgetPriv_%batchName%.vbs"
setlocal EnableDelayedExpansion

:checkPrivileges
NET FILE 1>NUL 2>NUL
if '%errorlevel%' == '0' ( goto gotPrivileges ) else ( goto getPrivileges )

:getPrivileges
if '%1'=='ELEV' (echo ELEV & shift /1 & goto gotPrivileges)
ECHO.
ECHO ********************************
ECHO 请求 UAC 权限批准……
ECHO ********************************

ECHO Set UAC = CreateObject^("Shell.Application"^) > "%vbsGetPrivileges%"
ECHO args = "ELEV " >> "%vbsGetPrivileges%"
ECHO For Each strArg in WScript.Arguments >> "%vbsGetPrivileges%"
ECHO args = args ^& strArg ^& " "  >> "%vbsGetPrivileges%"
ECHO Next >> "%vbsGetPrivileges%"
ECHO UAC.ShellExecute "!batchPath!", args, "", "runas", 1 >> "%vbsGetPrivileges%"
"%SystemRoot%\System32\WScript.exe" "%vbsGetPrivileges%" %*
exit /B

:gotPrivileges
setlocal & pushd .
cd /d %~dp0
if '%1'=='ELEV' (del "%vbsGetPrivileges%" 1>nul 2>nul  &  shift /1)

rem ---------------------------代码段结束--------------------------------
rem 将以上部分放在要执行的批处理之前。





@echo off
echo.
echo.

echo.
echo.
echo 此批处理会将【电源方案】设置为【高性能】，并逐一设定细项
echo.
echo 切换电源方案为【高性能】
Powercfg -s 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 
echo.
echo 从不关闭显示器
Powercfg -x -monitor-timeout-dc 0   
Powercfg -x -monitor-timeout-ac 0    
echo.
echo 从不关闭硬盘
powercfg -x -disk-timeout-dc 0   
powercfg -x -disk-timeout-ac 0    
echo.
echo 从不自动进入睡眠状态
powercfg -x -standby-timeout-dc 0    
powercfg -x -standby-timeout-ac 0   
echo.
echo 从不自动进入休眠状态
powercfg -x -hibernate-timeout-dc 0   
powercfg -x -hibernate-timeout-ac 0    
echo.
echo 唤醒时不需要密码
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c fea3413e-7e05-4911-9a71-700331f1c294 0e796bdb-100d-47d6-a2d5-f7d2daa51f51 0    
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c fea3413e-7e05-4911-9a71-700331f1c294 0e796bdb-100d-47d6-a2d5-f7d2daa51f51 0     
echo.
echo 从不关闭硬盘
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0      
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 0012ee47-9041-4b5d-9b77-535fba8b1442 6738e2c4-e8a5-4a42-b16a-e040e769756e 0      
echo.
echo JavaScript 计时器频率最高性能
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 02f815b5-a5cf-4c84-bf20-649d1f75d3d8 4c793e7d-a264-42e1-87d3-7a0d2f523ccd 1   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 02f815b5-a5cf-4c84-bf20-649d1f75d3d8 4c793e7d-a264-42e1-87d3-7a0d2f523ccd 1     
echo.
echo 桌面允许放映幻灯片
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 0   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 0d7dbae2-4294-402a-ba8e-26777e8488cd 309dce9b-bef4-4119-9921-a851fb12f0f4 0   
echo.
echo 无线适配器最高性能
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0    
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 19cbb8fa-5279-450e-9fac-8a3d5fedd0c1 12bbebe6-58d6-4636-95bb-3217ef867c1a 0    
echo.
echo 从不自动进入睡眠
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 29f6c1db-86da-48c5-9fdb-f2b67b1f44da 0   
echo.
echo 允许混合睡眠
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 1   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 94ac6d29-73ce-41a6-809f-6363ba21b47e 1   
echo.
echo 从不自动进入休眠
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 9d7815a6-7ee4-497e-8888-515a05f02364 0    
echo.
echo 允许使用唤醒定时器
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 1   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 238c9fa8-0aad-41ed-83f4-97be242c8f20 bd3b718a-0680-4d9d-8ab2-e1d2b4ac806d 1   
echo.
echo USB 选择性暂停设置
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 2a737441-1930-4402-8d77-b2bebba308a3 48e6b7a6-50f5-4782-a5d4-53bb8f07e226 0 
echo.
echo PCI Express 省电关闭
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 501a4d13-42af-4429-9fd1-a8218c268e20  ee12f906-d277-404b-b6da-e5fa1a576df5 0   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 501a4d13-42af-4429-9fd1-a8218c268e20  ee12f906-d277-404b-b6da-e5fa1a576df5 0  
echo.
echo 合上盖子，不采取任何操作
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 0   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 4f971e89-eebd-4455-a8de-9e59040e7347 5ca83367-6e45-459f-a27b-476b1d01c936 0   
echo.
echo 按下电源按钮为关机
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 3   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 4f971e89-eebd-4455-a8de-9e59040e7347 7648efa3-dd9c-4e3e-b566-50f929386280 3  
echo.
echo 按下睡眠按钮为睡眠
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 4f971e89-eebd-4455-a8de-9e59040e7347 96996bc0-ad50-47ec-923b-6f41874dd9eb 1   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 4f971e89-eebd-4455-a8de-9e59040e7347 96996bc0-ad50-47ec-923b-6f41874dd9eb 1   
echo.
echo 系统散热方式为主动
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 1   
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 54533251-82be-4824-96c1-47b60b740d00 94d3a615-a899-4ac5-ae2b-e4d8f634367f 1   
echo.
echo 媒体共享时，阻止计算机进入睡眠状态
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 03680956-93bc-4294-bba6-4e0f09bb717f 1
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 03680956-93bc-4294-bba6-4e0f09bb717f 1
echo.
echo Video playback quality bias
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 1
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 10778347-1370-4ee0-8bbd-33bdacaade49 1
echo.
echo 播放视频时，优化视频质量
powercfg -setdcvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 0
powercfg -setacvalueindex 8c5e7fda-e8bf-4a96-9a85-a6e23a8c635c 9596fb26-9850-41fd-ac3e-f7c3c00afd4b 34c7b99f-9a6d-4b3c-8dc7-b6693b78cef4 0

::::::::::::::::::::::修改注册表，获取管理员权限::::::::::::::::::
@echo off
echo 获取Administrator权限
cacls.exe "%SystemDrive%\System Volume Information" >nul 2>nul
if %errorlevel%==0 goto Admin
if exist "%temp%\getadmin.vbs" del /f /q "%temp%\getadmin.vbs"
echo Set RequestUAC = CreateObject^("Shell.Application"^)>"%temp%\getadmin.vbs"
echo RequestUAC.ShellExecute "%~s0","","","runas",1 >>"%temp%\getadmin.vbs"
echo WScript.Quit >>"%temp%\getadmin.vbs"
"%temp%\getadmin.vbs" /f
if exist "%temp%\getadmin.vbs" del /f /q "%temp%\getadmin.vbs"
exit
echo.
:Admin
echo.
echo 成功取得Administrator权限
echo.
::::::::::::::::::::::关闭Windows Defender::::::::::::::::::
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender" /v "DisableAntiSpyware" /d 1 /t REG_DWORD /f

::::::::::::::::::::::关闭Windows防火墙::::::::::::::::::
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsFirewall\DomainProfile" /v "EnableFirewall" /d 0 /t REG_DWORD /f
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsFirewall\PrivateProfile" /v "EnableFirewall" /d 0 /t REG_DWORD /f
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\WindowsFirewall\PublicProfile" /v "EnableFirewall" /d 0 /t REG_DWORD /f

::::::::::::::::::::::Windows 更新::::::::::::::::::
reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Action Center\Checks\%acguid%" /v CheckSetting /d "01000000d08c9ddf0115d1118c7a00c04fc297eb010000006049bbbf6216294aa2953641ffc9152c00000000020000000000106600000001000020000000a7ae8c9aa7ebe4742746b947752993893f926c5854829125b440977d5ee42ce5000000000e800000000200002000000019f8de4a9ee294910a8eb38395fd9a6bb95c9b9539f442f35a849b34959437f5d0000000816e8363d0d3a4ee18b296952d9a75e594bd8b0b70f7958b7ed114e2fc3a5e371cadb4a5a0d5d20a32f73106aa932dee2c77ad82b28e3a62034385ab0b282f60961ee50ac870ca46981ee4a5a57d0040bd3a3f940852f82951d4e08cb8eb0f61be0cc6b28efd6278ab5ad483d19ad2d65cd9fcdc8cdbadf618d2203a45575e407e961158a33f37ec30e504314a9526013c7690a204e8d77c17d6c9fccae82c308dd0070f0c9b237c1a849e8042632cd33f5f5b955c4c6fb5308cae6f76834f26c1a7ab2095037324618aabf8bbe5ffc440000000fce98e4305f7e85d4105e078edd7ca2ff76be6da04a03e476f8af4ca06354805e2bd69c105b43b19732253779f7d92616e5255f86cfc4833f4232770c74cfc10" /t REG_BINARY /f

::::::::::::::::::::::间谍软件和垃圾软件防护::::::::::::::::::
reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Action Center\Checks\%acguid%" /v CheckSetting /d "01000000d08c9ddf0115d1118c7a00c04fc297eb010000006049bbbf6216294aa2953641ffc9152c000000000200000000001066000000010000200000001e9257367aee8da07d46c24072e826aa1c15f8803c5caca94939cfe119824f02000000000e800000000200002000000035f6e4f999b399b08d58ba843e45dbdff3b2442e92961fb86f8f7c8b16d5b8a4300000002456b2243feec6e4f1cfa2c744ed5cbfc0dd806986c4657031eaf479fbd32aca00f23a1a978df2e3c422adbf2d00b0e3400000008bb2c177465efd545edf842c16b8668f496fe449ab9a640deca042e87175f99fef354a54c6dc4fcb470241f973203775917e4831c22bcfee232673cd29d72736" /t REG_BINARY /f

::::::::::::::::::::::用户账户控制(UAC)::::::::::::::::::
reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Action Center\Checks\%acguid%" /v CheckSetting /d "01000000d08c9ddf0115d1118c7a00c04fc297eb010000006049bbbf6216294aa2953641ffc9152c0000000002000000000010660000000100002000000085b19b580389f6fe3b433fa4a0dca27cfba7ebe10c75063b0151d03f316fd6d8000000000e80000000020000200000003b1268f462df692609d30181fc1b1bf301feaea81062b2b8d85d0453da9e8b8e30000000cd0a948cf29ecdf35d317d26f81ecb379d987adb1ed174e2f75f70c689f815ea00fe3cf5fcfec9a14b6c32a6a9fb12b2400000003d8c500984a8fd0aea4ed4651d109c55120e89260b8c02f117ee5b2c5255395b10cbb139c40d09f728394f254297a3ce2f73cfe515a405e4afd22264d16df833" /t REG_BINARY /f

::::::::::::::::::::::网络防火墙::::::::::::::::::
reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Action Center\Checks\%acguid%" /v CheckSetting /d "01000000d08c9ddf0115d1118c7a00c04fc297eb010000006049bbbf6216294aa2953641ffc9152c000000000200000000001066000000010000200000008ed73cb7d5922e58de1a7c681e336c27cb43f8b42b60a3148dd37289855494e0000000000e8000000002000020000000690226f5db59f646af3b56ec513ed574f28f7b10d6e1e393e6154351faea91a93000000086b9f35c5099fc9e68510b27bfe7e893999e84b16de9b79b04468708aa23e65ee61b7a4179455f426a0c446d5e5388344000000065e349a51dcd1290427e45e141f652c08c6230c574eea0e688231cd1ea6a1adb1858d21613c5266b0b547fc5b75bb310bba9a72893484d95ee8115f516993fc5" /t REG_BINARY /f

::::::::::::::::::::::病毒防护::::::::::::::::::
reg add "HKEY_CURRENT_USER\Software\Microsoft\Windows\CurrentVersion\Action Center\Checks\%acguid%" /v CheckSetting /d "01000000d08c9ddf0115d1118c7a00c04fc297eb010000006049bbbf6216294aa2953641ffc9152c0000000002000000000010660000000100002000000031843580b428b9af9595e96916cbd0f19ea89fde15f69a411682020989314a96000000000e80000000020000200000003de0e0d59079c8bfbf9b5166e4a049d90909799c2f1f163f3cbb6c86d6c3320730000000a9e333647b93b8be971c064c8acbf513de72634e77e134f5c45d46b07899dd8dd8f59245fe0d46f10e05b5af70cc98bc40000000cd231d8f5af986e73f5ba3faab6cd22d3f9d8f329590a9d00cb4d07d31b9309ee838bd6d1a5a11b763ff87c6be3323deb8704b3d22d1a9555857f690d3b3bd67" /t REG_BINARY /f
%1 mshta vbscript:CreateObject("Shell.Application").ShellExecute("cmd.exe","/c %~s0 ::","","runas",1)(window.close)&&exit


echo 显示计算机
REG ADD HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel /v {20D04FE0-3AEA-1069-A2D8-08002B30309D} /t REG_DWORD /d 0 /f
echo 显示控制面板
REG ADD HKCU\Software\Microsoft\Windows\CurrentVersion\Explorer\HideDesktopIcons\NewStartPanel /v {5399E694-6CE5-4D6C-8FCE-1D8870FDCBA0} /t REG_DWORD /d 0 /f

echo  关闭快速启动
powercfg /h off
echo  关闭防火墙
NetSh Advfirewall set allprofiles state off  
echo  防火墙状态
Netsh Advfirewall show allprofiles 

echo  关闭Defender
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender" /v "DisableAntiSpyware" /d 1 /t REG_DWORD /f

reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v "DisableBehaviorMonitoring" /d 1 /t REG_DWORD /f
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v "DisableIOAVProtection" /d 1 /t REG_DWORD /f
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v "DisableOnAccessProtection" /d 1 /t REG_DWORD /f
reg add "HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows Defender\Real-Time Protection" /v "DisableRealtimeMonitoring" /d 1 /t REG_DWORD /f

reg add "HKEY_LOCAL_MACHINE\SYSTEM\CurrentControlSet\Services\SecurityHealthService" /v "Start" /d 4 /t REG_DWORD /f
RunDll32.exe USER32.DLL,UpdatePerUserSystemParameters


taskkill /im Windows10UpgraderApp.exe 2>nul
del /f /q "%USERPROFILE%\Desktop\微软 Windows 10 易升.lnk" 2>nul
del /f /q "%USERPROFILE%\Desktop\Windows 10 Update Assistant.lnk" 2>nul


echo 添加防火墙规则 / Adding firewall rules... 


netsh advfirewall firewall add rule name="Block_Windows10UpgraderApp" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\Windows10UpgraderApp.exe" action=block
netsh advfirewall firewall add rule name="Block_WinREBootApp32" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\WinREBootApp32.exe" action=block
netsh advfirewall firewall add rule name="Block_WinREBootApp64" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\WinREBootApp64.exe" action=block
netsh advfirewall firewall add rule name="Block_bootsect" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\bootsect.exe" action=block
netsh advfirewall firewall add rule name="Block_DW20" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\DW20.EXE" action=block
netsh advfirewall firewall add rule name="Block_DWTRIG20" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\DWTRIG20.EXE" action=block
netsh advfirewall firewall add rule name="Block_GatherOSState" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\GatherOSState.EXE" action=block
netsh advfirewall firewall add rule name="Block_GetCurrentRollback" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\GetCurrentRollback.EXE" action=block
netsh advfirewall firewall add rule name="Block_HttpHelper" dir=in program="%SYSTEMDRIVE%\Windows10Upgrade\HttpHelper.exe" action=block
netsh advfirewall firewall add rule name="Block_UpdateAssistant" dir=in program="%SYSTEMROOT%\UpdateAssistant\UpdateAssistant.exe" action=block
netsh advfirewall firewall add rule name="Block_UpdateAssistantCheck" dir=in program="%SYSTEMROOT%\UpdateAssistant\UpdateAssistantCheck.exe" action=block
netsh advfirewall firewall add rule name="Block_Windows10Upgrade" dir=in program="%SYSTEMROOT%\UpdateAssistant\Windows10Upgrade.exe" action=block
netsh advfirewall firewall add rule name="Block_UpdateAssistantV2" dir=in program="%SYSTEMROOT%\UpdateAssistantV2\UpdateAssistant.exe" action=block
netsh advfirewall firewall add rule name="Block_UpdateAssistantCheckV2" dir=in program="%SYSTEMROOT%\UpdateAssistantV2\UpdateAssistantCheck.exe" action=block
netsh advfirewall firewall add rule name="Block_Windows10UpgradeV2" dir=in program="%SYSTEMROOT%\UpdateAssistantV2\Windows10Upgrade.exe" action=block


echo 设置ACL / Configurating ACL... 


echo y|cacls C:\Windows\UpdateAssistant\*.exe /t /p everyone:n 2>nul
echo y|cacls C:\Windows10Upgrade\*.exe /t /p everyone:n 2>nul

echo 停止Windows Update服务 / Disable Windows Update 


net stop wuauserv
sc config wuauserv start= disabled


echo 删除计划任务 / Delete task... 

schtasks /delete /TN "\Microsoft\Windows\UpdateOrchestrator\UpdateAssistant" /f 2>nul
schtasks /delete /TN "\Microsoft\Windows\UpdateOrchestrator\UpdateAssistantAllUsersRun" /f 2>nul
schtasks /delete /TN "\Microsoft\Windows\UpdateOrchestrator\UpdateAssistantCalendarRun" /f 2>nul
schtasks /delete /TN "\Microsoft\Windows\UpdateOrchestrator\UpdateAssistantWakeupRun" /f 2>nul


echo 设置注册表 / Editing Registry... 

del /q /f %SYSTEMDRIVE%\NAU.reg 2>nul
echo Windows Registry Editor Version 5.00 >> %SYSTEMDRIVE%\NAU.reg
echo.>> %SYSTEMDRIVE%\NAU.reg
echo [HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate]>> %SYSTEMDRIVE%\NAU.reg
echo "DoNotConnectToWindowsUpdateInternetLocations"=dword:00000001 >> %SYSTEMDRIVE%\NAU.reg
echo.>> %SYSTEMDRIVE%\NAU.reg
echo [HKEY_LOCAL_MACHINE\SOFTWARE\Policies\Microsoft\Windows\WindowsUpdate\AU]>> %SYSTEMDRIVE%\NAU.reg
echo "NoAutoUpdate"=dword:00000001>> %SYSTEMDRIVE%\NAU.reg
REG IMPORT %SYSTEMDRIVE%\NAU.reg
del /q /f %SYSTEMDRIVE%\NAU.reg 2>nul

powercfg /h off
echo 任务结束 / All Done！
gpupdate /force
taskkill /f /im explorer.exe
start %systemroot%\explorer




