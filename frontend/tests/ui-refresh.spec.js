import {test,expect} from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';

const evidence=path.resolve('../.build/ui-ux-20261023/evidence');
fs.mkdirSync(evidence,{recursive:true});
const hardware={CollectedAt:'2026-10-23T10:00:00+08:00',Sections:[
 {Id:'computer',Title:'电脑与系统',Devices:[{Id:'pc',Name:'桌面工作站',Fields:[{Label:'操作系统',Value:'Windows 11 专业版'}],Children:[]}]},
 {Id:'cpu',Title:'处理器 CPU',Devices:[{Id:'cpu',Name:'Intel Core i7',Fields:[{Label:'核心数',Value:'12 核 / 20 线程'}],Children:[]}]},
 {Id:'memory',Title:'内存',Devices:[{Id:'memory',Name:'32 GB DDR5',Fields:[{Label:'频率',Value:'5600 MHz'}],Children:[]}]},
 {Id:'graphics',Title:'显卡',Devices:[{Id:'gpu',Name:'NVIDIA GeForce RTX',Fields:[{Label:'驱动状态',Value:'正常'}],Children:[]}]}
]};
async function login(page){
 await page.goto('/');
 if(await page.locator('#startup-title').count())await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await expect(page.locator('.workspace')).toBeVisible();
}
test.beforeEach(async({page})=>{
 await page.addInitScript(hardware=>{window.__hardwareFixture=hardware;window.__uxStates={};},hardware);
 await page.route('**/__test/bridge.js',async route=>{
  const response=await route.fetch();let body=await response.text();
  body=body.replace("case'theme':break;","case'theme':if(window.__themeFailure)throw Error('偏好文件暂时无法写入');break;");
  body=body.replace("case'acceptStartup':","case'acceptStartup':if(window.__holdStartup)await new Promise(resolve=>window.__releaseStartup=resolve);");
  body=body.replace("case'requestLaunch':",`case'getToolStates':{
   const snapshot=structuredClone(window.__uxStates);
   if(window.__holdStatus) {window.__holdStatus=false;await new Promise(resolve=>window.__releaseStatus=resolve);}
   result=snapshot;break;
  }case'requestLaunch':{
   const id=m.args.toolId;
   if(window.__holdLaunch)await new Promise(resolve=>window.__releaseLaunch=resolve);
   if(window.__launchFailure){const message=window.__launchMessage||'工具文件缺失，请重新安装。';window.__uxStates[id]={Status:'failed',Message:message};throw Error(message);}
   window.__uxStates[id]={Status:'running',Message:''};result={toolStates:structuredClone(window.__uxStates)};break;
  }case'__unusedLaunch':`);
  await route.fulfill({response,body});
 });
});

test('capture review surfaces',async({page})=>{
 const phase=process.env.CZ_UX_PHASE||'after';
 const errors=[];page.on('pageerror',error=>errors.push(error.message));
 await login(page);
 for(const theme of ['深色','浅色']){
  await page.getByRole('button',{name:theme,exact:true}).click();
  for(const section of ['系统信息','全部工具','常用工具','软件合集','使用说明','软件更新']){
   await page.getByRole('button',{name:section,exact:true}).click();
   if(section==='软件合集')await expect(page.locator('.software-card').first()).toBeVisible();
   await page.screenshot({path:path.join(evidence,`${phase}-${theme}-${section}.png`),animations:'disabled'});
  }
 }
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 for(const [width,height] of [[1000,700],[1366,768],[1920,1080]]){
  await page.setViewportSize({width,height});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.screenshot({path:path.join(evidence,`${phase}-${width}.png`),animations:'disabled'});
 }
 expect(errors).toEqual([]);
});

test('detail tabs support arrow navigation and labelled panels',async({page})=>{
 await login(page);await page.getByRole('button',{name:'全部工具',exact:true}).click();
 const guide=page.getByRole('tab',{name:'使用说明',exact:true}),impact=page.getByRole('tab',{name:'注意事项',exact:true});
 await guide.focus();await guide.press('ArrowRight');await expect(impact).toBeFocused();
 await expect(impact).toHaveAttribute('aria-selected','true');
 await expect(page.getByRole('tabpanel',{name:'注意事项',exact:true})).toBeVisible();
 await impact.press('Home');await expect(guide).toBeFocused();
});

