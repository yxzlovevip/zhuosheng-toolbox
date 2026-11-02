import {test,expect} from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
const output=path.join(process.env.TEMP,'cz-system-info-qa');fs.mkdirSync(output,{recursive:true});
const field=(Label,Value)=>({Label,Value});
const device=(Id,Name,Fields=[],extra={})=>({Id,Name,Fields,Children:[],...extra});
const section=(Id,Title,Devices=[],extra={})=>({Id,Title,Devices,Error:'',Notice:'',...extra});
const report={CollectedAt:'2026-10-21T15:00:00+08:00',Sections:[
 section('computer','电脑与系统',[device('pc','测试工作站',[field('操作系统','Windows 11 专业版'),field('设备型号','Precision 5860')])]),
 section('cpu','处理器 CPU',[device('cpu0','Intel Core i7',[field('核心数','16'),field('CPU ID','CPU-ABC')])]),
 section('memory','内存',[device('DIMM0','DIMM A',[field('容量','16 GiB'),field('序列号','MEM-001')]),device('DIMM1','DIMM B',[field('容量','16 GiB'),field('序列号','MEM-002')])]),
 section('board','主板',[device('board','测试主板',[field('序列号','BOARD-001')])]),
 section('bios','BIOS 与固件状态',[device('bios','BIOS 1.2',[field('启动模式','UEFI'),field('安全启动 Secure Boot','已启用')])],{Notice:'XMP / EXPO 等厂商设置需要在 BIOS 中查看。'}),
 section('graphics','显卡',[device('gpu0','NVIDIA GeForce RTX 4070',[field('驱动版本','560.94')]),device('gpu1','Intel UHD Graphics',[field('驱动版本','32.0')])]),
 section('disks','磁盘与分区',[device('disk0','磁盘 0 · Samsung SSD',[field('容量','1 TiB')],{CapacityBytes:1099511627776,Children:[device('efi','分区 1 · EFI 系统分区',[field('盘符','无盘符'),field('分区类型','EFI'),field('分区容量','100 MiB')],{CapacityBytes:104857600}),device('c','C: 系统',[field('盘符','C:'),field('文件系统','NTFS'),field('卷容量','500 GiB'),field('卷可用空间','0 MiB')],{CapacityBytes:536870912000,FreeBytes:0})]}),device('disk1','磁盘 1 · Data SSD',[],{CapacityBytes:1000000000000,Children:[]})],{Notice:'无盘符分区仍会列出。'}),
 section('network','网络适配器',[device('lan','Intel Ethernet',[field('MAC 地址','AA-BB-CC-DD-EE-FF')])]),
 section('identifiers','机器码与设备标识',[device('uuid','整机 · 测试工作站',[field('系统 UUID','12345678-1234-1234-1234-123456789012')]),device('m1','内存 · DIMM A',[field('内存序列号','MEM-001')]),device('m2','内存 · DIMM B',[field('内存序列号','MEM-002')])]),
 section('devices','全部设备',[device('usb','USB 测试设备',[field('设备类别','USB'),field('PNP 设备标识','USB\\TEST-001')]),device('audio','Realtek Audio',[field('设备类别','MEDIA')])])
]};
const gpuReport={CollectedAt:'2026-10-21T15:01:00+08:00',Sections:[report.Sections.find(s=>s.Id==='graphics'),section('nvidia','NVIDIA 驱动详情',[device('n0','NVIDIA GeForce RTX 4070',[field('GPU UUID','GPU-1111'),field('显卡序列号','未提供'),field('显存（NVIDIA 驱动）','12 GiB')]),device('n1','NVIDIA 第二块显卡',[field('GPU UUID','GPU-2222')])])]};
test.beforeEach(async({page})=>{
 await page.addInitScript(({report,gpuReport})=>{window.__systemFixture=report;window.__gpuFixture=gpuReport;Object.defineProperty(navigator,'clipboard',{value:{writeText:async text=>{window.__copied=text;}}});},{report,gpuReport});
 await page.route('**/__test/bridge.js',async route=>{const response=await route.fetch();let body=await response.text();body=body.replace('const listeners=[];','let hardwareQuerying=false;const listeners=[];');body=body.replace('switch(m.action){',`switch(m.action){case'getSystemInfo':case'queryGraphicsInfo':if(!loggedIn)throw Error('请先登录。');if(hardwareQuerying)throw Error('信息查询正在进行，请稍候。');hardwareQuerying=true;try{if(window.__hardwareDelay)await new Promise(r=>setTimeout(r,window.__hardwareDelay));if(window.__hardwareFailure)throw Error(window.__hardwareFailure);result=structuredClone(m.action==='getSystemInfo'?window.__systemFixture:window.__gpuFixture);}finally{hardwareQuerying=false;}break;`);await route.fulfill({response,body});});
});
async function login(page){
 await page.goto('/');await page.waitForFunction(()=>document.querySelector('#password')||[...document.querySelectorAll('button')].some(b=>b.textContent.includes('我已了解，进入工具箱')));
 const notice=page.getByRole('button',{name:'我已了解，进入工具箱'});if(await notice.count())await notice.click();
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await expect(page.getByRole('heading',{name:'系统信息',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'系统信息',exact:true}).click();await expect(page.getByRole('heading',{name:'硬件概览',exact:true})).toBeVisible();
}
test('all hardware instances, hidden partitions and machine codes render and copy',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));await login(page);
 await expect(page.getByText('DIMM A',{exact:true})).toBeVisible();await expect(page.getByText('DIMM B',{exact:true})).toBeVisible();
 await page.getByRole('tab',{name:'磁盘分区'}).click();await expect(page.getByText('分区 1 · EFI 系统分区',{exact:true})).toBeVisible();await expect(page.getByText('无盘符',{exact:true})).toBeVisible();await expect(page.getByText('0 MiB',{exact:true})).toBeVisible();
 await expect(page.getByText('磁盘 1 · Data SSD',{exact:true})).toBeVisible();await page.screenshot({path:path.join(output,'partitions.png')});
 await page.getByRole('tab',{name:'机器码查询'}).click();await page.getByRole('button',{name:'复制当前信息',exact:true}).click();
 expect(await page.evaluate(()=>window.__copied)).toContain('12345678-1234-1234-1234-123456789012');expect(await page.evaluate(()=>window.__copied)).toContain('MEM-002');
 await page.getByRole('tab',{name:'全部设备'}).click();await page.getByRole('textbox',{name:'筛选设备'}).fill('USB\\TEST-001');await expect(page.getByText('USB 测试设备',{exact:true})).toBeVisible();await expect(page.getByText('Realtek Audio',{exact:true})).toHaveCount(0);
 await page.getByRole('textbox',{name:'筛选设备'}).fill('不存在的硬件');await expect(page.getByText('没有匹配的设备')).toBeVisible();expect(errors).toEqual([]);
});
test('BIOS and GPU support details are explicit and retain generic adapters',async({page})=>{
 await login(page);await page.getByRole('tab',{name:'BIOS 信息'}).click();await expect(page.getByText('UEFI',{exact:true})).toBeVisible();
 await page.getByRole('tab',{name:'显卡查询'}).click();await expect(page.getByText('Intel UHD Graphics',{exact:true})).toBeVisible();
 await page.evaluate(()=>window.__gpuFixture.Sections[1]={Id:'nvidia',Title:'NVIDIA 驱动详情',Devices:[],Error:'',Notice:'未找到 NVIDIA 查询组件。AMD / Intel 显卡请查看上方通用信息。'});
 await page.getByRole('button',{name:'查询显卡详情',exact:true}).click();await expect(page.getByText(/未找到 NVIDIA 查询组件/)).toBeVisible();await expect(page.getByText('Intel UHD Graphics',{exact:true})).toBeVisible();
 await page.evaluate(gpuReport=>window.__gpuFixture=gpuReport,gpuReport);await page.getByRole('button',{name:'查询显卡详情',exact:true}).click();await expect(page.getByText('GPU-1111',{exact:true})).toBeVisible();await expect(page.getByText('GPU-2222',{exact:true})).toBeVisible();
 await page.screenshot({path:path.join(output,'graphics.png')});
});
test('refresh failure marks retained data stale and retry replaces the error',async({page})=>{
 await login(page);await page.evaluate(()=>window.__hardwareFailure='测试：查询超时');await page.getByRole('button',{name:'刷新信息',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('查询超时');await expect(page.getByRole('alert')).toContainText('上次');await expect(page.getByText('DIMM B',{exact:true})).toBeVisible();
 await page.evaluate(()=>{window.__hardwareFailure='';window.__systemFixture.Sections.find(s=>s.Id==='cpu').Devices[0].Name='更新后的 CPU';});await page.getByRole('button',{name:'刷新信息',exact:true}).click();
 await expect(page.getByText('更新后的 CPU',{exact:true})).toBeVisible();await expect(page.getByRole('alert')).toHaveCount(0);
});
test('partial failure, themes and compact window remain usable',async({page})=>{
 await page.addInitScript(()=>window.__systemFixture.Sections.find(s=>s.Id==='bios').Error='固件读取被拒绝');await login(page);
 await page.setViewportSize({width:1000,height:700});await page.getByRole('tab',{name:'BIOS 信息'}).click();await expect(page.getByRole('alert')).toContainText('固件读取被拒绝');
 await page.getByRole('button',{name:'浅色',exact:true}).click();await expect(page.locator('html')).toHaveAttribute('data-theme','light');
 await page.getByRole('tab',{name:'硬件概览'}).click();await page.screenshot({path:path.join(output,'compact-light.png')});
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 await page.getByRole('button',{name:'深色',exact:true}).click();await page.setViewportSize({width:1380,height:860});await page.waitForTimeout(400);await page.screenshot({path:path.join(output,'overview-dark.png')});
});
test('querying state prevents duplicate refresh and late replies cannot unlock the app',async({page})=>{
 await page.addInitScript(()=>window.__hardwareDelay=800);await login(page);await expect(page.getByRole('button',{name:'刷新信息',exact:true})).toBeDisabled();
 await page.getByRole('button',{name:/锁定/}).click();await expect(page.locator('#password')).toBeVisible();await page.waitForTimeout(1000);
 await expect(page.getByRole('heading',{name:'硬件概览',exact:true})).toHaveCount(0);
});
test('leaving and returning during system and GPU queries recovers without duplicate rejection',async({page})=>{
 await page.addInitScript(()=>window.__hardwareDelay=1000);await login(page);
 await page.getByRole('button',{name:'网络工具',exact:true}).click();await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await expect(page.getByText('DIMM B',{exact:true})).toBeVisible();await expect(page.getByRole('alert')).toHaveCount(0);
 await page.getByRole('tab',{name:'显卡查询'}).click();await page.getByRole('button',{name:'查询显卡详情',exact:true}).click();
 await page.getByRole('button',{name:'网络工具',exact:true}).click();await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await expect(page.getByRole('tab',{name:'显卡查询'})).toHaveAttribute('aria-selected','true');
 await expect(page.getByText('GPU-2222',{exact:true})).toBeVisible();
 await page.getByRole('tab',{name:'硬件概览'}).click();
 await expect(page.getByText('DIMM B',{exact:true})).toBeVisible();await expect(page.getByRole('alert')).toHaveCount(0);
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='queryGraphicsInfo').length)).toBe(1);
});

