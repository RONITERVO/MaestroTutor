// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Cross-field eligibility after strict source/schema validation; native provider
// ownership, readiness and resource budgets remain authoritative at execution.
export interface StaticSupportSource {
 staticSupport?:{version:number;enabled:boolean};
 physics?:{mode:string;shape?:string};collision?:{shapes:unknown[]};
 modelGeometry?:{meshCollision:boolean};motion?:unknown;
}
export function validStaticSupportSource(source:StaticSupportSource,kind='recipe'):boolean {
 const support=source.staticSupport;
 if(support===undefined)return true;
 if(!support||support.version!==1||typeof support.enabled!=='boolean')return false;
 if(!support.enabled)return true;
 if((source.physics?.mode??'fixed')!=='fixed'||source.motion)return false;
 const imported=kind==='model',shape=source.physics?.shape??'automatic',shapes=source.collision?.shapes??[];
 if(imported&&(source.modelGeometry?.meshCollision||shape!=='automatic'))return false;
 if(shape==='sphere')return false;
 if(!imported&&shape==='box')return true;
 if(shape!=='automatic')return false;
 return shapes.length>0?shapes.every(s=>!!s&&['box','cylinder','ring'].includes((s as {shape:string}).shape)):!imported&&['block','drawing','recipe'].includes(kind);
}