test('stale polling cannot erase a newer launch state',async({page})=>{
 await login(page);await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await page.evaluate(()=>window.__holdStatus=true);
 await expect.poll(()=>page.evaluate(()=>typeof window.__releaseStatus)).toBe('function');
 await page.locator('.launch-button').click();
 await expect(page.locator('.detail-panel .tool-run-state')).toHaveText('运行中');
 await page.evaluate(()=>window.__releaseStatus());
 // The held reply is the empty snapshot from before launch. Check after its render,
 // before the next poll could conceal a regression by returning the current state.
 await page.waitForTimeout(150);
 await expect(page.locator('.detail-panel .tool-run-state')).toHaveText('运行中',{timeout:400});
});

test('failed launches expose persistent retry and file-check actions',async({page})=>{
 await login(page);await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await page.evaluate(()=>window.__launchFailure=true);await page.locator('.launch-button').click();
 await expect(page.locator('.tool-run-message')).toContainText('工具文件缺失');
 await expect(page.getByRole('button',{name:'重试打开',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'检查工具文件',exact:true}).click();
 await expect(page.locator('.tool-check-summary')).toBeVisible();
});

test('software list resumes its scroll position after navigation',async({page})=>{
 await login(page);await page.getByRole('button',{name:'软件合集',exact:true}).click();
 const results=page.locator('.software-results');await expect(page.locator('.software-card').first()).toBeVisible();
 await results.evaluate(el=>el.scrollTop=750);
 await expect.poll(()=>results.evaluate(el=>el.scrollTop)).toBeGreaterThan(500);
 await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await expect.poll(()=>results.evaluate(el=>el.scrollTop)).toBeGreaterThan(500);
});

test('theme persistence errors stay actionable without changing the preview',async({page})=>{
 await login(page);await page.evaluate(()=>window.__themeFailure=true);
 await page.getByRole('button',{name:'浅色',exact:true}).click();
 await expect(page.locator('html')).toHaveAttribute('data-theme','light');
 const error=page.locator('.workspace-notice');
 await expect(error).toContainText('尚未保存');await page.waitForTimeout(4600);
 await expect(error).toBeVisible();
 await page.evaluate(()=>window.__themeFailure=false);
 await error.getByRole('button',{name:'重新保存',exact:true}).click();
 await expect(error).toHaveCount(0);
});

test('software error retry clears the error only after the browser accepts the link',async({page})=>{
 await login(page);await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await page.evaluate(()=>window.__softwareLinkError='浏览器暂时无法打开');
 await page.locator('.software-card').first().getByRole('button').first().click();
 await expect(page.getByRole('alert')).toContainText('浏览器暂时无法打开');
 await page.evaluate(()=>delete window.__softwareLinkError);
 await page.getByRole('alert').getByRole('button',{name:'重试打开',exact:true}).click();
 await expect(page.getByRole('alert')).toHaveCount(0);
 await expect(page.locator('.toast')).toContainText('已在浏览器打开');
});

test('list motion does not restart when returning from another page',async({page})=>{
 await login(page);await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 expect(await page.locator('.tool-card').first().evaluate(el=>getComputedStyle(el).animationName)).toBe('none');
});

test('empty hardware reports offer a clear explanation and manual refresh',async({page})=>{
 await page.addInitScript(()=>window.__hardwareFixture={CollectedAt:'2026-10-23T10:00:00+08:00',Sections:[]});
 await login(page);
 await expect(page.getByText('暂未读取到这类设备信息',{exact:true})).toBeVisible();
 await expect(page.getByRole('button',{name:'刷新信息',exact:true})).toBeEnabled();
});

test('software errors remain local to their browsing context',async({page})=>{
 await login(page);await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await page.evaluate(()=>window.__softwareLinkError='浏览器暂时无法打开');
 await page.locator('.software-card').first().getByRole('button').first().click();
 await expect(page.getByRole('alert')).toContainText('浏览器暂时无法打开');
 await page.getByRole('button',{name:'系统信息',exact:true}).click();
 await expect(page.getByRole('alert')).toHaveCount(0);
});

test('long list positions survive filtering to a short category and back',async({page})=>{
 await login(page);await page.getByRole('button',{name:'软件合集',exact:true}).click();
 const results=page.locator('.software-results');await expect(page.locator('.software-card').first()).toBeVisible();
 await results.evaluate(el=>el.scrollTop=1000);
 await expect.poll(()=>results.evaluate(el=>el.scrollTop)).toBeGreaterThan(500);
 await page.getByRole('button',{name:'浏览器',exact:true}).click();
 await expect(page.locator('.software-card')).toHaveCount(8);
 await page.getByRole('button',{name:'全部软件',exact:true}).click();
 await expect.poll(()=>results.evaluate(el=>el.scrollTop)).toBeGreaterThan(500);
});

test('pending startup acceptance retains keyboard focus inside its notice',async({page})=>{
 await page.goto('/');await page.evaluate(()=>window.__holdStartup=true);
 await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await expect(page.getByRole('button',{name:'正在进入…'})).toBeDisabled();
 await page.keyboard.press('Tab');
 await expect(page.locator('.confirm-dialog .dialog-body')).toBeFocused();
 await page.evaluate(()=>window.__releaseStartup());await expect(page.locator('#password')).toBeVisible();
});

test('pending driver confirmation keeps a focusable dialog fallback',async({page})=>{
 await page.addInitScript(()=>{window.__holdLaunch=true;window.__newSystemFixture={InstalledAt:'2026-10-23T08:00:00+08:00',ShouldPrompt:true,Message:''};});
 await login(page);await page.getByRole('checkbox').check();
 await page.getByRole('button',{name:'打开驱动总裁',exact:true}).click();
 await expect(page.getByRole('button',{name:'正在校验…'})).toBeDisabled();
 await page.keyboard.press('Tab');await expect(page.getByRole('dialog')).toBeFocused();
 await page.evaluate(()=>window.__releaseLaunch());await expect(page.getByRole('dialog')).toHaveCount(0);
});

test('long failure details leave retry and file checking reachable in the minimum window',async({page})=>{
 await page.setViewportSize({width:1000,height:700});await login(page);
 await page.getByRole('button',{name:'重装维护',exact:true}).click();
 await page.getByRole('textbox',{name:'搜索工具'}).fill('技术员一键重装');
 await page.evaluate(()=>{window.__launchFailure=true;window.__launchMessage='工具文件无法访问：'+('C:\\Program Files\\卓盛工具箱\\很长的文件夹名称\\工具.exe；'.repeat(12))+'请检查文件或查看日志。';});
 await page.locator('.launch-button').click();
 await expect(page.getByRole('button',{name:'重试打开',exact:true})).toBeInViewport();
 await expect(page.getByRole('button',{name:'检查工具文件',exact:true})).toBeInViewport();
});

test('large tool file checks can outlast the old bridge timeout and still return a report',async({page})=>{
 await page.clock.install();await login(page);
 await page.getByRole('button',{name:'使用说明',exact:true}).click();
 await page.evaluate(()=>window.__holdToolCheck=true);
 await page.getByRole('button',{name:'检查工具文件',exact:true}).click();
 await expect.poll(()=>page.evaluate(()=>typeof window.__releaseToolCheck)).toBe('function');
 await page.clock.fastForward(46000);
 await expect(page.getByRole('button',{name:'正在检查…',exact:true})).toBeDisabled();
 await page.evaluate(()=>window.__releaseToolCheck());
 await expect(page.locator('.tool-check-summary')).toContainText('项文件完整');
 await expect(page.locator('.tool-check-error')).toHaveCount(0);
});

test('startup focus stays inside the notice and motion is bounded',async({page})=>{
 await page.goto('/');const accept=page.getByRole('button',{name:'我已了解，进入工具箱'});
 await accept.focus();await accept.press('Tab');
 await expect(page.locator('.confirm-dialog .dialog-body')).toBeFocused();
 await accept.click();await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 const card=page.locator('.tool-card').first();
 expect(await card.evaluate(el=>parseFloat(getComputedStyle(el).animationDuration))).toBeLessThanOrEqual(.25);
 await page.emulateMedia({reducedMotion:'reduce'});
 expect(await card.evaluate(el=>parseFloat(getComputedStyle(el).animationDuration))).toBeLessThan(.01);
});

for(const scale of [1.25,1.5])test.describe(`renderer scale ${scale}`,()=>{
 test.use({deviceScaleFactor:scale});
 test('small-window actions and text remain within the viewport',async({page})=>{
  await page.setViewportSize({width:1000,height:700});await login(page);
  await page.getByRole('button',{name:'全部工具',exact:true}).click();
  await expect(page.locator('.launch-button')).toBeInViewport();
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  expect(await page.evaluate(()=>window.devicePixelRatio)).toBe(scale);
  await page.screenshot({path:path.join(evidence,`renderer-${scale}.png`),animations:'disabled'});
 });
});