test('manual refresh finishing after navigation updates the remounted page',async({page})=>{
 await login(page);await expect(page.getByText('Intel Core i7',{exact:true})).toBeVisible();
 await page.evaluate(()=>{window.__hardwareDelay=800;window.__systemFixture.Sections.find(s=>s.Id==='cpu').Devices[0].Name='导航后刷新的 CPU';});
 await page.getByRole('button',{name:'刷新信息',exact:true}).click();await page.getByRole('button',{name:'网络工具',exact:true}).click();await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await expect(page.getByText('导航后刷新的 CPU',{exact:true})).toBeVisible();
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='getSystemInfo').length)).toBe(2);
});
test('refresh failure remains visible after leaving and returning and retry clears it',async({page})=>{
 await login(page);await expect(page.getByText('Intel Core i7',{exact:true})).toBeVisible();
 await page.evaluate(()=>{window.__hardwareDelay=500;window.__hardwareFailure='导航刷新失败';});
 await page.getByRole('button',{name:'刷新信息',exact:true}).click();await page.getByRole('button',{name:'网络工具',exact:true}).click();await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('导航刷新失败');await expect(page.getByText('Intel Core i7',{exact:true})).toBeVisible();
 await page.getByRole('button',{name:'网络工具',exact:true}).click();await page.getByRole('button',{name:'系统信息',exact:true}).click();await expect(page.getByRole('alert')).toContainText('上次');
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='getSystemInfo').length)).toBe(2);
 await page.evaluate(()=>{window.__hardwareDelay=0;window.__hardwareFailure='';});await page.getByRole('button',{name:'刷新信息',exact:true}).click();await expect(page.getByRole('alert')).toHaveCount(0);
});
