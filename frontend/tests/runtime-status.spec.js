import {test,expect} from '@playwright/test';

async function login(page){
 await page.goto('/');
 if(await page.locator('#startup-title').count())await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await unlock(page);
}
async function unlock(page){await page.locator('#password').fill('ui-test-only-password');await page.getByRole('button',{name:'登录',exact:true}).click();await page.getByRole('button',{name:'全部工具',exact:true}).click();}
test.beforeEach(async({page})=>{
 await page.emulateMedia({reducedMotion:'reduce'});
 await page.route('**/__test/bridge.js',async route=>{
  const response=await route.fetch();let body=await response.text();
  body=body.replace("case'requestLaunch':",`case'getToolStates':result=structuredClone(window.__toolStates||{});break;case'requestLaunch':
   await new Promise(r=>window.__releaseStatusLaunch=r);
   if(window.__launchFailure){window.__toolStates={[m.args.toolId]:{Status:'failed',Message:'工具文件缺失，请重新安装。'}};throw Error('工具文件缺失，请重新安装。');}
   window.__toolStates={[m.args.toolId]:{Status:'running',Message:''}};
   result={confirmation:false,toolStates:structuredClone(window.__toolStates)};break;case'__unusedLaunch':`);
  await route.fulfill({response,body});
 });
});
test('cards and details follow opening, running and exited across navigation and locking',async({page})=>{
 const errors=[];page.on('pageerror',error=>errors.push(error.message));
 await login(page);await expect(page).toHaveTitle('卓盛工具箱');
 const name=await page.locator('.tool-card.selected .card-select').getAttribute('aria-label');
 const card=page.locator('.tool-card').filter({has:page.getByRole('button',{name,exact:true})});
 await page.locator('.launch-button').click();
 await expect(card.locator('.tool-run-state')).toHaveText('正在打开');
 await expect(page.locator('.detail-panel .tool-run-state')).toHaveText('正在打开');
 await page.evaluate(()=>window.__releaseStatusLaunch());
 await expect(card.locator('.tool-run-state')).toHaveText('运行中');
 await page.getByRole('button',{name:'磁盘工具',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
 await expect(card.locator('.tool-run-state')).toHaveText('运行中');
 await page.getByRole('button',{name:'锁定',exact:true}).click();await unlock(page);
 await expect(card.locator('.tool-run-state')).toHaveText('运行中');
 await page.evaluate(()=>{for(const state of Object.values(window.__toolStates))state.Status='exited';});
 await expect(card.locator('.tool-run-state')).toHaveText('已退出');
 await expect(page.locator('.detail-panel .tool-run-state')).toHaveText('已退出');
 await page.getByRole('button',{name:'浅色',exact:true}).click();
 await expect(page.locator('vite-error-overlay')).toHaveCount(0);expect(errors).toEqual([]);
 await page.screenshot({path:process.env.TEMP+'/cz-runtime-status-light.png'});
 await page.setViewportSize({width:980,height:650});await page.getByRole('button',{name:'深色',exact:true}).click();
 await expect(page.locator('.detail-panel .tool-run-state')).toBeVisible();
 await page.screenshot({path:process.env.TEMP+'/cz-runtime-status-dark.png'});
});
test('failed launches retain the reason and can be retried',async({page})=>{
 await login(page);await page.evaluate(()=>window.__launchFailure=true);
 await page.locator('.launch-button').click();await page.evaluate(()=>window.__releaseStatusLaunch());
 await expect(page.locator('.detail-panel .tool-run-state')).toHaveText('启动失败');
 await expect(page.locator('.tool-run-message')).toContainText('工具文件缺失');
 await page.waitForTimeout(650);await page.evaluate(()=>window.__launchFailure=false);
 await page.locator('.launch-button').click();await page.evaluate(()=>window.__releaseStatusLaunch());
 await expect(page.locator('.detail-panel .tool-run-state')).toHaveText('运行中');
 await expect(page.locator('.tool-run-message')).toHaveCount(0);
});
