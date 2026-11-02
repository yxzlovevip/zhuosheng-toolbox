import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import {createHash} from 'node:crypto';
const root=new URL('../../',import.meta.url);
test('each collection item has an attributable bundled logo with matching bytes',()=>{
 const file=new URL('assets/software-logos.json',root);
 assert.ok(fs.existsSync(file),'local logo manifest is missing');
 const logos=JSON.parse(fs.readFileSync(file,'utf8'));
 const collection=JSON.parse(fs.readFileSync(new URL('assets/software-collection.json',root),'utf8'));
 for(const item of collection.items){
  const logo=logos.items[item.id];assert.ok(logo,'logo missing: '+item.name);
  assert.match(logo.path,/^software-logos\/[a-z0-9-]+\.png$/);
  const bytes=fs.readFileSync(new URL('frontend/public/'+logo.path,root));
  assert.equal(bytes.subarray(0,8).toString('hex'),'89504e470d0a1a0a',item.name);
  assert.equal(createHash('sha256').update(bytes).digest('hex'),logo.sha256,item.name);
  assert.ok(logo.width>=32&&logo.height>=32,item.name);
  assert.ok(logo.source.startsWith('https://')||logo.source.startsWith('http://')||logo.source.startsWith('bundled:'),item.name);
 }
 const source=name=>logos.items[collection.items.find(i=>i.name===name).id].source;
 assert.ok(!source('Wallpaper Engine').includes('/favicon.ico'),'Wallpaper Engine must use its own app image');
 assert.ok(!source('GPU-Z').includes('favicon'),'GPU-Z must use its product icon');
 assert.ok(!source('FurMark').includes('geeks3d-icon'),'FurMark must use its product icon');
 assert.ok(!source('DDU').includes('ddu_17.0.5.0_0.png'),'DDU must not use the empty banner');
 const afterburner=logos.items[collection.items.find(i=>i.name==='MSI Afterburner').id];
 assert.equal(afterburner.background,'#202a3b','white Afterburner logo requires a dark background');
});
