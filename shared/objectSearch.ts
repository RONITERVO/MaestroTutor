// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourFact} from './behaviourCatalog';
import {validCatalogView,type CatalogRequest,type CatalogView} from './roomCatalog';
export const objectSearchFeature='objectSearch.v1';
export type ObjectSearchEntry={id:string;name:string;kind:string};
export type ObjectSearchPage={worldId:string;offset:number;total:number;next:string;entries:ObjectSearchEntry[]};
export const objectSearchKinds=()=>behaviourFact('world.objects')?.input?.properties?.kind?.enum?.filter((v):v is string=>typeof v==='string')??[];
export const objectSearchRequest=(query='',kind='all',cursor=''):CatalogRequest=>({operation:'inspect',category:'facts',capability:'world.objects',version:1,arguments:{query,kind,cursor}});
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const exact=(v:Record<string,unknown>,keys:string[])=>Object.keys(v).length===keys.length&&keys.every(k=>Object.prototype.hasOwnProperty.call(v,k));
const text=(v:unknown,max:number):v is string=>typeof v==='string'&&v.length<=max&&!/[\u0000-\u001f\u007f-\u009f]/.test(v);
const objectId=(v:unknown):v is string=>typeof v==='string'&&/^(book|maestro|[a-fA-F0-9]{32})$/.test(v);
const integer=(v:unknown):v is number=>Number.isSafeInteger(v)&&Number(v)>=0;
/** Only consume a successful native fact for the exact query that was requested. */
export function readObjectSearchPage(view:CatalogView|null|undefined,request:CatalogRequest):ObjectSearchPage|null {
 if(!validCatalogView(view)||view.operation!=='inspect'||view.category!=='facts'||view.capability!=='world.objects'||view.version!==1||!view.available||request.operation!=='inspect'||request.capability!==view.capability||request.version!==view.version||request.category!=='facts')return null;
 const args=request.arguments;
 if(!args||!view.arguments||['query','kind','cursor'].some(k=>view.arguments![k]!==args[k]))return null;
 const page=view.value;
 if(!record(page)||!exact(page,['worldId','offset','total','next','entries'])||typeof page.worldId!=='string'||!/^[a-f0-9]{32}$/.test(page.worldId)||!integer(page.offset)||!integer(page.total)||!text(page.next,65)||!Array.isArray(page.entries)||page.entries.length>3)return null;
 const kinds=objectSearchKinds();
 if(page.entries.some(e=>!record(e)||!exact(e,['id','name','kind'])||!objectId(e.id)||!text(e.name,80)||typeof e.kind!=='string'||e.kind==='all'||!kinds.includes(e.kind)))return null;
 const entries=page.entries as ObjectSearchEntry[];
 if(entries.some((e,i)=>i>0&&entries[i-1].id>=e.id)||page.offset+entries.length>page.total)return null;
 const more=page.offset+entries.length<page.total;
 if(more?(entries.length===0||!new RegExp(`^${entries[entries.length-1].id}\\.[a-f0-9]{32}$`).test(page.next)):page.next!=='')return null;
 if(!entries.length&&page.total!==0)return null;
 return page as ObjectSearchPage;
}
