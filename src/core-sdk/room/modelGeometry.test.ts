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
const target='a'.repeat(32),id='object.model.geometry.set';
const settings={version:1,scaleMode:'source',metresPerUnit:1,pivot:'source',meshCollision:true,walkable:true};
const args={target,revision:1,settings};
it('shares one guarded model geometry action and feature across users, agent and programs',()=>{
 expect(validateCapabilityArguments(id,1,args)).toBeNull();expect(capabilityResources(id,args)).toEqual([target]);
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('modelGeometry.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','modelGeometry.v1']})).not.toThrow();
 const program={version:3,resources:[target],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'geometry',op:'invoke',capability:id,version:1,arguments:args,bindings:{}}]}],entry:'main'};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
});
it('reads settings into an independent revision guarded form draft',()=>{
 const value={target,revision:12,modelHash:'a'.repeat(64),settings,ready:true,reason:'',sourceSize:{x:6,y:3,z:6},size:{x:6,y:3,z:6}};
 const observation:CatalogView={operation:'inspect',category:'facts',capability:'object.model.geometry',version:1,definition:behaviourFact('object.model.geometry')!,arguments:{target},available:true,value,status:'Available'};
 const loaded=applyCurrentInputs(capabilityDefinition(id)!.input,args,observation);
 expect(loaded).toEqual({...args,revision:12});(loaded.settings as typeof settings).metresPerUnit=.5;expect(settings.metresPerUnit).toBe(1);
});
it('rejects malformed geometry fields before native dispatch',()=>{
 for(const patch of [{version:2},{scaleMode:'guess'},{pivot:'foot'},{metresPerUnit:0},{metresPerUnit:101},{walkable:'yes'},{meshCollision:1},{texture:'wood'}])expect(validateCapabilityArguments(id,1,{...args,settings:{...settings,...patch}})).not.toBeNull();
});
it('portable model geometry requires fixed rigid models and keeps window components',()=>{
 const surface={version:1,id:'Canvas',part:'',position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},width:1,height:1,enabled:false,strokes:[]};
 const p={version:5,geometry:{kind:'model',modelHash:'a'.repeat(64)},physics:{mode:'fixed',shape:'automatic'},modelGeometry:settings,surfaces:[surface],drawingTips:[],windows:[{version:1,id:'Window',surface:'Canvas',shape:'rectangle',reveal:1}]};
 expect(validCreationPrototypeGeometry(p)).toBe(true);
 for(const patch of [{version:4},{geometry:{kind:'block'}},{physics:{mode:'solid',shape:'automatic'}},{physics:{mode:'fixed',shape:'box'}},{motion:{loop:false,frames:[]}},{modelGeometry:{...settings,meshCollision:false}},{modelGeometry:{...settings,scaleMode:'fitted',metresPerUnit:2}}])expect(validCreationPrototypeGeometry({...p,...patch})).toBe(false);
});
