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


export interface CurrentInputLocation {
 /** Existing literal argument path. Numeric entries are stable array indexes for this draft. */
 path:(string|number)[];schema:CapabilitySchema;value:unknown;mapping:CurrentInputMapping;selection:unknown[];
}
/** Discover native annotations in the selected variants and present literal members. */
export function currentInputLocations(schema:CapabilitySchema,value:unknown):CurrentInputLocation[] {
 const result:CurrentInputLocation[]=[],hasMapping=new WeakMap<CapabilitySchema,boolean>();
 const contains=(s:CapabilitySchema):boolean=>{
  const known=hasMapping.get(s);if(known!==undefined)return known;
  const yes=!!s['x-current']||(s.oneOf??[]).some(contains)||Object.values(s.properties??{}).some(contains)||!!s.items&&contains(s.items);hasMapping.set(s,yes);return yes;
 };
 const visit=(s:CapabilitySchema,v:unknown,path:(string|number)[],ancestors:unknown[])=>{
  if(!contains(s))return;need(path.length<=12,'Current-input nesting exceeds 12');
  const selected=resolveCapabilitySchema(s,v);if(!selected)return;
  const selection=s.oneOf?[...ancestors,[path,(s['x-discriminators']??[]).map(p=>argumentValue(v,p))]]:ancestors;
  const mapping=currentInputMapping(s,v);
  if(mapping){need(result.length<32,'Read at most 32 current-input snapshots per action');result.push({path,schema:s,value:v,mapping,selection});}
  if(selected.type==='object'&&record(v))for(const [key,child] of Object.entries(selected.properties??{}))if(own(v,key))visit(child,v[key],[...path,key],selection);
  if(selected.type==='array'&&Array.isArray(v)&&selected.items){need(v.length<=(selected.maxItems??0),'Current-input list exceeds its contract');v.forEach((entry,i)=>visit(selected.items!,entry,[...path,i],selection));}
 };
 visit(schema,value,[],[]);return result;
}
export function currentInputFieldPath(location:CurrentInputLocation,key:string):string {return [...location.path,key].join('.');}
export function currentInputFieldLabel(path:string):string {return path.split('.').map(p=>/^(0|[1-9][0-9]*)$/.test(p)?String(Number(p)+1):p).join(' ');}
export function currentInputFields(schema:CapabilitySchema,value:unknown):{path:string;guard:boolean;location:CurrentInputLocation;field:string}[] {
 return currentInputLocations(schema,value).flatMap(location=>Object.keys(location.mapping.fields).map(field=>({path:currentInputFieldPath(location,field),guard:location.mapping.guards.includes(field),location,field})));
}
/** One reviewed draft identity, including membership/order and each member's guards. */
export function currentInputsIdentity(schema:CapabilitySchema,value:unknown,session:string):string {
 const locations=currentInputLocations(schema,value);if(!locations.length)return '';
 return JSON.stringify(locations.map(l=>[l.path,l.selection,currentInputIdentity(l.schema,l.value,session)]));
}
/** Validate every response before copying any value. Reads never refresh at execution time. */
export function applyCurrentInputSnapshots(schema:CapabilitySchema,value:unknown,views:CatalogView[]):Record<string,unknown> {
 const locations=currentInputLocations(schema,value);need(record(value)&&locations.length>0&&locations.length===views.length,'Current-input responses do not match this draft');
 const replacements=locations.map((l,i)=>({path:l.path,value:applyCurrentInputs(l.schema,l.value,views[i])}));
 const next=structuredClone(value) as Record<string,unknown>;
 // Mappings replace their own scalar fields only, preserving nested replacements and edited preferences.
 for(let i=0;i<replacements.length;i++){
  let target:unknown=next;for(const part of replacements[i].path)target=typeof part==='number'&&Array.isArray(target)?target[part]:record(target)?target[part]:undefined;
  need(record(target),'Current-input location is no longer present');
  for(const key of Object.keys(locations[i].mapping.fields))(target as Record<string,unknown>)[key]=replacements[i].value[key];
 }
 return next;
}
