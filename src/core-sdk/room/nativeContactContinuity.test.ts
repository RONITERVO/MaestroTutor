// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeContactContinuity.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts actual native contact states while keeping expired samples unknown',()=>{
 for(const state of [native.baseline,native.expired,native.refreshed,native.recovered,native.following]){
  for(const id of ['input.medium.contactState','object.medium.contactState'])expect(validFactValue(id,state)).toBe(true);
 }
 expect(native.baseline).toMatchObject({known:true,immersed:false});
 for(const state of [native.expired,native.refreshed]){
  expect(state).toMatchObject({known:false,immersed:false,bodyId:'',liquid:'',depthMetres:0,sampleSpeedMetresPerSecond:0,surface:{x:0,y:0,z:0}});
  expect(state.reason).not.toBe('');
 }
});
it('reestablishes contact without inventing the missed movement or an entry event',()=>{
 expect(native.recovered).toMatchObject({known:true,immersed:true,liquid:'Water',sampleSpeedMetresPerSecond:0,reason:''});
 expect(native.recovered.bodyId).toMatch(/^[a-f0-9]{32}$/);
 expect(native.recovered.depthMetres).toBeGreaterThan(0);
 expect(native.following).toMatchObject({known:true,immersed:false});
 expect(native.events).toEqual([{phase:'exited',participantId:'input:left',bodyId:native.recovered.bodyId,kind:'hand'}]);
});
it('gives user and agent programs the same typed continuity observation',()=>{
 const definition=behaviourFact('input.medium.contactState')!;
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'contact',initial:native.baseline,type:definition.type}],body:[{id:'read',op:'set',variable:'contact',value:{fact:definition.id,version:1,arguments:{side:'left'},bindings:{}}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toEqual(expect.arrayContaining(['liquidContacts.v1','factQueries.v1']));
 const wrong=structuredClone(program);wrong.functions[0].locals[0].initial={...native.baseline,known:'yes'};
 expect(parseProgram(JSON.stringify(wrong)).error).not.toBeNull();
});
