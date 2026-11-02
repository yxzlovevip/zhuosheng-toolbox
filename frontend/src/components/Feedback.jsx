import {Info,TriangleAlert,X} from 'lucide-react';
export function Toast({message}){
 return message?<div className="toast" role="status"><Info size={17} aria-hidden="true"/><span>{message}</span></div>:null;
}
export function WorkspaceNotice({notice,onDismiss}){
 if(!notice)return null;
 return <div className="workspace-notice" role="alert"><TriangleAlert size={18} aria-hidden="true"/><div><strong>{notice.title||'操作未完成'}</strong><p tabIndex={notice.message.length>160?0:undefined}>{notice.message}</p></div>{notice.action?<button className="secondary" onClick={notice.action}>{notice.actionLabel||'重试'}</button>:null}<button className="notice-dismiss" onClick={onDismiss} aria-label="关闭提示"><X size={17} aria-hidden="true"/></button></div>;
}
