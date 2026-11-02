import {test,expect} from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';

const output=path.join(process.env.TEMP,'cz-favorites-qa');
fs.mkdirSync(output,{recursive:true});
async function unlock(page){await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await expect(page.locator('.workspace')).toBeVisible();}
async function login(page){await page.goto('/');await expect(page.locator('#startup-title').or(page.locator('#password'))).toBeVisible();if(await page.locator('#startup-title').count())await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();await unlock(page);}
async function allTools(page){await page.getByRole('button',{name:'全部工具',exact:true}).click();}
async function favorites(page){await page.getByRole('button',{name:'常用工具',exact:true}).click();}
const heart=card=>card.getByRole('button',{name:/常用工具：/});
const launchCount=page=>page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='requestLaunch').length);
test.beforeEach(async({page})=>{await page.emulateMedia({reducedMotion:'reduce'});});

test('favorites start empty and guide users to choose their own tools',async({page})=>{
 await login(page);await favorites(page);await expect(page.locator('.tool-card')).toHaveCount(0);
 await expect(page.getByRole('heading',{name:'还没有常用工具',exact:true})).toBeVisible();
 await page.getByRole('button',{name:'去选择常用工具',exact:true}).click();
 await expect(page.getByRole('heading',{name:'工具中心',exact:true})).toBeVisible();
 await expect(page.locator('.tool-card').first()).toBeVisible();
 await expect(heart(page.locator('.tool-card').first())).toHaveAttribute('aria-pressed','false');
});

test('hearts select favorites across categories and survive locking and reload',async({page})=>{
 const errors=[];page.on('pageerror',error=>errors.push(error.message));await login(page);await allTools(page);
 const cards=page.locator('.tool-card');const firstName=await cards.first().locator('.card-select').getAttribute('aria-label');
 await heart(cards.first()).click();await expect(heart(cards.first())).toHaveAttribute('aria-pressed','true');
 await page.getByRole('button',{name:'磁盘工具',exact:true}).click();
 const diskName=await cards.first().locator('.card-select').getAttribute('aria-label');await heart(cards.first()).click();
 expect(await launchCount(page)).toBe(0);await favorites(page);await expect(cards).toHaveCount(2);
 await expect(page.locator('.section-title')).toContainText('2 个工具');
 for(const name of [firstName,diskName])await expect(page.locator('.tool-grid').getByRole('button',{name,exact:true})).toBeVisible();
 await page.getByRole('textbox',{name:'搜索工具'}).fill('肯定不存在的收藏名称');await expect(cards).toHaveCount(0);
 await expect(page.getByText('没有找到匹配的工具',{exact:true}).first()).toBeVisible();await page.locator('.empty-results').getByRole('button',{name:'清除搜索',exact:true}).click();await expect(cards).toHaveCount(2);
 await page.getByRole('button',{name:'锁定',exact:true}).click();await unlock(page);await favorites(page);await expect(cards).toHaveCount(2);
 await page.reload();await unlock(page);await favorites(page);await expect(cards).toHaveCount(2);
 for(const [theme,width,height] of [['深色',1380,860],['浅色',980,650]]){
  await page.setViewportSize({width,height});await page.getByRole('button',{name:theme,exact:true}).click();
  await expect(heart(cards.first())).toBeVisible();expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await page.screenshot({path:path.join(output,`favorites-${width}.png`)});
 }
 expect(errors).toEqual([]);
});

test('removing favorites updates the list immediately and stays removed after reload',async({page})=>{
 await login(page);await allTools(page);await heart(page.locator('.tool-card').first()).click();await favorites(page);
 await expect(page.locator('.tool-card')).toHaveCount(1);await heart(page.locator('.tool-card').first()).click();
 await expect(page.locator('.tool-card')).toHaveCount(0);await expect(page.getByRole('heading',{name:'还没有常用工具',exact:true})).toBeVisible();
 await page.reload();await unlock(page);await favorites(page);await expect(page.locator('.tool-card')).toHaveCount(0);
});

test('keyboard and double-click on hearts never open or select a tool',async({page})=>{
 await login(page);await allTools(page);const cards=page.locator('.tool-card');const selected=await page.locator('.tool-card.selected .card-select').getAttribute('aria-label');
 const button=heart(cards.nth(1));await button.focus();await page.keyboard.press('Enter');await expect(button).toHaveAttribute('aria-pressed','true');
 await page.keyboard.press('Space');await expect(button).toHaveAttribute('aria-pressed','false');
 await button.dblclick();await expect(button).toHaveAttribute('aria-pressed','false');expect(await launchCount(page)).toBe(0);
 await expect(page.locator('.tool-card.selected .card-select')).toHaveAttribute('aria-label',selected);
});

test('invalid saved favorites do not prevent loading or choosing tools',async({page})=>{
 await page.addInitScript(()=>localStorage.setItem('favorite-tools','{broken json'));await login(page);await favorites(page);
 await expect(page.locator('.tool-card')).toHaveCount(0);await allTools(page);await heart(page.locator('.tool-card').first()).click();await favorites(page);await expect(page.locator('.tool-card')).toHaveCount(1);
});

test('a failed save keeps the previous selection and explains the failure',async({page})=>{
 await page.addInitScript(()=>{const original=Storage.prototype.setItem;Storage.prototype.setItem=function(key,value){if(key==='favorite-tools')throw Error('storage unavailable');return original.call(this,key,value);};});
 await login(page);await allTools(page);const button=heart(page.locator('.tool-card').first());await button.click();
 await expect(button).toHaveAttribute('aria-pressed','false');await expect(page.getByRole('alert')).toContainText('常用工具保存失败');
 await favorites(page);await expect(page.locator('.tool-card')).toHaveCount(0);
});
