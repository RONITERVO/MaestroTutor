// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeLiquidReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts the captured liquid lifecycle without equating ownership with water availability',()=>{
 expect(validFactValue('runtime.liquidBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({instances:32,instanceLimit:32,streamPointLimit:33});
 expect(native.budget.preparing+native.budget.ready+native.budget.retiring).toBe(native.budget.instances);
 for(const phase of [native.preparing,native.ready,native.disabled,native.retiring]){
  expect(phase).toHaveLength(1);expect(validFactValue('runtime.liquidReservation',phase[0])).toBe(true);
 }
 expect(native.preparing[0]).toMatchObject({phase:'preparing',active:false,surfaceAllocated:true,streamAllocated:true});
 expect(native.ready[0]).toEqual({...native.preparing[0],phase:'ready',active:true});
 expect(native.disabled[0]).toEqual({...native.ready[0],active:false});
 expect(native.retiring[0]).toEqual({...native.disabled[0],phase:'retiring'});
 for(const key of ['reservationId','worldId','regionId','target'] as const)expect(native.ready[0][key]).toHaveLength(32);
});
it('bounds passive liquid ownership queries and states their limits',()=>{
 expect(behaviourFact('runtime.liquidBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.liquidReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.liquidBudget')?.description).toContain('empty containers');
 expect(behaviourFact('runtime.liquidBudget')?.description).toContain('Per-tick simulation/query work');
 expect(behaviourFact('runtime.liquidReservation')?.description).toContain('does not certify water availability or simulation');
 for(const index of [0,31])expect(validateFactArguments('runtime.liquidReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:32},{index:.5},{index:'0'},{index:0,startPhysics:true}])expect(validateFactArguments('runtime.liquidReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.liquidBudget',{...native.budget,instances:'unknown'})).toBe(false);
 expect(validFactValue('runtime.liquidReservation',{...native.ready[0],privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.liquidReservation',{...native.ready[0],surfaceAllocated:'yes'})).toBe(false);
});
it('shares native liquid diagnostics between human and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.liquidBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
