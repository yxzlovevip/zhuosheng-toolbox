import {useCallback,useDeferredValue,useEffect,useMemo,useRef,useState} from 'react';
import {CircuitBoard,Copy,Cpu,Database,HardDrive,Info,Layers3,LoaderCircle,MemoryStick,Monitor,Network,RefreshCw,Search,ShieldCheck,Fingerprint,Usb,X} from 'lucide-react';
import {invoke} from './bridge';
import './system-info.css';

const tabs=[['overview','硬件概览',Layers3],['bios','BIOS 信息',CircuitBoard],['disks','磁盘分区',HardDrive],['identifiers','机器码查询',Fingerprint],['graphics','显卡查询',Monitor],['devices','全部设备',Usb]];
const icons={computer:Monitor,cpu:Cpu,memory:MemoryStick,board:CircuitBoard,bios:ShieldCheck,graphics:Monitor,disks:HardDrive,network:Network,identifiers:Fingerprint,devices:Usb,nvidia:Monitor};
const overviewIds=['computer','cpu','memory','board','graphics','disks','network'];
let pendingHardwareQuery=null;
const completedReports=new WeakMap();
const queryErrors=new WeakMap();
function queryHardware(action,session,force=false){
 // Sidebar navigation can remount this page while its native query is still running.
 // Reuse that request within the same session; different queries wait their turn.
 if(pendingHardwareQuery?.action===action&&pendingHardwareQuery.session===session)return pendingHardwareQuery.promise;
 const cached=completedReports.get(session);
 if(!force&&cached?.[action]){
  const error=queryErrors.get(session)?.[action];
  return error?Promise.reject(error):Promise.resolve(cached[action]);
 }
 const previous=pendingHardwareQuery?.promise;
 const read=()=>invoke(action,{refresh:force&&action==='getSystemInfo'}).then(result=>{
  const cached=completedReports.get(session)||{};completedReports.set(session,{...cached,[action]:result});
  const errors=queryErrors.get(session);if(errors)delete errors[action];
  return result;
 }).catch(error=>{queryErrors.set(session,{...queryErrors.get(session),[action]:error});throw error;});
 const promise=previous?previous.catch(()=>{}).then(read):read();
 const pending={action,session,promise};pendingHardwareQuery=pending;
 const clear=()=>{if(pendingHardwareQuery===pending)pendingHardwareQuery=null;};
 promise.then(clear,clear);return promise;
}
function reportTime(date){return date?new Date(date).toLocaleString('zh-CN',{hour12:false}):'尚未查询';}
function deviceText(device){return [device.Name,...device.Fields.map(f=>`${f.Label}：${f.Value||'未提供'}`),...(device.Children||[]).map(deviceText)].join('\n');}

