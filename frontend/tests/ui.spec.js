import {test,expect} from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
const output=path.resolve('../artifacts/react-ui');fs.mkdirSync(output,{recursive:true});
const expectedTools=JSON.parse(fs.readFileSync(path.resolve('../assets/catalog.json'),'utf8'));
const qaOutput=path.join(process.env.TEMP,'cz-reinstall-qa');fs.mkdirSync(qaOutput,{recursive:true});
test('startup notice appears before login and stays accepted after relocking',async({page})=>{
 await page.goto('/');await expect(page.getByRole('dialog',{name:'使用前提示'})).toBeVisible();
 await expect(page.locator('#password')).toHaveCount(0);
 await page.setViewportSize({width:1000,height:700});
 await expect(page.getByRole('button',{name:'我已了解，进入工具箱'})).toBeInViewport();
 await page.screenshot({path:path.join(qaOutput,'startup-notice.png')});
 await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:/锁定/}).click();await expect(page.locator('#password')).toBeVisible();await expect(page.getByRole('dialog')).toHaveCount(0);
 await page.reload();await expect(page.locator('#password')).toBeVisible();await expect(page.getByRole('dialog')).toHaveCount(0);
 await page.evaluate(()=>sessionStorage.removeItem('startup-accepted'));await page.reload();await expect(page.getByRole('dialog',{name:'使用前提示'})).toBeVisible();
});
async function openApp(page){await page.goto('/');await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();}
test('all tools with system or data changes open without repeated toolbox confirmations',async({page})=>{
 await openApp(page);await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 for(const tool of expectedTools.filter(t=>t.NeedsConfirmation)){
  await page.getByRole('textbox',{name:'搜索工具'}).fill(tool.Name);
  await page.getByRole('button',{name:tool.Id==='uac'?'打开注册表导入':'打开工具',exact:true}).click();
  await expect(page.locator('.toast')).toContainText(`已启动：${tool.Name}`);
  await expect(page.getByRole('dialog')).toHaveCount(0);
 }
});
test('login, search, direct UAC launch, themes, update and lock',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));await openApp(page);
 await expect(page.getByRole('heading',{name:'卓盛电脑维护助手'})).toBeVisible();
 await page.locator('#password').fill('0000');await page.getByRole('button',{name:'登录',exact:true}).click();await expect(page.getByRole('alert')).toHaveText('密码错误。');
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await page.getByRole('button',{name:'全部工具',exact:true}).click();await expect(page.getByRole('heading',{name:'工具中心'})).toBeVisible();
 await expect(page.locator('.tool-card')).toHaveCount(expectedTools.length);await page.waitForTimeout(650);await page.screenshot({path:path.join(output,'dark.png')});
 const contrast=await page.locator('.launch-button').evaluate(el=>{let s=getComputedStyle(el);return {fg:s.color,bg:s.backgroundColor};});expect(contrast.fg).not.toBe(contrast.bg);
 await page.getByRole('button',{name:'浅色',exact:true}).click();await expect(page.locator('html')).toHaveAttribute('data-theme','light');await page.waitForTimeout(400);await page.screenshot({path:path.join(output,'light.png')});
 await page.getByRole('button',{name:'深色',exact:true}).click();
 await page.getByRole('textbox',{name:'搜索工具'}).fill('UAC');await expect(page.locator('.tool-card')).toHaveCount(1);await page.getByRole('tab',{name:'注意事项',exact:true}).click();await expect(page.getByText('EnableLUA=0',{exact:false})).toBeVisible();
 await page.getByRole('button',{name:'打开注册表导入',exact:true}).click();await expect(page.getByRole('dialog')).toHaveCount(0);await expect(page.locator('.toast')).toContainText('已启动');
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='confirmLaunch').length)).toBe(0);
 await page.getByRole('button',{name:'软件更新',exact:true}).click();await expect(page.getByRole('heading',{name:'尚未配置更新服务器'})).toBeVisible();
 await page.getByRole('button',{name:/锁定/}).click();await expect(page.locator('#password')).toBeVisible();await expect(page.locator('#password')).toHaveValue('');await page.screenshot({path:path.join(output,'login.png')});expect(errors).toEqual([]);
});
test('cards animate and support reduced motion without layout overflow',async({page})=>{
 await openApp(page);await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await page.getByRole('button',{name:'全部工具',exact:true}).click();await expect(page.locator('.tool-card')).toHaveCount(expectedTools.length);
 const motion=await page.locator('.tool-card').first().evaluate(e=>getComputedStyle(e).animationName);expect(motion).toContain('card-arrive');await page.waitForTimeout(750);
 await page.locator('.tool-card').nth(1).hover();await page.waitForTimeout(200);expect(await page.locator('.tool-card').nth(1).evaluate(e=>getComputedStyle(e).transform)).not.toBe('none');
 await page.setViewportSize({width:1000,height:700});await page.waitForTimeout(200);expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:path.join(output,'compact.png')});
 await page.emulateMedia({reducedMotion:'reduce'});const duration=await page.locator('.tool-card').first().evaluate(e=>parseFloat(getComputedStyle(e).animationDuration));expect(duration).toBeLessThan(.01);
});

