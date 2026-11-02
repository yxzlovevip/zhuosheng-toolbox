const labels={opening:'正在打开',running:'运行中',exited:'已退出',failed:'启动失败'};
export function ToolRunState({state}){
 const label=labels[state?.Status];
 return label?<span className={`tool-run-state ${state.Status}`}><i aria-hidden="true"/>{label}</span>:null;
}
