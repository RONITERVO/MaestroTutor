// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowWorkReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts captured native complete-pass flow reservations independently of geometry readiness',()=>{
 expect(validFactValue('runtime.flowWorkBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({reservationLimit:64,unitLimit:524288,streamSegments:32,queryHits:64,scoopSamples:5,rainSamples:5});
 expect(native.budget.units).toBeLessThanOrEqual(native.budget.unitLimit);
 for(const phase of [native.current,native.stale,native.resized]){
  expect(phase).toHaveLength(1);expect(validFactValue('runtime.flowWorkReservation',phase[0])).toBe(true);
 }
 expect(native.current[0]).toMatchObject({current:true,vessels:1,units:2644});
 expect(native.stale[0]).toEqual({...native.current[0],vessels:2,current:false});
 expect(native.resized[0]).toEqual({...native.current[0],vessels:2,units:9296,current:false});
 expect(native.released).toEqual([]);
 for(const key of ['reservationId','worldId','regionId'] as const)expect(native.current[0][key]).toHaveLength(32);
});
it('bounds passive flow work diagnostics without promising unmeasured device performance',()=>{
 expect(behaviourFact('runtime.flowWorkBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.flowWorkReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.flowWorkBudget')?.description).toMatch(/not (measured )?CPU time/);
 expect(behaviourFact('runtime.flowWorkReservation')?.description).toContain('at least one complete group');
 for(const index of [0,63])expect(validateFactArguments('runtime.flowWorkReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:64},{index:.5},{index:'0'},{index:0,startPhysics:true}])expect(validateFactArguments('runtime.flowWorkReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.flowWorkBudget',{...native.budget,units:'unknown'})).toBe(false);
 expect(validFactValue('runtime.flowWorkReservation',{...native.current[0],privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.flowWorkReservation',{...native.current[0],current:'yes'})).toBe(false);
});
it('lets user and agent programs share flow diagnostic types and documented waiting semantics',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.flowWorkBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
 for(const id of ['object.container.live','object.container.scooping','object.container.rain'])expect(behaviourFact(id)?.description).toMatch(/waiting/i);
});
