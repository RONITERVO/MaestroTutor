// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const fact='object.water.habitat.path',habitat='a'.repeat(32);
const args={target:'maestro',habitat,destination:{x:0,y:-2,z:1}};
it('shares exact actor and habitat references with generated user and agent fields',()=>{
 const definition=behaviourFact(fact)!;
 expect(validateFactArguments(fact,1,args)).toBeNull();
 for(const bad of [{...args,habitat:''},{...args,habitat:'nearest water'},{...args,target:'book'},{...args,destination:{x:26,y:0,z:0}},{...args,swim:true}])
  expect(validateFactArguments(fact,1,bad)).not.toBeNull();
 expect(definition.input?.properties?.target['x-resource']).toBe('object');
 expect(definition.input?.properties?.habitat['x-resource']).toBe('object');
 expect(definition.features).toContain('waterHabitat.v1');
});
it('requires advertised native habitat support rather than treating a point-water query as equivalent',()=>{
 const base=['catalog.v1','catalogVocabulary.v1','factQueries.v1'];
 const commands=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact,version:1,arguments:args}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...base,'liquidMedium.v1']})).toThrow('waterHabitat.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...base,'waterHabitat.v1']})).not.toThrow();
 expect(()=>requireRoomCapabilities([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact,version:1}}],{capabilities:base})).not.toThrow();
});
it('preserves known-negative and incomplete habitat results in ordinary program values',()=>{
 const value={bodyId:habitat,contained:false,reason:'The chosen habitat contains no liquid',availability:'ready',dependencyCount:0,dependencies:[]};
 expect(validFactValue(fact,value)).toBe(true);
 expect(validFactValue(fact,{...value,availability:'unavailable',dependencyCount:1,dependencies:[{target:habitat,state:'unavailable',boundsKnown:true}]})).toBe(true);
 expect(validFactValue(fact,{...value,contained:'yes'})).toBe(false);
 expect(validFactValue(fact,{...value,availability:42})).toBe(false);
});
