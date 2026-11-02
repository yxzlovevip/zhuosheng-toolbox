import {test} from 'node:test';
import assert from 'node:assert/strict';
import fs from 'node:fs';
import os from 'node:os';
import path from 'node:path';
import {execFileSync} from 'node:child_process';

test('rebuilding open-source entries preserves purpose aliases and unrelated catalog entries',()=>{
 const root=fs.mkdtempSync(path.join(os.tmpdir(),'cz-search-regeneration-'));
 const write=(relative,value)=>{const file=path.join(root,relative);fs.mkdirSync(path.dirname(file),{recursive:true});fs.writeFileSync(file,typeof value==='string'?value:JSON.stringify(value));};
 const releases=JSON.parse(fs.readFileSync(new URL('../../assets/opensource-releases.json',import.meta.url),'utf8'));
 write('assets/opensource-releases.json',releases);
 write('assets/catalog.json',[{Id:'pt-power-rename',Name:'批量改文件名',SearchAliases:['批量改名']},{Id:'custom',Name:'现有工具',SearchAliases:['保留旧用途']}]);
 for(const release of releases)write(`工具/开源工具/${release.Id}/payload.json`,[]);
 write('工具/开源工具/CZOpenSourceTools.exe','inert fixture never executed');
 write('scripts/integrate-opensource-tools.mjs',fs.readFileSync(new URL('../../scripts/integrate-opensource-tools.mjs',import.meta.url),'utf8'));
 execFileSync(process.execPath,[path.join(root,'scripts/integrate-opensource-tools.mjs')],{stdio:'pipe'});
 const generated=JSON.parse(fs.readFileSync(path.join(root,'assets/catalog.json'),'utf8'));
 assert.deepEqual(generated.find(t=>t.Id==='pt-power-rename').SearchAliases,['批量改名']);
 assert.deepEqual(generated.find(t=>t.Id==='custom').SearchAliases,['保留旧用途']);
});
