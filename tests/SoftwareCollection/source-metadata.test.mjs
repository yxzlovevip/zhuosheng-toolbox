import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
const catalog=JSON.parse(fs.readFileSync(new URL('../../assets/software-collection.json',import.meta.url),'utf8'));
test('each download entry separates its actual source from collection attribution',()=>{
 for(const item of catalog.items)for(const link of item.links){
  assert.ok(['official','cloud','third-party','unverified'].includes(link.sourceType),`${item.name}: missing source type`);
  assert.ok(['pending','checked','unavailable'].includes(link.verification?.status),`${item.name}: missing review status`);
  if(link.verification.status==='pending')assert.equal(link.verification.checkedAt,null,'import date is not a review date');
  else {assert.match(link.verification.checkedAt,/^\d{4}-\d{2}-\d{2}$/);assert.ok(link.verification.evidenceUrl);}
 }
 const item=catalog.items.find(i=>i.name==='Everything');
 assert.equal(item.links[0].sourceType,'official');
 assert.equal(item.links.find(l=>l.url.includes('360tpcdn.com')).sourceType,'third-party');
 assert.equal(catalog.items.find(i=>i.name==='Geek Uninstaller').links.find(l=>l.type==='share').sourceType,'cloud');
});
test('an extension store entry is not the publisher website merely because Microsoft hosts it',()=>{
 const link=catalog.items.find(i=>i.name==='浏览器助手').links[0];
 assert.equal(link.sourceType,'third-party');
 assert.equal(link.verification.status,'pending');
});
test('source classification does not trust URL text, a share label or catalog attribution',async()=>{
 const {describeSoftwareLink}=await import('../../scripts/lib/software-link-metadata.mjs');
 assert.equal(describeSoftwareLink('Test',{url:'https://lanzoul.com.evil.example/a',type:'share'},{}).sourceType,'unverified');
 assert.equal(describeSoftwareLink('Test',{url:'https://example.com/?next=https://7-zip.org',type:'website'},{}).sourceType,'unverified');
 assert.equal(describeSoftwareLink('Test',{url:'https://huajin.lanzoul.com/a',type:'share'},{}).sourceType,'cloud');
 const pending=describeSoftwareLink('Test',{url:'https://example.com/',type:'website'},{});
 assert.deepEqual(pending.verification,{status:'pending',checkedAt:null});
});
