// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validateFactArguments,validFactValue} from '../../../shared/behaviourFacts';
import {validCatalogRequest,validCatalogView} from '../../../shared/roomCatalog';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

const capability='object.collision.availability',target='1'.repeat(32);
const ready={target,state:'ready',reason:''};
const view=(value:unknown)=>({operation:'inspect',category:'facts',capability,version:1,arguments:{target},
 definition:behaviourFact(capability),available:true,value,status:'Current collision availability'});

it.each([
 {target,state:'ready',reason:''},
 {target,state:'pending',reason:'Preparing native collision geometry'},
 {target,state:'unavailable',reason:'The saved object has no active native instance'},
])('shares a complete $state collision observation through the generic catalog',value=>{
 expect(behaviourFact(capability)?.features).toContain('factQueries.v1');
 expect(validateFactArguments(capability,1,{target})).toBeNull();
 expect(validCatalogRequest({operation:'inspect',category:'facts',capability,version:1,arguments:{target}})).toBe(true);
 expect(validFactValue(capability,value)).toBe(true);expect(validCatalogView(view(value))).toBe(true);
});

it('rejects incomplete, malformed and additional collision telemetry',()=>{
 for(const value of [null,false,[],{...ready,target:42},{...ready,state:false},{...ready,reason:NaN},
  {...ready,reason:'x'.repeat(129)},{...ready,reason:'Line\nbreak'},
  {...ready,ready:true},{...ready,revision:1},{...ready,colliders:0}]){
  expect(validFactValue(capability,value)).toBe(false);expect(validCatalogView(view(value))).toBe(false);
 }
 for(const omitted of Object.keys(ready)){
  const value=Object.fromEntries(Object.entries(ready).filter(([key])=>key!==omitted));
  expect(validFactValue(capability,value)).toBe(false);
 }
});

it('distinguishes an unavailable geometry value from an unavailable fact without manufacturing state',()=>{
 expect(validCatalogView(view({target,state:'unavailable',reason:'Native collision geometry is unavailable'}))).toBe(true);
 const missing={...view(null),available:false};
 expect(validCatalogView(missing)).toBe(true);
 expect(validCatalogView({...missing,value:ready})).toBe(false);
 expect(validCatalogView(view(null))).toBe(false);
 expect(validCatalogView({...view(ready),definition:null})).toBe(false);
 expect(validCatalogView({...view(ready),version:2})).toBe(false);
});

it('keeps target arguments exact and rejects unknown versions',()=>{
 for(const args of [{},{target:''},{target:'not-an-object'},{target:42},{target,load:true}]){
  expect(validateFactArguments(capability,1,args)).not.toBeNull();
  expect(validCatalogView({...view(ready),arguments:args})).toBe(false);
 }
 expect(validateFactArguments(capability,2,{target})).not.toBeNull();
});

it('makes the same structured fact available to programs without acquiring object authority',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{
  name:'main',returns:'void',parameters:[],locals:[{name:'collision',initial:ready}],
  body:[{id:'observe',op:'set',variable:'collision',value:{fact:capability,version:1,arguments:{target},bindings:{}}}]
 }]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toContain('factQueries.v1');
 const invalid=structuredClone(program),read=invalid.functions[0].body[0];
 if(read.op!=='set'||!('fact' in read.value))throw new Error('Expected fact assignment');
 read.value.bindings={target:{value:true}};
 expect(parseProgram(JSON.stringify(invalid)).program).toBeNull();
});
