import {ToolCard} from './ToolCard';

export function ToolGrid({tools,category,selected,onSelect,onLaunch,busy,toolStates,favorites,onToggleFavorite}) {
 const groups=category==='重装维护'?['系统重装','驱动与修复','激活与部署'].map(name=>({name,tools:tools.filter(t=>(t.Group||'驱动与修复')===name)})).filter(g=>g.tools.length):[{name:'',tools}];
 return <>{groups.map(group=><section className="tool-group" key={group.name||'all'} aria-label={group.name||'工具列表'}>{group.name?<h2 className="tool-group-title">{group.name}<span>{group.tools.length} 个工具</span></h2>:null}<div className="tool-grid">{group.tools.map((tool,index)=><ToolCard key={tool.Id} index={index} tool={tool} selected={selected===tool.Id} onSelect={onSelect} onLaunch={onLaunch} busy={busy} runState={toolStates?.[tool.Id]} favorite={favorites.has(tool.Id)} onToggleFavorite={onToggleFavorite}/>)}</div></section>)}</>;
}
