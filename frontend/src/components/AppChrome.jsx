import {memo} from 'react';
import {Minus,Square,X,Sun,Moon,Monitor,Search,PanelLeftClose,PanelLeftOpen} from 'lucide-react';
import {navIcons,LockKeyhole} from '../icons';
import {CompanyMark} from './CompanyLogo';
import {invoke} from '../bridge';

export const toolCategories=['全部工具','常用工具','效率工具','网络工具','磁盘工具','重装维护','系统设置'];
export function TitleBar(){
 return <header className="titlebar" onPointerDown={event=>{if(event.button===0&&!event.target.closest('button'))invoke('drag');}} onDoubleClick={event=>{if(!event.target.closest('button'))invoke('maximize');}}><span><CompanyMark size={20}/>卓盛工具箱</span><div className="window-actions">{[[Minus,'最小化','minimize'],[Square,'最大化','maximize'],[X,'关闭窗口','close']].map(([Icon,label,action])=><button key={action} aria-label={label} onClick={()=>invoke(action)}><Icon size={action==='maximize'?12:16} aria-hidden="true"/></button>)}</div></header>;
}
export function ThemeToggle({mode,onChange}){
 return <div className="theme-toggle" role="group" aria-label="界面主题">{[['light',Sun,'浅色'],['dark',Moon,'深色'],['system',Monitor,'跟随系统']].map(([key,Icon,label])=><button key={key} className={mode===key?'active':''} onClick={()=>onChange(key)} aria-label={label} title={label} aria-pressed={mode===key}><Icon size={17} aria-hidden="true"/></button>)}</div>;
}
export const Sidebar=memo(function Sidebar({hidden,category,onNavigate,onLock,version,hasUpdate}){
 function navItem(name){const Icon=navIcons[name];return <button key={name} title={name} aria-label={name} data-section={name} aria-current={category===name?'page':undefined} className={category===name?'active':''} onClick={()=>onNavigate(name)}><Icon size={20} strokeWidth={1.65} aria-hidden="true"/><span>{name}</span>{name==='软件更新'&&hasUpdate?<i aria-label="有新版本"/>:null}</button>;}
 return <aside id="main-sidebar" className="sidebar" hidden={hidden}><div className="brand"><CompanyMark/><strong>卓盛工具箱</strong></div><nav aria-label="主要导航">{['系统信息',...toolCategories,'软件合集'].map(navItem)}</nav><div className="sidebar-bottom"><nav aria-label="帮助与更新">{['使用说明','软件更新'].map(navItem)}</nav><button aria-label="锁定" title="锁定工具箱" className="lock-button" onClick={onLock}><LockKeyhole size={18} aria-hidden="true"/><span>锁定</span><small>v{version}</small></button></div></aside>;
});
export function WorkspaceHeader({category,count,query,onQuery,theme,onTheme,sidebarHidden,onToggleSidebar}){
 const isTools=toolCategories.includes(category);
 return <div className="workspace-top"><div className="section-title"><button className="sidebar-toggle" aria-label={sidebarHidden?'展开侧栏':'收起侧栏'} title={sidebarHidden?'展开侧栏':'收起侧栏'} aria-expanded={!sidebarHidden} aria-controls="main-sidebar" onClick={onToggleSidebar}>{sidebarHidden?<PanelLeftOpen size={20} aria-hidden="true"/>:<PanelLeftClose size={20} aria-hidden="true"/>}</button><h1>{category==='全部工具'?'工具中心':category}</h1>{isTools?<span aria-live="polite">{count} 个工具</span>:null}</div><div className="top-controls">{isTools?<div className="search-box"><Search size={17} aria-hidden="true"/><input name="tool-search" autoComplete="off" spellCheck={false} aria-label="搜索工具" placeholder="搜索名称或用途…" value={query} onChange={event=>onQuery(event.target.value)}/>{query?<button aria-label="清除搜索" onClick={()=>onQuery('')}><X size={16} aria-hidden="true"/></button>:null}</div>:null}<ThemeToggle mode={theme} onChange={onTheme}/></div></div>;
}
