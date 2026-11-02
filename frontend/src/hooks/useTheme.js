import {useCallback,useEffect,useRef,useState} from 'react';
import {invoke} from '../bridge';
import {transitionView} from '../motion';
export function useTheme({onError,onSaved}){
 const [theme,setTheme]=useState('system');const request=useRef(0);
 useEffect(()=>{
  const media=matchMedia('(prefers-color-scheme: dark)');
  const apply=()=>{document.documentElement.dataset.theme=theme==='system'?(media.matches?'dark':'light'):theme;};
  apply();media.addEventListener('change',apply);return()=>media.removeEventListener('change',apply);
 },[theme]);
 const changeTheme=useCallback(async mode=>{
  const current=++request.current;transitionView(()=>setTheme(mode));
  try{await invoke('theme',{mode});if(current===request.current)onSaved();}
  catch(error){if(current===request.current)onError({title:'主题偏好尚未保存',message:`当前主题已预览。${error.message}，下次启动可能恢复原设置。`,actionLabel:'重新保存',action:()=>changeTheme(mode)});}
 },[onError,onSaved]);
 return {theme,setTheme,changeTheme};
}
