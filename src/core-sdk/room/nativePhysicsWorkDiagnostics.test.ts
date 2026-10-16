// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativePhysicsWorkReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts actual native work grants without confusing a reservation with running physics',()=>{
 expect(validFactValue('runtime.physicsWorkBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({reservationLimit:128,unitLimit:524288,samplesPerBody:64,collidersPerBody:64,nativeMediaPerSample:32,missingMediaPerSample:32});
 expect(native.budget.units).toBeLessThanOrEqual(native.budget.unitLimit);
 for(const phase of [native.current,native.stale,native.resized]){
  expect(phase).toHaveLength(1);expect(validFactValue('runtime.physicsWorkReservation',phase[0])).toBe(true);
 }
 expect(native.current[0].current).toBe(true);
 expect(native.stale[0]).toEqual({...native.current[0],current:false});
 expect(native.resized[0]).toEqual({...native.current[0],units:native.current[0].units+64});
 expect(native.released).toEqual([]);
 for(const key of ['reservationId','worldId','regionId','target'] as const)expect(native.current[0][key]).toHaveLength(32);
});
it('bounds passive work queries and explicitly excludes unmeasured simulation costs',()=>{
 expect(behaviourFact('runtime.physicsWorkBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.physicsWorkReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.physicsWorkBudget')?.description).toContain('pouring, scooping, rain');
 expect(behaviourFact('runtime.physicsWorkBudget')?.description).toContain('not measured CPU time');
 expect(behaviourFact('runtime.physicsWorkReservation')?.description).toContain('not that its complete group');
 for(const index of [0,127])expect(validateFactArguments('runtime.physicsWorkReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:128},{index:.5},{index:'0'},{index:0,startPhysics:true}])expect(validateFactArguments('runtime.physicsWorkReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.physicsWorkBudget',{...native.budget,units:'unknown'})).toBe(false);
 expect(validFactValue('runtime.physicsWorkReservation',{...native.current[0],privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.physicsWorkReservation',{...native.current[0],current:'yes'})).toBe(false);
});
it('uses one typed work diagnostic vocabulary for human and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.physicsWorkBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
