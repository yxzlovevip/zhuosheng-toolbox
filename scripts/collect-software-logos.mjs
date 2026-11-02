import fs from 'node:fs';
import path from 'node:path';
import {createHash} from 'node:crypto';
import {createRequire} from 'node:module';
const require=createRequire(import.meta.url);
const sharp=require(process.env.CZ_SHARP_MODULE||'C:/Users/yxzlo/.cache/codex-runtimes/codex-primary-runtime/dependencies/node/node_modules/sharp');
const root=path.resolve(import.meta.dirname,'..'),catalog=JSON.parse(fs.readFileSync(path.join(root,'assets/software-collection.json')));
const output=path.join(root,'frontend/public/software-logos'),cache=path.join(root,'.build/software-logo-cache');
fs.mkdirSync(output,{recursive:true});fs.mkdirSync(cache,{recursive:true});
const manifestFile=path.join(root,'assets/software-logos.json');
const manifest=fs.existsSync(manifestFile)?JSON.parse(fs.readFileSync(manifestFile)):{updatedAt:'2026-10-22',items:{}};
const overridesFile=path.join(root,'assets/software-logo-sources.json');
const overrides=fs.existsSync(overridesFile)?JSON.parse(fs.readFileSync(overridesFile)):{};
const sourceHtml=fs.readFileSync(path.join(root,'.build/software-collection-sources/local.html'),'utf8');
const originalIcons=new Map();
for(const m of sourceHtml.matchAll(/\{\s*name:\s*"([^"]+)"[\s\S]*?icon:\s*"([^"]+)"[\s\S]*?category:\s*"([^"]+)"\s*\}/g))originalIcons.set(m[1],m[2]);
const inflight=new Map();
async function download(url){
 if(inflight.has(url))return inflight.get(url);
 const promise=(async()=>{
  const filename=path.join(cache,createHash('sha256').update(url).digest('hex'));
  if(fs.existsSync(filename))return fs.readFileSync(filename);
  const response=await fetch(url,{signal:AbortSignal.timeout(9000),headers:{'User-Agent':'Mozilla/5.0','Accept':'image/avif,image/webp,image/svg+xml,image/*,text/html;q=0.5'}});
  if(!response.ok)throw Error('HTTP '+response.status);
  if(Number(response.headers.get('content-length'))>4000000)throw Error('asset too large');
  const buffer=Buffer.from(await response.arrayBuffer());if(buffer.length>4000000)throw Error('asset too large');
  fs.writeFileSync(filename,buffer);return buffer;
 })();inflight.set(url,promise);return promise;
}
function icoImage(bytes){
 if(bytes.length<22||bytes.readUInt32LE(0)!==65536)return bytes;
 const entries=Array.from({length:bytes.readUInt16LE(4)},(_,i)=>{const p=6+i*16;return {width:bytes[p]||256,height:bytes[p+1]||256,size:bytes.readUInt32LE(p+8),offset:bytes.readUInt32LE(p+12)};}).sort((a,b)=>b.width-a.width);
 for(const e of entries){
  const image=bytes.subarray(e.offset,e.offset+e.size);
  if(image.subarray(0,8).toString('hex')==='89504e470d0a1a0a')return image;
  if(image.length<40)continue;
  const header=image.readUInt32LE(0),width=image.readInt32LE(4),height=Math.abs(image.readInt32LE(8))/2,bits=image.readUInt16LE(14);
  if(header!==40||width<1||height<1||width>256||height>256||![24,32].includes(bits))continue;
  const stride=Math.ceil(width*bits/32)*4,pixels=Buffer.alloc(width*height*4);let hasAlpha=false;
  for(let y=0;y<height;y++)for(let x=0;x<width;x++){const p=40+(height-y-1)*stride+x*(bits/8),q=(y*width+x)*4;if(p+bits/8>image.length)continue;pixels[q]=image[p+2];pixels[q+1]=image[p+1];pixels[q+2]=image[p];pixels[q+3]=bits===32?image[p+3]:255;if(pixels[q+3]>0)hasAlpha=true;}
  if(!hasAlpha)for(let i=3;i<pixels.length;i+=4)pixels[i]=255;
  return {raw:pixels,width,height};
 }
 throw Error('unsupported ICO');
}
async function raster(bytes){
 const decoded=icoImage(bytes);
 const pipeline=decoded.raw?sharp(decoded.raw,{raw:{width:decoded.width,height:decoded.height,channels:4}}):sharp(decoded,{limitInputPixels:16000000});
 const meta=await pipeline.metadata();if(meta.width<8||meta.height<8)throw Error('not a usable icon');
 return pipeline.resize(128,128,{fit:'contain',background:'#00000000'}).png().toBuffer();
}
function primarySites(item){
 const urls=item.links.filter(l=>l.type==='website').map(l=>l.url).filter(url=>!/(lanzou|feijipan|download\.microsoft|apps\.microsoft|github\.com)/i.test(new URL(url).hostname));
 return [...new Set(urls.map(url=>new URL(url).origin))].slice(0,2);
}
async function collect(item){
 const existing=manifest.items[item.id];if(existing&&fs.existsSync(path.join(root,'frontend/public',existing.path))&&!process.argv.includes('--refresh'))return;
 const preferred=overrides[item.name]||{};
 const urls=[...(preferred.images||[])];
 if(!preferred.ignoreOriginal)for(const name of [item.name,...item.aliases])if(originalIcons.has(name))urls.push(originalIcons.get(name));
 if(preferred.bundled){const png=await raster(fs.readFileSync(path.join(root,'frontend/public',preferred.bundled)));return save(item,png,'bundled:'+preferred.bundled);}
 for(const url of [...new Set(urls)])try{return save(item,await raster(await download(url)),url);}catch{}
 const sites=[...(preferred.sites||[]),...primarySites(item)];
 for(const site of [...new Set(sites)]){
  try{const html=(await download(site)).toString('utf8');
   for(const tag of html.matchAll(/<link\b[^>]*>/gi)){const rel=tag[0].match(/\brel\s*=\s*["']([^"']+)["']/i)?.[1],href=tag[0].match(/\bhref\s*=\s*["']([^"']+)["']/i)?.[1];if(rel&&href&&/(^|\s)(icon|apple-touch-icon)(\s|$)/i.test(rel))urls.push(new URL(href.replace(/&amp;/g,'&'),site).href);}
  }catch{}
  urls.push(new URL('/favicon.ico',site).href,new URL('/favicon.png',site).href);
 }
 for(const url of [...new Set(urls)])try{const png=await raster(await download(url));return save(item,png,url);}catch{}
 console.log('MISSING '+item.name);
}
function save(item,png,source){const relative='software-logos/'+item.id+'.png';fs.writeFileSync(path.join(root,'frontend/public',relative),png);manifest.items[item.id]={path:relative,source,sha256:createHash('sha256').update(png).digest('hex'),width:128,height:128,...(overrides[item.name]?.background?{background:overrides[item.name].background}:{})};console.log('OK '+item.name);}
const work=catalog.items.filter(item=>!process.env.CZ_LOGO_NAMES||process.env.CZ_LOGO_NAMES.split('|').includes(item.name));let cursor=0;
await Promise.all(Array.from({length:12},async()=>{while(cursor<work.length){const item=work[cursor++];try{await collect(item);}catch(e){console.log('ERROR '+item.name+': '+e.message);}fs.writeFileSync(manifestFile,JSON.stringify(manifest,null,2)+'\n');}}));
const missing=catalog.items.filter(item=>!manifest.items[item.id]);fs.writeFileSync(path.join(cache,'missing.json'),JSON.stringify(missing.map(i=>({name:i.name,id:i.id,url:i.links[0].url})),null,2));
console.log(JSON.stringify({logos:Object.keys(manifest.items).length,total:catalog.items.length,missing:missing.map(i=>i.name)}));
