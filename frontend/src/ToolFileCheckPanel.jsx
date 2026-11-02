import {useState} from 'react';
import {Check,RefreshCw} from 'lucide-react';
import './tool-check.css';

const labels={ready:'文件完整',missing:'文件缺失',changed:'文件已变化',unavailable:'无法检查'};
export function ToolFileCheckPanel({state,onCheck,onOpenUpdate}){
 const [showAll,setShowAll]=useState(false);
 const items=state.report?.Items||[],problems=items.filter(item=>item.Status!=='ready');
 const shown=showAll?items:problems;
 return <section className="tool-check-panel" aria-label="工具文件检查" aria-busy={state.checking}>
  <div className="tool-check-heading"><div><h3>工具文件检查</h3><p>检查随附工具是否齐全、是否与当前版本一致，不运行维护程序。</p></div><button className="secondary" onClick={onCheck} disabled={state.checking}><RefreshCw size={16} className={state.checking?'tool-check-spinning':undefined}/>{state.checking?'正在检查…':state.report||state.error?'重新检查':'检查工具文件'}</button></div>
  {state.checking?<p role="status">正在逐项检查，文件较多时可能需要 1–2 分钟。可切换页面，稍后回来查看结果。</p>:null}
  {state.error?<p className="tool-check-error" role="alert">{state.error}</p>:null}
  {state.report?<><div className="tool-check-summary" role="status"><Check size={17}/><strong>{items.length-problems.length} / {items.length} 项文件完整</strong><span>检查于 {new Date(state.report.CheckedAt).toLocaleString('zh-CN',{hour12:false})}</span></div>
   {problems.length?<p className="tool-check-advice">有 {problems.length} 项需要处理。文件缺失或已变化时，请重新安装完整工具箱；可先检查是否有新版本。</p>:<p className="tool-check-advice">随附工具文件齐全，与本版本一致。具体操作结果仍以原工具为准。</p>}
   {shown.length?<ul className="tool-check-results">{shown.map(item=><li key={item.Id}><div><strong>{item.Name}</strong><p>{item.Category}{item.Status!=='ready'?` · ${item.Message}`:''}</p></div><span className="tool-check-status" data-state={item.Status}>{labels[item.Status]||'无法检查'}</span></li>)}</ul>:null}
   <div className="tool-check-actions">{items.length>problems.length?<button className="secondary" onClick={()=>setShowAll(value=>!value)}>{showAll?'只看需处理项目':'查看全部结果'}</button>:null}{problems.length?<button className="secondary" onClick={onOpenUpdate}>前往软件更新</button>:null}</div>
  </>:null}
 </section>;
}
