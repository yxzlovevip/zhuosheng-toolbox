import fs from 'node:fs';
import path from 'node:path';
import crypto from 'node:crypto';
import {fileURLToPath} from 'node:url';
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const base='工具/开源工具';
const read=p=>JSON.parse(fs.readFileSync(path.join(root,p),'utf8').replace(/^\uFEFF/,''));
const write=(p,value)=>fs.writeFileSync(path.join(root,p),JSON.stringify(value,null,2)+'\n');
const hash=p=>crypto.createHash('sha256').update(fs.readFileSync(path.join(root,p))).digest('hex').toUpperCase();
const releases=read('assets/opensource-releases.json');
const definitions=[
 ['oss-powertoys','powertoys','PowerToys 效率工具集','效率工具','PowerToys.exe','launch',['--open-settings'],'窗口置顶、文字提取、分区布局和更多 Windows 实用功能。','打开后在左侧选择需要的功能。首次使用先阅读功能页面的快捷键和设置。','首次启动可能启用原工具的快捷键和后台模块；按需在 PowerToys 内关闭不需要的功能。'],
 ['oss-localsend','localsend','LocalSend 手机电脑传文件','网络工具','localsend_app.exe','launch',[],'同一局域网内，手机和电脑之间互传文件。','发送和接收设备都打开 LocalSend，连接同一局域网，选择对方设备后发送。','传输会占用局域网带宽和接收端空间；接收方需确认。关闭工具箱会中断尚未完成的传输。'],
 ['oss-trafficmonitor','trafficmonitor','TrafficMonitor 网速与性能','网络工具','TrafficMonitor/TrafficMonitor.exe','launch',[],'用悬浮窗或任务栏查看网速、CPU 和内存占用。','打开后查看悬浮窗，右键进入设置，可按需开启任务栏显示。','默认查看状态；修改任务栏显示和皮肤只影响本工具。温度传感器能否读取取决于硬件与驱动。'],
 ['oss-sharex','sharex','ShareX 截图与录屏','效率工具','ShareX.exe','launch',[],'区域截图、长截图、标注打码、录屏和文字识别。','打开后从“捕捉”选择截图方式，标注后保存；需要录屏时选择屏幕录制。','截图和录屏可能包含隐私。按需使用上传功能；当前集成使用独立设置目录。'],
 ['oss-screentogif','screentogif','ScreenToGif 动图录制','效率工具','ScreenToGif.exe','launch',[],'录制屏幕区域，剪辑并保存成 GIF 或视频。','打开“录像机”，选择区域并录制，完成后在编辑器中剪辑、另存为。','录制占用内存和磁盘空间。保存作品后再关闭工具箱，避免未保存的录制丢失。'],
 ['oss-quicklook','quicklook','QuickLook 空格预览','效率工具','QuickLook.exe','launch',[],'在资源管理器中选中文件，按空格快速预览。','打开后工具驻留在通知区域；回到文件夹选中文件，按空格预览，再按空格或 Esc 关闭。','不同文件格式的支持范围以当前插件为准；工具箱关闭后，文件预览服务一起结束。'],
 ['oss-bcuninstaller','bcuninstaller','BCUninstaller 批量卸载','效率工具','win-x64/BCUninstaller.exe','launch',[],'批量卸载不需要的软件，检查残留和孤立程序。','先等待软件列表加载，仅勾选确认不需要的软件，按原工具步骤预览并卸载。','卸载和残留清理会删除软件及相关数据。请保存工作，仔细核对目标，不能把卸载当成普通清理。'],
 ['oss-fileconverter','fileconverter','File Converter 文件格式转换','效率工具','FileConverter.exe','convert',[],'选择文件后，转换图片、音频、视频等格式。','先选择文件，再选择目标格式，点击“开始转换”。结果保存在原文件所在文件夹，文件名增加 _converted。','默认保留原文件、另存转换结果。支持的输入格式以所选预设为准，转换过程中不要关闭工具箱。'],
 ['pt-image-resizer','powertoys','图片批量缩小','效率工具','WinUI3Apps/PowerToys.ImageResizer.exe','launch',[],'用 PowerToys 批量调整图片尺寸。','打开后选择图片，设置尺寸并处理。保留原图时不要勾选替换原图。','原工具可选择覆盖原图；请先核对输出方式和尺寸。'],
 ['pt-power-rename','powertoys','批量改文件名','效率工具','WinUI3Apps/PowerToys.PowerRename.exe','rename',[],'用 PowerRename 搜索替换文件名、批量编号。','先选择文件，再进入 PowerRename，查看改名前后的预览，确认后应用。','会修改所选文件的名称，可能影响依赖固定路径的软件或项目。先检查预览。'],
 ['pt-file-locksmith','powertoys','文件占用查询','效率工具','FileLocksmithCLI.exe','lock',[],'查询哪个程序占用了删除不了或正在使用的文件。','选择需要查询的文件，点击“查询文件占用”，查看进程名称与编号。','本入口只查询，不结束进程、不强制删除文件。'],
 ['pt-awake','powertoys','临时防休眠（1 小时）','效率工具','PowerToys.Awake.exe','launch',['--time-limit','3600'],'临时保持电脑唤醒一小时，便于下载或演示。','打开后在通知区域查看 Awake 状态。一小时后自动结束本次保持唤醒。','临时增加耗电；屏幕仍可按系统设置熄灭。关闭工具箱会提前结束保持唤醒。'],
 ['pt-always-on-top','powertoys','窗口置顶设置','效率工具','PowerToys.exe','launch',['--open-settings=AlwaysOnTop'],'设置 PowerToys 窗口置顶，用快捷键固定当前窗口。','打开设置并启用窗口置顶，选中目标窗口后按页面显示的快捷键；默认 Win + Ctrl + T。','置顶窗口会遮住其他窗口，再按相同快捷键取消。入口打开设置，不自动置顶当前窗口。'],
 ['pt-text-extractor','powertoys','屏幕文字提取设置','效率工具','PowerToys.exe','launch',['--open-settings=TextExtractor'],'设置从屏幕区域识别并复制文字。','打开设置并启用文字提取，按页面显示的快捷键框选文字；默认 Win + Shift + T。','识别准确率取决于文字清晰度和系统 OCR 语言包。入口打开设置，不自动读取屏幕。'],
];
const mutable=new Set(['trafficmonitor/TrafficMonitor/global_cfg.ini','sharex/PersonalPath.cfg','bcuninstaller/win-x64/BCUninstaller.log']);
function* walk(dir){for(const item of fs.readdirSync(path.join(root,dir),{withFileTypes:true})){const relative=dir+'/'+item.name;if(item.isSymbolicLink())throw Error('Payload link: '+relative);if(item.isDirectory())yield* walk(relative);else yield relative;}}
if(process.argv.includes('--refresh-payload')) {
 for(const release of releases){const dir=base+'/'+release.Id;const files=[...walk(dir)].filter(p=>!p.endsWith('/payload.json')&&!/\.(log|patching)$/i.test(p)&&!mutable.has(p.slice((base+'/').length))).sort();write(dir+'/payload.json',files.map(p=>({Path:p.slice(dir.length+1),Size:fs.statSync(path.join(root,p)).size,Sha256:hash(p)})));}
}
const launchers=definitions.map(([id,project,name,category,exe,mode,args])=>({Id:id,Name:name,RelativePath:`${base}/${project}/${exe}`,PayloadManifest:`${base}/${project}/payload.json`,PayloadManifestSha256:hash(`${base}/${project}/payload.json`),Mode:mode,Arguments:args}));
// Copy only pinned distribution files. Runtime logs, screenshots and machine-specific
// settings created during smoke tests must never enter the installer via a glob.
const bundled=new Set([base+'/CZOpenSourceTools.exe',base+'/使用说明.md']);
for(const release of releases){const dir=base+'/'+release.Id;const manifest=dir+'/payload.json';bundled.add(manifest);for(const file of read(manifest))bundled.add(dir+'/'+file.Path);}
bundled.add(base+'/trafficmonitor/TrafficMonitor/global_cfg.ini');
const xml=s=>s.replaceAll('&','&amp;').replaceAll('"','&quot;').replaceAll('<','&lt;').replaceAll('>','&gt;');
fs.writeFileSync(path.join(root,'assets/opensource-payload.props'),'<Project>\n <ItemGroup>\n'+[...bundled].sort().map(p=>`  <Content Include="$(MSBuildThisFileDirectory)..\\${xml(p.replaceAll('/','\\'))}" Link="tools/${xml(p)}" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" ExcludeFromSingleFile="true" />`).join('\n')+'\n </ItemGroup>\n</Project>\n');
if(process.argv.includes('--prepare')){write('assets/opensource-launchers.json',launchers);console.log('Prepared '+launchers.length+' fixed launch entries.');process.exit(0);}
const wrapper=base+'/CZOpenSourceTools.exe';
if(!fs.existsSync(path.join(root,wrapper)))throw Error('Publish OpenSourceTools before catalog generation.');
const wrapperHash=hash(wrapper);
const additions=definitions.map(([id,project,name,category,exe,mode,args,summary,steps,impact],i)=>{
 const release=releases.find(r=>r.Id===project);const launch=launchers[i];
 return {Id:id,Name:name,Category:category,Group:project==='powertoys'?'PowerToys 实用功能':'日常实用工具',RelativePath:wrapper,Sha256:wrapperHash,Arguments:[id],PayloadManifest:launch.PayloadManifest,PayloadManifestSha256:launch.PayloadManifestSha256,InstanceExecutable:mode==='launch'?launch.RelativePath:'',IconSourcePath:launch.RelativePath,Summary:summary,Purpose:summary,Preparation:project==='localsend'?'确认两个设备处于可互通的同一局域网，并在手机或另一台电脑打开 LocalSend。':project==='bcuninstaller'?'保存工作，确认需要卸载的软件及其资料已经备份。':'先保存正在编辑的资料，准备需要处理的文件。',Steps:[steps,'在原工具内检查结果；处理中的文件请等待完成后再关闭工具箱。'],Impact:impact,Result:'以实际输出文件、窗口显示或原工具结果为准。启动成功不等于操作已经完成。',Risk:project==='bcuninstaller'?'较高':mode==='rename'?'中等':'常规',NeedsConfirmation:project==='bcuninstaller',Restart:'通常无需重启；以原工具提示为准。',Evidence:`来源：${release.Repository} ${release.Version}，官方发行包已核对 SHA-256。集成保留原界面，通过统一入口启动；跨设备传输、真实软件卸载等操作未在本机全面验收。${project==='fileconverter'?'此副本已适配为程序相对路径和独立用户设置，无需安装右键扩展。':''}`,SourceUrl:release.ReleaseUrl,UpstreamVersion:release.Version};
});
const previous=read('assets/catalog.json');
const aliasesById=new Map(previous.map(tool=>[tool.Id,tool.SearchAliases||[]]));
for(const tool of additions)tool.SearchAliases=aliasesById.get(tool.Id)||[];
const ids=new Set(additions.map(t=>t.Id));const existing=previous.filter(t=>!ids.has(t.Id));
write('assets/catalog.json',[...existing,...additions]);
console.log(`Integrated ${releases.length} projects with ${additions.length} entries; catalog now ${existing.length+additions.length}.`);
