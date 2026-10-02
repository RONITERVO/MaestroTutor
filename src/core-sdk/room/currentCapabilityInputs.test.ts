// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourCatalog,behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,type CapabilitySchema} from '../../../shared/capabilities';
import {applyCurrentInputs,currentInputIdentity,currentInputRequest,validateCurrentInputMapping} from '../../../shared/currentCapabilityInputs';
import native from '../../../test-fixtures/browser/spatialSettings.json';
import type {CatalogView} from '../../../shared/roomCatalog';
const schema=()=>capabilityDefinition('object.physics.configure')!.input;
const args=()=>({...native.physics.selected.call.arguments});
const view=():CatalogView=>({operation:'inspect',category:'facts',capability:'object.physics.settings',version:1,definition:behaviourFact('object.physics.settings')!,arguments:{target:native.beforePhysics.target},available:true,value:native.beforePhysics,status:'Available'});
it('checks every native current-input annotation against both registered contracts',()=>{
 let count=0;for(const action of behaviourCatalog.actions)for(const s of (action.input as CapabilitySchema).oneOf??[action.input as CapabilitySchema]){
  if(s['x-current'])count++;expect(()=>validateCurrentInputMapping(s)).not.toThrow();
 }expect(count).toBe(25);
});
it('loads exact fact values atomically and distinguishes guards from editable preferences',()=>{
 const next=applyCurrentInputs(schema(),args(),view());expect(next).toEqual({target:native.beforePhysics.target,revision:native.beforePhysics.revision,mode:native.beforePhysics.mode,shape:native.beforePhysics.shape,mass:native.beforePhysics.mass});
 const key=currentInputIdentity(schema(),next,'session');expect(currentInputIdentity(schema(),{...next,mass:7},'session')).toBe(key);
 for(const changed of [{...next,target:'a'.repeat(32)},{...next,revision:100}])expect(currentInputIdentity(schema(),changed,'session')).not.toBe(key);
 expect(currentInputIdentity(schema(),next,'anotherSession')).not.toBe(key);expect(args()).toEqual(native.physics.selected.call.arguments);
});
it('refuses missing, mismatched, unavailable, invalid and out-of-range fact responses',()=>{
 const v=view();if(v.operation!=='inspect'||v.category!=='facts')throw Error();
 for(const bad of [{...v,available:false,value:null},{...v,arguments:{target:'b'.repeat(32)}},{...v,version:2},{...v,value:{...native.beforePhysics,mass:25}},{...v,value:{...native.beforePhysics,revision:0}},{...v,value:{...native.beforePhysics,mode:'cloth'}}])expect(()=>applyCurrentInputs(schema(),args(),bad)).toThrow();
 expect(()=>currentInputRequest(schema(),{...args(),target:'book'})).toThrow();
});
it('refuses bad metadata references, dependency collisions, static fields and prototype paths',()=>{
 for(const change of [(s:CapabilitySchema)=>s['x-current']!.fact='missing.fact',(s:CapabilitySchema)=>s['x-current']!.version=2,(s:CapabilitySchema)=>s['x-current']!.fields.mass=['constructor'],(s:CapabilitySchema)=>s['x-current']!.fields.mass=['mode'],(s:CapabilitySchema)=>s['x-current']!.guards=['other'],(s:CapabilitySchema)=>s['x-current']!.arguments={},(s:CapabilitySchema)=>s['x-current']!.fields.target=['target'],(s:CapabilitySchema)=>s.properties!.mass['x-static']=true]){const s=schema();change(s);expect(()=>validateCurrentInputMapping(s)).toThrow();}
});
it('keeps selected walking identities and desired controller modes when reading only guards',()=>{
 for(const [id,value,fact] of [['avatar.walk.select',{target:'maestro',revision:1,source:'embedded',modelHash:'a'.repeat(64),clipIndex:12},'avatar.walk.settings'],['controller.mode.set',{operation:'user.enable',stateId:'0'.repeat(32)},'controller.mode']] as const){
  const s=capabilityDefinition(id)!.input;expect(currentInputRequest(s,value)).toEqual({operation:'inspect',category:'facts',capability:fact,version:1});
  if(id==='avatar.walk.select')expect(applyCurrentInputs(s,value,{operation:'inspect',category:'facts',capability:fact,version:1,definition:behaviourFact(fact)!,available:true,value:native.beforeWalk,status:'Available'})).toEqual({...value,revision:native.beforeWalk.revision});
 }
});
