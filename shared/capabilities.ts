// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {parseRecipe} from './roomRecipe';
import {behaviourCatalog,type BehaviourValueType} from './behaviourCatalog';
export interface CapabilitySchema {
 type:'object'|'array'|'string'|'number'|'integer'|'boolean';
 items?:CapabilitySchema;minItems?:number;maxItems?:number;nullable?:boolean;
 properties?:Record<string,CapabilitySchema>;required?:string[];additionalProperties?:false;
 format?:'unitQuaternion'|'boundedOffset'|'roomRecipe';'x-resource'?:'object';'x-requires'?:Record<string,string>;
 minimum?:number;maximum?:number;maxLength?:number;pattern?:string;enum?:string[];
}
export interface CapabilityDefinition {
 id:string;version:number;label:string;description?:string;input:CapabilitySchema;output?:CapabilitySchema;example?:Record<string,unknown>;
 duration:string;ownership:string;channels:string[];requirements:string[];
}
export interface CapabilityInvocation {id:string;version:number;arguments:Record<string,unknown>}
const clone=<T>(value:T):T=>JSON.parse(JSON.stringify(value));
// Authoring receives copies, so editing a schema cannot change validation.
const definitions=new Map((clone(behaviourCatalog.actions) as unknown as CapabilityDefinition[]).map(value=>[value.id,value]));
const record=(value:unknown):value is Record<string,unknown>=>value!==null&&typeof value==='object'&&!Array.isArray(value);
const own=(value:object,key:string)=>Object.prototype.hasOwnProperty.call(value,key);
export function capabilityDefinition(id:string):CapabilityDefinition|null {const value=definitions.get(id);return value?clone(value):null;}
function validate(value:unknown,schema:CapabilitySchema,path:string):string|null {
 const error=path+' does not match the capability contract';
 if(value===null&&schema.nullable)return null;
 switch(schema.type) {
  case 'object': {
   if(!record(value))return error;const properties=schema.properties??{};
   if((schema.required??[]).some(key=>!own(value,key))||Object.keys(value).some(key=>!own(properties,key)))return error;
   for(const [key,entry] of Object.entries(value)){
    const error=validate(entry,properties[key],path+'.'+key);if(error)return error;
    if(Object.entries(properties[key]['x-requires']??{}).some(([field,expected])=>value[field]!==expected))return path+'.'+key+' has incompatible arguments';
   }
   if(schema.format==='roomRecipe')return parseRecipe(value)?null:path+' is an invalid construction recipe';
   if(schema.format){const norm=Object.values(value).reduce<number>((sum,x)=>sum+Number(x)**2,0);
    if(schema.format==='boundedOffset'&&norm>1||schema.format==='unitQuaternion'&&Math.abs(norm-1)>=.01)return path+' has an invalid length';}
   return null;
  }
  case 'array': {
   if(!Array.isArray(value)||value.length<(schema.minItems??0)||value.length>(schema.maxItems??0)||!schema.items)return error;
   for(let i=0;i<value.length;i++){const failure=validate(value[i],schema.items,path+'['+i+']');if(failure)return failure;}return null;
  }
  case 'string':return typeof value==='string'&&!/[\u0000-\u001f\u007f-\u009f]/.test(value)&&
   (schema.maxLength===undefined||value.length<=schema.maxLength)&&(!schema.pattern||new RegExp(schema.pattern).test(value))&&(!schema.enum||schema.enum.includes(value))?null:error;
  case 'boolean':return typeof value==='boolean'?null:error;
  case 'number':case 'integer':return typeof value==='number'&&Number.isFinite(value)&&
   (schema.minimum===undefined||value>=schema.minimum)&&(schema.maximum===undefined||value<=schema.maximum)&&(schema.type!=='integer'||Number.isInteger(value))?null:error;
 }
}
/** Structural authoring check. The connected native handler remains authoritative
 * for domain constraints, target availability, model/rig compatibility and ownership.
 */
export function validateCapabilityArguments(id:string,version:number,args:unknown):string|null {
 const definition=definitions.get(id);
 if(!definition||definition.version!==version)return 'Unknown capability or unsupported capability version';
 return validate(args,definition.input,'arguments');
}
export function validCapabilityInvocation(value:unknown):value is CapabilityInvocation {
 return record(value)&&Object.keys(value).length===3&&['id','version','arguments'].every(key=>own(value,key))&&typeof value.id==='string'&&
 typeof value.version==='number'&&validateCapabilityArguments(value.id,value.version,value.arguments)===null;
}
export function validateCapabilityOutput(id:string,version:number,output:unknown):string|null {
 const definition=definitions.get(id);
 if(!definition||definition.version!==version||!definition.output)return 'Unknown action output contract';
 return validate(output,definition.output,'result');
}
export function capabilityOutputType(id:string,key:string):BehaviourValueType|null {
 const schema=definitions.get(id)?.output?.properties;
 if(!schema||!own(schema,key))return null;
 const type=schema[key].type;return type==='string'?'text':type==='integer'?'number':type==='number'||type==='boolean'?type:null;
}
export function literalCapabilityResources(id:string,args:Record<string,unknown>,bindings:Record<string,unknown>,version:number):string[] {
 const literal={...args},schema=definitions.get(id)?.input.properties;
 if(version===3)for(const key of Object.keys(bindings))if(schema?.[key]?.['x-resource']==='object')delete literal[key];
 return capabilityResources(id,literal);
}
export function capabilityParameterType(id:string,parameter:string):BehaviourValueType|null {
 const schema=definitions.get(id)?.input.properties;
 if(!schema||!own(schema,parameter))return null;
 const type=schema[parameter].type;return type==='string'?'text':type==='integer'?'number':type==='number'||type==='boolean'?type:null;
}

/** Schema-declared object references used for ownership and optimistic revisions. */
export function capabilityResources(id:string,args:Record<string,unknown>):string[] {
 const result=new Set<string>();
 const visit=(value:unknown,schema:CapabilitySchema|undefined)=>{
  if(!schema)return;if(schema['x-resource']==='object'&&typeof value==='string')result.add(value);
  if(Array.isArray(value))for(const entry of value)visit(entry,schema.items);
  if(record(value))for(const [key,entry] of Object.entries(value))visit(entry,schema.properties?.[key]);
 };visit(args,definitions.get(id)?.input);return [...result];
}
