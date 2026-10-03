// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validCreationBatchGeometry} from './creationBatch';
import {readDataType,type DataType} from './programValues';
import {validCollisionRecipe} from './collisionRecipe';
import {moduleHash,validModuleRecord} from './programModuleIdentity';
import {parseRecipe,validLathePart} from './roomRecipe';
import {behaviourCatalog,type BehaviourValueType} from './behaviourCatalog';
export interface CurrentInputMapping {
 fact:string;version:number;arguments:Record<string,string>;fields:Record<string,string[]>;guards:string[];
}
export interface CapabilitySchema {
 type:'object'|'array'|'string'|'number'|'integer'|'boolean';
 'x-current'?:CurrentInputMapping;
 oneOf?:CapabilitySchema[];'x-confirmation'?:string;'x-discriminators'?:string[];title?:string;description?:string;examples?:unknown[];'x-static'?:boolean;'x-channels'?:string[];'x-requirements'?:string[];'x-features'?:string[];
 items?:CapabilitySchema;minItems?:number;maxItems?:number;nullable?:boolean;
 properties?:Record<string,CapabilitySchema>;required?:string[];additionalProperties?:false;
 format?:'unitQuaternion'|'boundedOffset'|'roomRecipe'|'lathePart'|'collisionRecipe'|'programModule'|'programMemoryValue'|'objectLayout'|'creationBatch'|'structureSource';'x-resource'?:'object';'x-requires'?:Record<string,string>;
 minimum?:number;maximum?:number;maxLength?:number;pattern?:string;enum?:string[];'x-enum-labels'?:Record<string,string>;'x-enum-images'?:Record<string,string>;
}
export interface CapabilityDefinition {
 id:string;version:number;label:string;description?:string;input:CapabilitySchema;output?:CapabilitySchema;example?:Record<string,unknown>;
 domain?:'room'|'workspace';duration:string;ownership:string;channels:string[];requirements:string[];
}
export interface CapabilityInvocation {id:string;version:number;arguments:Record<string,unknown>}
const clone=<T>(value:T):T=>JSON.parse(JSON.stringify(value));
// Authoring receives copies, so editing a schema cannot change validation.
const definitions=new Map((clone(behaviourCatalog.actions) as unknown as CapabilityDefinition[]).map(value=>[value.id,value]));
const record=(value:unknown):value is Record<string,unknown>=>value!==null&&typeof value==='object'&&!Array.isArray(value);
const own=(value:object,key:string)=>Object.prototype.hasOwnProperty.call(value,key);
export function capabilityDefinition(id:string):CapabilityDefinition|null {const value=definitions.get(id);return value?clone(value):null;}
/** Resolve literal variant selectors without treating readiness as validation. */
export function schemaField(schema:CapabilitySchema|undefined,path:string,value?:unknown):CapabilitySchema|undefined {
 for(const key of path.split('.')){schema=resolveCapabilitySchema(schema,value);schema=schema?.properties&&own(schema.properties,key)?schema.properties[key]:undefined;value=record(value)&&own(value,key)?value[key]:undefined;}return schema;
}
export function argumentValue(value:unknown,path:string):unknown {for(const key of path.split('.'))value=record(value)?value[key]:undefined;return value;}
export function resolveCapabilitySchema(schema:CapabilitySchema|undefined,value:unknown):CapabilitySchema|undefined {
 if(!schema?.oneOf)return schema;
 const matches=schema.oneOf.filter(branch=>(schema['x-discriminators']??[]).every(path=>schemaField(branch,path)?.enum?.includes(argumentValue(value,path) as string)));
 return matches.length===1?matches[0]:undefined;
}
export function capabilityInput(id:string,args:Record<string,unknown>):CapabilitySchema|undefined {return resolveCapabilitySchema(definitions.get(id)?.input,args);}
export function capabilityBindingFields(id:string,args:Record<string,unknown>):Record<string,CapabilitySchema> {
 const result:Record<string,CapabilitySchema>={};
 const visit=(schema:CapabilitySchema|undefined,path:string)=>{schema=resolveCapabilitySchema(schema,path?argumentValue(args,path):args);if(!schema||schema['x-static'])return;
  if(schema.type==='object')for(const [key,field] of Object.entries(schema.properties??{}))visit(field,path?path+'.'+key:key);
  else if(schema.type!=='array')result[path]=schema;
 };visit(capabilityInput(id,args),'');return result;
}
export function validateCapabilityValue(value:unknown,schema:CapabilitySchema):string|null {return validate(value,schema,'value');}
function validate(value:unknown,schema:CapabilitySchema,path:string):string|null {
 const error=path+' does not match the capability contract';
 if(schema.oneOf){const selected=resolveCapabilitySchema(schema,value);return selected?validate(value,selected,path):path+' has an unsupported variant';}
 if(value===null&&schema.nullable)return null;
 switch(schema.type) {
  case 'object': {
   if(!record(value))return error;
   if(schema.format==='programModule'){try{return validModuleRecord(value,moduleHash(value))?null:error;}catch{return error;}}
   const properties=schema.properties??{};
   if((schema.required??[]).some(key=>!own(value,key))||Object.keys(value).some(key=>!own(properties,key)))return error;
   for(const [key,entry] of Object.entries(value)){
    const error=validate(entry,properties[key],path+'.'+key);if(error)return error;
    if(Object.entries(properties[key]['x-requires']??{}).some(([field,expected])=>value[field]!==expected))return path+'.'+key+' has incompatible arguments';
   }
   if(schema.format==='structureSource'){
    const entries=(value.kind==='capture'?value.members:value.slots) as {slot:string;target?:string;placement?:{target:string;position:{x:number;y:number;z:number}}}[];
    return new Set(entries.map(x=>x.slot)).size===entries.length&&new Set(entries.map(x=>x.target??x.placement!.target)).size===entries.length&&entries.every(x=>!x.placement||x.placement.position.x**2+x.placement.position.y**2+x.placement.position.z**2<=625)?null:path+' needs distinct slots and valid baseline placements';
   }
   if(schema.format==='creationBatch')return validCreationBatchGeometry(value)?null:path+' needs distinct idle pieces with valid transformed placements';
   if(schema.format==='objectLayout'){
    const placements=value.placements as {target:string;position:{x:number;y:number;z:number}}[];
    return new Set(placements.map(p=>p.target)).size===placements.length&&placements.every(p=>p.position.x**2+p.position.y**2+p.position.z**2<=625)?null:path+' needs distinct objects within 25 metres';
   }
   if(schema.format==='collisionRecipe')return validCollisionRecipe(value)?null:path+' needs bounded valid collision shapes';
   if(schema.format==='lathePart')return validLathePart(value)?null:path+' needs a simple counter-clockwise lathe profile';
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
export function capabilityOutputType(id:string,key:string):DataType|null {
 const schema=definitions.get(id)?.output?.properties;
 if(!schema||!own(schema,key))return null;
 const shape=(field:CapabilitySchema|undefined,depth:number):DataType=>{
  if(!field||depth>4||field.oneOf||field.nullable)throw new Error('No fixed output type');
  if(field.type==='string')return 'text';if(field.type==='number'||field.type==='integer')return 'number';if(field.type==='boolean')return 'boolean';
  if(field.type==='array')return {list:shape(field.items,depth+1)};
  if(!field.properties||field.required?.length!==Object.keys(field.properties).length)throw new Error('Optional record fields are not program values');
  return {record:Object.fromEntries(Object.entries(field.properties).map(([k,v])=>[k,shape(v,depth+1)]))};
 };
 try{return readDataType(shape(schema[key],0));}catch{return null;}
}
export function literalCapabilityResources(id:string,args:Record<string,unknown>,bindings:Record<string,unknown>,version:number):string[] {
 const literal=clone(args),schema=capabilityInput(id,args);
 if(version===3)for(const key of Object.keys(bindings))if(schemaField(schema,key,args)?.['x-resource']==='object'){
  const parts=key.split('.'),parent=parts.length===1?literal:argumentValue(literal,parts.slice(0,-1).join('.'));
  if(record(parent))delete parent[parts[parts.length-1]];
 }
 return capabilityResources(id,literal);
}
export function capabilityParameterType(id:string,parameter:string,args:Record<string,unknown>={}):BehaviourValueType|null {
 const schema=schemaField(capabilityInput(id,args),parameter,args);
 if(!schema||schema['x-static'])return null;
 const type=schema.type;return type==='string'?'text':type==='integer'?'number':type==='number'||type==='boolean'?type:null;
}

/** Schema-declared object references used for ownership and optimistic revisions. */
export function capabilityResources(id:string,args:Record<string,unknown>):string[] {
 const result=new Set<string>();
 const visit=(value:unknown,schema:CapabilitySchema|undefined)=>{
  schema=resolveCapabilitySchema(schema,value);if(!schema)return;if(schema['x-resource']==='object'&&typeof value==='string')result.add(value);
  if(Array.isArray(value))for(const entry of value)visit(entry,schema.items);
  if(record(value))for(const [key,entry] of Object.entries(value))visit(entry,schema.properties?.[key]);
 };visit(args,definitions.get(id)?.input);return [...result];
}

/** Feature requirements may also belong to optional arguments, so older calls stay usable. */
export function capabilityFeatures(id:string,args:Record<string,unknown>,bindings:string[]=[]):string[] {
 const required=new Set<string>();
 const visit=(schema:CapabilitySchema|undefined,value:unknown)=>{
  if(!schema||value===undefined)return;const selected=resolveCapabilitySchema(schema,value);if(!selected)return;
  for(const feature of [...schema['x-features']??[],...selected['x-features']??[]])required.add(feature);
  if(selected.type==='object'&&record(value))for(const [key,child] of Object.entries(selected.properties??{}))visit(child,value[key]);
  if(selected.type==='array'&&Array.isArray(value))for(const item of value)visit(selected.items,item);
 };
 const input=capabilityDefinition(id)?.input;visit(input,args);
 for(const path of bindings){const parts=path.split('.');for(let i=1;i<=parts.length;i++)for(const feature of schemaField(input,parts.slice(0,i).join('.'),args)?.['x-features']??[])required.add(feature);}
 return [...required];
}
