// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeObjectReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts native object reservations through preparation, private construction, disable and retirement',()=>{
 expect(validFactValue('runtime.objectBudget',native.budget)).toBe(true);
 expect(native.budget.instances).toBe(128);expect(native.budget.instanceLimit).toBe(128);
 expect(native.budget.preparing+native.budget.ready+native.budget.retiring).toBe(native.budget.instances);
 for(const value of [native.preparing,native.ready,native.disabled,native.retiring])expect(validFactValue('runtime.objectReservation',value)).toBe(true);
 expect(native.preparing).toMatchObject({role:'object',kind:'Block',phase:'preparing',active:false});
 expect(native.ready).toEqual({...native.preparing,phase:'ready',active:true});
 expect(native.disabled).toEqual({...native.ready,active:false});
 expect(native.retiring).toEqual({...native.disabled,phase:'retiring'});
 for(const key of ['reservationId','worldId','regionId','target'] as const)expect(native.ready[key]).toHaveLength(32);
});
it('keeps shared root limits independent of saved capacity and reports their exclusions',()=>{
 expect(behaviourFact('runtime.objectBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.objectReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.objectBudget')?.description).toContain('independent of saved-object capacity');
 expect(behaviourFact('runtime.objectBudget')?.description).toContain('Book, Maestro, tools and other system roots are excluded');
 expect(behaviourFact('runtime.objectReservation')?.description).toContain('imported-model readiness');
 for(const index of [0,127])expect(validateFactArguments('runtime.objectReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:128},{index:.5},{index:'0'},{index:0,evict:true}])expect(validateFactArguments('runtime.objectReservation',1,args)).not.toBeNull();
});
it('rejects unsupported telemetry and malformed native root observations',()=>{
 expect(validFactValue('runtime.objectBudget',{...native.budget,instances:'unknown'})).toBe(false);
 expect(validFactValue('runtime.objectReservation',{...native.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.objectReservation',{...native.ready,active:'yes'})).toBe(false);
});
it('requires the native diagnostics feature when user or agent programs inspect root capacity',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.objectBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
