// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowDiscovery.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('reads actual native discovery allowances without confusing booking with completed geometry',()=>{
 for(const sample of Object.values(native)){
  expect(validFactValue('runtime.flowDiscoveryBudget',sample.budget)).toBe(true);
  expect(sample.budget.units).toBeLessThanOrEqual(sample.budget.unitLimit);
  expect(sample.budget.executions).toBeLessThanOrEqual(sample.budget.reservations);
 }
 expect(native.waiting.budget.units).toBe(native.waiting.budget.unitLimit);
 expect(native.waiting).toMatchObject({captures:0,collisionReads:0,cavityReads:0,nativePending:false});
 expect(native.resumed.captures).toBe(1);
 expect(native.resumed.budget.step).toBeGreaterThan(native.waiting.budget.step);
 expect(native.physicsFirst.islandQueries).toBeGreaterThan(0);
});
it('preserves fresh native discovery across moved obstacles with one collision capture',()=>{
 for(const sample of [native.initial,native.movedObstacle]){
  expect(sample.probes).toBeGreaterThan(1);
  expect(sample.collisionReads).toBeGreaterThan(0);
  expect(sample.collisionReads).toBeLessThanOrEqual(sample.objects);
  expect(sample.collisionCandidates).toBeGreaterThan(sample.collisionReads);
 }
 const fact=behaviourFact('runtime.flowDiscoveryBudget');
 expect(fact?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(fact?.description).toContain('atomically');
 expect(fact?.description).toContain('not measured CPU time');
 expect(validFactValue('runtime.flowDiscoveryBudget',{...native.waiting.budget,units:'unknown'})).toBe(false);
 expect(validFactValue('runtime.flowDiscoveryBudget',{...native.waiting.budget,startPhysics:true})).toBe(false);
});
it('uses the same discovery diagnostic type in user and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.waiting.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.flowDiscoveryBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
