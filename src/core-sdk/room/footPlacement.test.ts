// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import type {CatalogView} from '../../../shared/roomCatalog';
import {validCreationPrototypeGeometry} from '../../../shared/creationPrototype';
import {applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {parseProgram} from './programs';
const id='object.feet.configure',target='maestro';
const args={target,revision:1,mode:'grounded',modelHash:'',leftClearance:0,rightClearance:0};
it('uses the same guarded completion action for Maestro, imported actors, agent and programs',()=>{
 expect(capabilityDefinition(id)).toMatchObject({duration:'completion',channels:['wholeTarget']});
 for(const actor of [target,'a'.repeat(32)]){
  expect(validateCapabilityArguments(id,1,{...args,target:actor})).toBeNull();
  expect(capabilityResources(id,{...args,target:actor})).toEqual([actor]);
 }
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('humanoidFootPlacement.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','humanoidFootPlacement.v1']})).not.toThrow();
 const program={version:3,resources:[target],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'feet',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
});
it('loads the exact model and both existing shoe clearances into a revision guarded human draft',()=>{
 const value={...args,revision:8,modelHash:'b'.repeat(64),leftClearance:.02,rightClearance:-.01,mode:'grounded',available:true,temporary:false,status:'Ready',calibration:'restBounds',presentation:'both feet supported'};
 const observation:CatalogView={operation:'inspect',category:'facts',capability:'object.feet.settings',version:1,definition:behaviourFact('object.feet.settings')!,arguments:{target},available:true,value,status:'Available'};
 const loaded=applyCurrentInputs(capabilityDefinition(id)!.input,args,observation);
 expect(loaded).toEqual({...args,revision:8,modelHash:value.modelHash,leftClearance:.02,rightClearance:-.01});
 expect(args.leftClearance).toBe(0);
});
it('rejects invalid foot input before dispatch and does not expose book as a character',()=>{
 for(const patch of [{target:'book'},{mode:'fly'},{leftClearance:NaN},{leftClearance:.101},{rightClearance:-.101},{modelHash:'wrong'},{enabled:true}])
  expect(validateCapabilityArguments(id,1,{...args,...patch})).not.toBeNull();
});
it('keeps copied foot settings bound to exact imported identities in version nine',()=>{
 const footPlacement={version:1,enabled:true,modelHash:'b'.repeat(64),leftClearance:.01,rightClearance:0};
 const p={version:9,geometry:{kind:'model',modelHash:'a'.repeat(64)},physics:{mode:'fixed',shape:'automatic'},surfaces:[],drawingTips:[],footPlacement};
 expect(validCreationPrototypeGeometry(p)).toBe(true); // changed models retain an unavailable calibration, not a silent retarget
 for(const patch of [{version:8},{geometry:{kind:'block'}},{footPlacement:{...footPlacement,version:2}},{footPlacement:{...footPlacement,modelHash:''}},{footPlacement:{...footPlacement,enabled:false}},{footPlacement:{...footPlacement,leftClearance:NaN}}])
  expect(validCreationPrototypeGeometry({...p,...patch})).toBe(false);
});
