import {memo} from 'react';
import {Heart} from 'lucide-react';
import {ToolIcon} from './icons';
import {ToolPassword} from './ToolPassword';
import {ToolRunState} from './ToolRunState';
export const ToolCard=memo(function ToolCard({tool,selected,onSelect,onLaunch,busy,index,runState,favorite,onToggleFavorite}) {
 return <article className={`tool-card ${selected?'selected':''}`} data-category={tool.Category} style={{'--order':Math.min(index,8)}} onClick={()=>onSelect(tool.Id)} onDoubleClick={()=>{if(!busy)onLaunch(tool);}}>
  <button className="card-select" aria-label={tool.Name} aria-pressed={selected} title="单击查看说明，双击打开；按 Enter 打开" onKeyDown={event=>{if(event.key==='Enter'){event.preventDefault();if(!busy&&!event.repeat)onLaunch(tool);}}}>
   <span className="tool-icon"><ToolIcon tool={tool}/></span><span className="card-copy"><strong>{tool.Name}</strong><span className="card-summary">{tool.Summary}</span><span className="card-category">{tool.Group||tool.Category}</span></span>
  </button>
  <button className="favorite-button" aria-label={`${favorite?'取消':'标记'}常用工具：${tool.Name}`} title={favorite?'取消常用工具':'标记为常用工具'} aria-pressed={favorite} onClick={event=>{event.stopPropagation();onToggleFavorite(tool.Id);}} onDoubleClick={event=>event.stopPropagation()}><Heart size={18} fill={favorite?'currentColor':'none'} strokeWidth={1.8} aria-hidden="true"/></button>
  <ToolRunState state={runState}/><ToolPassword tool={tool} compact/>
 </article>;
});
