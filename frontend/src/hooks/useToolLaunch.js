import {useCallback,useRef,useState} from 'react';
import {invoke} from '../bridge';
export function useToolLaunch({currentSession,receive,refresh,onNotice,onError,onRecovered}){
 const [launchingId,setLaunchingId]=useState(null);const guard=useRef(false),cooldown=useRef({id:null,until:0});
 const reset=useCallback(()=>{guard.current=false;cooldown.current={id:null,until:0};setLaunchingId(null);},[]);
 const launch=useCallback(async tool=>{
  const origin=currentSession.current;if(!origin||guard.current||(cooldown.current.id===tool.Id&&Date.now()<cooldown.current.until))return false;
  guard.current=true;setLaunchingId(tool.Id);
  try{const result=await invoke('requestLaunch',{toolId:tool.Id});if(currentSession.current===origin){receive(result.toolStates);onRecovered();onNotice(`${result.toolStates?.[tool.Id]?.Status==='exited'?'已退出':'已启动'}：${tool.Name}`);return true;}}
  catch(error){if(currentSession.current===origin){onError(error.message,{title:`${tool.Name}未能打开`,kind:'launch'});await refresh(true);}}
  finally{if(currentSession.current===origin){guard.current=false;cooldown.current={id:tool.Id,until:Date.now()+600};setLaunchingId(null);}}
  return false;
 },[currentSession,receive,refresh,onNotice,onError,onRecovered]);
 return {launchingId,launch,reset};
}
