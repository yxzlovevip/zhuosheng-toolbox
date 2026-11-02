import {Heart,Search} from 'lucide-react';
import {useEffect,useState} from 'react';
import {ToolGrid} from '../ToolGrid';
import {ToolDetail} from '../ToolDetail';
import {useScrollMemory} from '../hooks/useScrollMemory';
const opening={Status:'opening',Message:''};

export function ToolWorkspace({category,query,tools,tool,launcher,states,stateError,favorites,onFavorite,onSelect,onClear,onAllTools,onCheckFiles,onLogs,scrollMemory}){
 const scroll=useScrollMemory(scrollMemory,`tools/${category}/${query}`);
 const [entering,setEntering]=useState(()=>!scrollMemory.current.has(`visited/${category}`));
 useEffect(()=>{scrollMemory.current.set(`visited/${category}`,true);const timer=setTimeout(()=>setEntering(false),400);return()=>clearTimeout(timer);},[scrollMemory,category]);
 useEffect(()=>{if(query)setEntering(false);},[query]);
 const {launchingId,launch}=launcher,busy=launchingId!==null;
 const visibleStates=launchingId?{...states,[launchingId]:opening}:states;
 const emptyFavorites=category==='常用工具'&&!tools.length&&!query.trim();
 return <div className="tool-workspace"><section className="tool-grid-scroll" aria-label="工具目录" data-entering={entering&&!query} {...scroll}>
  {category==='常用工具'?<p className="favorites-intro"><Heart size={16} aria-hidden="true"/>你的常用工具，随时点亮或取消爱心。</p>:category==='全部工具'?<p className="favorites-intro"><Heart size={16} aria-hidden="true"/>点亮爱心，放进常用工具。</p>:null}
  {category==='重装维护'?<div className="topic-intro"><h2>重装与维护</h2><p>重装前先备份资料。单击查看说明，双击打开工具；工具密码可直接复制。</p></div>:null}
  <ToolGrid tools={tools} category={category} selected={tool?.Id} onSelect={onSelect} onLaunch={launch} busy={busy} toolStates={visibleStates} favorites={favorites} onToggleFavorite={onFavorite}/>
  {emptyFavorites?<div className="empty-results favorites-empty"><Heart size={30} aria-hidden="true"/><h2>还没有常用工具</h2><p>在工具卡片上点亮爱心，下次查找更方便。</p><button className="secondary" onClick={onAllTools}>去选择常用工具</button></div>:!tools.length?<div className="empty-results"><Search size={28} aria-hidden="true"/><h2>没有找到匹配的工具</h2><p>试试更短的名称或用途。</p><button className="secondary" onClick={onClear}>清除搜索</button></div>:null}
  <div className="catalog-footer"><span>单击查看说明 · 双击或 Enter 打开</span></div>
 </section><ToolDetail key={tool?.Id} tool={tool} onLaunch={launch} busy={busy} launching={launchingId===tool?.Id} runState={visibleStates[tool?.Id]} stateError={stateError} emptyFavorites={emptyFavorites} onCheckFiles={onCheckFiles} onLogs={onLogs}/></div>;
}
