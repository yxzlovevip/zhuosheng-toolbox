import {test,expect} from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

test('three supplied maintenance tools are searchable and show their actual impact',async({page})=>{
 const errors=[];page.on('pageerror',error=>errors.push(error.message));
 await page.goto('/');await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 const cases=[['Win10 优化系统','系统设置','关闭防火墙'],['HEU KMS Activator 63.3.4','重装维护','激活'],['ACE 虚拟化环境检测修复','系统设置','WSL']];
 for(const [name,category,impact] of cases){
  await page.getByRole('button',{name:category,exact:true}).click();
  await page.getByRole('textbox',{name:'搜索工具'}).fill(name);
  await expect(page.locator('.tool-card')).toHaveCount(1);await page.locator('.tool-card').click();
  const detail=page.getByRole('complementary',{name:'工具详情'});
  await expect(detail.getByRole('heading',{name,exact:true})).toBeVisible();
  await page.getByRole('tab',{name:'注意事项',exact:true}).click();
  await expect(detail).toContainText(impact);await expect(detail).toContainText('未');
  if(name==='Win10 优化系统'){
   const output=path.join(process.env.TEMP,'cz-supplied-tools-qa');fs.mkdirSync(output,{recursive:true});
   await page.screenshot({path:path.join(output,'win10-optimization.png'),animations:'disabled'});
  }
  await page.getByRole('button',{name:'打开工具',exact:true}).click();
  await expect(page.getByRole('status').filter({hasText:`已启动：${name}`})).toBeVisible();
  await expect(page.getByRole('dialog')).toHaveCount(0);
  await page.getByRole('textbox',{name:'搜索工具'}).fill('');
 }
 expect(await page.evaluate(()=>window.__nativeCalls.filter(call=>call.action==='requestLaunch').map(call=>call.args.toolId))).toEqual(['win10-optimization','heu-kms','ace-virtualization']);
 expect(errors).toEqual([]);
});

test('removed tools have no launch entries while ACE and other activation and update tools remain',async({page})=>{
 await page.goto('/');await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await expect(page.getByRole('button',{name:'ACE 虚拟化环境检测修复',exact:true})).toBeVisible();
 await expect(page.getByRole('button',{name:'系统激活工具',exact:true})).toHaveCount(0);
 await expect(page.getByRole('button',{name:'关杀毒更新工具',exact:true})).toHaveCount(0);
 await expect(page.getByRole('button',{name:'HEU KMS Activator 63.3.4',exact:true})).toBeVisible();
 await expect(page.getByRole('button',{name:'Windows 更新设置工具',exact:true})).toBeVisible();
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='requestLaunch').length)).toBe(0);
});
