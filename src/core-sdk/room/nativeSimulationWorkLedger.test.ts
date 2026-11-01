// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeSimulationWorkLedger.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

const id='runtime.simulationWorkLedger';
type Ledger=typeof native.flow.consumed;
type Resource=keyof Ledger['limits'];
const resources=Object.keys(native.flow.consumed.limits) as Resource[];
// The native medium case reads each row through BehaviourCatalog.TryRead and
// reconstructs this aggregate. This is one real room, not world-capacity evidence.
function row(sample:Ledger,resource:Resource){
 return {step:sample.step,resource,committed:sample.committed[resource],
  consumed:sample.consumed[resource],retired:sample.retired[resource],limit:sample.limits[resource]};
}

it('accepts each native resource receipt within the shared bounded value contract',()=>{
 for(const sample of [...Object.values(native.flow),native.medium.active,native.medium.retired,native.medium.next]){
  for(const resource of resources){
   const value=row(sample,resource);
   expect(validFactValue(id,value)).toBe(true);
   expect(value.retired).toBeGreaterThanOrEqual(0);
   expect(value.consumed).toBeGreaterThanOrEqual(value.retired);
   expect(value.committed).toBeGreaterThanOrEqual(value.consumed);
   expect(value.committed).toBeLessThanOrEqual(value.limit);
  }
 }
 expect(behaviourFact(id)?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
});

it('retains actual liquid, force and contact allowances after their native requests retire',()=>{
 const flow=native.flow;
 for(const resource of ['discoveryUnits','liquidUnits','liquidPasses'] as Resource[]){
  expect(flow.consumed.consumed[resource]).toBeGreaterThan(0);
  expect(flow.retired.retired[resource]).toBe(flow.consumed.consumed[resource]);
  expect(flow.retired.committed[resource]).toBe(flow.consumed.committed[resource]);
  expect(flow.next.retired[resource]).toBe(0);
  expect(flow.next.consumed[resource]).toBe(0);
 }
 expect(flow.retired.step).toBe(flow.consumed.step);
 expect(flow.next.step).toBe(flow.retired.step+1);
 // The peer receives a new logical reservation after the clock advances.
 // It does not run native work in this fixture.
 expect(flow.next.committed.liquidUnits).toBeGreaterThan(0);
 expect(flow.next.consumed.liquidUnits).toBe(0);
 const medium=native.medium;
 expect(medium.medium.applied).toBe(true);
 expect(medium.contact).toMatchObject({known:true,immersed:true});
 for(const resource of ['physicsUnits','contactUnits','physicsBodies','contactPasses'] as Resource[]){
  expect(medium.active.consumed[resource]).toBeGreaterThan(0);
  expect(medium.retired.retired[resource]).toBe(medium.active.consumed[resource]);
  expect(medium.next.retired[resource]).toBe(0);
 }
 expect(medium.next.step).toBe(medium.retired.step+1);
});

it('gives human and agent programs the same checked resource query',()=>{
 for(const resource of resources)expect(validateFactArguments(id,1,{resource})).toBeNull();
 for(const args of [undefined,{}, {resource:'cpuMilliseconds'},{resource:0},{resource:'liquidUnits',start:true}])
  expect(validateFactArguments(id,1,args)).not.toBeNull();
 const initial=row(native.flow.consumed,'liquidUnits');
 expect(validFactValue(id,{...initial,consumed:'unknown'})).toBe(false);
 expect(validFactValue(id,{...initial,privatePath:'hidden'})).toBe(false);
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],
  functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'ledger',initial}],
   body:[{id:'read',op:'set',variable:'ledger',value:{fact:id,version:1,arguments:{resource:'liquidUnits'},bindings:{}}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
 expect([...programFeatureRequirements(program)]).toContain('factQueries.v1');
});
