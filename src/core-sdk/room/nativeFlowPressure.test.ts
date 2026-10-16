// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowPressure.json';
import {validFactValue} from '../../../shared/behaviourFacts';

it('reads real native room pressure without equating saved inventory and native allocation',()=>{
 expect(native).toMatchObject({rooms:9,savedContainers:144,nativeContainers:20,dormantContainers:124,conservationAndUndo:true});
 const first=native.trace[0];
 expect(new Set(first.rooms.map(room=>room.worldId)).size).toBe(9);
 expect(first.rooms.every(room=>room.vessels===16)).toBe(true);
 expect(first.rooms.reduce((sum,room)=>sum+room.nativeVessels,0)).toBe(20);
 expect(first.rooms.reduce((sum,room)=>sum+room.dormantVessels,0)).toBe(124);
 expect(native.hostUnits).toBeGreaterThan(native.flowUnitLimit);
 expect(native.discoveryUnits).toBeLessThan(native.discoveryUnitLimit);
 expect(native.resourceBudgets.liquids.instances).toBeLessThan(native.resourceBudgets.liquids.instanceLimit);
 expect(native.resourceBudgets.physics.bodies).toBeLessThan(native.resourceBudgets.physics.bodyLimit);
 expect(native.resourceBudgets.objects.instances).toBeLessThan(native.resourceBudgets.objects.instanceLimit);
 // These are desktop captures, not a claim that nine rooms are a user workflow
 // or that this count meets headset frame-time/memory requirements.
});

it('accepts the same native waiting and flowing facts for shared user and agent readers',()=>{
 for(const sample of native.trace){
  expect(validFactValue('runtime.flowWorkBudget',sample.flowBudget)).toBe(true);
  expect(validFactValue('runtime.flowDiscoveryBudget',sample.discoveryBudget)).toBe(true);
  expect(sample.flowBudget.units).toBeLessThanOrEqual(sample.flowBudget.unitLimit);
  expect(sample.discoveryBudget.units).toBeLessThanOrEqual(sample.discoveryBudget.unitLimit);
  expect(sample.nativeBudget).toMatchObject({instances:20,preparing:0,retiring:0});
  for(const room of sample.rooms){
   expect(room.pending).toBe(false);
   for(const value of [...room.sources,...room.receivers]){
    expect(validFactValue('object.container.live',value)).toBe(true);
    expect(value.contents.amountMl).toBeGreaterThanOrEqual(0);
    expect(value.contents.amountMl).toBeLessThanOrEqual(value.contents.capacityMl);
   }
   if(!room.ready){
    expect(room).toMatchObject({discoveryCaptures:0,executedGroups:0,executedMembers:0,pourCandidates:0});
    expect(room.sources[0].phase).toBe('waiting');
   }
  }
 }
 const waiting=native.trace[0].rooms[8];
 expect(waiting.ready).toBe(false);
 expect(waiting.receivers[0].contents.amountMl).toBe(0);
 const resumed=native.trace.find(sample=>sample.rooms[8].receivers[0].contents.amountMl>0);
 expect(resumed).toBeDefined();
 expect(resumed!.step).toBeGreaterThan(native.trace[0].step);
 expect(resumed!.rooms[8].receivers[0].contents.amountMl).toBeLessThanOrEqual(18.75);
 expect(resumed!.rooms[8].sources[0].phase).toBe('flowing');
});

it('preserves captured real regrouping under pressure without loading distant cavities',()=>{
 const merged=native.trace.find(sample=>'topology' in sample&&sample.topology==='merged')!;
 const split=native.trace.find(sample=>'topology' in sample&&sample.topology==='split')!;
 expect(merged).toBeDefined();expect(split).toBeDefined();
 expect(merged.rooms[0]).toMatchObject({executedGroups:2,executedMembers:4,scoopCandidates:10,nativeVessels:4,dormantVessels:12});
 expect(split.rooms[0]).toMatchObject({executedGroups:2,executedMembers:4,scoopCandidates:8,nativeVessels:4,dormantVessels:12});
 expect(split.step).toBeGreaterThan(merged.step);
 expect(validFactValue('runtime.flowDiscoveryBudget',{...split.discoveryBudget,units:'ready'})).toBe(false);
});
