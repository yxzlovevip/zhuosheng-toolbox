// Source ownership and reachability are separate. Importing a catalog is never a link check.
const cloudDomains=['lanzoul.com','lanzout.com','lanzouh.com','ilanzou.com','feijipan.com'];
const within=(host,domain)=>host===domain||host.endsWith('.'+domain);
export function describeSoftwareLink(software,link,registry={}){
 const url=new URL(link.url),host=url.hostname.toLowerCase();
 const review=registry.entries?.[url.href];
 const matchingReview=review?.software===software?review:null;
 const cloud=cloudDomains.some(domain=>within(host,domain))||
  (['pan.baidu.com','pan.quark.cn','123pan.com','123684.com','123912.com'].some(domain=>within(host,domain))&&url.pathname.startsWith('/s/'));
 const publisher=(registry.publishers||[]).some(rule=>rule.names.includes(software)&&rule.hosts.some(domain=>within(host,domain)));
 const sourceType=matchingReview?.sourceType||(cloud?'cloud':publisher?'official':'unverified');
 const verification=matchingReview?.verification||{status:'pending',checkedAt:null};
 if(!['official','cloud','third-party','unverified'].includes(sourceType)||!['pending','checked','unavailable'].includes(verification.status))throw Error('下载入口核对记录类型无效：'+url.href);
 if(verification.status!=='pending'&&(!/^\d{4}-\d{2}-\d{2}$/.test(verification.checkedAt||'')||!verification.evidenceUrl))throw Error('已核对入口缺少日期或依据：'+url.href);
 if(verification.status==='pending'&&verification.checkedAt!=null)throw Error('待核对入口不能填写核对日期：'+url.href);
 return {sourceType,verification:{...verification}};
}
