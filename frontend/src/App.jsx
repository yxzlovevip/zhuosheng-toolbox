import {lazy,Suspense,useCallback,useDeferredValue,useEffect,useMemo,useRef,useState} from 'react';
import {Info} from 'lucide-react';
import {invoke} from './bridge';
import {useUpdates} from './useUpdates';
import {useToolStates} from './useToolStates';
import {useFavorites} from './useFavorites';
import {Boxes} from './icons';
import {Login} from './Login';
import {ConfirmDialog,StartupNotice} from './Dialogs';
import {HelpPage,UpdatePage} from './UtilityPages';
import {SystemInfoPage} from './SystemInfoPage';
import {TitleBar,ThemeToggle,Sidebar,WorkspaceHeader,toolCategories} from './components/AppChrome';
import {ToolWorkspace} from './components/ToolWorkspace';
import {Toast,WorkspaceNotice} from './components/Feedback';
import {useNotices} from './hooks/useNotices';
import {useTheme} from './hooks/useTheme';
import {useToolLaunch} from './hooks/useToolLaunch';
import {useToolFileCheck} from './hooks/useToolFileCheck';
import {transitionView} from './motion';
import {useWorkspaceNavigation} from './hooks/useWorkspaceNavigation';
const SoftwareCollectionPage=lazy(()=>import('./SoftwareCollectionPage').then(module=>({default:module.SoftwareCollectionPage})));
const emptyTools=[];
const defaultToolView={query:'',selected:'firewall'};
const defaultSoftwareView={category:'全部软件',query:''};
const defaultSystemView={tab:'overview',filter:''};

