import {useCallback,useState} from 'react';

const storageKey='favorite-tools';
function readFavorites(){
 try{
  const saved=JSON.parse(localStorage.getItem(storageKey)||'[]');
  return new Set(Array.isArray(saved)?saved.filter(id=>typeof id==='string'):[]);
 }catch{return new Set();}
}
export function useFavorites(onNotice){
 const [favorites,setFavorites]=useState(readFavorites);
 const toggleFavorite=useCallback(id=>{
  const next=new Set(favorites);
  if(next.has(id))next.delete(id);else next.add(id);
  try{localStorage.setItem(storageKey,JSON.stringify([...next]));setFavorites(next);}
  catch{onNotice('常用工具保存失败，请重试。');}
 },[favorites,onNotice]);
 return {favorites,toggleFavorite};
}
