import {useState} from 'react';
import {ArrowRight,Eye,EyeOff,KeyRound,ShieldCheck} from 'lucide-react';
import {CompanyMark} from './components/CompanyLogo';
export function Login({onLogin,version}) {
 const [password,setPassword]=useState(''),[visible,setVisible]=useState(false),[busy,setBusy]=useState(false),[error,setError]=useState('');
 async function submit(e){e.preventDefault();if(busy)return;setBusy(true);setError('');try{await onLogin(password);setPassword('');}catch(e){setError(e.message);setPassword('');}finally{setBusy(false);}}
 return <div className="login-screen"><div className="login-orbit orbit-one"/><div className="login-orbit orbit-two"/><div className="login-card"><div className="login-brand"><CompanyMark size={44}/><span>卓盛工具箱</span></div><h1>卓盛电脑维护助手</h1><p className="login-subtitle">访问验证</p><form onSubmit={submit}><label htmlFor="password">访问密码</label><div className="password-field"><KeyRound size={17}/><input id="password" autoFocus type={visible?'text':'password'} value={password} onChange={e=>setPassword(e.target.value)} autoComplete="off" maxLength={128} placeholder="输入密码" disabled={busy}/><button type="button" aria-label={visible?'隐藏密码':'显示密码'} onClick={()=>setVisible(v=>!v)}>{visible?<EyeOff size={17}/>:<Eye size={17}/>}</button></div><div className="login-error" role="alert">{error}</div><button className="primary" disabled={busy||!password}><span>{busy?'验证中…':'登录'}</span><ArrowRight size={17}/></button></form><div className="login-bottom"><span><ShieldCheck size={13}/>管理员运行</span><span>内部专用 · {version}</span></div></div></div>;
}
