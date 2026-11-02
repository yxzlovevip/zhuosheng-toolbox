import {useCallback,useLayoutEffect,useRef} from 'react';
// Scroll positions belong to the unlocked workspace, not persistent storage.
export function useScrollMemory(memory,key){
 const ref=useRef(null);
 // onScroll records the position before React changes the descendants. Reading
 // scrollTop during cleanup can instead capture the new, shorter list's clamp.
 useLayoutEffect(()=>{const element=ref.current;if(element)element.scrollTop=memory.current.get(key)||0;},[memory,key]);
 const onScroll=useCallback(event=>{memory.current.set(key,event.currentTarget.scrollTop);},[memory,key]);
 return {ref,onScroll};
}
