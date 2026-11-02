import {useCallback,useEffect,useRef,useState} from 'react';
import {invoke} from './bridge';

export function useToolStates(session){
 const [states,setStates]=useState({}),[error,setError]=useState('');
 const activeSession=useRef(null),flight=useRef(null),revision=useRef(0);
 const receive=useCallback(snapshot=>{
  if(!snapshot)return;
  revision.current++;setError('');
  setStates(previous=>{
   const next={};let changed=Object.keys(previous).length!==Object.keys(snapshot).length;
   for(const [id,state] of Object.entries(snapshot)){
    const old=previous[id];
    next[id]=old?.Status===state.Status&&old?.Message===state.Message?old:state;
    if(next[id]!==old)changed=true;
   }
   return changed?next:previous;
  });
 },[]);
 const refresh=useCallback((force=false)=>{
  const origin=activeSession.current;if(!origin)return Promise.resolve();
  if(!force&&flight.current?.origin===origin)return flight.current.promise;
  if(force)revision.current++;
  const request={origin,revision:revision.current};flight.current=request;
  const valid=()=>activeSession.current===origin&&revision.current===request.revision;
  request.promise=invoke('getToolStates').then(snapshot=>{
   if(valid()){receive(snapshot);setError('');}return true;
  }).catch(()=>{if(valid())setError('运行状态暂时无法刷新，将自动重试。');return false;})
   .finally(()=>{if(flight.current===request)flight.current=null;});
  return request.promise;
 },[receive]);
 useEffect(()=>{
  activeSession.current=session;revision.current++;setStates(session?.toolStates||{});setError('');
  if(!session)return;
  let timer,cancelled=false,cycle=0,failures=0;
  async function poll(){
   const current=++cycle,success=await refresh();if(cancelled||current!==cycle)return;
   failures=success?0:Math.min(failures+1,4);
   timer=setTimeout(poll,document.hidden?15000:Math.min(1000*2**failures,15000));
  }
  function visibility(){document.documentElement.dataset.appHidden=String(document.hidden);clearTimeout(timer);cycle++;if(!document.hidden)poll();else timer=setTimeout(poll,15000);}
  poll();document.addEventListener('visibilitychange',visibility);
  return()=>{cancelled=true;cycle++;clearTimeout(timer);document.removeEventListener('visibilitychange',visibility);activeSession.current=null;};
 },[session,refresh]);
 return {states,error,receive,refresh};
}
