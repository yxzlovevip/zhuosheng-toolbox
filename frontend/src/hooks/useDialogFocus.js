import {useEffect,useRef} from 'react';
export function useDialogFocus(initialRef,onEscape){
 const dialogRef=useRef(null),escape=useRef(onEscape);escape.current=onEscape;
 useEffect(()=>{
  const previous=document.activeElement,dialog=dialogRef.current;
  const background=document.querySelector('.workspace');
  const wasInert=background?.inert;if(background)background.inert=true;
  initialRef.current?.focus();
  function keydown(event){
   if(event.key==='Escape'){escape.current?.();return;}
   if(event.key!=='Tab')return;
   const nodes=[...dialog.querySelectorAll('button:not([disabled]),input:not([disabled]),[tabindex="0"]')];
   const first=nodes[0],last=nodes.at(-1);
   if(!nodes.length){event.preventDefault();dialog.focus();return;}
   const activeIsEnabled=nodes.includes(document.activeElement);
   if(event.shiftKey&&(document.activeElement===first||!activeIsEnabled)){event.preventDefault();last.focus();}
   else if(!event.shiftKey&&(document.activeElement===last||!activeIsEnabled)){event.preventDefault();first.focus();}
  }
  document.addEventListener('keydown',keydown,true);
  return()=>{document.removeEventListener('keydown',keydown,true);if(background)background.inert=wasInert;if(previous?.isConnected)previous.focus();else document.getElementById('main-content')?.focus();};
 },[initialRef]);
 return dialogRef;
}
