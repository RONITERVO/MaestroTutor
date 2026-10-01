// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourFact} from './behaviourCatalog';
import {validateFactArguments} from './behaviourFacts';
import {argumentValue,resolveCapabilitySchema,validateCapabilityValue,type CapabilitySchema,type CurrentInputMapping} from './capabilities';
import {validCatalogView,type CatalogRequest,type CatalogView} from './roomCatalog';
import type {DataType} from './programValues';
const record=(v:unknown):v is Record<string,unknown>=>!!v&&typeof v==='object'&&!Array.isArray(v);
const own=(v:object,k:string)=>Object.prototype.hasOwnProperty.call(v,k);
const name=(v:unknown):v is string=>typeof v==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(v)&&!['__proto__','constructor','prototype'].includes(v);
const need=(v:unknown,message:string)=>{if(!v)throw new Error(message);};
const scalar=(schema:CapabilitySchema|undefined)=>schema?.type==='string'?'text':schema?.type==='integer'?'number':schema?.type;
/** Metadata is shipped in the native manifest. Validate references as well as shape in CI. */
export function validateCurrentInputMapping(schema:CapabilitySchema):void {
 const m=schema['x-current'];if(!m)return;
 need(record(m)&&Object.keys(m).sort().join(',')==='arguments,fact,fields,guards,version','Invalid current-input metadata');
 const fact=behaviourFact(m.fact);need(fact&&fact.version===m.version,'Unknown current-input fact');
 need(record(m.arguments)&&record(m.fields)&&Array.isArray(m.guards),'Invalid current-input maps');
 need(Object.keys(m.arguments).length<=8&&Object.keys(m.fields).length>0&&Object.keys(m.fields).length<=8,'Current-input map exceeds its bounds');
 for(const [key,source] of Object.entries(m.arguments)){
  need(name(key)&&name(source)&&own(fact!.input?.properties??{},key)&&own(schema.properties??{},source),'Unknown current-input dependency');
  need(scalar(fact!.input?.properties?.[key])===scalar(schema.properties?.[source]),'Current-input dependency type differs');
  need(!own(m.fields,source),'Current-input reads cannot replace their own dependencies');
 }
 need((fact!.input?.required??[]).every(key=>own(m.arguments,key)),'Missing current-input dependency');
 for(const [key,path] of Object.entries(m.fields)){
  need(name(key)&&own(schema.properties??{},key)&&!schema.properties![key]['x-static'],'Unknown or static current-input field');
  need(Array.isArray(path)&&path.length>0&&path.length<=4&&path.every(name),'Invalid current-input fact path');
  let type:DataType|undefined=fact!.type;
  for(const part of path)type=typeof type==='object'&&'record' in type&&own(type.record,part)?type.record[part]:undefined;
  need(type!==undefined&&typeof type==='string'&&type===scalar(schema.properties![key]),'Current-input fact type differs');
 }
 need(m.guards.length<=8&&new Set(m.guards).size===m.guards.length&&m.guards.every(key=>name(key)&&own(m.fields,key)),'Unknown current-input guard');
}
export function currentInputMapping(schema:CapabilitySchema,value:unknown):CurrentInputMapping|undefined {
 const selected=resolveCapabilitySchema(schema,value);if(!selected?.['x-current'])return undefined;
 validateCurrentInputMapping(selected);return selected['x-current'];
}
export function currentInputRequest(schema:CapabilitySchema,value:unknown):CatalogRequest {
 const m=currentInputMapping(schema,value);need(m&&record(value),'Choose the action inputs first.');
 const args=Object.fromEntries(Object.entries(m!.arguments).map(([key,source])=>[key,(value as Record<string,unknown>)[source]]));
 const input=Object.keys(args).length?args:undefined;
 need(validateFactArguments(m!.fact,m!.version,input)===null,'Choose valid inputs for the current-value lookup.');
 return {operation:'inspect',category:'facts',capability:m!.fact,version:m!.version,...(input?{arguments:input}:{})};
}
/** Copy atomically, from the exact requested fact. A failed read never fills fallback values. */
export function applyCurrentInputs(schema:CapabilitySchema,value:unknown,view:CatalogView):Record<string,unknown> {
 const query=currentInputRequest(schema,value),m=currentInputMapping(schema,value)!;
 need(view.operation==='inspect'&&view.category==='facts'&&validCatalogView(view),'Invalid current-value response.');
 if(view.operation!=='inspect'||view.category!=='facts'||query.operation!=='inspect')throw new Error('Expected a fact snapshot.');
 need(view.capability===query.capability&&view.version===query.version&&JSON.stringify(view.arguments)===JSON.stringify(query.arguments),'Current-value response belongs to different inputs.');
 need(view.available,view.status||'Current values are unavailable.');
 const next={...value as Record<string,unknown>},selected=resolveCapabilitySchema(schema,value)!;
 for(const [key,path] of Object.entries(m.fields)){
  let entry:unknown=view.value;
  for(const part of path)entry=record(entry)&&own(entry,part)?entry[part]:undefined;
  need(validateCapabilityValue(entry,selected.properties![key])===null,'Current '+key+' cannot be used for this action.');
  next[key]=entry;
 }
 return next;
}

/** Identity of a reviewed snapshot, excluding the preferences the user is editing. */
export function currentInputIdentity(schema:CapabilitySchema,value:unknown,session:string):string {
 const mapping=currentInputMapping(schema,value);if(!mapping)return '';
 return JSON.stringify([session,currentInputRequest(schema,value),mapping.guards.map(key=>(value as Record<string,unknown>)[key]),(schema['x-discriminators']??[]).map(path=>argumentValue(value,path))]);
}
