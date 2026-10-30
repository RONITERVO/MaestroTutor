// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowGroupScheduling.json';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';

it('reads native complete-group admission and member identity through the shared catalog',()=>{
 expect(validFactValue('runtime.flowGroupSchedule',native.schedule)).toBe(true);
 expect(native.schedule).toMatchObject({groups:2,active:2,booked:2,ready:2,executionLimit:32,groupLimit:1024,units:18744,unitLimit:524288});
 expect(native.groups).toHaveLength(2);expect(native.members).toHaveLength(4);
 expect(new Set(native.members.map(member=>member.target)).size).toBe(4);
 for(const group of native.groups){
  expect(validFactValue('runtime.flowGroup',group)).toBe(true);
  expect(group).toMatchObject({members:2,units:9372,active:true,discovered:true,booked:true,ready:true,step:native.schedule.step});
  const members=native.members.filter(member=>member.reservationId===group.reservationId&&member.membershipRevision===group.membershipRevision&&member.firstMember===group.firstMember);
  expect(members).toHaveLength(group.members);
  expect(members.reduce((sum,member)=>sum+member.units,0)).toBe(group.units);
  for(const member of members){
   expect(validFactValue('runtime.flowGroupMember',member)).toBe(true);
   expect(member.membershipRevision).toBe(group.membershipRevision);expect(member.step).toBe(group.step);expect(member.order).toBeGreaterThan(0);
  }
 }
});
it('detects an actual same-step regroup even when the first member and reservation stay the same',()=>{
 const {before,after,member}=native.regroup;
 expect(after.step).toBe(before.step);expect(after.reservationId).toBe(before.reservationId);
 expect(after.firstMember).toBe(before.firstMember);expect(before.members).toBe(2);expect(after.members).toBe(4);
 expect(after.membershipRevision).toBeGreaterThan(before.membershipRevision);
 expect(member.membershipRevision).toBe(after.membershipRevision);
 expect(validFactValue('runtime.flowGroup',after)).toBe(true);
 expect(validFactValue('runtime.flowGroupMember',member)).toBe(true);
});
it('keeps group diagnostics passive, bounded and distinct from host registrations',()=>{
 for(const id of ['runtime.flowGroupSchedule','runtime.flowGroup','runtime.flowGroupMember']){
  expect(behaviourFact(id)?.features).toContain('runtimeDiagnostics.v1');
 }
 expect(behaviourFact('runtime.flowWorkSchedule')?.description).toContain('host counts');
 expect(behaviourFact('runtime.flowGroup')?.description).toContain('canonical members');
 expect(validFactValue('runtime.flowGroup',{...native.groups[0],ready:'yes'})).toBe(false);
 expect(validFactValue('runtime.flowGroupMember',{...native.members[0],execute:true})).toBe(false);
});

it('uses the same typed and bounded group queries in human and agent programs',()=>{
 const id='runtime.flowGroup';
 expect(validateFactArguments(id,1,{index:0})).toBeNull();
 expect(validateFactArguments(id,1,{index:1023})).toBeNull();
 expect(validateFactArguments(id,1,{index:1024})).not.toBeNull();
 expect(validateFactArguments('runtime.flowGroupMember',1,{index:0,memberIndex:15})).toBeNull();
 expect(validateFactArguments('runtime.flowGroupMember',1,{index:0,memberIndex:16})).not.toBeNull();
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],
  functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'group',initial:native.groups[0]}],
   body:[{id:'read',op:'set',variable:'group',value:{fact:id,version:1,arguments:{index:0},bindings:{}}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program)]).toEqual(expect.arrayContaining(['runtimeDiagnostics.v1','factQueries.v1']));
});
