import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {fileURLToPath} from 'node:url';
import {describeSoftwareLink} from './lib/software-link-metadata.mjs';

// Parse data literals only. Never execute scripts from either source document.
const root=path.resolve(path.dirname(fileURLToPath(import.meta.url)),'..');
const sourceReviews=JSON.parse(fs.readFileSync(path.join(root,'assets/software-source-reviews.json'),'utf8'));
const localFile=process.argv[2]||path.join(root,'.build/software-collection-sources/local.html');
const websiteFile=process.argv[3]||path.join(root,'.build/software-collection-sources/website.html');
const local=fs.readFileSync(localFile,'utf8'),website=fs.readFileSync(websiteFile,'utf8');
const webMatch=website.match(/const data = (\{[\s\S]*?\});\s*const categories/);
if(!webMatch)throw Error('网站数据格式已变化，请重新审核导入。');
const websiteData=JSON.parse(webMatch[1]);
const aliases=new Map();
function alias(name,...names){for(const n of [name,...names])aliases.set(n.normalize('NFKC').toLowerCase().replace(/\s+/g,''),name);}
alias('Google Chrome','谷歌浏览器','谷歌Chromium浏览器');
alias('Microsoft Edge','Edge');alias('Mozilla Firefox','火狐浏览器');
alias('360安全浏览器','360浏览器');alias('夸克浏览器','夸克');
alias('Geek Uninstaller','Geek');alias('7-Zip','7-zip','7-Zip压缩解压');alias('Bandizip','Bandizip解压缩');
alias('QQ','腾讯QQNT');alias('QQ 怀旧版','腾讯QQ怀旧版');alias('微信','微信(WeChat)','微信(WeiXin)');
alias('KOOK','KOOK语音');alias('ToDesk','Todesk');alias('PotPlayer','potplayer','Potplayer');
alias('向日葵远程控制','向日葵远程','贝锐向日葵远程');alias('网易UU远程','UU远程');
alias('网易UU加速器','UU加速器');alias('搜狗拼音输入法','搜狗输入法');
alias('DirectX 修复工具','DirectX修复工具','DirectX_Repair');alias('Visual C++ 运行库合集','Visual C 合集包','微软常用运行库');
alias('Ubisoft Connect','Uplay','育碧游戏');alias('暴雪战网','战网','Battle.net','暴雪战网平台');
alias('5E对战平台','5E平台');alias('完美世界竞技平台','完美平台');alias('QQ游戏大厅','QQ游戏');
alias('三角洲行动（国际服）','三角洲行动[国际服]');alias('无畏契约（国服）','Valorant');
alias('抖音直播伴侣','抖音直播');alias('快手直播伴侣','快手直播');
alias('NVIDIA 驱动下载','NVIDIA 驱动官网','英伟达','NVIDIA显卡');alias('AMD 驱动下载','AMD驱动','AMD显卡');
alias('联想驱动下载','Lenovo(联想)驱动官网','联想驱动下载中心');
alias('戴尔驱动下载','Dell(戴尔)驱动官网','戴尔支持网站');alias('惠普驱动下载','HP(惠普)驱动官网','惠普客户支持');
alias('华硕驱动下载','ASUS(华硕)驱动官网','华硕下载中心');alias('微星驱动下载','Msi(微星)驱动官网','微星支持中心');
alias('技嘉驱动下载','GiGabyte(技嘉)驱动官网','GIGABYTE');alias('七彩虹驱动下载','Colorful(七彩虹)驱动官网','七彩虹驱动');
alias('罗技 G HUB','罗技 G-Hub 游戏驱动','罗技驱动');alias('ASUS Armoury Crate','ASUS Armoury Crat','华硕奥创');
alias('GIGABYTE Control Center','GiGABYTE Control Centel');alias('MSI Afterburner','Msi AFterBurner');
alias('DiskGenius','diskgenius');alias('BlueScreenView','bluescreenview');alias('PixPin','Pixpin');
alias('NVIDIA 历史驱动下载','Nvidia显卡历史版本');
const categoryMap={'系统':'系统维护','驱动':'驱动工具','工具':'常用工具','浏览器':'浏览器','聊天交友':'聊天通讯','视频音乐':'影音娱乐','办公':'办公学习','游戏':'游戏平台','加速器':'游戏加速','远程协助':'远程控制','装机必备':'常用工具','音乐视频':'影音娱乐','游戏加速器':'游戏加速','社交软件':'聊天通讯','办公软件':'办公学习','实用软件':'常用工具','直播软件':'直播录屏','维护安全':'硬件检测','热门资源':'常用工具','工具导航':'资源导航','最常用软件':'常用工具','AI导航':'AI 工具','驱动安装':'驱动工具','电脑检测':'硬件检测','N卡驱动推荐':'历史显卡驱动'};
const overrides=new Map();
function category(value,names){for(const name of names.split('|'))overrides.set(name,value);}
category('浏览器','Google Chrome|Microsoft Edge|Mozilla Firefox|夸克浏览器|QQ浏览器|360安全浏览器|360极速浏览器|360极速浏览器X');
category('聊天通讯','QQ|QQ 怀旧版|微信|企业微信|KOOK|YY语音|OOPZ|TT语音|黑盒语音');
category('办公学习','WPS Office|腾讯文档|腾讯会议|钉钉|飞书|Adobe Acrobat|福昕PDF阅读器|Microsoft - Office|Xmind|网易邮箱大师');
category('网盘下载','百度网盘|阿里云盘|夸克网盘|迅雷');category('AI 工具','豆包');
category('影音娱乐','抖音|剪映|PotPlayer');category('游戏平台','Steam|蒸汽平台|Ubisoft Connect|暴雪战网|QQ游戏大厅|游戏加加|雷电模拟器|TapTap');
category('直播录屏','抖音直播伴侣|快手直播伴侣');
category('系统维护','DirectX 修复工具|Visual C++ 运行库合集|火绒安全|360安全卫士|360安全卫士极速版|360系统急救箱|系统激活|HyperV-off|DDU|DiskGenius|CCleaner|显卡更新安装工具');
category('常用工具','Everything|Geek Uninstaller|7-Zip|Bandizip|WinRAR|PixPin|Honeyview|美图秀秀');
category('硬件检测','图吧工具箱|测速网');category('壁纸美化','TranslucentTB');
const corrections=[];
function corrected(name,url){
 let next=url;
 if(name==='GIGABYTE Control Center')next='https://www.gigabyte.com/Consumer/Software/GIGABYTE-Control-Center/global/';
 if(name==='TT语音'&&/\.apk(?:$|\?)/i.test(url))next='https://www.52tt.com/';
 if(name==='测速网'&&new URL(url).hostname==='www.baidu.com')next='https://www.speedtest.cn/';
 if(name==='酷我音乐'&&new URL(url).hostname==='download.kugou.com')next='https://www.kuwo.cn/static/page/downloadPage/index.html';
 if(name==='逆水寒'&&new URL(url).hostname==='df.qq.com')next='https://n.163.com/download/';
 if(name.startsWith('NVIDIA ')){next=next.replace('/cnGeForce/www.linkedin.com','/cn/').replace('/en-us/Buradan/','/en-us/');}
 if(next!==url)corrections.push({name,original:url,replacement:next});
 return next;
}
const hash=value=>createHash('sha256').update(value).digest('hex').slice(0,16);
const items=new Map();let localCount=0,websiteCount=0;
function add(rawName,description,rawCategory,url,source){
 if(rawName==='本网站的新版UI')return;
 let name=aliases.get(rawName.normalize('NFKC').toLowerCase().replace(/\s+/g,''))||rawName;
 if(name==='Steam'&&new URL(url).hostname==='store.steamchina.com')name='蒸汽平台';
 const version=name.match(/^NVIDIA (\d+\.\d+)驱动/);
 if(version){name=`NVIDIA ${version[1]} 驱动`;description='历史显卡驱动版本，请在下载页核对支持的显卡型号和操作系统。';}
 const fixed=corrected(name,url),parsed=new URL(fixed);
 if(!['http:','https:'].includes(parsed.protocol)||parsed.username||parsed.password)throw Error(`不支持的链接：${rawName}`);
 // Normalize equivalent host/trailing-slash spellings without discarding download query parameters.
 const normalized=parsed.href;
 const item=items.get(name)||{id:'software-'+hash(name),name,description:description||name,category:overrides.get(name)||categoryMap[rawCategory]||rawCategory,aliases:[],sources:[],links:[]};
 if(!item.aliases.includes(rawName)&&rawName!==name)item.aliases.push(rawName);
 if(!item.sources.includes(source))item.sources.push(source);
 let link=item.links.find(l=>l.url===normalized);
 if(!link){
  const isShare=/lanzou|feijipan/i.test(parsed.hostname)||(/(^|\.)(baidu\.com|quark\.cn|123pan\.com|123684\.com|123912\.com)$/i.test(parsed.hostname)&&parsed.pathname.startsWith('/s/'));
  const type=isShare?'share':/\.(exe|msi|zip|7z|rar|apk)(?:$|\?)/i.test(parsed.pathname+parsed.search)?'file':'website';
  link={id:'link-'+hash(normalized),url:normalized,type,sources:[]};item.links.push(link);
 }
 if(!link.sources.includes(source))link.sources.push(source);
 items.set(name,item);
}
const localData=local.match(/const softwareData\s*=\s*\[([\s\S]*?)\];/);
if(!localData)throw Error('HTML 软件数据格式已变化，请重新审核导入。');
for(const match of localData[1].matchAll(/\{([^{}]+)\}/g)){
 const read=key=>{const m=match[1].match(new RegExp('\\b'+key+'\\s*:\\s*("(?:[^"\\\\]|\\\\.)*")'));if(!m)throw Error('条目缺少 '+key);return JSON.parse(m[1]);};
 add(read('name'),read('description'),read('category'),read('downloadUrl'),'local');localCount++;
}
for(const group of Object.values(websiteData))for(const item of group.list)if(item.download){add(item.title,item.desc,group.displayName,item.download,'website');websiteCount++;}
if(localCount!==102||websiteCount<200)throw Error('来源条目数量发生明显变化，请人工检查。');
const order=['常用工具','浏览器','聊天通讯','影音娱乐','办公学习','网盘下载','游戏平台','游戏加速','远程控制','驱动工具','硬件检测','系统维护','AI 工具','直播录屏','壁纸美化','资源导航','历史显卡驱动'];
const list=[...items.values()];
for(const item of list){
 for(const link of item.links)Object.assign(link,describeSoftwareLink(item.name,link,sourceReviews));
 item.links.sort((a,b)=>{
  const rank=l=>(l.type==='share'?30:l.type==='file'?20:0)+(l.sources.includes('website')?0:2)+(new URL(l.url).protocol==='http:'?5:0);
  return rank(a)-rank(b);
 });
}
list.sort((a,b)=>order.indexOf(a.category)-order.indexOf(b.category)||a.name.localeCompare(b.name,'zh-CN'));
const result={updatedAt:'2026-10-22',sources:[{id:'local',name:'工具大合集',file:'工具大合集.html',entries:localCount,sha256:createHash('sha256').update(local).digest('hex')},{id:'website',name:'装机必备软件库',url:'http://qiufengjiema.cc.cd/',entries:websiteCount,sha256:createHash('sha256').update(website).digest('hex')}],categories:order.filter(c=>list.some(i=>i.category===c)),corrections,excluded:[{name:'本网站的新版UI',reason:'原网站自身的页面切换入口，不属于软件条目。'}],items:list};
fs.writeFileSync(path.join(root,'assets/software-collection.json'),JSON.stringify(result,null,2)+'\n');
console.log(JSON.stringify({local:localCount,website:websiteCount,merged:list.length,links:list.reduce((n,i)=>n+i.links.length,0),corrections:corrections.length,categories:result.categories}));
