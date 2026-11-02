import {useState} from 'react';
import {Boxes,LayoutGrid,Heart,Globe2,HardDrive,MonitorCog,Settings2,BookOpen,RefreshCw,LockKeyhole,Activity,Gauge,ListTree,Network,ShieldCheck,Trash2,PackageOpen,ArchiveRestore,KeyRound,Power,ShieldOff,ToggleLeft,FolderArchive,MemoryStick,ChartPie,Layers3} from 'lucide-react';
export const navIcons={'全部工具':LayoutGrid,'常用工具':Heart,'效率工具':Boxes,'网络工具':Globe2,'磁盘工具':HardDrive,'重装系统':ArchiveRestore,'重装维护':MonitorCog,'系统设置':Settings2,'软件合集':Layers3,'系统信息':MonitorCog,'使用说明':BookOpen,'软件更新':RefreshCw};
const icons={firewall:Activity,speed:Gauge,autoruns:ListTree,gpu:MemoryStick,defender:ShieldCheck,'defender-control':ShieldCheck,uninstall:Trash2,runtime:Boxes,archive:FolderArchive,driver:PackageOpen,reinstall:ArchiveRestore,'technician-reinstall':ArchiveRestore,activation:KeyRound,pe:Power,isolation:ShieldOff,security:ShieldOff,ipv6:Network,updates:RefreshCw,hyperv:ToggleLeft,uac:ShieldOff,'browser-cleanup':Trash2,'smb-connection-test':Network,diskgenius:HardDrive,spacesniffer:ChartPie};
Object.assign(icons,{'win10-optimization':Settings2,'heu-kms':KeyRound,'ace-virtualization':MonitorCog});
export function ToolIcon({tool,id=tool?.Id,size=32,...props}){
 const [failed,setFailed]=useState(false);
 const Icon=icons[id]||Boxes;
 return tool?.IconPath&&!failed?<img className="tool-logo" src={`./${tool.IconPath}`} width={size} height={size} alt="" draggable="false" onError={()=>setFailed(true)}/>:<span className={`fallback-tool-logo icon-${id}`}><Icon size={size} strokeWidth={1.65} {...props}/></span>;
}
export {Boxes,LockKeyhole};
