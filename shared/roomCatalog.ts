// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validModuleRecord,type ModuleRecord} from './programModuleIdentity';
import {capabilityDefinition,type CapabilityDefinition,type CapabilityInvocation} from './capabilities';
import {behaviourEvent,type BehaviourEventDefinition} from './behaviourEvents';
import {behaviourFact,type BehaviourFactDefinition} from './behaviourCatalog';
export type CatalogCategory='actions'|'events'|'facts'|'modules';
export type CatalogRequest={operation:'search';query:string;offset:number;category?:CatalogCategory}|{operation:'inspect';capability:string;version:number;category?:CatalogCategory}|{operation:'check';call:CapabilityInvocation};
type LibraryState={revision:number;ready:boolean;pending:boolean};
type Inspection={operation:'inspect';capability:string;version:number};
export type CatalogView=(
 {operation:'search';query:string;offset:number;pageSize:number;total:number;entries:{id:string;version:number;label:string}[];category?:CatalogCategory;revision?:number;ready?:boolean;pending?:boolean}|
 Inspection&({category?:'actions';definition:CapabilityDefinition|null}|{category:'events';definition:BehaviourEventDefinition|null}|{category:'facts';definition:BehaviourFactDefinition|null;available:boolean;value:number|boolean|string|null}|{category:'modules';definition:ModuleRecord|null}&LibraryState)|
 {operation:'check';call:CapabilityInvocation;valid:boolean;available:boolean;occupied:boolean;resources:string[]}
)&{status:string};
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const exact=(v:Record<string,unknown>,keys:string[])=>Object.keys(v).length===keys.length&&keys.every(k=>Object.prototype.hasOwnProperty.call(v,k));
const text=(v:unknown,max:number):v is string=>typeof v==='string'&&v.length<=max&&!/[\u0000-\u001f\u007f-\u009f]/.test(v);
const integer=(v:unknown,min=0):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=1000000;
const moduleId=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{64}$/.test(v);
const id=(v:unknown)=>text(v,96)&&/^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$/.test(v);
export const boundedCapabilityCall=(v:unknown):v is CapabilityInvocation=>{
 if(!record(v)||!exact(v,['id','version','arguments'])||!id(v.id)||!integer(v.version,1)||!record(v.arguments)||JSON.stringify(v.arguments).length>24000)return false;
 let count=0;
 const bounded=(v:unknown,depth:number):boolean=>{
  if(++count>4096||depth>12)return false;
  if(record(v))return Object.entries(v).every(([key,x])=>text(key,80)&&bounded(x,depth+1));
  if(Array.isArray(v))return v.length<=64&&v.every(x=>bounded(x,depth+1));
  return v===null||typeof v==='boolean'||typeof v==='number'&&Number.isFinite(v)&&Math.abs(v)<=1000000||text(v,128);
 };return bounded(v.arguments,0);
};
const queryKeys=(v:Record<string,unknown>,keys:string[])=>Object.prototype.hasOwnProperty.call(v,'category')?
 typeof v.category==='string'&&['actions','events','facts','modules'].includes(v.category)&&exact(v,[...keys,'category']):exact(v,keys);
export function validCatalogRequest(v:unknown):v is CatalogRequest {
 if(!record(v))return false;
 if(v.operation==='search')return queryKeys(v,['operation','query','offset'])&&text(v.query,80)&&integer(v.offset);
 if(v.operation==='inspect')return queryKeys(v,['operation','capability','version'])&&(v.category==='modules'?moduleId(v.capability):id(v.capability))&&integer(v.version,1);
 return v.operation==='check'&&exact(v,['operation','call'])&&boundedCapabilityCall(v.call);
}
function equal(a:unknown,b:unknown):boolean {
 if(a===b)return true;
 if(Array.isArray(a)&&Array.isArray(b))return a.length===b.length&&a.every((v,i)=>equal(v,b[i]));
 if(record(a)&&record(b))return exact(a,Object.keys(b))&&Object.entries(a).every(([key,v])=>equal(v,b[key]));
 return false;
}
export function validCatalogView(v:unknown):v is CatalogView {
 if(!record(v)||!text(v.status,2048))return false;
 if(v.category==='modules'){
  if(!integer(v.revision,1)||typeof v.ready!=='boolean'||typeof v.pending!=='boolean')return false;
  const meta=['revision','ready','pending'];
  if(v.operation==='search')return queryKeys(v,['operation','query','offset','pageSize','total','entries','status',...meta])&&text(v.query,80)&&integer(v.offset)&&integer(v.total)&&v.total<=256&&v.pageSize===6&&Array.isArray(v.entries)&&v.entries.length<=6&&v.entries.every(x=>record(x)&&exact(x,['id','version','label'])&&moduleId(x.id)&&x.version===1&&text(x.label,128))&&new Set(v.entries.map(x=>x.id)).size===v.entries.length&&v.offset+v.entries.length<=v.total;
  return v.operation==='inspect'&&queryKeys(v,['operation','capability','version','definition','status',...meta])&&moduleId(v.capability)&&integer(v.version,1)&&(v.definition===null||v.ready&&v.version===1&&validModuleRecord(v.definition,v.capability as string));
 }
 if(v.operation==='search')return queryKeys(v,['operation','query','offset','pageSize','total','entries','status'])&&text(v.query,80)&&integer(v.offset)&&integer(v.total)&&v.pageSize===6&&Array.isArray(v.entries)&&v.entries.length<=6&&v.entries.every(x=>record(x)&&exact(x,['id','version','label'])&&id(x.id)&&integer(x.version,1)&&text(x.label,128))&&new Set(v.entries.map(x=>x.id)).size===v.entries.length&&v.offset+v.entries.length<=v.total;
 if(v.operation==='inspect'){
  const keys=['operation','capability','version','definition','status',...(v.category==='facts'?['available','value']:[])];
  if(!queryKeys(v,keys)||!id(v.capability)||!integer(v.version,1))return false;
  const known=v.category==='events'?behaviourEvent(v.capability as string):v.category==='facts'?behaviourFact(v.capability as string):capabilityDefinition(v.capability as string);
  // Native and its bundled web client must agree on the exact requested category
  // and version. Reading an unavailable fact must never manufacture false/zero.
  if(v.definition!==null&&(!known||known.version!==v.version||!equal(v.definition,known)))return false;
  if(v.category!=='facts')return true;
  if(typeof v.available!=='boolean')return false;
  if(!v.available)return v.value===null;
  if(v.definition===null||!known||!('type' in known))return false;
  return known.type==='text'?text(v.value,128):known.type==='boolean'?typeof v.value==='boolean':typeof v.value==='number'&&Number.isFinite(v.value)&&Math.abs(v.value)<=1000000;
 }
 return v.operation==='check'&&exact(v,['operation','call','valid','available','occupied','resources','status'])&&boundedCapabilityCall(v.call)&&
  ['valid','available','occupied'].every(k=>typeof v[k]==='boolean')&&(!v.available||v.valid===true&&!v.occupied)&&Array.isArray(v.resources)&&v.resources.length<=16&&
  v.resources.every(x=>typeof x==='string'&&/^(maestro|book|[a-fA-F0-9]{32})$/.test(x))&&new Set(v.resources).size===v.resources.length;
}
