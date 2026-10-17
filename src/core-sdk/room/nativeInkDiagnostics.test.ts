// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeInkReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts captured native ink ownership through preparation, adoption, disable and deferred destruction',()=>{
 expect(validFactValue('runtime.inkBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({strokes:384,strokeLimit:384,surfaceLimit:128,vertexLimit:196608});
 expect(native.budget.preparing+native.budget.ready+native.budget.retiring).toBe(native.budget.strokes+native.budget.surfaces);
 for(const value of [native.preparing,native.ready,native.disabled,native.retiring])expect(validFactValue('runtime.inkReservation',value)).toBe(true);
 expect(native.preparing).toMatchObject({source:'spatial',role:'ink',kind:'stroke',phase:'preparing',vertices:6,active:false});
 expect(native.ready).toEqual({...native.preparing,phase:'ready',active:true});
 expect(native.disabled).toEqual({...native.ready,active:false});
 expect(native.retiring).toEqual({...native.disabled,phase:'retiring'});
 for(const key of ['reservationId','worldId','regionId','target'] as const)expect(native.ready[key]).toHaveLength(32);
});
it('exposes bounded passive ink queries without claiming memory or publication evidence',()=>{
 expect(behaviourFact('runtime.inkBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.inkReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.inkBudget')?.description).toContain('not measured GPU memory');
 expect(behaviourFact('runtime.inkBudget')?.description).toContain('selection outlines are excluded');
 expect(behaviourFact('runtime.inkReservation')?.description).toContain('does not certify publication or visibility');
 for(const index of [0,511])expect(validateFactArguments('runtime.inkReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:512},{index:.5},{index:'0'},{index:0,evict:true}])expect(validateFactArguments('runtime.inkReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.inkBudget',{...native.budget,vertices:'unknown'})).toBe(false);
 expect(validFactValue('runtime.inkReservation',{...native.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.inkReservation',{...native.ready,active:'yes'})).toBe(false);
});
it('uses the same native ink diagnostics for human and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.inkBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