function DeviceCard({device,onCopy,partitions=false,compact=false}){
 const [open,setOpen]=useState(false);
 const fields=<dl className="hardware-fields">{device.Fields.map((field,index)=><div key={field.Label+index}><dt>{field.Label}</dt><dd>{field.Value||'未提供'}</dd></div>)}</dl>;
 const hasChildren=partitions&&(device.Children||[]).length>0;
 return <article className={`hardware-device${compact?' compact-device':''}${partitions?' disk-device':''}`}>
  <div className="hardware-device-title"><h4>{device.Name||'未命名设备'}</h4><button className="hardware-copy" title="复制设备信息" aria-label={`复制 ${device.Name||'设备'} 信息`} onClick={()=>onCopy(deviceText(device))}><Copy size={14}/></button></div>
  {compact?<><p className="hardware-device-subtitle">{device.Fields.find(f=>f.Label==='设备类别')?.Value||'设备'}<span>{device.Fields.find(f=>f.Label==='状态')?.Value}</span></p><button className="device-disclosure" aria-expanded={open} onClick={()=>setOpen(v=>!v)}>{open?'收起详情':'查看设备标识与驱动'}<span>{open?'−':'+'}</span></button>{open?fields:null}</>:fields}
  {hasChildren?<div className="disk-partitions"><div className="partition-label"><Database size={13}/><span>{device.Children.length} 个分区</span></div><div className="partition-map" aria-hidden="true">{device.Children.map((child,index)=><span key={child.Id+index} title={child.Name} style={{flexGrow:child.CapacityBytes||1}}/>)}</div><div className="partition-cards">{device.Children.map((child,index)=><div className="partition-card" key={child.Id+index}><DeviceCard device={child} onCopy={onCopy}/>{child.FreeBytes!=null&&child.CapacityBytes>0?<div className="partition-usage"><span style={{width:`${Math.min(100,Math.max(0,(1-child.FreeBytes/child.CapacityBytes)*100))}%`}}/><small>{Math.round(Math.min(100,Math.max(0,(1-child.FreeBytes/child.CapacityBytes)*100)))}% 分区容量已占用（按卷可用空间估算）</small></div>:null}</div>)}</div></div>:partitions?<p className="hardware-empty-inline">未读取到此磁盘的分区；可能尚未分区或存储接口未提供信息。</p>:null}
 </article>;
}
function HardwareSection({section,onCopy,partitions=false,compact=false}){
 const Icon=icons[section.Id]||Layers3;
 return <section className={`hardware-section section-${section.Id}`} aria-label={section.Title}>
  <header><span className="hardware-section-icon"><Icon size={18}/></span><h3>{section.Title}</h3><span className="hardware-count">{section.Devices.length} 项</span></header>
  {section.Notice?<p className="hardware-note">{section.Notice}</p>:null}
  {section.Error?<div className="hardware-error" role="alert"><Info size={15}/><span>部分信息未能读取：{section.Error}</span></div>:null}
  {section.Devices.length?<div className={`hardware-devices${partitions?' physical-disks':''}`}>{section.Devices.map((device,index)=><DeviceCard key={device.Id+index} device={device} onCopy={onCopy} partitions={partitions} compact={compact}/>)}</div>:<p className="hardware-empty-inline">{section.Error?'请刷新重试，或确认对应驱动与设备状态。':'当前未检测到设备，或系统没有提供此类信息。'}</p>}
 </section>;
}

function HardwareOverview({sections,onCopy}){
 const byId=id=>sections.find(s=>s.Id===id);
 const computer=byId('computer');
 return <div className="hardware-overview-grid">{computer?<HardwareSection section={computer} onCopy={onCopy}/>:null}{[['cpu','board','disks'],['memory','graphics','network']].map((ids,index)=><div className="overview-column" key={index}>{ids.map(byId).filter(Boolean).map(section=><HardwareSection key={section.Id} section={section} onCopy={onCopy}/>)}</div>)}</div>;
}

