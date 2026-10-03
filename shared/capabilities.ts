// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {validConnectionDefinition,type ConnectionDefinition} from './roomConnection';
import {validCreationPrototypeGeometry} from './creationPrototype';
import {validCreationBatchGeometry} from './creationBatch';
import {readDataType,type DataType} from './programValues';
import {validCollisionRecipe} from './collisionRecipe';
import {moduleHash,validModuleRecord} from './programModuleIdentity';
import {parseRecipe,validLathePart,validExtrudedPart,validSweptPart} from './roomRecipe';
import {behaviourCatalog} from './behaviourCatalog';
export interface CurrentInputMapping {
 fact:string;version:number;arguments:Record<string,string>;fields:Record<string,string[]>;guards:string[];
}
export interface CapabilitySchema {
 type:'object'|'array'|'string'|'number'|'integer'|'boolean';
 'x-current'?:CurrentInputMapping;
 oneOf?:CapabilitySchema[];'x-confirmation'?:string;'x-discriminators'?:string[];title?:string;description?:string;examples?:unknown[];'x-static'?:boolean;'x-channels'?:string[];'x-requirements'?:string[];'x-features'?:string[];
 items?:CapabilitySchema;minItems?:number;maxItems?:number;nullable?:boolean;
 properties?:Record<string,CapabilitySchema>;required?:string[];additionalProperties?:false;
 format?:'unitQuaternion'|'boundedOffset'|'roomRecipe'|'lathePart'|'extrusionPart'|'sweepPart'|'collisionRecipe'|'programModule'|'programMemoryValue'|'objectLayout'|'creationBatch'|'creationPrototype'|'structureSource'|'connectionConfiguration'|'constructionSelection'|'groupTransform'|'snapPointDefinition'|'snapPlacement'|'containerDefinition'|'containerTransfer';'x-resource'?:'object';'x-requires'?:Record<string,string>;
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
/** Indexes address existing literal array entries; they never resize an argument. */
function argumentIndex(value:unknown,key:string):number|null {
 if(!Array.isArray(value)||!/^(0|[1-9][0-9]{0,3})$/.test(key))return null;
 const index=Number(key);return index<value.length?index:null;
}
function argumentChild(value:unknown,key:string):unknown {
 const index=argumentIndex(value,key);return index!==null?(value as unknown[])[index]:record(value)&&own(value,key)?value[key]:undefined;
}
export function schemaField(schema:CapabilitySchema|undefined,path:string,value?:unknown):CapabilitySchema|undefined {
 for(const key of path.split('.')){
  schema=resolveCapabilitySchema(schema,value);
  schema=schema?.type==='array'?(argumentIndex(value,key)!==null?schema.items:undefined):schema?.properties&&own(schema.properties,key)?schema.properties[key]:undefined;
  value=argumentChild(value,key);
 }return schema;
}
export function argumentValue(value:unknown,path:string):unknown {for(const key of path.split('.'))value=argumentChild(value,key);return value;}
export function resolveCapabilitySchema(schema:CapabilitySchema|undefined,value:unknown):CapabilitySchema|undefined {
 if(!schema?.oneOf)return schema;
 const matches=schema.oneOf.filter(branch=>(schema['x-discriminators']??[]).every(path=>schemaField(branch,path)?.enum?.includes(argumentValue(value,path) as string)));
 return matches.length===1?matches[0]:undefined;
}
export function capabilityInput(id:string,args:Record<string,unknown>):CapabilitySchema|undefined {return resolveCapabilitySchema(definitions.get(id)?.input,args);}
export function capabilityBindingFields(id:string,args:Record<string,unknown>):Record<string,CapabilitySchema> {
 const result:Record<string,CapabilitySchema>={};
 const visit=(schema:CapabilitySchema|undefined,path:string)=>{schema=resolveCapabilitySchema(schema,path?argumentValue(args,path):args);if(!schema||schema['x-static'])return;
  if(path&&capabilityParameterType(id,path,args))result[path]=schema;
  if(schema.type==='object')for(const [key,field] of Object.entries(schema.properties??{}))visit(field,path?path+'.'+key:key);
  const value=path?argumentValue(args,path):args;if(schema.type==='array'&&Array.isArray(value))value.forEach((_,i)=>visit(schema.items,path+'.'+i));
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
   if(schema.format==='groupTransform'){const members=value.members as {target:string}[],p=value.position as {x:number;y:number;z:number};return new Set(members.map(m=>m.target)).size===members.length&&p.x*p.x+p.y*p.y+p.z*p.z<=625?null:path+' needs distinct members and an origin within 25 metres';}
   if(schema.format==='snapPointDefinition'){const p=(value.frame as {position:{x:number;y:number;z:number}}).position;return p.x*p.x+p.y*p.y+p.z*p.z<=100?null:path+' needs a snap frame within ten local metres';}
   if(schema.format==='containerDefinition'){const p=(value.frame as {position:{x:number;y:number;z:number}}).position;return p.x*p.x+p.y*p.y+p.z*p.z<=100&&(value.amountMl as number)<=(value.capacityMl as number)?null:path+' needs a cavity within ten local metres and contents within capacity';}
   if(schema.format==='containerTransfer')return (value.source as {target:string}).target!==(value.destination as {target:string}).target?null:path+' needs distinct source and destination';
   if(schema.format==='snapPlacement'){const members=value.members as {target:string}[],destination=value.destination as {target:string};return new Set(members.map(m=>m.target)).size===members.length&&!members.some(m=>m.target===destination.target)?null:path+' needs distinct moving members and an outside destination';}
   if(schema.format==='constructionSelection'){const members=value.members as string[];return new Set(members).size===members.length?null:path+' needs distinct construction pieces';}
   if(schema.format==='structureSource'){
    const entries=(value.kind==='capture'?value.members:value.slots) as {slot:string;target?:string;placement?:{target:string;position:{x:number;y:number;z:number}}}[];
    return new Set(entries.map(x=>x.slot)).size===entries.length&&new Set(entries.map(x=>x.target??x.placement!.target)).size===entries.length&&entries.every(x=>!x.placement||x.placement.position.x**2+x.placement.position.y**2+x.placement.position.z**2<=625)?null:path+' needs distinct slots and valid baseline placements';
   }
   if(schema.format==='connectionConfiguration')return value.target!==value.connected&&(value.operation!=='configure'||validConnectionDefinition(value.definition as ConnectionDefinition))?null:path+' needs different objects, bounded anchors and a spring target inside its limits';
   if(schema.format==='creationPrototype')return validCreationPrototypeGeometry(value)?null:path+' needs valid local geometry, ink and motion';
   if(schema.format==='creationBatch')return validCreationBatchGeometry(value)?null:path+' needs distinct idle pieces with valid transformed placements';
   if(schema.format==='objectLayout'){
    const placements=value.placements as {target:string;position:{x:number;y:number;z:number}}[];
    return new Set(placements.map(p=>p.target)).size===placements.length&&placements.every(p=>p.position.x**2+p.position.y**2+p.position.z**2<=625)?null:path+' needs distinct objects within 25 metres';
   }
   if(schema.format==='collisionRecipe')return validCollisionRecipe(value)?null:path+' needs bounded valid collision shapes';
   if(schema.format==='lathePart')return validLathePart(value)?null:path+' needs a simple counter-clockwise lathe profile';
   if(schema.format==='sweepPart')return validSweptPart(value)?null:path+' needs an open sweep path with a simple profile and no folded sides';
   if(schema.format==='extrusionPart')return validExtrudedPart(value)?null:path+' needs a simple counter-clockwise extrusion outline';
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
 const fields=definitions.get(id)?.output?.properties;
 return fields&&own(fields,key)?capabilityValueType(fields[key]):null;
}
function capabilityValueType(schema:CapabilitySchema|undefined):DataType|null {
 const shape=(field:CapabilitySchema|undefined,depth:number):DataType=>{
  if(!field||depth>4||field.oneOf||field.nullable)throw new Error('No fixed output type');
  if(field.type==='string')return 'text';if(field.type==='number'||field.type==='integer')return 'number';if(field.type==='boolean')return 'boolean';
  if(field.type==='array')return {list:shape(field.items,depth+1)};
  if(!field.properties||field.required?.length!==Object.keys(field.properties).length)throw new Error('Optional record fields are not program values');
  return {record:Object.fromEntries(Object.entries(field.properties).map(([k,v])=>[k,shape(v,depth+1)]))};
 };
 try{return readDataType(shape(schema,0));}catch{return null;}
}
export function literalCapabilityResources(id:string,args:Record<string,unknown>,bindings:Record<string,unknown>,version:number):string[] {
 const literal=clone(args);
 if(version===3)for(const key of Object.keys(bindings))if(capabilityParameterType(id,key,args)!==null){
  const parts=key.split('.'),parent=parts.length===1?literal:argumentValue(literal,parts.slice(0,-1).join('.'));
  const last=parts[parts.length-1],index=argumentIndex(parent,last);
  if(index!==null)(parent as unknown[])[index]=null;else if(record(parent))delete parent[last];
 }
 return capabilityResources(id,literal);
}
export function separateCapabilityBindings(bindings:Record<string,unknown>):boolean {
 const names=Object.keys(bindings);return names.every(name=>!names.some(parent=>name.startsWith(parent+'.')));
}
export function capabilityParameterType(id:string,parameter:string,args:Record<string,unknown>={}):DataType|null {
 let schema=definitions.get(id)?.input,value:unknown=args;
 for(const key of parameter.split('.')) {
  if(schema?.['x-static']||schema?.['x-discriminators']?.includes(key))return null;
  const selected=resolveCapabilitySchema(schema,value);
  schema=selected?.type==='array'?(argumentIndex(value,key)!==null?selected.items:undefined):selected?.properties?.[key];value=argumentChild(value,key);
 }
 const mutable=(field:CapabilitySchema|undefined):boolean=>!!field&&!field['x-static']&&!field.oneOf&&!field.nullable&&field.format!=='programModule'&&
  (field.type==='array'?mutable(field.items):field.type!=='object'||Object.values(field.properties??{}).every(mutable));
 return mutable(schema)?capabilityValueType(schema):null;
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
 if(bindings.some(path=>path.split('.').some(part=>/^(0|[1-9][0-9]*)$/.test(part))))required.add('indexedInputs.v1');
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
