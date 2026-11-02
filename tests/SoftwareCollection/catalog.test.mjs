import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
const file=new URL('../../assets/software-collection.json',import.meta.url);
test('collection merges both sources with unique names and valid, attributable links',()=>{
 assert.ok(fs.existsSync(file),'merged software collection has not been created');
 const data=JSON.parse(fs.readFileSync(file,'utf8'));
 assert.equal(data.sources.length,2);assert.ok(data.items.length>100);
 assert.equal(new Set(data.items.map(i=>i.id)).size,data.items.length);
 assert.equal(new Set(data.items.map(i=>i.name.toLowerCase())).size,data.items.length);
 for(const item of data.items){
  assert.ok(item.category&&item.description,item.name);
  assert.ok(item.links.length>0,item.name);
  assert.equal(new Set(item.links.map(l=>l.url)).size,item.links.length,item.name);
  for(const link of item.links){const url=new URL(link.url);assert.ok(['http:','https:'].includes(url.protocol));assert.equal(url.username,'');assert.ok(link.sources.length>0);}
 }
 const everything=data.items.filter(i=>i.name==='Everything');assert.equal(everything.length,1);
 assert.equal(new URL(everything[0].links[0].url).hostname,'www.voidtools.com');
 assert.equal(new Set(everything[0].links.flatMap(l=>l.sources)).size,2);
 assert.ok(data.items.some(i=>i.name==='TranslucentTB'));assert.ok(data.items.some(i=>i.name==='DeepSeek'));
 const gigabyte=data.items.find(i=>i.name==='GIGABYTE Control Center');assert.ok(gigabyte);
 assert.ok(gigabyte.links.every(l=>!l.url.includes('huawei')),'incorrect Huawei download retained');
 const riot=data.items.find(i=>i.name==='Riot Client'),valorant=data.items.find(i=>i.name==='无畏契约（国服）');
 assert.ok(riot&&valorant,'Riot international client and Tencent Valorant must remain separate');
 assert.ok(riot.links.every(l=>!l.url.includes('val.qq.com')));
 for(const link of data.items.flatMap(i=>i.links).filter(l=>l.url.includes('share.feijipan.com')))assert.equal(link.type,'share','cloud shares must be visibly labeled');
 assert.ok(data.items.find(i=>i.name==='酷我音乐').links.every(l=>!l.url.includes('kugou.com')),'Kuwo must not download Kugou');
 assert.ok(data.items.find(i=>i.name==='逆水寒').links.every(l=>!l.url.includes('df.qq.com')),'Nishuihan must not download Delta Force');
 for(const item of data.items.filter(i=>/^NVIDIA \d/.test(i.name))){assert.equal(item.category,'历史显卡驱动');assert.ok(!/稳定|推荐|高配|通用/.test(item.name));}
});
