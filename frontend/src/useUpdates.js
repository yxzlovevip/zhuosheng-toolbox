import {useCallback,useEffect,useRef,useState} from 'react';
import {invoke,listen} from './bridge';

const initial={message:'尚未检查更新',notes:'登录后自动检查新版。',stage:'idle'};
export function useUpdates(enabled){
 const [state,setState]=useState(initial);
 const active=useRef(false),epoch=useRef(0),operation=useRef(null),current=useRef(initial),cancelRequested=useRef(false);
 const publish=useCallback(next=>{current.current=typeof next==='function'?next(current.current):next;setState(current.current);},[]);
 const run=useCallback(async(kind)=>{
  if(!active.current||operation.current||(kind==='check'&&current.current.ready&&current.current.stage!=='error'))return;
  const generation=epoch.current;
  const valid=()=>active.current&&generation===epoch.current;
  operation.current=kind;cancelRequested.current=false;
  try{
   if(kind==='check'){
    publish(v=>({...v,busy:true,ready:false,progress:null,stage:'checking',message:'正在检查更新…'}));
    const result=await invoke('checkUpdate');if(!valid())return;
    const message=result.State===0?'尚未配置更新服务器':result.State===1?'当前已是最新版本':`发现新版本 ${result.Info.Version}`;
    publish({...result,busy:false,stage:'checked',message,notes:result.State===0?'更新地址尚未配置，请联系维护人员。':result.Info?.Notes||(result.State===2?'可下载新版，下载并校验后再安装。':'已连接更新服务，暂无可用的新版本。'),lastChecked:Date.now()});
   }else if(kind==='download'){
    publish(v=>({...v,busy:true,ready:false,progress:0,stage:'downloading',message:'正在下载更新…',notes:'下载完成后自动校验，安装前请关闭已启动的工具。'}));
    await invoke('downloadUpdate');if(!valid())return;
    publish(v=>({...v,busy:false,ready:true,progress:100,stage:'ready',message:'更新包已校验',notes:'准备就绪。关闭已启动的工具后，点击安装并重启。'}));
   }else{
    publish(v=>({...v,busy:true,stage:'installing',message:'正在启动安装…'}));
    await invoke('installUpdate');
   }
  }catch(error){
   if(!valid())return;
   const cancelled=cancelRequested.current;
   publish(v=>({...v,busy:false,ready:kind==='install'&&v.ready,progress:null,stage:'error',message:cancelled?'下载已取消':kind==='check'?'检查更新失败':kind==='download'?'下载未完成':'安装未开始',notes:cancelled?'可以重新下载更新，现有工具仍可使用。':error.message,...(kind==='check'?{State:null,Info:null}:{})}));
  }finally{if(valid())operation.current=null;}
 },[publish]);
 useEffect(()=>{
  active.current=enabled;epoch.current++;operation.current=null;publish(initial);
  if(!enabled)return;
  run('check');
  const remove=listen(data=>{if(active.current&&operation.current==='download'&&data.event==='updateProgress')publish(v=>({...v,progress:Math.max(0,Math.min(100,Math.round(data.value*100)))}));});
  const timer=setInterval(()=>{if(!current.current.ready&&!operation.current)run('check');},6*60*60*1000);
  return()=>{active.current=false;epoch.current++;operation.current=null;remove();clearInterval(timer);};
 },[enabled,run,publish]);
 const cancel=useCallback(async()=>{
  if(operation.current!=='download'||cancelRequested.current)return;
  const generation=epoch.current;
  cancelRequested.current=true;publish(v=>({...v,stage:'cancelling',message:'正在取消下载…'}));
  try{await invoke('cancelUpdate');}catch(error){
   if(!active.current||generation!==epoch.current||operation.current!=='download')return;
   cancelRequested.current=false;publish(v=>({...v,stage:'downloading',notes:error.message}));
  }
 },[publish]);
 return {state,check:()=>run('check'),download:()=>run('download'),install:()=>run('install'),cancel};
}
