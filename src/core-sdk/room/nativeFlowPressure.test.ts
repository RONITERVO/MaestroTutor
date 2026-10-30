// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/nativeFlowPressure.json';
import {validFactValue} from '../../../shared/behaviourFacts';

// New native captures must contain every presentation counter; the explicit
// shape lets this test reject missing fields in an older evidence file too.
type PourPresentationWork={pourSearchSegments:number;pourDepositionQueries:number;pourSurfaceQueries:number;
 pourPresentationPoints:number;pourSurfaceCaptures:number;pourPresentations:number};

it('reads real native room pressure without equating saved inventory and native allocation',()=>{
 expect(native).toMatchObject({rooms:9,savedContainers:144,nativeContainers:20,dormantContainers:124,conservationAndUndo:true});
 const first=native.trace[0];
 expect(new Set(first.rooms.map(room=>room.worldId)).size).toBe(9);
 expect(first.rooms.every(room=>room.vessels===16)).toBe(true);
 expect(first.rooms.reduce((sum,room)=>sum+room.nativeVessels,0)).toBe(20);
 expect(first.rooms.reduce((sum,room)=>sum+room.dormantVessels,0)).toBe(124);
 expect(native.hostUnits).toBeGreaterThan(native.flowUnitLimit);
 expect(native.groupUnits).toBe(93720);
 expect(first.flowBudget.units).toBe(native.groupUnits);
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
  for(const captured of sample.rooms){
   const room=captured as typeof captured&PourPresentationWork;
   expect(room.pending).toBe(false);
   // Captured query visits stay within the complete-pass reservation; these
   // counters are not measured CPU/GPU time or a particle-simulation claim.
   for(const key of ['pourSearchSegments','pourDepositionQueries','pourSurfaceQueries','pourPresentationPoints','pourSurfaceCaptures','pourPresentations','pourRayQueries','pourHitCandidates','scoopPathQueries','scoopPathCandidates'] as const){
    expect(Number.isInteger(room[key])).toBe(true);expect(room[key]).toBeGreaterThanOrEqual(0);
   }
   expect(room.pourSearchSegments).toBeLessThanOrEqual(room.pourSegments);
   expect(room.pourDepositionQueries).toBeLessThanOrEqual(room.pourSegments);
   expect(room.pourSurfaceQueries).toBeLessThanOrEqual(room.pourSegments);
   expect(room.pourSegments).toBeLessThanOrEqual(room.pourOriginQueries*sample.flowBudget.streamSegments);
   expect(room.pourRayQueries).toBe(room.pourSegments);
   expect(room.pourPresentationPoints).toBeLessThanOrEqual(room.pourOriginQueries*(sample.flowBudget.streamSegments+1));
   expect(room.pourSurfaceCaptures).toBeLessThanOrEqual(room.pourOriginQueries*2);
   expect(room.pourPresentations).toBeLessThanOrEqual(room.pourOriginQueries);
   const maxGroup=room.executedMembers; // A conservative bound also covers the merged three-vessel phase.
   const geometry=room.pourCandidates+room.pourDepositionQueries+room.pourSurfaceQueries;
   expect(geometry).toBeLessThanOrEqual(room.pourOriginQueries*(maxGroup*sample.flowBudget.streamSegments+2));
   expect(geometry+room.pourPresentationPoints+room.pourSurfaceCaptures+room.pourPresentations)
    .toBeLessThanOrEqual(room.pourOriginQueries*(maxGroup*sample.flowBudget.streamSegments+38));
   expect(room.pourHitCandidates).toBeLessThanOrEqual(room.pourRayQueries*sample.flowBudget.queryHits);
   expect(room.scoopPathQueries).toBeLessThanOrEqual(room.scoopCandidates*sample.flowBudget.scoopSamples*3);
   expect(room.scoopPathCandidates).toBeLessThanOrEqual(room.scoopPathQueries*sample.flowBudget.queryHits);
   for(const value of [...room.sources,...room.receivers]){
    expect(validFactValue('object.container.live',value)).toBe(true);
    expect(value.contents.amountMl).toBeGreaterThanOrEqual(0);
    expect(value.contents.amountMl).toBeLessThanOrEqual(value.contents.capacityMl);
   }
   if(!room.ready){
    expect(room).toMatchObject({discoveryCaptures:1,executedGroups:0,executedMembers:0,pourCandidates:0,pourSearchSegments:0,pourDepositionQueries:0,pourSurfaceQueries:0,pourPresentationPoints:0,pourSurfaceCaptures:0,pourPresentations:0,pourRayQueries:0,pourHitCandidates:0,scoopPathQueries:0,scoopPathCandidates:0});
    expect(room.sources[0].phase).toBe('waiting');
   }
  }
 }
 const first=native.trace[0];
 expect(first.rooms.every(room=>room.ready)).toBe(true);
 expect(first.rooms.reduce((count,room)=>count+room.executedGroups,0)).toBe(10);
 expect(first.rooms.reduce((count,room)=>count+room.pourRayQueries,0)).toBeGreaterThan(0);
 expect(first.rooms.reduce((count,room)=>count+room.pourHitCandidates,0)).toBeGreaterThan(0);
 for(const captured of first.rooms){
  const room=captured as typeof captured&PourPresentationWork;
  expect(room.pourCandidates).toBe(2*room.pourSearchSegments);
  expect(room.pourDepositionQueries).toBeGreaterThan(0);expect(room.pourSurfaceQueries).toBeGreaterThan(0);
  expect(room.pourPresentations).toBe(room.sources.length);
  expect(room.receivers[0].contents.amountMl).toBeGreaterThan(0);
  expect(room.receivers[0].contents.amountMl).toBeLessThanOrEqual(18.75);
  expect(room.sources[0].phase).toBe('flowing');
 }
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