export default function App(){
 const [startupAccepted,setStartupAccepted]=useState(false),[startupBusy,setStartupBusy]=useState(false);
 const [version,setVersion]=useState('—'),[ready,setReady]=useState(false),[session,setSession]=useState(null),[category,setCategory]=useState('系统信息'),[confirm,setConfirm]=useState(null),[startupError,setStartupError]=useState('');
 const [toolViews,setToolViews]=useState({}),[softwareView,setSoftwareView]=useState(defaultSoftwareView),[systemView,setSystemView]=useState(defaultSystemView);
 const currentSession=useRef(null),scrollMemory=useRef(new Map());
 const {toast,setToast,notice,setNotice,notifyError,clearNotice,clearNotices}=useNotices();
 const navigate=useWorkspaceNavigation(category,setCategory,setNotice);
 const themeError=useCallback(error=>setNotice({...error,kind:'theme'}),[setNotice]);
 const themeSaved=useCallback(()=>setNotice(current=>current?.kind==='theme'?null:current),[setNotice]);
 const {theme,setTheme,changeTheme}=useTheme({onError:themeError,onSaved:themeSaved});
 const receiveSession=useCallback(data=>{currentSession.current=data;setSession(data);},[]);
 const {query,selected}=toolViews[category]||defaultToolView;
 const deferred=useDeferredValue(query);
 const updateToolView=useCallback(patch=>setToolViews(views=>({...views,[category]:{...(views[category]||defaultToolView),...patch}})),[category]);
 const select=useCallback(id=>updateToolView({selected:id}),[updateToolView]);
 const setQuery=useCallback(query=>updateToolView({query}),[updateToolView]);
 const [sidebarHidden,setSidebarHidden]=useState(()=>{try{return localStorage.getItem('sidebar-hidden')==='true';}catch{return false;}});
 function toggleSidebar(){const hidden=!sidebarHidden;transitionView(()=>setSidebarHidden(hidden));try{localStorage.setItem('sidebar-hidden',String(hidden));}catch{notifyError('侧栏偏好未能保存，本次仍可使用。');}}
 const updates=useUpdates(!!session),update=updates.state;
 const {states:toolStates,error:toolStateError,receive:receiveToolStates,refresh:refreshToolStates}=useToolStates(session);
 const clearLaunchError=useCallback(()=>setNotice(current=>current?.kind==='launch'?null:current),[setNotice]);
 const clearSoftwareError=useCallback(()=>setNotice(current=>current?.kind==='software'?null:current),[setNotice]);
 const launcher=useToolLaunch({currentSession,receive:receiveToolStates,refresh:refreshToolStates,onNotice:setToast,onError:notifyError,onRecovered:clearLaunchError});
 const {reset:resetLaunch}=launcher;
 const {state:toolCheck,check:checkToolFiles,reset:resetToolCheck}=useToolFileCheck(currentSession);
 const busy=startupBusy||launcher.launchingId!==null;
 const [systemStatus,setSystemStatus]=useState(null),[pendingDriverReminder,setPendingDriverReminder]=useState(null);
 useEffect(()=>{
  if(!session){setSystemStatus(null);setPendingDriverReminder(null);return;}
  let active=true;
  invoke('checkNewSystem').then(status=>{if(active){setSystemStatus(status);if(status.ShouldPrompt)setPendingDriverReminder(status);}}).catch(error=>{if(active)setSystemStatus({Message:`系统安装时间读取失败：${error.message}`});});
  return()=>{active=false;};
 },[session]);
 useEffect(()=>{
  if(!session||!pendingDriverReminder||confirm||busy)return;
  const driver=session.tools?.find(tool=>tool.Id==='driver');setPendingDriverReminder(null);
  if(driver)setConfirm({tool:driver,newSystem:pendingDriverReminder});
 },[session,pendingDriverReminder,confirm,busy]);
 useEffect(()=>{let active=true;invoke('bootstrap').then(data=>{if(!active)return;setStartupAccepted(data.startupAccepted===true);setVersion(data.version||'—');setTheme(data.theme||'system');receiveSession(data.unlocked?data:null);setReady(true);}).catch(error=>{if(active){setStartupError(error.message);setReady(true);}});return()=>{active=false;};},[receiveSession,setTheme]);
 const {favorites,toggleFavorite}=useFavorites(notifyError);
 const tools=session?.tools||emptyTools;
 const searchIndex=useMemo(()=>tools.map(tool=>({tool,text:`${tool.Name} ${tool.Summary} ${tool.Category} ${tool.Group||''} ${tool.Purpose} ${(tool.SearchAliases||[]).join(' ')}`.toLowerCase()})),[tools]);
 const filtered=useMemo(()=>{const terms=deferred.trim().toLowerCase().split(/\s+/).filter(Boolean);return searchIndex.filter(({tool,text})=>(category==='全部工具'||(category==='常用工具'?favorites.has(tool.Id):tool.Category===category))&&terms.every(term=>text.includes(term))).map(({tool})=>tool);},[searchIndex,category,deferred,favorites]);
 const tool=filtered.find(item=>item.Id===selected)||filtered[0];
 async function login(password){const data=await invoke('login',{password});receiveSession(data);setCategory('系统信息');}
 const lock=useCallback(async()=>{
  try{
   await invoke('lock');receiveSession(null);setConfirm(null);clearNotices();resetLaunch();
   setToolViews({});setSoftwareView(defaultSoftwareView);setSystemView(defaultSystemView);
   // Replace the map so the outgoing page's cleanup cannot repopulate a new session.
   scrollMemory.current=new Map();resetToolCheck();
  }catch(error){notifyError(error.message);}
 },[receiveSession,clearNotices,resetLaunch,resetToolCheck,notifyError]);
 async function acceptStartup(){if(busy)return;setStartupBusy(true);try{await invoke('acceptStartup');setStartupAccepted(true);}catch(error){notifyError(error.message);}finally{setStartupBusy(false);}}
 const cancelConfirm=useCallback(async()=>{
  if(busy)return;setConfirm(null);const origin=currentSession.current;
  if(confirm?.newSystem)try{await invoke('dismissNewSystem');}catch(error){if(currentSession.current===origin)notifyError(`本次已取消，提醒记录保存失败：${error.message}`);}
 },[confirm,busy,notifyError]);
 async function confirmLaunch(){
  if(!confirm||busy)return;const origin=currentSession.current,request=confirm;
  const started=await launcher.launch(request.tool);if(currentSession.current!==origin)return;setConfirm(null);
  if(started&&request.newSystem)try{await invoke('dismissNewSystem');}catch(error){if(currentSession.current===origin)notifyError(`已启动驱动总裁，但提醒记录保存失败：${error.message}`);}
 }
 function openToolCheck(){clearNotice();setCategory('使用说明');checkToolFiles();}
 function openLogs(){invoke('openLogs').catch(error=>notifyError(error.message,{title:'日志目录未能打开'}));}
 const updateNotice=update.stage==='error'?update.message:update.ready?'更新包已校验，可安装':['downloading','cancelling'].includes(update.stage)?update.message:`发现新版本 ${update.Info?.Version||''}`;
 return <div className="app-frame"><TitleBar/>
  {!ready?<div className="initializing" role="status"><Boxes size={28} aria-hidden="true"/><span>正在加载…</span></div>
   :!startupError&&!startupAccepted?<><StartupNotice onAccept={acceptStartup} onExit={()=>invoke('close')} busy={startupBusy}/>{notice?<div className="startup-feedback"><WorkspaceNotice notice={notice} onDismiss={clearNotice}/></div>:null}</>
   :!session?<><div className="login-theme"><ThemeToggle mode={theme} onChange={changeTheme}/></div>{startupError?<div className="host-error" role="alert"><Info aria-hidden="true"/><p>{startupError}</p><p>请关闭后重新打开工具箱。</p></div>:<Login onLogin={login} version={version}/>}<div className="startup-feedback"><WorkspaceNotice notice={notice} onDismiss={clearNotice}/></div></>
   :<div className="workspace"><Sidebar hidden={sidebarHidden} category={category} onNavigate={navigate} onLock={lock} version={version} hasUpdate={update.State===2}/><main id="main-content" className="main-area" tabIndex={-1}>
    <WorkspaceHeader category={category} count={filtered.length} query={query} onQuery={setQuery} theme={theme} onTheme={changeTheme} sidebarHidden={sidebarHidden} onToggleSidebar={toggleSidebar}/>
    <WorkspaceNotice notice={notice} onDismiss={clearNotice}/>
    {update.State===2&&category!=='软件更新'?<button className="update-banner" onClick={()=>setCategory('软件更新')} aria-label={`查看新版本 ${update.Info.Version}`}><span className="update-dot" aria-hidden="true"/><span>{updateNotice}</span><span>查看更新 →</span></button>:null}
    {toolCategories.includes(category)?<ToolWorkspace key={category} category={category} query={deferred} tools={filtered} tool={tool} launcher={launcher} states={toolStates} stateError={toolStateError} favorites={favorites} onFavorite={toggleFavorite} onSelect={select} onClear={()=>setQuery('')} onAllTools={()=>setCategory('全部工具')} onCheckFiles={openToolCheck} onLogs={openLogs} scrollMemory={scrollMemory}/>
     :category==='软件合集'?<Suspense fallback={<div className="initializing" role="status"><Boxes size={28} aria-hidden="true"/><span>正在加载软件目录…</span></div>}><SoftwareCollectionPage view={softwareView} onViewChange={setSoftwareView} onNotice={setToast} onError={notifyError} onRecovered={clearSoftwareError} scrollMemory={scrollMemory}/></Suspense>
     :category==='系统信息'?<SystemInfoPage session={session} installationStatus={systemStatus} view={systemView} onViewChange={setSystemView}/>
     :category==='使用说明'?<HelpPage toolCheck={toolCheck} onCheckTools={checkToolFiles} onOpenUpdate={()=>setCategory('软件更新')} onLogs={openLogs}/>
     :<UpdatePage version={version} state={update} tools={tools} toolStates={toolStates} toolStateError={toolStateError} launchingId={launcher.launchingId} onCheck={updates.check} onDownload={updates.download} onInstall={updates.install} onCancel={updates.cancel}/>}
   </main></div>}
  {confirm?<ConfirmDialog tool={confirm.tool} onCancel={cancelConfirm} onConfirm={confirmLaunch} busy={busy} newSystem={confirm.newSystem}/>:null}<Toast message={toast}/>
 </div>;
}
