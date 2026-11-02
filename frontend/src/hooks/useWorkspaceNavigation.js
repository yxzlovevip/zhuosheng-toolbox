import {useCallback,useEffect,useRef} from 'react';
export function useWorkspaceNavigation(category,setCategory,setNotice){
 const previous=useRef(category);
 useEffect(()=>{
  if(previous.current==='软件合集'&&category!=='软件合集')setNotice(current=>current?.kind==='software'?null:current);
  previous.current=category;
 },[category,setNotice]);
 return useCallback(next=>setCategory(next),[setCategory]);
}
