let sequence=0;
const pending=new Map();
const listeners=new Set();
const native=window.chrome?.webview;
if(native) native.addEventListener('message',({data})=>{
 if(data.event) {for(const fn of listeners) fn(data); return;}
 const request=pending.get(data.id); if(!request)return;
 clearTimeout(request.timeout); pending.delete(data.id);
 data.ok?request.resolve(data.result):request.reject(new Error(data.error||'操作失败。'));
});
export function invoke(action,args={}) {
 if(!native)return Promise.reject(new Error('请通过 Windows 主程序运行。'));
 return new Promise((resolve,reject)=>{const id=++sequence;const timeout=setTimeout(()=>{pending.delete(id);reject(new Error('请求超时，请重试。'));},action==='downloadUpdate'?900000:action==='checkToolFiles'?150000:45000); pending.set(id,{resolve,reject,timeout}); native.postMessage({id,action,args});});
}
export function listen(fn){listeners.add(fn);return()=>listeners.delete(fn);}
