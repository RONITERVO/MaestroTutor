// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativePhysicsReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts captured native body and potential-joint lifetimes without equating them with simulation',()=>{
 expect(validFactValue('runtime.physicsBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({bodies:128,joints:32,bodyLimit:128,jointLimit:32});
 expect(native.budget.preparing+native.budget.ready+native.budget.retiring).toBe(native.budget.bodies+native.budget.joints);
 expect(native.preparing.map(x=>x.kind)).toEqual(['body','joint']);
 for(const phase of [native.preparing,native.ready,native.disabled,native.retiring])for(const value of phase)expect(validFactValue('runtime.physicsReservation',value)).toBe(true);
 for(let i=0;i<2;i++){
  expect(native.preparing[i]).toMatchObject({phase:'preparing',active:false,allocated:false});
  expect(native.ready[i]).toEqual({...native.preparing[i],phase:'ready',active:true,allocated:i===0});
  expect(native.disabled[i]).toEqual({...native.ready[i],active:false});
  for(const key of ['reservationId','worldId','regionId','target'] as const)expect(native.ready[i][key]).toHaveLength(32);
 }
 expect(native.retiring).toEqual([{...native.disabled[0],phase:'retiring'}]);
});
it('bounds passive physics ownership queries and keeps readiness claims precise',()=>{
 expect(behaviourFact('runtime.physicsBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.physicsReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.physicsBudget')?.description).toContain('not measured PhysX memory');
 expect(behaviourFact('runtime.physicsBudget')?.description).toContain('simulation query work are excluded');
 expect(behaviourFact('runtime.physicsReservation')?.description).toContain('does not certify simulation');
 for(const index of [0,159])expect(validateFactArguments('runtime.physicsReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:160},{index:.5},{index:'0'},{index:0,startPhysics:true}])expect(validateFactArguments('runtime.physicsReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.physicsBudget',{...native.budget,bodies:'unknown'})).toBe(false);
 expect(validFactValue('runtime.physicsReservation',{...native.ready[0],privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.physicsReservation',{...native.ready[0],allocated:'yes'})).toBe(false);
});
it('shares native physics diagnostics between human and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.physicsBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
