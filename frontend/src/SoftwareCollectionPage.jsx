import {memo,useCallback,useDeferredValue,useEffect,useMemo,useRef,useState} from 'react';
import {ArrowUpRight,Check,ChevronDown,Download,Layers3,LoaderCircle,Search,X} from 'lucide-react';
import {invoke} from './bridge';
import collection from '../../assets/software-collection.json';
import logos from '../../assets/software-logos.json';
import './software-collection.css';
import {useScrollMemory} from './hooks/useScrollMemory';

const sourceNames=Object.fromEntries(collection.sources.map(s=>[s.id,s.name]));
const categoryCounts={};
const searchableItems=collection.items.map(item=>{
 categoryCounts[item.category]=(categoryCounts[item.category]||0)+1;
 return {item,text:`${item.name} ${item.aliases.join(' ')} ${item.description} ${item.category}`.toLowerCase()};
});
const buttonLabel=link=>link.type==='share'?'网盘下载':link.type==='file'?'下载文件':'访问网站';
const host=link=>new URL(link.url).hostname.replace(/^www\./,'');
const sourceLabels={official:'官网',cloud:'网盘','third-party':'第三方来源',unverified:'来源待核对'};
function LinkSource({link}){
 const review=link.verification;
 return <span className="software-link-info"><span className="software-link-host" title={link.url}>{host(link)}</span><span className="software-link-meta"><strong>{sourceLabels[link.sourceType]||'来源待核对'}</strong><span title={review?.note}>{review?.status==='checked'?`入口已核对 · ${review.checkedAt}`:review?.status==='unavailable'?`入口暂不可用 · ${review.checkedAt}`:'待核对'}</span></span></span>;
}

const SoftwareCard=memo(function SoftwareCard({item,opening,onOpen}){
 const [logoFailed,setLogoFailed]=useState(false);
 const primary=item.links[0];
 return <article className="software-card">
  <div className="software-card-heading"><span className="software-logo-frame" style={{background:logos.items[item.id]?.background||'#fff'}}>{logoFailed||!logos.items[item.id]?.path?<Layers3 size={23} aria-hidden="true"/>:<img className="software-logo" src={`./${logos.items[item.id].path}`} width="38" height="38" alt={`${item.name} Logo`} loading="lazy" decoding="async" draggable="false" onError={()=>setLogoFailed(true)}/>}</span><div><h3>{item.name}</h3><span className="software-category">{item.category}</span></div></div>
  <p className="software-description">{item.description}</p>
  <div className="software-sources">{item.sources.map(s=><span key={s}>{sourceNames[s]}</span>)}</div>
  <div className="software-primary-link"><LinkSource link={primary}/><button className="secondary" disabled={!!opening} onClick={()=>onOpen(item,primary)}>{opening===`${item.id}/${primary.id}`?<LoaderCircle size={13} className="query-spinner"/>:<ArrowUpRight size={13}/>}<span>{buttonLabel(primary)}</span></button></div>
  {item.links.length>1?<details className="software-alternatives"><summary>其他入口 · {item.links.length-1}<ChevronDown size={12}/></summary><div>{item.links.slice(1).map(link=><div className="software-alternative" key={link.id}><LinkSource link={link}/><button disabled={!!opening} title={link.url} onClick={()=>onOpen(item,link)}>{buttonLabel(link)}<ArrowUpRight size={12}/></button></div>)}</div></details>:null}
 </article>;
});

export function SoftwareCollectionPage({view,onViewChange,onNotice,onError,onRecovered,scrollMemory}){
 const {category,query}=view;
 const setCategory=category=>onViewChange(current=>({...current,category}));
 const setQuery=query=>onViewChange(current=>({...current,query}));
 const [opening,setOpening]=useState('');
 const openingGuard=useRef(false),live=useRef(false);
 useEffect(()=>{live.current=true;return()=>{live.current=false;};},[]);
 const deferredQuery=useDeferredValue(query);
 const scroll=useScrollMemory(scrollMemory,`software/${category}/${deferredQuery}`);
 const filtered=useMemo(()=>{
  const terms=deferredQuery.trim().toLowerCase().split(/\s+/).filter(Boolean);
  return searchableItems.filter(({item,text})=>(category==='全部软件'||item.category===category)&&terms.every(term=>text.includes(term))).map(({item})=>item);
 },[category,deferredQuery]);
 const openLink=useCallback(async(item,link)=>{
  if(openingGuard.current)return;
  openingGuard.current=true;
  setOpening(`${item.id}/${link.id}`);
  try{await invoke('openSoftwareLink',{softwareId:item.id,linkId:link.id});if(live.current){onRecovered();onNotice(`已在浏览器打开：${item.name}`);}}
  catch(error){if(live.current)onError(error.message,{title:'下载入口未能打开',kind:'software',actionLabel:'重试打开',action:()=>openLink(item,link)});}
  finally{openingGuard.current=false;if(live.current)setOpening('');}
 },[onNotice,onError,onRecovered]);
 return <section className="software-collection page-enter" aria-label="软件合集目录">
  <div className="software-banner"><span className="software-banner-icon"><Layers3 size={25} strokeWidth={1.65} aria-hidden="true"/></span><div><h2>常用软件，一处找到</h2><p>按用途查找，在浏览器中打开下载入口。</p></div><div className="software-total"><strong>{collection.items.length}</strong><span>款软件 · {collection.categories.length} 个分类</span></div></div>
  <div className="software-surface">
   <div className="software-toolbar"><div className="software-search"><Search size={17}/><input aria-label="搜索软件" placeholder="搜索软件名称、用途…" value={query} onChange={e=>setQuery(e.target.value)}/>{query?<button aria-label="清除软件搜索" onClick={()=>setQuery('')}><X size={15}/></button>:null}</div><span className="software-result-count" aria-live="polite">{filtered.length} 款软件</span></div>
   <div className="software-categories" role="group" aria-label="软件分类">{['全部软件',...collection.categories].map(name=><button key={name} onClick={()=>setCategory(name)} aria-pressed={category===name}>{name}<span aria-hidden="true">{name==='全部软件'?collection.items.length:categoryCounts[name]}</span></button>)}</div>
   <div className="software-results" aria-busy={query!==deferredQuery} {...scroll}>
    {category==='历史显卡驱动'?<p className="software-history-note">以下为来源收录的历史版本，请在下载页确认显卡型号与系统支持情况。</p>:null}
    {filtered.length?<div className="software-grid">{filtered.map(item=><SoftwareCard key={item.id} item={item} opening={opening} onOpen={openLink}/>)}</div>:<div className="software-empty"><Search size={28}/><h3>没有找到匹配的软件</h3><p>试试其他名称，或切换到“全部软件”。</p><button className="secondary" onClick={()=>{setQuery('');setCategory('全部软件');}}>查看全部软件</button></div>}
   </div>
   <footer className="software-footer"><span><Check size={13}/>核对状态仅针对入口，不代表下载文件已验证</span><span><Download size={13}/>目录可离线浏览，下载时需联网</span></footer>
  </div>
 </section>;
}
