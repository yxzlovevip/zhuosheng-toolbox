import {useState} from 'react';
import {CompanyLogo} from './components/CompanyLogo';
import {helpSections} from './helpContent';
import {ToolFileCheckPanel} from './ToolFileCheckPanel';
import {ArrowDownToLine,Check,FileText,RefreshCw,ShieldCheck} from 'lucide-react';
export function UpdatePage({version,state,tools=[],toolStates={},toolStateError,launchingId,onCheck,onDownload,onInstall,onCancel}) {
 const available=state?.State===2,busy=state?.busy,downloading=['downloading','cancelling'].includes(state?.stage);
 const running=tools.filter(tool=>['opening','running'].includes(toolStates[tool.Id]?.Status)||tool.Id===launchingId);
 const installBlocked=running.length>0||!!launchingId||!!toolStateError;
 const step=state?.ready?2:downloading?1:0;
 return <div className="utility-surface update-surface page-enter">
  <div className={`update-symbol ${busy?'is-busy':''}`}><RefreshCw size={34}/></div>
  <h2>软件更新</h2><p className="version">当前版本 {version}{available?<span className="new-version">新版本 {state.Info.Version}</span>:null}</p>
  <p className="update-policy">登录后自动检查，每 6 小时检查一次</p>
  <ol className="update-steps" aria-label="更新流程">{['检查新版','下载并校验','安装并重启'].map((label,i)=><li key={label} className={i<=step?'active':''}><span>{i<step?<Check size={13}/>:i+1}</span>{label}</li>)}</ol>
  <div className="update-message" aria-live="polite"><h3>{state?.message||'尚未检查更新'}</h3><p>{state?.notes}</p>{available&&state.Info.Size?<small>更新包 {(state.Info.Size/1048576).toFixed(1)} MB</small>:null}</div>
  {state?.progress!=null?<div className="download-progress"><div className="progress-label"><span>{state.ready?'校验完成':'下载进度'}</span><strong>{state.progress}%</strong></div><div className="progress" role="progressbar" aria-label="更新下载进度" aria-valuenow={state.progress} aria-valuemin={0} aria-valuemax={100}><span style={{width:`${state.progress}%`}}/></div></div>:null}
  {available&&!state.ready?<p className="update-use-hint">下载时可以继续使用工具，安装前请先保存工作并关闭工具。</p>:null}
  {state?.ready&&running.length>0?<section className="update-blockers" aria-label="安装前需要关闭的工具"><h3>安装前请先关闭这些工具</h3><p>请在工具中保存工作并退出，列表会自动更新。</p><ul>{running.map(tool=><li key={tool.Id}>{tool.Name}<span>{tool.Id===launchingId||toolStates[tool.Id]?.Status==='opening'?'正在打开':'运行中'}</span></li>)}</ul></section>:null}
  {state?.ready&&toolStateError?<p className="update-state-error" role="status">暂时无法确认工具是否已退出，正在重新检查。确认后即可安装。</p>:null}
  <div className="utility-actions"><button className="secondary" onClick={onCheck} disabled={busy||(state?.ready&&state?.stage!=='error')} title={state?.ready&&state?.stage!=='error'?'更新包已准备好，安装后可继续检查':undefined}><RefreshCw size={16}/>{state?.stage==='checking'?'正在检查…':'检查更新'}</button>{available&&!state.ready?<button className="primary" onClick={onDownload} disabled={busy}><ArrowDownToLine size={16}/>下载更新</button>:null}{state?.ready?<button className="primary" onClick={onInstall} disabled={busy||installBlocked}>安装并重启</button>:null}{downloading?<button className="secondary" onClick={onCancel} disabled={state.stage==='cancelling'}>{state.stage==='cancelling'?'正在取消…':'取消下载'}</button>:null}</div>
  {state?.lastChecked?<p className="last-checked">上次检查 {new Date(state.lastChecked).toLocaleString('zh-CN',{hour12:false})}</p>:null}
  <div className="update-verification"><ShieldCheck size={16}/>签名校验<span/>文件完整性校验</div>
 </div>;
}
export function HelpPage({onLogs,toolCheck,onCheckTools,onOpenUpdate}) {
 const [active,setActive]=useState(0);
 return <div className="help-surface page-enter"><div className="help-brand-header"><div><h2>使用说明</h2><p className="help-intro">从查看电脑配置到打开维护工具，按实际使用顺序说明。操作前请先阅读右侧工具详情。</p></div><CompanyLogo/></div><ToolFileCheckPanel state={toolCheck} onCheck={onCheckTools} onOpenUpdate={onOpenUpdate}/><div className="help-list">{helpSections.map(([title,text],i)=><section key={title}><button aria-expanded={active===i} onClick={()=>setActive(active===i?-1:i)}><span>{String(i+1).padStart(2,'0')}</span><strong>{title}</strong><span>{active===i?'−':'+'}</span></button>{active===i?<p>{text}</p>:null}</section>)}</div><button className="secondary" onClick={onLogs}><FileText size={16}/>打开日志目录</button></div>;
}
