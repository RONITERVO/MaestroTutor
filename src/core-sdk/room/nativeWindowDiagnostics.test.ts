// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeWindowReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts captured native window bundle and mask lifetimes',()=>{
 expect(validFactValue('runtime.windowBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({masks:32,bundles:32,maskLimit:32,bundleLimit:32});
 expect(native.budget.preparing+native.budget.ready+native.budget.retiring).toBe(native.budget.masks+native.budget.bundles);
 expect(native.preparing.map(x=>x.kind)).toEqual(['bundle','mask']);
 for(const phase of [native.preparing,native.ready,native.disabled,native.retiring])for(const value of phase)expect(validFactValue('runtime.windowReservation',value)).toBe(true);
 for(let i=0;i<2;i++){
  expect(native.preparing[i]).toMatchObject({phase:'preparing',active:false,window:i===0?'':'Window'});
  expect(native.ready[i]).toEqual({...native.preparing[i],phase:'ready',active:true});
  expect(native.disabled[i]).toEqual({...native.ready[i],active:false});
  expect(native.retiring[i]).toEqual({...native.disabled[i],phase:'retiring'});
  for(const key of ['reservationId','worldId','regionId','target'] as const)expect(native.ready[i][key]).toHaveLength(32);
 }
});
it('exposes bounded passive window queries without claiming publication or passthrough readiness',()=>{
 expect(behaviourFact('runtime.windowBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.windowReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.windowBudget')?.description).toContain('not measured GPU memory');
 expect(behaviourFact('runtime.windowBudget')?.description).toContain('compositor and passthrough resources are excluded');
 expect(behaviourFact('runtime.windowReservation')?.description).toContain('does not certify publication or visibility');
 for(const index of [0,63])expect(validateFactArguments('runtime.windowReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:64},{index:.5},{index:'0'},{index:0,enablePassthrough:true}])expect(validateFactArguments('runtime.windowReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.windowBudget',{...native.budget,masks:'unknown'})).toBe(false);
 expect(validFactValue('runtime.windowReservation',{...native.ready[0],privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.windowReservation',{...native.ready[0],active:'yes'})).toBe(false);
});
it('uses the same native window diagnostics for human and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.windowBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
