// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeLiquidPath.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {defaultDataValue} from '../../../shared/programValues';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('preserves actual native wet, dry and unavailable paths without treating unknown as air',()=>{
 for(const value of [native.wet,native.dry,native.unavailable])expect(validFactValue('world.medium.path',value)).toBe(true);
 expect(native.wet).toMatchObject({availability:'ready',bodyCount:1,dependencyCount:0});
 expect(native.wet.wetMetres).toBeCloseTo(1,5);expect(native.wet.dryMetres).toBeCloseTo(1,5);
 expect(native.dry).toMatchObject({availability:'ready',wetMetres:0,bodyCount:0});
 expect(native.dry.dryMetres).toBeCloseTo(2,5);
 expect(native.unavailable).toMatchObject({availability:'unavailable',wetMetres:0,dryMetres:0,dependencyCount:1});
 expect(native.unavailable.reason).not.toBe('');
 expect(validFactValue('world.medium.path',{...native.wet,wetMetres:"unknown"})).toBe(false);
 expect(validFactValue('world.medium.path',{...native.wet,privateCameraFrame:'not a path result'})).toBe(false);
});

it('keeps a user or agent authored path read typed and gated on native support',()=>{
 const fact=behaviourFact('world.medium.path')!;
 const program:BehaviourProgram={version:3,dataVersion:1,state:[],events:[],entry:'main',resources:[],functions:[{
  name:'main',returns:'void',parameters:[],locals:[{name:'path',type:fact.type,initial:defaultDataValue(fact.type)}],
  body:[{id:'measure',op:'set',variable:'path',value:{fact:fact.id,version:fact.version,arguments:fact.example!,bindings:{}}}]
 }]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toContain('liquidPaths.v1');
 expect(fact.features).toContain('liquidPaths.v1');
 expect(validateFactArguments(fact.id,1,{...fact.example,from:{x:20,y:1,z:0}})).toBeNull();
 expect(validateFactArguments(fact.id,1,{...fact.example,from:{x:26,y:0,z:0}})).not.toBeNull();
});