export function SystemInfoPage({session,installationStatus,view,onViewChange}){
 const {tab,filter}=view;
 const setTab=tab=>onViewChange(current=>({...current,tab,filter:''}));
 const setFilter=filter=>onViewChange(current=>({...current,filter}));
 const [report,setReport]=useState(()=>completedReports.get(session)?.getSystemInfo||null),[graphics,setGraphics]=useState(()=>completedReports.get(session)?.queryGraphicsInfo||null),[busy,setBusy]=useState(()=>!completedReports.get(session)?.getSystemInfo),[gpuBusy,setGpuBusy]=useState(false),[error,setError]=useState(()=>queryErrors.get(session)?.getSystemInfo?.message||''),[gpuError,setGpuError]=useState(()=>queryErrors.get(session)?.queryGraphicsInfo?.message||''),[copyStatus,setCopyStatus]=useState('');
 const live=useRef(false),inFlight=useRef(false),copyTimer=useRef(null);
 const deferredFilter=useDeferredValue(filter);
 const refresh=useCallback(async(force=true)=>{
  if(inFlight.current)return;inFlight.current=true;setBusy(true);setError('');
  try{const result=await queryHardware('getSystemInfo',session,force);if(live.current){setReport(result);if(force){setGraphics(null);const cache=completedReports.get(session);if(cache)delete cache.queryGraphicsInfo;const errors=queryErrors.get(session);if(errors)delete errors.queryGraphicsInfo;setGpuError('');}}}
  catch(e){if(live.current)setError(e.message);}
  finally{inFlight.current=false;if(live.current)setBusy(false);}
 },[session]);
 useEffect(()=>{
  live.current=true;refresh(false);
  const gpuPending=pendingHardwareQuery;
  if(gpuPending?.session===session&&gpuPending.action==='queryGraphicsInfo'){
   setGpuBusy(true);
   gpuPending.promise.then(result=>{if(live.current){setGraphics(result);setGpuError('');}},error=>{if(live.current)setGpuError(error.message);}).finally(()=>{if(live.current)setGpuBusy(false);});
  }
  return()=>{live.current=false;clearTimeout(copyTimer.current);};
 },[refresh,session]);
 async function queryGraphics(){
  if(inFlight.current)return;inFlight.current=true;setGpuBusy(true);setGpuError('');
  try{const result=await queryHardware('queryGraphicsInfo',session,true);if(live.current)setGraphics(result);}
  catch(e){if(live.current)setGpuError(e.message);}
  finally{inFlight.current=false;if(live.current)setGpuBusy(false);}
 }
 async function copy(text){
  try{await navigator.clipboard.writeText(text);if(live.current)setCopyStatus('已复制到剪贴板');}
  catch{if(live.current)setCopyStatus('复制失败，请选中文本后按 Ctrl+C。');}
  clearTimeout(copyTimer.current);copyTimer.current=setTimeout(()=>{if(live.current)setCopyStatus('');},3500);
 }
 const sections=useMemo(()=>{
  const source=tab==='graphics'&&graphics?graphics:report;if(!source)return [];
  let selected=tab==='overview'?overviewIds.map(id=>source.Sections.find(s=>s.Id===id)).filter(Boolean):source.Sections.filter(s=>tab==='graphics'?['graphics','nvidia'].includes(s.Id):s.Id===tab);
  const q=deferredFilter.trim().toLowerCase();
  if(tab==='devices'&&q)selected=selected.map(s=>({...s,Devices:s.Devices.filter(d=>`${d.Name} ${d.Fields.map(f=>f.Value).join(' ')}`.toLowerCase().includes(q))}));
  return selected;
 },[tab,graphics,report,deferredFilter]);
 const title=tabs.find(([id])=>id===tab)[1];
 const computer=report?.Sections.find(s=>s.Id==='computer')?.Devices[0];
 const count=id=>report?.Sections.find(s=>s.Id===id)?.Devices.length??'—';
 const visibleCount=sections.reduce((sum,s)=>sum+s.Devices.length,0);
 const currentReport=tab==='graphics'&&graphics?graphics:report;
 function copyCurrent(){copy([`系统信息 · ${title}`,`采集时间：${reportTime(currentReport?.CollectedAt)} · 本次开机自动采集一次，可手动刷新`,error?'本次刷新失败，以下为上次查询结果。':'',gpuError&&tab==='graphics'?'显卡详情刷新失败，以下可能为上次结果。':'',...sections.map(s=>[s.Title,s.Notice,s.Error?`读取错误：${s.Error}`:'',...s.Devices.map(deviceText)].filter(Boolean).join('\n'))].filter(Boolean).join('\n\n'));}
 function keyTabs(event,index){
  const delta=event.key==='ArrowRight'?1:event.key==='ArrowLeft'?-1:0;
  if(!delta&&event.key!=='Home'&&event.key!=='End')return;event.preventDefault();
  const next=event.key==='Home'?0:event.key==='End'?tabs.length-1:(index+delta+tabs.length)%tabs.length;
  setTab(tabs[next][0]);setFilter('');document.getElementById(`system-tab-${tabs[next][0]}`)?.focus();
 }
 return <div className="system-info-page page-enter">
  <div className="system-info-banner"><span className="system-info-symbol"><Monitor size={25} strokeWidth={1.5}/></span><div><h2>{computer?.Name||'本机系统信息'}</h2><p>设备配置、磁盘分区与硬件标识，一处查看。</p></div><span className="read-only-badge"><ShieldCheck size={14}/>本机只读查询</span></div>
  <div className="hardware-statistics">{[['cpu','处理器',Cpu],['memory','内存条',MemoryStick],['graphics','显卡',Monitor],['disks','物理磁盘',HardDrive],['devices','全部设备',Usb]].map(([id,label,Icon])=><div key={id}><Icon size={17}/><span>{label}</span><strong>{count(id)}</strong></div>)}</div>
  <div className="system-info-surface">
   <div className="system-info-tabs" role="tablist" aria-label="系统信息分类">{tabs.map(([id,label,Icon],index)=><button id={`system-tab-${id}`} key={id} role="tab" aria-selected={tab===id} aria-controls="system-info-content" tabIndex={tab===id?0:-1} onKeyDown={e=>keyTabs(e,index)} onClick={()=>{setTab(id);setFilter('');}}><Icon size={16}/>{label}</button>)}</div>
   <div className="system-info-toolbar"><div><h2>{title}</h2><p>{busy?'正在读取本机信息…':`采集时间：${reportTime(currentReport?.CollectedAt)} · 本次开机自动采集一次，可手动刷新`}</p></div><div className="system-info-actions"><button className="secondary" disabled={busy||gpuBusy||!report||visibleCount===0} onClick={copyCurrent}><Copy size={14}/>复制当前信息</button><button className="primary" aria-label="刷新信息" disabled={busy||gpuBusy} onClick={()=>refresh(true)}>{busy?<LoaderCircle size={15} className="query-spinner"/>:<RefreshCw size={15}/>}<span>{busy?'正在查询':'刷新信息'}</span></button></div></div>
   <div className="system-info-scroll" id="system-info-content" role="tabpanel" aria-labelledby={`system-tab-${tab}`} aria-busy={busy||gpuBusy}>
    {error?<div className="hardware-error" role="alert"><Info size={17}/><span>{error}{report?' 当前显示上次查询结果，请刷新重试。':' 请点击“刷新信息”重试。'}</span></div>:null}
    {tab==='graphics'?<div className="graphics-query-intro"><div><h3>显卡详情查询</h3><p>查看所有显卡的设备与驱动信息；NVIDIA 还可读取 UUID、显存及当前温度。</p>{graphics?<small>显卡详情查询时间：{reportTime(graphics.CollectedAt)}</small>:null}</div><button className="secondary" disabled={busy||gpuBusy} onClick={queryGraphics}>{gpuBusy?<LoaderCircle size={15} className="query-spinner"/>:<Search size={15}/>}<span>{gpuBusy?'正在查询显卡…':'查询显卡详情'}</span></button></div>:null}
    {tab==='graphics'&&gpuError?<div className="hardware-error" role="alert"><Info size={16}/><span>{gpuError}{graphics?' 当前显示上次显卡查询结果。':' 上方通用显卡信息仍可查看。'}</span></div>:null}
    {tab==='identifiers'?<p className="system-info-description">读取设备原始标识，支持复制。序列号缺失时显示“未提供”，不会生成或修改机器码。</p>:null}
    {tab==='devices'?<div className="device-filter"><Search size={16}/><input aria-label="筛选设备" placeholder="搜索设备名称、类别或设备标识…" value={filter} onChange={e=>setFilter(e.target.value)}/>{filter?<button onClick={()=>setFilter('')} aria-label="清除设备筛选"><X size={14}/></button>:null}<span>{visibleCount} 个设备</span></div>:null}
    {!report&&busy?<div className="hardware-loading" role="status"><LoaderCircle size={26} className="query-spinner"/><p>正在读取本机设备…</p><small>首次查询可能需要几秒钟。</small></div>:null}
    {tab==='overview'&&installationStatus?.Message?<p className="hardware-note">{installationStatus.Message}</p>:null}
    {tab==='devices'&&report&&visibleCount===0&&filter?<div className="hardware-no-results"><Search size={25} aria-hidden="true"/><p>没有匹配的设备</p><button className="secondary" onClick={()=>setFilter('')}>清除筛选</button></div>:report&&!busy&&!gpuBusy&&!sections.length?<div className="hardware-no-results" role="status"><Info size={25} aria-hidden="true"/><h3>暂未读取到这类设备信息</h3><p>系统可能未提供数据。可点击上方“刷新信息”重试。</p></div>:tab==='overview'?<HardwareOverview sections={sections} onCopy={copy}/>:<div className="hardware-section-list">{sections.map(section=><HardwareSection key={section.Id} section={section} onCopy={copy} partitions={tab==='disks'} compact={tab==='devices'}/>)}</div>}
    {tab==='graphics'&&!graphics&&!busy?<p className="hardware-note graphics-query-hint">点击“查询显卡详情”读取 NVIDIA UUID 等信息。AMD / Intel 当前使用通用设备信息，不生成替代 UUID。</p>:null}
   </div>
   <footer className="system-info-footer"><span><ShieldCheck size={13}/>信息在本机读取与展示</span><span role="status">{copyStatus||'磁盘空间、网络和温度为采集时快照 · 需要最新状态请刷新'}</span></footer>
  </div>
 </div>;
}
