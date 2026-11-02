import {flushSync} from 'react-dom';
let activeTransition;
export function transitionView(update){
 if(!document.startViewTransition||matchMedia('(prefers-reduced-motion: reduce)').matches){update();return;}
 activeTransition?.skipTransition();
 activeTransition=document.startViewTransition(()=>flushSync(update));
 activeTransition.finished.catch(()=>{});
}
