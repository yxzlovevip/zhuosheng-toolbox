import {useEffect,useId,useRef,useState} from 'react';
import {ArrowUpRight,Info} from 'lucide-react';
import {ToolIcon} from './icons';
import {ToolPassword} from './ToolPassword';
import {ToolRunState} from './ToolRunState';
export function ToolDetail({tool,onLaunch,busy,launching,runState,stateError,emptyFavorites,onCheckFiles,onLogs}) {
 const [tab,setTab]=useState('guide');
 const id=useId();
 const content=useRef(null);
 useEffect(()=>{if(runState?.Status==='failed'&&content.current)content.current.scrollTop=0;},[runState?.Status]);
 function keyTabs(event){
  if(!['ArrowLeft','ArrowRight','Home','End'].includes(event.key))return;
  event.preventDefault();const next=event.key==='Home'?'guide':event.key==='End'?'impact':tab==='guide'?'impact':'guide';
  setTab(next);document.getElementById(`${id}-${next}`)?.focus();
 }
 if(!tool)return <aside className="detail-panel empty-detail"><Info/><h2>{emptyFavorites?'选择你常用的工具':'没有找到匹配的工具'}</h2><p>{emptyFavorites?'点击工具卡片上的爱心，添加到常用工具。':'请清除搜索，或切换到其他分类。'}</p></aside>;
 return <aside className="detail-panel" data-category={tool.Category} aria-label="工具详情">
  <div className="detail-heading"><span className="tool-icon large"><ToolIcon tool={tool} size={36}/></span><div><h2>{tool.Name}</h2><p>{tool.Summary}</p></div></div>
  <ToolPassword tool={tool}/>
  <div className="detail-tabs" role="tablist" aria-label="工具说明">{[['guide','使用说明'],['impact','注意事项']].map(([key,label])=><button key={key} id={`${id}-${key}`} role="tab" tabIndex={tab===key?0:-1} aria-selected={tab===key} aria-controls={`${id}-panel`} onKeyDown={keyTabs} onClick={()=>setTab(key)}>{label}</button>)}</div>
  <div ref={content} className="detail-scroll" id={`${id}-panel`} role="tabpanel" aria-labelledby={`${id}-${tab}`} tabIndex={0} key={tool.Id+tab}>
  {runState||stateError?<div className="detail-run-status" role="status" aria-live="polite"><ToolRunState state={runState}/>{runState?.Message?<p className="tool-run-message">{runState.Message}</p>:null}{stateError?<p className="tool-state-error">{stateError}</p>:null}</div>:null}
  <div className="detail-sections">
   {tab==='guide'?<><section><h3>这个工具能做什么</h3><p>{tool.Purpose}</p></section><section><h3>使用前准备</h3><p>{tool.Preparation}</p></section><section><h3>操作步骤</h3><ol>{tool.Steps.map((step,i)=><li key={i}><span>{i+1}</span><p>{step}</p></li>)}</ol></section><section><h3>如何确认结果</h3><p>{tool.Result}</p></section><section><h3>打不开怎么办</h3><p>先查看提示。文件缺失或校验失败请重新安装工具箱；原工具报错时，记录原工具的提示，再检查系统、驱动或设备是否支持。</p></section></>:<><section><h3>影响范围</h3><p>{tool.Impact}</p></section><section><h3>重启要求</h3><p>{tool.Restart}</p></section><section><h3>结果验证</h3><p>{tool.Result}</p></section><section><h3>说明依据</h3><p>{tool.Evidence}</p></section></>}
  </div></div>
  <div className="detail-footer">{runState?.Status==='failed'||runState?.Message?<div className="tool-recovery"><button className="secondary" onClick={onCheckFiles}>检查工具文件</button><button className="secondary" onClick={onLogs}>查看日志</button></div>:null}<div className={`impact-note${tool.NeedsConfirmation?' caution-note':''}`}><Info size={16} aria-hidden="true"/><span>{tool.Id==='win10-optimization'?'打开后原脚本立即执行，会修改系统设置。':tool.NeedsConfirmation?'涉及系统或数据变更，请先阅读注意事项。':'按需使用，操作结果以工具显示为准。'}</span></div><button className="primary launch-button" onClick={()=>onLaunch(tool)} disabled={busy}><span>{launching?'正在打开…':busy?'其他工具正在打开…':runState?.Status==='failed'?'重试打开':tool.Id==='uac'?'打开注册表导入':'打开工具'}</span><ArrowUpRight size={17} aria-hidden="true"/></button><p className="launch-hint">双击卡片或按 Enter 也可打开</p></div>
 </aside>;
}
