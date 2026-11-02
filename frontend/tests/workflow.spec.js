import {test,expect} from '@playwright/test';

const hardware={CollectedAt:'2026-10-22T16:00:00+08:00',Sections:[
 {Id:'computer',Title:'电脑与系统',Devices:[{Id:'pc',Name:'流程测试电脑',Fields:[],Children:[]}]},
 {Id:'devices',Title:'全部设备',Devices:[{Id:'usb',Name:'USB 测试设备',Fields:[{Label:'设备类别',Value:'USB'}],Children:[]}]}
]};
async function login(page){
 await page.goto('/');await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await unlock(page);
}
async function unlock(page){
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await expect(page.locator('.workspace')).toBeVisible();
}
test.beforeEach(async({page})=>{
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.addInitScript(hardware=>{window.__hardwareFixture=hardware;},hardware);
});

test('tool categories retain independent search and selection',async({page})=>{
 await login(page);await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await page.getByRole('textbox',{name:'搜索工具'}).fill('防火墙');
 await expect(page.locator('.tool-card')).not.toHaveCount(0);
 const selected=await page.locator('.tool-card.selected .card-select').getAttribute('aria-label');
 await page.getByRole('button',{name:'网络工具',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'搜索工具'})).toHaveValue('');
 await expect(page.locator('.tool-card')).not.toHaveCount(0);
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'搜索工具'})).toHaveValue('防火墙');
 await expect(page.locator('.tool-card.selected .card-select')).toHaveAttribute('aria-label',selected);
});

test('software filters survive navigation and reset after locking',async({page})=>{
 await login(page);await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await page.getByRole('button',{name:'浏览器',exact:true}).click();
 await page.getByRole('textbox',{name:'搜索软件'}).fill('Chrome');
 await expect(page.locator('.software-card')).not.toHaveCount(0);
 await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'搜索软件'})).toHaveValue('Chrome');
 await expect(page.getByRole('button',{name:'浏览器',exact:true})).toHaveAttribute('aria-pressed','true');
 await page.getByRole('button',{name:'锁定',exact:true}).click();await unlock(page);
 await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'搜索软件'})).toHaveValue('');
 await expect(page.getByRole('button',{name:'全部软件',exact:true})).toHaveAttribute('aria-pressed','true');
});

test('hardware tab and device search survive navigation without another query',async({page})=>{
 await login(page);await page.getByRole('tab',{name:'全部设备',exact:true}).click();
 await page.getByRole('textbox',{name:'筛选设备'}).fill('USB');
 await expect(page.getByText('USB 测试设备',{exact:true})).toBeVisible();
 await page.getByRole('button',{name:'网络工具',exact:true}).click();
 await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await expect(page.getByRole('tab',{name:'全部设备',exact:true})).toHaveAttribute('aria-selected','true');
 await expect(page.getByRole('textbox',{name:'筛选设备'})).toHaveValue('USB');
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='getSystemInfo').length)).toBe(1);
});

test('update banner distinguishes discovery, downloading and verified package',async({page})=>{
 await page.addInitScript(()=>{window.__updateFixture={State:2,Info:{Version:'9.0.0',Size:100,Notes:'新版说明'}};});
 await login(page);await expect(page.locator('.update-banner')).toContainText('发现新版本 9.0.0');
 await page.locator('.update-banner').click();await page.getByRole('button',{name:'下载更新',exact:true}).click();
 await expect.poll(()=>page.evaluate(()=>typeof window.__finishDownload)).toBe('function');
 await page.getByRole('button',{name:'网络工具',exact:true}).click();
 await expect(page.locator('.update-banner')).toContainText('正在下载更新');
 await page.evaluate(()=>window.__finishDownload());
 await expect(page.locator('.update-banner')).toContainText('更新包已校验，可安装');
 await page.locator('.update-banner').click();
 await expect(page.getByRole('button',{name:'检查更新',exact:true})).toBeDisabled();
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeEnabled();
});

