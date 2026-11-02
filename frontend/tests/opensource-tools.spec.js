import {test,expect} from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

test('all eight projects and PowerToys shortcuts are accessible with guides and launch dispatch',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await page.goto('/');await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'效率工具',exact:true}).click();
 await expect(page.locator('.tool-card')).toHaveCount(12);
 const catalog=await page.request.get('/__test/catalog').then(r=>r.json());
 const tools=catalog.filter(t=>t.Id.startsWith('oss-')||t.Id.startsWith('pt-'));
 expect(tools).toHaveLength(14);
 for(const tool of tools){
  await page.getByRole('button',{name:tool.Category,exact:true}).click();
  await page.getByRole('textbox',{name:'搜索工具'}).fill(tool.Name);
  await expect(page.locator('.tool-card')).toHaveCount(1);await page.locator('.tool-card').click();
  const detail=page.getByRole('complementary',{name:'工具详情'});
  await expect(detail.getByRole('heading',{name:tool.Name,exact:true})).toBeVisible();
  await expect(detail).toContainText(tool.Purpose);
  await page.getByRole('tab',{name:'注意事项',exact:true}).click();await expect(detail).toContainText(tool.Impact);
  await page.getByRole('button',{name:'打开工具',exact:true}).click();
  await expect(page.getByRole('status').filter({hasText:'已启动：'+tool.Name})).toBeVisible();
 }
 const dispatched=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='requestLaunch').map(c=>c.args));
 expect(dispatched.map(c=>c.toolId)).toEqual(tools.map(t=>t.Id));
 expect(dispatched.every(c=>Object.keys(c).join(',')==='toolId')).toBe(true);
 await page.getByRole('button',{name:'效率工具',exact:true}).click();
 await page.getByRole('textbox',{name:'搜索工具'}).fill('');
 const output=path.join(process.env.TEMP,'cz-opensource-qa');fs.mkdirSync(output,{recursive:true});
 await page.screenshot({path:path.join(output,'efficiency-tools.png'),animations:'disabled'});
 expect(errors).toEqual([]);
});
