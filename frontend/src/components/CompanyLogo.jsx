import companyLogo from '../assets/company-logo.jpg';

// Frame the supplied symbol without redrawing or modifying the original artwork.
export function CompanyMark({size=40}){
 return <svg className="company-mark" width={size} height={size} viewBox="300 144 156 146" aria-hidden="true" focusable="false"><image href={companyLogo} width="758" height="556"/></svg>;
}

export function CompanyLogo(){
 return <img className="company-logo" src={companyLogo} width="758" height="556" alt="卓盛网络科技 · 无限科技 无尽商机" decoding="async"/>;
}
