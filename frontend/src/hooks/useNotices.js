import {useCallback,useEffect,useState} from 'react';
export function useNotices(){
 const [toast,setToast]=useState(''),[notice,setNotice]=useState(null);
 useEffect(()=>{if(!toast)return;const timer=setTimeout(()=>setToast(''),4500);return()=>clearTimeout(timer);},[toast]);
 const clearNotice=useCallback(()=>setNotice(null),[]);
 const notifyError=useCallback((message,options={})=>setNotice({message,...options}),[]);
 const clearNotices=useCallback(()=>{setToast('');setNotice(null);},[]);
 return {toast,setToast,notice,setNotice,notifyError,clearNotice,clearNotices};
}
