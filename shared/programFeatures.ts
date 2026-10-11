// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {behaviourFact} from './behaviourCatalog';
import {behaviourEvent} from './behaviourEvents';
import {capabilityParameterType,capabilityDefinition,capabilityOutputType,capabilityFeatures,capabilityInput,resolveCapabilitySchema,type CapabilitySchema} from './capabilities';
import type {BehaviourProgram} from './programSyntax';
import {visitProgramNodes,visitNodeExpressions} from './programTraversal';
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);

/** A compatibility check, not validation or execution. Callers validate commands first.
 * Only schema-declared module payloads contain source; other native arguments are data. */
export function invocationFeatureRequirements(id:string,args:Record<string,unknown>,bindings:string[]=[]):Set<string> {
 const features=new Set(capabilityFeatures(id,args,bindings));
 const visit=(schema:CapabilitySchema|undefined,value:unknown)=>{
  schema=resolveCapabilitySchema(schema,value);if(!schema)return;
  if(schema.format==='programModule'&&record(value)&&record(value.program)){
   for(const feature of programFeatureRequirements(value.program as unknown as BehaviourProgram))features.add(feature);
  }else if(schema.type==='object'&&record(value)){
   for(const [key,field] of Object.entries(schema.properties??{}))visit(field,value[key]);
  }else if(schema.type==='array'&&Array.isArray(value))for(const item of value)visit(schema.items,item);
 };
 visit(capabilityInput(id,args),args);return features;
}
/** Walk executable positions once. Literal records can use names such as op/fact
 * without acquiring code semantics. Imports remain source, including unused functions. */
export function programFeatureRequirements(program:BehaviourProgram):Set<string> {
 const features=new Set<string>();
 const add=(items:Iterable<string>)=>{for(const item of items)features.add(item);};
 const visit=(p:BehaviourProgram)=>{
  if(p.dataVersion!==undefined)features.add('structuredValues.v1');
  if(p.moduleVersion!==undefined)features.add('programModules.v1');
  if(p.memoryVersion!==undefined)features.add('rememberedVariables.v1');
  if(p.parallelVersion!==undefined)features.add('parallelPrograms.v1');
  for(const fn of p.functions)visitProgramNodes(fn.body,node=>{
   switch(node.op){
    case 'checkpoint':features.add('rememberedVariables.v1');break;
    case 'parallel':features.add('parallelPrograms.v1');break;
    case 'awaitCondition':features.add('conditionWaits.v1');break;
    case 'awaitEvent':
     if(node.fields!==undefined)features.add('eventFields.v1');
     add(behaviourEvent(node.event)?.features??[]);break;
    case 'invoke':
     if(Object.keys(node.bindings).some(key=>{const type=capabilityParameterType(node.capability,key,node.arguments);return type&&typeof type==='object';}))features.add('structuredInputs.v1');
     // These original program edits predate catalog feature annotations.
     if(['object.position.set','object.scale.set','object.color.set','object.delete'].includes(node.capability))features.add('objectEdits.v1');
     add(invocationFeatureRequirements(node.capability,node.arguments,Object.keys(node.bindings)));
     if(node.waitForChannels!==undefined)features.add('channelWaits.v1');
     if(Object.keys(node.results??{}).some(key=>{const type=capabilityOutputType(node.capability,key);return type&&typeof type==='object';}))features.add('structuredResults.v1');
     if(node.results!==undefined||Object.keys(capabilityDefinition(node.capability)?.output?.properties??{}).length>0)features.add('actionResults.v1');
     break;
   }
   visitNodeExpressions(node,expression=>{if('fact' in expression)add(behaviourFact(expression.fact)?.features??[]);});
  });
  for(const imported of p.imports??[])visit(imported.module.program);
 };
 visit(program);return features;
}
