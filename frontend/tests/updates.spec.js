import {test,expect} from '@playwright/test';

async function login(page){
 await page.goto('/');
 await page.getByRole('button',{name:'我已了解，进入工具箱'}).click();
 await page.locator('#password').fill('ui-test-only-password');
 await page.getByRole('button',{name:'登录',exact:true}).click();
 await page.getByRole('button',{name:'全部工具',exact:true}).click();
}
test('checks automatically once per session, notifies and downloads before installation',async({page})=>{
 await page.addInitScript(()=>{window.__updateFixture={State:2,Info:{Version:'1.3.0',Size:104857600,Notes:'新版说明'}};});
 await login(page);
 await expect(page.getByRole('button',{name:'查看新版本 1.3.0'})).toBeVisible();
 await page.getByRole('button',{name:'查看新版本 1.3.0'}).click();
 await expect(page.getByText('新版说明',{exact:true})).toBeVisible();
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toHaveCount(0);
 await page.getByRole('button',{name:'下载更新',exact:true}).click();
 await expect(page.getByRole('button',{name:'取消下载',exact:true})).toBeVisible();
 await expect(page.getByRole('progressbar')).toHaveAttribute('aria-valuenow','50');
 await page.evaluate(()=>window.__finishDownload());
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeEnabled();
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='checkUpdate').length)).toBe(1);
 await page.getByRole('button',{name:'安装并重启',exact:true}).click();
 await expect.poll(()=>page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='installUpdate').length)).toBe(1);
});
test('cancelled download can be retried and never exposes install',async({page})=>{
 await page.addInitScript(()=>{window.__updateFixture={State:2,Info:{Version:'1.3.0',Size:100,Notes:'新版说明'}};});
 await login(page);await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await page.getByRole('button',{name:'下载更新',exact:true}).click();
 await page.getByRole('button',{name:'取消下载',exact:true}).click();
 await expect(page.getByRole('heading',{name:'下载已取消'})).toBeVisible();
 await expect(page.getByRole('button',{name:'下载更新',exact:true})).toBeEnabled();
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toHaveCount(0);
});
test('offline checks report failure and retry succeeds without blocking tools',async({page})=>{
 await page.addInitScript(()=>{window.__updateError='网络连接失败';});
 await login(page);await expect(page.locator('.tool-card').first()).toBeVisible();
 await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await expect(page.getByRole('heading',{name:'检查更新失败'})).toBeVisible();
 await expect(page.getByText('网络连接失败',{exact:false})).toBeVisible();
 await page.evaluate(()=>{window.__updateError=null;window.__updateFixture={State:1};});
 await page.getByRole('button',{name:'检查更新',exact:true}).click();
 await expect(page.getByRole('heading',{name:'当前已是最新版本'})).toBeVisible();
});
test('a completed old session check cannot notify a locked session',async({page})=>{
 await page.addInitScript(()=>{window.__holdUpdate=true;window.__updateFixture={State:2,Info:{Version:'1.3.0',Size:100,Notes:'新版说明'}};});
 await login(page);await expect.poll(()=>page.evaluate(()=>typeof window.__releaseUpdate)).toBe('function');
 await page.getByRole('button',{name:/锁定/}).click();
 await page.evaluate(()=>window.__releaseUpdate());
 await expect(page.locator('#password')).toBeVisible();
 await expect(page.getByRole('button',{name:'查看新版本 1.3.0'})).toHaveCount(0);
});
test('six-hour checks repeat without interrupting a verified update',async({page})=>{
 await page.clock.install();
 await page.addInitScript(()=>{window.__updateFixture={State:2,Info:{Version:'9.0.0',Size:100,Notes:'新版说明'}};});
 await login(page);
 await expect(page.getByRole('button',{name:'查看新版本 9.0.0'})).toBeVisible();
 await page.clock.fastForward(6*60*60*1000);
 await expect.poll(()=>page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='checkUpdate').length)).toBe(2);
 await page.getByRole('button',{name:'软件更新',exact:true}).click();
 await page.getByRole('button',{name:'下载更新',exact:true}).click();
 await expect.poll(()=>page.evaluate(()=>typeof window.__finishDownload)).toBe('function');
 await page.evaluate(()=>window.__finishDownload());
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeEnabled();
 await page.clock.fastForward(6*60*60*1000);
 expect(await page.evaluate(()=>window.__nativeCalls.filter(c=>c.action==='checkUpdate').length)).toBe(2);
 await expect(page.getByRole('button',{name:'安装并重启',exact:true})).toBeEnabled();
});
