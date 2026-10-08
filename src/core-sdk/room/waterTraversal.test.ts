// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityFeatures,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {applyCurrentInputs,currentInputIdentity,currentInputRequest} from '../../../shared/currentCapabilityInputs';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const id='object.water.traversal.configure';
const args={target:'maestro',revision:1,mode:'wade',maxDepthMetres:.2};
it('shares bounded water policies and requires explicit native feature support',()=>{
 expect(validateCapabilityArguments(id,1,args)).toBeNull();
 expect(capabilityResources(id,args)).toEqual(['maestro']);
 expect(capabilityFeatures(id,args)).toEqual(expect.arrayContaining(['spatialSettings.v1','waterTraversal.v1']));
 for(const bad of [{...args,mode:'swim'},{...args,target:'book'},{...args,maxDepthMetres:2.01},{...args,maxDepthMetres:-1},{...args,revision:0},{...args,force:true}])expect(validateCapabilityArguments(id,1,bad)).not.toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}];
 const capabilities=['execution.v1','spatialSettings.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('waterTraversal.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'waterTraversal.v1']})).not.toThrow();
});
it('book current-value controls preserve target identity and bind saves to the inspected revision',()=>{
 const schema=capabilityDefinition(id)!.input,fact='object.water.traversal.settings';
 const value={target:'maestro',revision:15,mode:'default',effectiveMode:'avoid',maxDepthMetres:.25,temporary:false};
 expect(validFactValue(fact,value)).toBe(true);
 expect(currentInputRequest(schema,args)).toEqual({operation:'inspect',category:'facts',capability:fact,version:1,arguments:{target:'maestro'}});
 const next=applyCurrentInputs(schema,args,{operation:'inspect',category:'facts',capability:fact,version:1,definition:behaviourFact(fact)!,arguments:{target:'maestro'},available:true,status:'Available',value});
 expect(next).toEqual({target:'maestro',revision:15,mode:'default',maxDepthMetres:.25});
 expect(currentInputIdentity(schema,next,'room')).toBe(currentInputIdentity(schema,{...next,mode:'wade'},'room'));
 expect(currentInputIdentity(schema,next,'room')).not.toBe(currentInputIdentity(schema,{...next,revision:16},'room'));
});
it('exposes bounded read-only route queries without claiming swimming or general navigation',()=>{
 const fact='object.water.traversal.path';
 expect(validateFactArguments(fact,1,{target:'maestro',position:{x:0,y:-2,z:4}})).toBeNull();
 expect(validateFactArguments(fact,1,{target:'book',position:{x:0,y:0,z:0}})).not.toBeNull();
 expect(validFactValue(fact,{allowed:false,bodyId:'a'.repeat(32),depthMetres:.6,reason:'This water is too deep'})).toBe(true);
 expect(behaviourFact(fact)?.features).toContain('waterTraversal.v1');
});
