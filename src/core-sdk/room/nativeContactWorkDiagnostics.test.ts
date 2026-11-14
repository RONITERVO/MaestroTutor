// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeContactWorkReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts actual native work grants without confusing a reservation with a known contact',()=>{
 expect(validFactValue('runtime.contactWorkBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({reservationLimit:64,unitLimit:524288,samplesPerBody:64,collidersPerBody:64,avatarSamples:6,inputSamples:2});
 expect(native.budget.units).toBeLessThanOrEqual(native.budget.unitLimit);
 for(const phase of [native.current,native.stale,native.resized]){
  expect(phase).toHaveLength(1);expect(validFactValue('runtime.contactWorkReservation',phase[0])).toBe(true);
 }
 expect(native.current[0].current).toBe(true);
 expect(native.stale[0]).toEqual({...native.current[0],current:false});
 expect(native.resized[0]).toEqual({...native.current[0],units:native.current[0].units+64});
 expect(native.released).toEqual([]);
 for(const key of ['reservationId','worldId','regionId'] as const)expect(native.current[0][key]).toHaveLength(32);
});
it('bounds passive work queries and explicitly excludes unmeasured simulation costs',()=>{
 expect(behaviourFact('runtime.contactWorkBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.contactWorkReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.contactWorkBudget')?.description).toContain('pouring, scooping, rain');
 expect(behaviourFact('runtime.contactWorkBudget')?.description).toContain('not measured CPU time');
 expect(behaviourFact('runtime.contactWorkReservation')?.description).toContain('not that geometry');
 for(const index of [0,31,63])expect(validateFactArguments('runtime.contactWorkReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:64},{index:.5},{index:'0'},{index:0,startPhysics:true}])expect(validateFactArguments('runtime.contactWorkReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.contactWorkBudget',{...native.budget,units:'unknown'})).toBe(false);
 expect(validFactValue('runtime.contactWorkReservation',{...native.current[0],privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.contactWorkReservation',{...native.current[0],current:'yes'})).toBe(false);
});
it('uses one typed work diagnostic vocabulary for human and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.contactWorkBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
