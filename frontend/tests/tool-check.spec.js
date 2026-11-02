import {test,expect} from '@playwright/test';
import fs from 'node:fs';
import path from 'node:path';
async function login(page){await page.goto('/');await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await page.getByRole('button',{name:'使用说明',exact:true}).click();}

test('checks bundled files and explains missing and changed tools without launching them',async({page})=>{
 test.setTimeout(12000);
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await login(page);
 await page.evaluate(()=>{window.__toolCheckFixture={CheckedAt:'2026-10-22T17:00:00+08:00',Items:[{Id:'uninstaller',Name:'Geek 软件卸载工具',Category:'常用工具',Status:'ready',Message:'文件完整'},{Id:'runtime-repair',Name:'系统必备运行库修复',Category:'重装维护',Status:'missing',Message:'文件缺失，请重新安装完整工具箱。'},{Id:'ace-virtualization',Name:'ACE 虚拟化环境检测修复',Category:'系统设置',Status:'changed',Message:'文件已变化，请重新安装。'}]};});
 await page.getByRole('button',{name:'检查工具文件',exact:true}).click();
 const panel=page.getByRole('region',{name:'工具文件检查'});
 await expect(panel).toContainText('1 / 3 项文件完整');await expect(panel).toContainText('系统必备运行库修复');await expect(panel).toContainText('ACE 虚拟化环境检测修复');
 await expect(panel.getByText('Geek 软件卸载工具',{exact:true})).toHaveCount(0);
 await panel.getByRole('button',{name:'查看全部结果'}).click();await expect(panel.getByText('Geek 软件卸载工具',{exact:true})).toBeVisible();
 await page.emulateMedia({reducedMotion:'reduce'});await page.setViewportSize({width:1000,height:700});await page.getByRole('button',{name:'浅色',exact:true}).click();
 expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
 const output=path.resolve('../artifacts/react-ui');fs.mkdirSync(output,{recursive:true});await panel.screenshot({path:path.join(output,'tool-file-check.png')});
 await panel.getByRole('button',{name:'前往软件更新'}).click();await expect(page.getByRole('heading',{name:'软件更新',exact:true,level:1})).toBeVisible();
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='requestLaunch').length)).toBe(0);expect(errors).toEqual([]);
});

test('file check result survives navigation, retries errors and clears after locking',async({page})=>{
 await login(page);await page.evaluate(()=>{window.__toolCheckError='文件检查暂时失败，请重试。';});
 await page.getByRole('button',{name:'检查工具文件',exact:true}).click();await expect(page.getByRole('region',{name:'工具文件检查'})).toContainText('文件检查暂时失败');
 await page.evaluate(()=>{window.__toolCheckError=null;window.__holdToolCheck=true;});await page.getByRole('button',{name:'重新检查',exact:true}).click();
 await expect(page.getByRole('button',{name:'正在检查…',exact:true})).toBeDisabled();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();await page.getByRole('button',{name:'使用说明',exact:true}).click();await expect(page.getByRole('button',{name:'正在检查…',exact:true})).toBeDisabled();
 await page.evaluate(()=>{window.__releaseToolCheck();});await expect(page.getByRole('region',{name:'工具文件检查'})).toContainText('项文件完整');
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='checkToolFiles').length)).toBe(2);
 await page.getByRole('button',{name:/锁定/}).click();await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await page.getByRole('button',{name:'使用说明',exact:true}).click();await expect(page.getByRole('button',{name:'检查工具文件',exact:true})).toBeVisible();
});

test('a file check finishing after relogin cannot replace the new session result',async({page})=>{
 await login(page);await page.evaluate(()=>{window.__holdToolCheck=true;});await page.getByRole('button',{name:'检查工具文件',exact:true}).click();
 await page.getByRole('button',{name:/锁定/}).click();await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await page.getByRole('button',{name:'使用说明',exact:true}).click();
 await page.evaluate(()=>{window.__holdToolCheck=false;window.__releaseToolCheck();});
 await expect(page.getByRole('button',{name:'检查工具文件',exact:true})).toBeVisible();await expect(page.getByRole('region',{name:'工具文件检查'})).not.toContainText('项文件完整');
});
