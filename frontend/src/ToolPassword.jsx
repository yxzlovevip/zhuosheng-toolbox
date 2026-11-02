import {useEffect,useRef,useState} from 'react';
import {Check,Copy,KeyRound} from 'lucide-react';

export function ToolPassword({tool,compact=false}) {
 const [copied,setCopied]=useState(false),[error,setError]=useState(false);
 const timer=useRef(null);
 useEffect(()=>()=>clearTimeout(timer.current),[]);
 if(!tool.ToolPassword)return null;
 async function copy(event){
  event.stopPropagation();
  try{await navigator.clipboard.writeText(tool.ToolPassword);setCopied(true);setError(false);}
  catch{setError(true);}
  clearTimeout(timer.current);timer.current=setTimeout(()=>{setCopied(false);setError(false);},1800);
 }
 return <div className={`tool-password${compact?' compact-password':''}`} onClick={e=>e.stopPropagation()} onDoubleClick={e=>e.stopPropagation()}>
  <KeyRound size={14}/><span>工具密码</span><code>{tool.ToolPassword}</code>
  <button type="button" aria-label={`复制 ${tool.Name} 工具密码${compact?'':'（详情）'}`} title={error?'复制失败，可选中密码按 Ctrl+C':copied?'已复制':'复制密码'} onClick={copy}>{copied?<Check size={14}/>:<Copy size={14}/>}<span>{error?'复制失败':copied?'已复制':'复制'}</span></button>
 </div>;
}
