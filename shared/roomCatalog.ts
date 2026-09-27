// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {capabilityDefinition,type CapabilityDefinition,type CapabilityInvocation} from './capabilities';
export type CatalogRequest={operation:'search';query:string;offset:number}|{operation:'inspect';capability:string;version:number}|{operation:'check';call:CapabilityInvocation};
export type CatalogView=(
 {operation:'search';query:string;offset:number;pageSize:number;total:number;entries:{id:string;version:number;label:string}[]}|
 {operation:'inspect';capability:string;version:number;definition:CapabilityDefinition|null}|
 {operation:'check';call:CapabilityInvocation;valid:boolean;available:boolean;occupied:boolean;resources:string[]}
)&{status:string};
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const exact=(v:Record<string,unknown>,keys:string[])=>Object.keys(v).length===keys.length&&keys.every(k=>Object.prototype.hasOwnProperty.call(v,k));
const text=(v:unknown,max:number):v is string=>typeof v==='string'&&v.length<=max&&!/[\u0000-\u001f\u007f-\u009f]/.test(v);
const integer=(v:unknown,min=0):v is number=>typeof v==='number'&&Number.isInteger(v)&&v>=min&&v<=1000000;
const id=(v:unknown)=>text(v,96)&&/^[a-z][a-zA-Z0-9]*(\.[a-z][a-zA-Z0-9]*)+$/.test(v);
const call=(v:unknown):v is CapabilityInvocation=>{
 if(!record(v)||!exact(v,['id','version','arguments'])||!id(v.id)||!integer(v.version,1)||!record(v.arguments)||JSON.stringify(v.arguments).length>8000)return false;
 let count=0;
 const bounded=(v:unknown,depth:number):boolean=>{
  if(++count>128||depth>8)return false;
  if(record(v))return Object.entries(v).every(([key,x])=>text(key,80)&&bounded(x,depth+1));
  if(Array.isArray(v))return v.length<=64&&v.every(x=>bounded(x,depth+1));
  return v===null||typeof v==='boolean'||typeof v==='number'&&Number.isFinite(v)&&Math.abs(v)<=1000000||text(v,128);
 };return bounded(v.arguments,0);
};
export function validCatalogRequest(v:unknown):v is CatalogRequest {
 if(!record(v))return false;
 if(v.operation==='search')return exact(v,['operation','query','offset'])&&text(v.query,80)&&integer(v.offset);
 if(v.operation==='inspect')return exact(v,['operation','capability','version'])&&id(v.capability)&&integer(v.version,1);
 return v.operation==='check'&&exact(v,['operation','call'])&&call(v.call);
}
function equal(a:unknown,b:unknown):boolean {
 if(a===b)return true;
 if(Array.isArray(a)&&Array.isArray(b))return a.length===b.length&&a.every((v,i)=>equal(v,b[i]));
 if(record(a)&&record(b))return exact(a,Object.keys(b))&&Object.entries(a).every(([key,v])=>equal(v,b[key]));
 return false;
}
export function validCatalogView(v:unknown):v is CatalogView {
 if(!record(v)||!text(v.status,2048))return false;
 if(v.operation==='search')return exact(v,['operation','query','offset','pageSize','total','entries','status'])&&text(v.query,80)&&integer(v.offset)&&integer(v.total)&&v.pageSize===6&&Array.isArray(v.entries)&&v.entries.length<=6&&v.entries.every(x=>record(x)&&exact(x,['id','version','label'])&&id(x.id)&&integer(x.version,1)&&text(x.label,128))&&new Set(v.entries.map(x=>x.id)).size===v.entries.length&&v.offset+v.entries.length<=v.total;
 if(v.operation==='inspect'){
  if(!exact(v,['operation','capability','version','definition','status'])||!id(v.capability)||!integer(v.version,1))return false;
  if(v.definition===null)return true;const known=capabilityDefinition(v.capability as string);
  // Native and its bundled web client must agree on a definition. Reordered
  // JSON keys are harmless; changed fields are not silently reinterpreted.
  return known!==null&&known.version===v.version&&equal(v.definition,known);
 }
 return v.operation==='check'&&exact(v,['operation','call','valid','available','occupied','resources','status'])&&call(v.call)&&
  ['valid','available','occupied'].every(k=>typeof v[k]==='boolean')&&(!v.available||v.valid===true&&!v.occupied)&&Array.isArray(v.resources)&&v.resources.length<=16&&
  v.resources.every(x=>typeof x==='string'&&/^(maestro|book|[a-fA-F0-9]{32})$/.test(x))&&new Set(v.resources).size===v.resources.length;
}
