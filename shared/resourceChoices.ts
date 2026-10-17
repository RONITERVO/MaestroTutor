// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourFact} from './behaviourCatalog';
import {argumentValue,schemaField,validateCapabilityValue,type CapabilitySchema,type ResourceChoice} from './capabilities';
import {validCatalogView,type CatalogRequest,type CatalogView} from './roomCatalog';
import type {DataType} from './programValues';
const record=(v:unknown):v is Record<string,unknown>=>!!v&&typeof v==='object'&&!Array.isArray(v);
const own=(v:object,k:string)=>Object.prototype.hasOwnProperty.call(v,k);
const need=(v:unknown,message:string)=>{if(!v)throw new Error(message);};
const path=(v:unknown):v is string=>typeof v==='string'&&v.split('.').length<=4&&v.split('.').every(k=>/^[a-zA-Z0-9_]{1,32}$/.test(k)&&!['__proto__','constructor','prototype'].includes(k));
const text=(v:unknown,empty=false)=>typeof v==='string'&&(empty||v.trim().length>0)&&v.length<=80&&!/[\x00-\x1f\x7f-\x9f]/.test(v);
function mutableField(schema:CapabilitySchema,key:string,lookup=false):CapabilitySchema|undefined {
 if(!path(key))return undefined;
 for(const part of key.split('.')){
  if(schema.type!=='object'||schema.oneOf||schema['x-static']||!own(schema.properties??{},part))return undefined;
  if(schema['x-discriminators']?.includes(part)||schema['x-current']?.guards.includes(part)||!lookup&&Object.values(schema['x-current']?.arguments??{}).includes(part))return undefined;
  schema=schema.properties![part];
 }
 return schema['x-static']||schema.oneOf?undefined:schema;
}
function fields(type:DataType|undefined):Record<string,DataType>|undefined {return typeof type==='object'&&'record' in type?type.record:undefined;}
/** Catalog-owned references, not a second action vocabulary. CI checks both endpoints. */
export function resourceChoices(schema:CapabilitySchema):ResourceChoice[] {
 const choices=schema['x-choices'];if(choices===undefined)return [];
 need(Array.isArray(choices)&&choices.length>0&&choices.length<=8,'Invalid resource choices');
 const used:string[]=[];
 for(const choice of choices){
  need(record(choice)&&Object.keys(choice).every(k=>['label','fact','version','id','revision','emptyLabel','lookup'].includes(k))&&text(choice.label),'Invalid resource choice metadata');
  const fact=behaviourFact(choice.fact),input=fact?.input,offset=input?.properties?.offset;
  need(fact&&fact.version===choice.version&&input?.type==='object'&&Object.keys(input.properties??{}).join(',')==='offset'&&input.required?.join(',')==='offset'&&offset?.type==='integer'&&offset.minimum===0&&Number.isSafeInteger(offset.maximum)&&offset.maximum!>=1&&offset.maximum!<=1024,'Resource choice needs a bounded paged fact');
  const output=fields(fact!.type),list=output?.entries,entry=fields(typeof list==='object'&&'list' in list?list.list:undefined);
  need(output?.total==='number'&&entry?.id==='text'&&entry?.name==='text'&&entry?.revision==='number'&&(output?.next==='number'||output?.offset==='number'&&output?.pageSize==='number'),'Resource choice fact has incompatible entries');
  if(choice.lookup!==undefined)need(choice.lookup===true&&choice.revision===undefined&&choice.emptyLabel===undefined&&Object.values(schema['x-current']?.arguments??{}).includes(choice.id),'Lookup choice must select exactly one current-value dependency');
  const id=mutableField(schema,choice.id,choice.lookup),revision=choice.revision===undefined?undefined:mutableField(schema,choice.revision);
  need(id?.type==='string'&&(choice.revision===undefined||revision?.type==='integer'),'Resource choice has an invalid destination');
  for(const key of [choice.id,...(choice.revision===undefined?[]:[choice.revision])]){
   need(!used.some(p=>p===key||p.startsWith(key+'.')||key.startsWith(p+'.')),'Resource choice destinations overlap');used.push(key);
  }
  if(choice.emptyLabel!==undefined)need(text(choice.emptyLabel)&&validateCapabilityValue('',id!)===null&&(!revision||validateCapabilityValue(0,revision)===null),'Resource choice cannot clear these fields');
 }
 return choices;
}
export interface ResourceChoiceEntry {id:string;name:string;revision:number}
export interface ResourceChoicePage {offset:number;total:number;next:number|null;entries:ResourceChoiceEntry[]}
function declared(schema:CapabilitySchema,choice:ResourceChoice){need(resourceChoices(schema).some(c=>JSON.stringify(c)===JSON.stringify(choice)),'Unknown resource choice');}
export function resourceChoiceRequest(schema:CapabilitySchema,choice:ResourceChoice,offset:number):CatalogRequest {
 declared(schema,choice);const input=behaviourFact(choice.fact)!.input!;
 need(validateCapabilityValue({offset},input)===null,'Invalid resource page');
 return {operation:'inspect',category:'facts',capability:choice.fact,version:choice.version,arguments:{offset}};
}
export function readResourceChoicePage(schema:CapabilitySchema,choice:ResourceChoice,offset:number,view:CatalogView):ResourceChoicePage {
 const request=resourceChoiceRequest(schema,choice,offset);
 need(validCatalogView(view)&&view.operation==='inspect'&&view.category==='facts','Invalid resource list response');
 if(view.operation!=='inspect'||view.category!=='facts'||request.operation!=='inspect')throw new Error('Expected a resource fact');
 need(view.capability===request.capability&&view.version===request.version&&JSON.stringify(view.arguments)===JSON.stringify(request.arguments),'Resource list belongs to another request');
 need(view.available,view.status||'Saved resources are unavailable');
 const value=view.value;need(record(value),'Resource list is missing');
 const result=value as Record<string,unknown>,total=result.total as number,entries=result.entries as ResourceChoiceEntry[];
 const maximum=behaviourFact(choice.fact)!.input!.properties!.offset.maximum!;
 need(Number.isSafeInteger(total)&&total>=0&&total<=maximum&&Array.isArray(entries)&&entries.length<=16&&entries.length<=Math.max(0,total-offset),'Invalid resource page bounds');
 need(entries.length>0||offset>=total,'Resource list made no progress');
 need(new Set(entries.map(e=>e.id)).size===entries.length,'Duplicate resource identity');
 for(const entry of entries)validateEntry(schema,choice,entry);
 const next=offset+entries.length<total?offset+entries.length:null;
 if(own(result,'next'))need(result.next===(next??-1),'Invalid next resource page');
 if(own(result,'offset'))need(result.offset===offset,'Wrong resource page offset');
 if(own(result,'pageSize'))need(Number.isSafeInteger(result.pageSize)&&(result.pageSize as number)>=1&&(result.pageSize as number)<=16&&entries.length===Math.min(result.pageSize as number,Math.max(0,total-offset)),'Invalid resource page size');
 return {offset,total,next,entries:entries.map(e=>({id:e.id,name:e.name,revision:e.revision}))};
}
function validateEntry(schema:CapabilitySchema,choice:ResourceChoice,entry:ResourceChoiceEntry){
 need(record(entry)&&typeof entry.id==='string'&&entry.id.length>0&&entry.id.length<=128&&text(entry.name,true)&&Number.isSafeInteger(entry.revision)&&entry.revision>0,'Invalid saved resource');
 need(validateCapabilityValue(entry.id,schemaField(schema,choice.id)!)===null&&(!choice.revision||validateCapabilityValue(entry.revision,schemaField(schema,choice.revision)!)===null),'Resource does not fit the action');
}
/** One synchronous draft edit. Never reads, runs, retries, or updates other fields. */
export function applyResourceChoice(schema:CapabilitySchema,choice:ResourceChoice,value:unknown,entry:ResourceChoiceEntry|null):Record<string,unknown> {
 declared(schema,choice);need(record(value),'Choose action inputs first');
 if(entry)validateEntry(schema,choice,entry);else need(choice.emptyLabel!==undefined,'This resource is required');
 const next=structuredClone(value) as Record<string,unknown>;
 const set=(key:string,v:unknown)=>{const parts=key.split('.');let node=next;for(const part of parts.slice(0,-1)){need(record(node[part])&&own(node,part),'Resource destination is missing');node=node[part] as Record<string,unknown>;}need(own(node,parts[parts.length-1]),'Resource field is missing');node[parts[parts.length-1]]=v;};
 set(choice.id,entry?.id??'');if(choice.revision)set(choice.revision,entry?.revision??0);
 return next;
}
export function resourceChoiceKey(choice:ResourceChoice,value:unknown):string {return JSON.stringify([argumentValue(value,choice.id),choice.revision?argumentValue(value,choice.revision):null]);}
