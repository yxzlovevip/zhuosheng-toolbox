import {test,expect} from '@playwright/test';
import path from 'node:path';
import fs from 'node:fs';
const output=path.resolve('../artifacts/theme-update');fs.mkdirSync(output,{recursive:true});
test('vivid light and dark themes remain readable at desktop and minimum window sizes',async({page})=>{
 const errors=[];page.on('pageerror',error=>{errors.push(error.message);console.log('UI error:',error.message);});
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.goto('/');await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await page.setViewportSize({width:1588,height:990});
 for(const mode of ['深色','浅色']){
  await page.getByRole('button',{name:mode,exact:true}).click();
  await expect(page.locator('html')).toHaveAttribute('data-theme',mode==='深色'?'dark':'light');
  await expect(page.locator('.tool-card').last()).toHaveCSS('opacity','1');
  const colors=await page.locator('.launch-button').evaluate(element=>{
   const style=getComputedStyle(element);
   const rgb=value=>value.match(/[\d.]+/g).slice(0,3).map(Number);
   const lum=values=>values.map(v=>{v/=255;return v<=.04045?v/12.92:((v+.055)/1.055)**2.4;}).reduce((sum,v,i)=>sum+v*[.2126,.7152,.0722][i],0);
   const fg=rgb(style.color),bg=rgb(style.backgroundColor),a=lum(fg),b=lum(bg);
   return {contrast:(Math.max(a,b)+.05)/(Math.min(a,b)+.05),saturation:Math.max(...bg)-Math.min(...bg)};
  });
  expect(colors.contrast).toBeGreaterThanOrEqual(4.5);expect(colors.saturation).toBeGreaterThan(80);
  await page.screenshot({path:path.join(output,mode==='深色'?'dark.png':'light.png')});
 }
 for(const [width,height] of [[1366,768],[980,650],[390,844]]){
  await page.setViewportSize({width,height});
  expect(await page.evaluate(()=>document.documentElement.scrollWidth<=innerWidth)).toBe(true);
  await expect(page.getByRole('button',{name:'软件更新',exact:true})).toBeInViewport();
  await page.getByRole('button',{name:'软件更新',exact:true}).click();
  await expect(page.getByRole('button',{name:'检查更新',exact:true})).toBeVisible();
  await page.screenshot({path:path.join(output,`updates-${width}.png`)});
  await page.getByRole('button',{name:'全部工具',exact:true}).click();
 }
 expect(errors).toEqual([]);
});

