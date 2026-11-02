import {test,expect} from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const output=path.join(process.env.TEMP,'cz-improvements-qa');fs.mkdirSync(output,{recursive:true});
const fields=n=>Array.from({length:n},(_,i)=>({Label:`项目 ${i+1}`,Value:`值 ${i+1}`}));
const section=(Id,count,items)=>({Id,Title:{computer:'电脑与系统',cpu:'处理器 CPU',memory:'内存',board:'主板',graphics:'显卡',disks:'磁盘与分区',network:'网络适配器'}[Id],Error:'',Notice:'',Devices:Array.from({length:count},(_,i)=>({Id:Id+i,Name:`测试 ${Id} ${i+1}`,Fields:fields(items),Children:[]}))});
const report={CollectedAt:'2026-10-22T10:00:00+08:00',Sections:[section('computer',1,8),section('cpu',1,4),section('memory',3,8),section('board',1,4),section('graphics',2,8),section('disks',2,6),section('network',1,4)]};
test.beforeEach(async({page})=>{
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.addInitScript(({report})=>{window.__hardwareFixture=report;Object.defineProperty(navigator,'clipboard',{value:{writeText:async text=>{window.__copied=text;}}});},{report});
 await page.route('**/__test/bridge.js',async route=>{const response=await route.fetch();let body=await response.text();body=body.replace('switch(m.action){',`switch(m.action){case'getSystemInfo':if(!loggedIn)throw Error('请先登录。');result=window.__hardwareFixture;break;`);await route.fulfill({response,body});});
});
async function login(page){await page.goto('/');const notice=page.getByRole('button',{name:'我已了解，进入工具箱'});await expect(notice.or(page.locator('#password'))).toBeVisible();if(await notice.count())await notice.click();await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await expect(page.locator('.workspace')).toBeVisible();}

test('system information is first and initial page, repeated visits reuse collected result',async({page})=>{
 await login(page);await expect(page.getByRole('heading',{name:'系统信息',exact:true})).toBeVisible();await expect(page.locator('.sidebar>nav button').first()).toHaveText('系统信息');await expect(page.getByText('测试 cpu 1',{exact:true})).toBeVisible();
 await page.getByRole('button',{name:'网络工具',exact:true}).click();await page.getByRole('button',{name:'系统信息',exact:true}).click();await expect(page.getByText('测试 cpu 1',{exact:true})).toBeVisible();
 expect(await page.evaluate(()=>window.__nativeCalls.filter(x=>x.action==='getSystemInfo').length)).toBe(1);
 await page.getByRole('button',{name:'刷新信息',exact:true}).click();await expect.poll(()=>page.evaluate(()=>window.__nativeCalls.filter(x=>x.action==='getSystemInfo'&&x.args.refresh===true).length)).toBe(1);
});
test('merged maintenance lists original tools and passwords have explicit copy action',async({page})=>{
 await login(page);await page.getByRole('button',{name:'重装维护',exact:true}).click();await expect(page.getByRole('button',{name:'重装系统',exact:true})).toHaveCount(0);await expect(page.locator('.tool-group-title').filter({hasText:'系统重装'})).toBeVisible();
 await expect(page.getByText('天猫一键重装',{exact:true}).first()).toBeVisible();await expect(page.getByText('驱动总裁',{exact:true}).first()).toBeVisible();await expect(page.locator('.tool-password').first()).toBeVisible();
 await page.getByRole('button',{name:'复制 天猫一键重装 工具密码',exact:true}).click();expect(await page.evaluate(()=>window.__copied)).toBe('1788');
 await expect(page.locator('.system-install-status,.hash-line')).toHaveCount(0);await expect(page.locator('.detail-panel')).not.toContainText('SHA-256');await page.screenshot({path:path.join(output,'maintenance-dark.png')});
});
test('double-click launches once, single click only selects, passwords do not launch',async({page})=>{
 await login(page);await page.getByRole('button',{name:'全部工具',exact:true}).click();const card=page.locator('.tool-card').first();await card.click();expect(await page.evaluate(()=>window.__nativeCalls.filter(x=>x.action==='requestLaunch').length)).toBe(0);await card.dblclick();
 await expect.poll(()=>page.evaluate(()=>window.__nativeCalls.filter(x=>x.action==='requestLaunch').length)).toBe(1);await expect(page.locator('.toast')).toContainText('已启动');
});
test('sidebar can be hidden and restored from top, preference survives a reload',async({page})=>{
 await login(page);await page.getByRole('button',{name:'收起侧栏',exact:true}).click();await expect(page.locator('.sidebar')).toBeHidden();await expect(page.getByRole('button',{name:'展开侧栏',exact:true})).toBeVisible();
 await page.reload();await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await expect(page.locator('.sidebar')).toBeHidden();await page.getByRole('button',{name:'展开侧栏',exact:true}).click();await expect(page.locator('.sidebar')).toBeVisible();
});
test('hardware cards pack independently without stretching shorter adjacent sections',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));await login(page);await expect(page.getByText('测试 cpu 1',{exact:true})).toBeVisible();
 const layout=await page.locator('.hardware-overview-grid').evaluate(grid=>{let cpu=grid.querySelector('.section-cpu').getBoundingClientRect(),board=grid.querySelector('.section-board').getBoundingClientRect(),memory=grid.querySelector('.section-memory').getBoundingClientRect();return {gap:board.top-cpu.bottom,cpu:cpu.height,memory:memory.height};});expect(layout.gap).toBeGreaterThanOrEqual(0);expect(layout.gap).toBeLessThan(28);expect(layout.cpu).toBeLessThan(layout.memory/2);
 for(const [width,height] of [[1920,1080],[1366,768],[980,650]]){await page.setViewportSize({width,height});expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:path.join(output,`hardware-${width}.png`)});}
 await page.getByRole('button',{name:'浅色',exact:true}).click();await expect(page.locator('html')).toHaveAttribute('data-theme','light');await page.screenshot({path:path.join(output,'hardware-light.png')});expect(errors).toEqual([]);
});
test('every tool has working original or generated icon and brand and help are updated',async({page})=>{
 await login(page);await expect(page).toHaveTitle('卓盛工具箱');await page.getByRole('button',{name:'全部工具',exact:true}).click();await expect(page.locator('.tool-card')).not.toHaveCount(0);
 await expect.poll(()=>page.locator('.tool-card img').evaluateAll(imgs=>imgs.filter(i=>!i.complete||!i.naturalWidth).length)).toBe(0);expect(await page.locator('.tool-card .tool-icon img').count()).toBe(await page.locator('.tool-card').count());
 await page.getByRole('button',{name:'使用说明',exact:true}).click();await expect(page.locator('.help-surface')).toContainText('双击');await expect(page.locator('.help-list section').count()).resolves.toBeGreaterThanOrEqual(8);
});