test('reinstallation topic contains only two installers and shows their separate passwords',async({page})=>{
 await openApp(page);await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'重装维护',exact:true}).click();await expect(page.getByRole('region',{name:'系统重装'}).locator('.tool-card')).toHaveCount(2);
 await page.getByRole('button',{name:'天猫一键重装',exact:true}).click();await expect(page.getByRole('complementary',{name:'工具详情'})).toContainText('1788');
 await page.getByRole('button',{name:'技术员一键重装',exact:true}).click();await expect(page.getByRole('complementary',{name:'工具详情'})).toContainText('vip0');
 await page.waitForTimeout(600);await page.screenshot({path:path.join(qaOutput,'reinstall-topic.png')});
 await page.getByRole('button',{name:'打开工具',exact:true}).click();await expect(page.locator('.toast')).toContainText('已启动：技术员一键重装');
 await expect(page.getByRole('dialog')).toHaveCount(0);
 await page.getByRole('button',{name:'重装维护',exact:true}).click();await expect(page.locator('.tool-card').filter({hasText:/技术员一键重装|天猫一键重装/})).toHaveCount(2);
});

test('new system reminder requires confirmation and dismisses once across reopening',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.addInitScript(()=>{window.__newSystemFixture={InstalledAt:'2026-10-20T08:30:00+08:00',ShouldPrompt:true,Message:''};});
 await openApp(page);await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 const dialog=page.getByRole('dialog');await expect(dialog).toContainText('新系统');await expect(dialog).toContainText('2026');
 await expect(page.getByRole('button',{name:'打开驱动总裁',exact:true})).toBeDisabled();
 await page.waitForTimeout(400);await page.screenshot({path:path.join(qaOutput,'new-system-reminder.png')});
 await page.setViewportSize({width:1000,height:700});await page.waitForTimeout(200);
 await expect(page.getByRole('button',{name:'暂不检查',exact:true})).toBeInViewport();await expect(page.getByRole('checkbox')).toBeInViewport();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);await page.screenshot({path:path.join(qaOutput,'new-system-compact.png')});
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='requestLaunch'||c.action==='confirmLaunch').length)).toBe(0);
 await page.getByRole('button',{name:'暂不检查',exact:true}).click();await expect(dialog).toHaveCount(0);
 await page.reload();await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await expect(page.getByRole('heading',{name:'系统信息',exact:true})).toBeVisible();await page.waitForTimeout(300);await expect(dialog).toHaveCount(0);expect(errors).toEqual([]);
});

test('new system reminder opens the driver without another launch confirmation',async({page})=>{
 await page.addInitScript(()=>{window.__newSystemFixture={InstalledAt:'2026-10-20T08:30:00+08:00',ShouldPrompt:true,Message:''};});
 await openApp(page);await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await expect(page.getByRole('dialog')).toContainText('新系统');await page.getByRole('checkbox').check();await page.getByRole('button',{name:'打开驱动总裁',exact:true}).click();
 await expect(page.locator('.toast')).toContainText('已启动：驱动总裁');await expect(page.getByRole('dialog')).toHaveCount(0);
 const calls=await page.evaluate(()=>window.__nativeCalls.filter(c=>['requestLaunch','confirmLaunch','dismissNewSystem'].includes(c.action)));
 expect(calls.map(c=>c.action)).toEqual(['requestLaunch','dismissNewSystem']);expect(calls[0].args.toolId).toBe('driver');
});

test('a delayed new system reminder does not add a tool launch confirmation',async({page})=>{
 await page.addInitScript(()=>{window.__holdNewSystem=true;window.__newSystemFixture={InstalledAt:'2026-10-20T08:30:00+08:00',ShouldPrompt:true,Message:''};});
 await openApp(page);await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await page.getByRole('textbox',{name:'搜索工具'}).fill('UAC');await page.getByRole('button',{name:'打开注册表导入',exact:true}).click();
 await expect(page.locator('.toast')).toContainText('已启动');await expect(page.getByRole('dialog')).toHaveCount(0);
 await expect.poll(()=>page.evaluate(()=>typeof window.__releaseNewSystem)).toBe('function');await page.evaluate(()=>window.__releaseNewSystem());
 await expect(page.locator('.system-install-status')).toHaveCount(0);
 await expect(page.getByRole('dialog')).toContainText('新系统');
});
