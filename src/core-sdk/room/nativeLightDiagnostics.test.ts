// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeLightReservations.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts native lamp ownership through preparation, adoption, disable and deferred destruction',()=>{
 expect(validFactValue('runtime.lightBudget',native.budget)).toBe(true);
 expect(native.budget).toMatchObject({lights:8,lightLimit:8,shadowFaceLimit:12});
 expect(native.budget.preparing+native.budget.ready+native.budget.retiring).toBe(native.budget.lights);
 for(const value of [native.preparing,native.ready,native.disabled,native.retiring])expect(validFactValue('runtime.lightReservation',value)).toBe(true);
 expect(native.preparing).toMatchObject({light:'Lamp',kind:'spot',phase:'preparing',shadowFaces:1,active:false});
 expect(native.ready).toEqual({...native.preparing,phase:'ready',active:true});
 expect(native.disabled).toEqual({...native.ready,active:false});
 expect(native.retiring).toEqual({...native.disabled,phase:'retiring'});
 for(const key of ['reservationId','worldId','regionId','target'] as const)expect(native.ready[key]).toHaveLength(32);
});
it('exposes bounded passive lamp queries with explicit accounting limits',()=>{
 expect(behaviourFact('runtime.lightBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.lightReservation')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.lightBudget')?.description).toContain('not measured GPU memory');
 expect(behaviourFact('runtime.lightBudget')?.description).toContain('Sun and other system lighting are excluded');
 for(const index of [0,7])expect(validateFactArguments('runtime.lightReservation',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:8},{index:.5},{index:'0'},{index:0,evict:true}])expect(validateFactArguments('runtime.lightReservation',1,args)).not.toBeNull();
 expect(validFactValue('runtime.lightBudget',{...native.budget,shadowFaces:'unknown'})).toBe(false);
 expect(validFactValue('runtime.lightReservation',{...native.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.lightReservation',{...native.ready,active:'yes'})).toBe(false);
});
it('uses the same native diagnostics capability for user and agent programs',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.lightBudget'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
