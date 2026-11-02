import {test,expect} from '@playwright/test';

async function login(page){
 await page.goto('/');
 await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');
 await page.getByRole('button',{name:'登录',exact:true}).click();
}
test('purpose queries find the intended tools without launching them',async({page})=>{
 await login(page);await page.getByRole('button',{name:'全部工具',exact:true}).click();
 for(const [query,name] of [['找大文件','SpaceSniffer 空间分析'],['查文件占用','文件占用查询'],['批量改名','批量改文件名']]){
  await page.getByRole('textbox',{name:'搜索工具',exact:true}).fill(query);
  await expect(page.locator('.tool-card')).toHaveCount(1);
  await expect(page.locator('.tool-card')).toContainText(name);
 }
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='requestLaunch').length)).toBe(0);
});
test('all download alternatives expose source type and truthful review state',async({page})=>{
 await login(page);await page.getByRole('button',{name:'软件合集',exact:true}).click();
 await page.getByRole('textbox',{name:'搜索软件'}).fill('Everything');
 const card=page.locator('.software-card');
 await expect(card.locator('.software-primary-link')).toContainText('官网');
 await expect(card.locator('.software-primary-link')).toContainText('入口已核对');
 await card.locator('summary').click();
 await expect(card.locator('.software-alternative').filter({hasText:'360tpcdn.com'})).toContainText('第三方来源');
 await expect(card.locator('.software-alternative').filter({hasText:'360tpcdn.com'})).toContainText('待核对');
 await page.getByRole('textbox',{name:'搜索软件'}).fill('Geek Uninstaller');
 await page.locator('.software-card summary').click();
 await expect(page.locator('.software-alternative').filter({hasText:'lanzoul.com'})).toContainText('网盘');
 await expect(page.locator('.software-alternative').filter({hasText:'lanzoul.com'})).toContainText('待核对');
});
test('running tools allow a download and are listed before install until they exit',async({page})=>{
 await page.addInitScript(()=>{
  window.__updateFixture={State:2,Info:{Version:'2.0.0',Size:100,Notes:'三项改进'}};
  window.__toolStates={spacesniffer:{Status:'running',Message:''}};
 });
 await login(page);await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await page.getByRole('button',{name:'下载更新',exact:true}).click();
 await expect(page.getByRole('button',{name:'取消下载',exact:true})).toBeVisible();
 await page.evaluate(()=>window.__finishDownload());
 const blockers=page.getByRole('region',{name:'安装前需要关闭的工具'});
 await expect(blockers).toContainText('SpaceSniffer 空间分析');
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeDisabled();
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='installUpdate').length)).toBe(0);
 await page.evaluate(()=>{window.__toolStates={spacesniffer:{Status:'exited',Message:''}};});
 await expect(blockers).toHaveCount(0);
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeEnabled();
 await page.getByRole('button',{name:'安装并重启',exact:true}).click();
 await expect.poll(()=>page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='installUpdate').length)).toBe(1);
});
test('an unknown tool state blocks install until polling recovers, including at the minimum window size',async({page})=>{
 await page.addInitScript(()=>{
  window.__updateFixture={State:2,Info:{Version:'2.0.0',Size:100,Notes:'三项改进'}};
  window.__toolStates={spacesniffer:{Status:'running',Message:''},'pt-power-rename':{Status:'running',Message:''}};
 });
 await page.route('**/__test/bridge.js',async route=>{
  const response=await route.fetch();const body=(await response.text()).replace("case'getToolStates':","case'getToolStates':if(window.__statusUnavailable)throw Error('状态暂时不可用');");
  await route.fulfill({response,body});
 });
 await page.setViewportSize({width:1000,height:700});
 await login(page);await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await page.getByRole('button',{name:'下载更新',exact:true}).click();await page.evaluate(()=>window.__finishDownload());
 await expect(page.getByRole('region',{name:'安装前需要关闭的工具'})).toContainText('批量改文件名');
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeInViewport();
 await page.evaluate(()=>{window.__toolStates={};window.__statusUnavailable=true;});
 await expect(page.getByText('暂时无法确认工具是否已退出，正在重新检查。确认后即可安装。')).toBeVisible();
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeDisabled();
 await page.evaluate(()=>{window.__statusUnavailable=false;});
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeEnabled({timeout:10000});
});