test('locking suppresses a late launch result and clears its busy state',async({page})=>{
 await page.route('**/__test/bridge.js',async route=>{
  const response=await route.fetch();let body=await response.text();
  body=body.replace("case'requestLaunch':", "case'requestLaunch':await new Promise(r=>window.__releaseLaunch=r);");
  await route.fulfill({response,body});
 });
 await login(page);await page.getByRole('button',{name:'网络工具',exact:true}).click();
 await page.locator('.launch-button').click();
 await expect.poll(()=>page.evaluate(()=>typeof window.__releaseLaunch)).toBe('function');
 await page.getByRole('button',{name:'锁定',exact:true}).click();await unlock(page);
 await page.getByRole('button',{name:'网络工具',exact:true}).click();
 await expect(page.locator('.launch-button')).toBeEnabled();
 await page.evaluate(()=>window.__releaseLaunch());
 await expect.poll(()=>page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='requestLaunch').length)).toBe(1);
 await expect(page.locator('.toast')).toHaveCount(0);
});

test('software page is loaded only when first opened and reused on return',async({page})=>{
 const chunks=[];page.on('request',req=>{if(/SoftwareCollectionPage[^/]*\.js/.test(req.url()))chunks.push(req.url());});
 await login(page);expect(chunks).toHaveLength(0);
 await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'搜索软件'})).toBeVisible();expect(chunks).toHaveLength(1);
 await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'搜索软件'})).toBeVisible();expect(chunks).toHaveLength(1);
});

test('an unusable update package can be checked and downloaded again',async({page})=>{
 await page.addInitScript(()=>{window.__updateFixture={State:2,Info:{Version:'9.0.0',Size:100,Notes:'新版说明'}};});
 await page.route('**/__test/bridge.js',async route=>{
  const response=await route.fetch();const body=(await response.text()).replace("case'installUpdate':", "case'installUpdate':throw Error('更新包已变化，请重新下载。');");
  await route.fulfill({response,body});
 });
 await login(page);await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await page.getByRole('button',{name:'下载更新',exact:true}).click();
 await expect.poll(()=>page.evaluate(()=>typeof window.__finishDownload)).toBe('function');
 await page.evaluate(()=>window.__finishDownload());await page.getByRole('button',{name:'安装并重启',exact:true}).click();
 await expect(page.getByRole('heading',{name:'安装未开始',exact:true})).toBeVisible();
 await expect(page.getByRole('button',{name:'检查更新',exact:true})).toBeEnabled();
 await page.getByRole('button',{name:'检查更新',exact:true}).click();
 await expect(page.getByRole('button',{name:'下载更新',exact:true})).toBeEnabled();
});

test('a late cancel failure cannot overwrite the next login update state',async({page})=>{
 await page.addInitScript(()=>{window.__updateFixture={State:2,Info:{Version:'9.0.0',Size:100,Notes:'新版说明'}};});
 await page.route('**/__test/bridge.js',async route=>{
  const response=await route.fetch();const body=(await response.text()).replace("case'cancelUpdate':", "case'cancelUpdate':await new Promise((_,reject)=>window.__rejectCancel=()=>reject(Error('旧会话取消失败')));");
  await route.fulfill({response,body});
 });
 await login(page);await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await page.getByRole('button',{name:'下载更新',exact:true}).click();await page.getByRole('button',{name:'取消下载',exact:true}).click();
 await expect.poll(()=>page.evaluate(()=>typeof window.__rejectCancel)).toBe('function');
 await page.getByRole('button',{name:'锁定',exact:true}).click();
 await page.evaluate(()=>window.__updateFixture={State:1});await unlock(page);
 await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await expect(page.getByRole('heading',{name:'当前已是最新版本',exact:true})).toBeVisible();
 await page.evaluate(async()=>{window.__rejectCancel();await new Promise(resolve=>requestAnimationFrame(()=>requestAnimationFrame(resolve)));});
 await expect(page.locator('.update-message')).not.toContainText('旧会话取消失败');
 await expect(page.getByRole('button',{name:'检查更新',exact:true})).toBeEnabled();
});
