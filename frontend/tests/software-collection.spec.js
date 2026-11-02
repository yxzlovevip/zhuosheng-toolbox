import {test,expect} from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
const output=path.join(process.env.TEMP,'cz-software-collection-qa');
fs.mkdirSync(output,{recursive:true});
async function openCollection(page){
 await page.goto('/');
 await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');
 await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await expect(page.getByRole('textbox',{name:'搜索软件'})).toBeVisible();
}
test('every software logo is bundled locally and decodes without external requests',async({page})=>{
 const external=[];
 await page.route('**/*',route=>{const url=new URL(route.request().url());if(url.hostname!=='127.0.0.1'){external.push(url.href);return route.abort();}return route.continue();});
 await openCollection(page);
 const cards=page.locator('.software-card');const count=await cards.count();
 await expect(page.locator('.software-logo')).toHaveCount(count);
 const result=await page.locator('.software-logo').evaluateAll(async images=>{
  await Promise.all(images.map(async image=>{image.loading='eager';try{await image.decode();}catch{}}));
  return images.map(image=>({src:image.getAttribute('src'),ok:image.naturalWidth>0&&image.naturalHeight>0}));
 });
 expect(result.every(i=>i.src.startsWith('./software-logos/')&&i.ok)).toBe(true);
 expect(external).toEqual([]);
 await expect(page.getByRole('img',{name:'MSI Afterburner Logo'}).locator('..')).toHaveCSS('background-color','rgb(32, 42, 59)');
});
test('merged collection searches both sources and opens an identified download through the host',async({page})=>{
 const errors=[];page.on('pageerror',e=>errors.push(e.message));
 await openCollection(page);
 await expect(page.getByRole('heading',{name:'软件合集',exact:true})).toBeVisible();
 expect(await page.locator('.software-card').count()).toBeGreaterThan(100);
 await page.getByRole('textbox',{name:'搜索软件'}).fill('Everything');
 await expect(page.locator('.software-card')).toHaveCount(1);
 const card=page.locator('.software-card');
 await expect(card).toContainText('工具大合集');await expect(card).toContainText('装机必备软件库');
 await expect(card).toContainText('voidtools.com');
 await card.getByRole('button',{name:'访问网站',exact:true}).click();
 await expect(page.getByRole('status')).toContainText('已在浏览器打开');
 const calls=await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='openSoftwareLink'));
 expect(calls).toHaveLength(1);expect(calls[0].args.softwareId).toBeTruthy();expect(calls[0].args.linkId).toBeTruthy();expect(calls[0].args.url).toBeUndefined();
 await page.getByRole('textbox',{name:'搜索软件'}).fill('TranslucentTB');await expect(page.locator('.software-card')).toHaveCount(1);
 await page.getByRole('textbox',{name:'搜索软件'}).fill('DeepSeek');await expect(page.locator('.software-card')).toHaveCount(1);
 await page.screenshot({path:path.join(output,'search.png')});
 expect(errors).toEqual([]);
});
test('categories, empty results, link errors and window sizes remain usable',async({page})=>{
 await openCollection(page);
 await page.getByRole('button',{name:'浏览器',exact:true}).click();
 await expect(page.locator('.software-card')).not.toHaveCount(0);
 for(const card of await page.locator('.software-card').all())await expect(card.locator('.software-category')).toHaveText('浏览器');
 await page.getByRole('textbox',{name:'搜索软件'}).fill('no-such-software-xy123');
 await expect(page.getByText('没有找到匹配的软件')).toBeVisible();
 await page.getByRole('button',{name:'清除软件搜索'}).click();
 await page.evaluate(()=>window.__softwareLinkError='浏览器打开失败，请重试。');
 await page.locator('.software-card').first().getByRole('button',{name:'访问网站',exact:true}).click();
 await expect(page.getByRole('alert')).toContainText('浏览器打开失败');
 await page.evaluate(()=>delete window.__softwareLinkError);
 await page.getByRole('alert').getByRole('button',{name:'关闭提示',exact:true}).click();
 await expect(page.getByRole('alert')).toHaveCount(0);
 await page.getByRole('button',{name:'全部软件',exact:true}).click();
 for(const [width,height,theme] of [[1380,860,'深色'],[1000,700,'浅色']]){
  await page.setViewportSize({width,height});await page.getByRole('button',{name:theme,exact:true}).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme',theme==='深色'?'dark':'light');
  await page.waitForTimeout(500);
  await expect(page.getByRole('textbox',{name:'搜索软件'})).toBeInViewport();
  expect(await page.locator('.software-collection').evaluate(el=>el.scrollWidth<=el.clientWidth+1)).toBe(true);
  await page.screenshot({path:path.join(output,`${width}-${theme}.png`)});
 }
 await page.getByRole('complementary').getByRole('button',{name:'常用工具',exact:true}).click();await expect(page.getByRole('textbox',{name:'搜索工具'})).toBeVisible();
});
