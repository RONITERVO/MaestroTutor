// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Numeric magnitude does not bound execution cost. Keep exact integers through the
// JS/double safe range; action schemas still impose their own physical limits.
import {programRecordFieldLimit} from './programLimits';
export const MAX_PROGRAM_NUMBER=Number.MAX_SAFE_INTEGER;
export const validProgramNumber=(value:unknown):value is number=>typeof value==='number'&&Number.isFinite(value)&&Math.abs(value)<=MAX_PROGRAM_NUMBER;
export type ScalarType='number'|'boolean'|'text';
export type DataType=ScalarType|{list:DataType}|{record:Record<string,DataType>};
export type DataValue=number|boolean|string|DataValue[]|{[key:string]:DataValue};
const object=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const name=(v:string)=>/^[a-zA-Z0-9_]{1,32}$/.test(v)&&!['__proto__','constructor','prototype'].includes(v);
const need=(v:unknown,message:string)=>{if(!v)throw new Error(message);};
export function readDataType(v:unknown,depth=0):DataType {
 need(depth<=4,'Value type nesting exceeds 4');
 if(typeof v==='string'){need(['number','boolean','text'].includes(v),'Unknown value type');return v as ScalarType;}
 need(object(v)&&Object.keys(v).length===1,'Expected a value type');const value=v as Record<string,unknown>;
 if(Object.prototype.hasOwnProperty.call(value,'list'))return {list:readDataType(value.list,depth+1)};
 need(object(value.record)&&Object.keys(value.record).length<=programRecordFieldLimit,`A record type needs up to ${programRecordFieldLimit} fields`);
 const fields=value.record as Record<string,unknown>;return {record:Object.fromEntries(Object.keys(fields).sort().map(k=>{need(name(k),'Invalid record field');return [k,readDataType(fields[k],depth+1)];}))};
}
export const sameDataType=(a:DataType|'void',b:DataType|'void'):boolean=>JSON.stringify(a)==JSON.stringify(b)||typeof a!=='string'&&typeof b!=='string'&&JSON.stringify(readDataType(a))===JSON.stringify(readDataType(b));
export function inferDataType(value:unknown,depth=0):DataType {
 need(depth<=4,'Value nesting exceeds 4');
 if(typeof value==='number')return 'number';if(typeof value==='boolean')return 'boolean';if(typeof value==='string')return 'text';
 if(Array.isArray(value)){need(value.length>0,'An empty list needs an explicit type');const list=inferDataType(value[0],depth+1);need(value.every(v=>sameDataType(list,inferDataType(v,depth+1))),'List items have different types');return {list};}
 need(object(value),'Expected a bounded value');return readDataType({record:Object.fromEntries(Object.entries(value as Record<string,unknown>).map(([k,v])=>[k,inferDataType(v,depth+1)]))},depth);
}
export function checkedDataValue(value:unknown,declared?:unknown):DataType {
 const type=declared===undefined?inferDataType(value):readDataType(declared);let nodes=0;
 const check=(v:unknown,t:DataType,depth:number)=>{
  need(depth<=4&&++nodes<=128,'Value nesting or node limit exceeded');
  if(typeof t==='string'){need(t==='number'?validProgramNumber(v):t==='boolean'?typeof v==='boolean':typeof v==='string'&&v.length<=128&&!/[\u0000-\u001f\u007f-\u009f]/.test(v),'Expected bounded number, boolean or text');return;}
  if('list' in t){need(Array.isArray(v)&&v.length<=32,'Expected a list of at most 32 items');(v as unknown[]).forEach(item=>check(item,t.list,depth+1));return;}
  need(object(v)&&Object.keys(v).length===Object.keys(t.record).length&&Object.keys(t.record).every(k=>Object.prototype.hasOwnProperty.call(v,k)),'Record fields differ from its type');
  Object.entries(t.record).forEach(([k,t])=>check((v as Record<string,unknown>)[k],t,depth+1));
 };
 check(value,type,0);need(dataValueCost(value as DataValue)<=1024,'Value exceeds its 1024 character budget');return type;
}
export function defaultDataValue(type:DataType):DataValue {
 return typeof type==='string'?type==='number'?0:type==='boolean'?false:'':'list' in type?[]:Object.fromEntries(Object.entries(type.record).map(([k,t])=>[k,defaultDataValue(t)]));
}
export function dataTypeLabel(type:DataType|'void'):string{return typeof type==='string'?type:'list' in type?'list of '+dataTypeLabel(type.list):'record {'+Object.entries(type.record).map(([k,t])=>k+': '+dataTypeLabel(t)).join(', ')+'}';}
export function dataOperationType(op:string,types:DataType[],literalField?:unknown):DataType|null {
 const a=types[0];if(!['length','at','append','replace','remove','field','withField'].includes(op))return null;
 if(op==='field'||op==='withField'){
  need(typeof a==='object'&&'record' in a&&typeof literalField==='string'&&Object.prototype.hasOwnProperty.call(a.record,literalField),'Choose a literal record field');
  const t=(a as {record:Record<string,DataType>}).record[literalField as string];need(types.length===(op==='field'?2:3)&&types[1]==='text'&&(op==='field'||sameDataType(types[2],t)),'Record operation types differ');return op==='field'?t:a;
 }
 need(typeof a==='object'&&'list' in a,'List operation needs a list');const item=(a as {list:DataType}).list;
 need(types.length===(op==='length'?1:op==='replace'?3:2),'Invalid list operation arguments');
 if(op==='append')need(sameDataType(types[1],item),'List item type differs');
 else if(op!=='length')need(types[1]==='number','List index needs a number');
 if(op==='replace')need(sameDataType(types[2],item),'List item type differs');
 return op==='length'?'number':op==='at'?item:a;
}

/** Upper bound on compact JSON length, identical across JS and native number formatting. */
export function dataValueCost(value:DataValue):number {
 if(typeof value==='number')return 30;if(typeof value==='boolean')return 5;
 if(typeof value==='string')return JSON.stringify(value).replace(/[\u2028\u2029]/g,'XXXXXX').length;
 const entries=Array.isArray(value)?value.map(dataValueCost):Object.entries(value).map(([k,v])=>JSON.stringify(k).length+1+dataValueCost(v));
 return 2+Math.max(0,entries.length-1)+entries.reduce((a,b)=>a+b,0);
}

export function validDataObservation(text:string,kind:'list'|'record'):boolean {
 try {
  const root:unknown=JSON.parse(text);if(kind==='list'?!Array.isArray(root):!object(root))return false;
  let nodes=0;const check=(v:unknown,depth:number):boolean=>{
   if(depth>4||++nodes>128)return false;
   if(Array.isArray(v))return v.length<=32&&v.every(x=>check(x,depth+1));
   if(object(v))return Object.keys(v).length<=programRecordFieldLimit&&Object.entries(v).every(([k,x])=>name(k)&&check(x,depth+1));
   checkedDataValue(v);return true;
  };
  return check(root,0)&&dataValueCost(root as DataValue)<=1024;
 }catch{return false;}
}
