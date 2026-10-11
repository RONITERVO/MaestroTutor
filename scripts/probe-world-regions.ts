// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeWorldRegions(execute:Execute,directory:string){
 const fact=async(capability:string,args:Record<string,unknown>)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}])).value as Record<string,unknown>;
 const invoke=async(id:string,args:Record<string,unknown>)=>{
  const state=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);
  assert.equal(state.execution!.selected!.phase,'completed');return state.execution!.selected!;
 };
 const state=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.regions',version:1,arguments:{offset:0}}}]);
 const target=state.objects.find(o=>/^[a-fA-F0-9]{32}$/.test(o.id))!.id;
 const before=await fact('object.region',{target}),definition=await fact('object.definition',{target});
 const presence=await fact('object.presence',{target});assert.equal(presence.state,'active');assert.equal(presence.saved,true);assert.equal(presence.nativeInstance,true);assert.equal(presence.active,true);assert.equal(presence.revision,definition.revision);
 const spatial=await fact('object.spatialBounds',{target});assert.equal(spatial.source,'native');assert.equal(spatial.coordinates,'room');assert.equal(spatial.revision,definition.revision);
 const visual=spatial.visual as {known:boolean;hasBounds:boolean;min:{x:number;y:number;z:number};max:{x:number;y:number;z:number}};assert.equal(visual.known,true);assert.equal(visual.hasBounds,true);for(const axis of ['x','y','z'] as const){assert.ok(Number.isFinite(visual.min[axis]));assert.ok(visual.min[axis]<=visual.max[axis]);}
 const observed=state.objects.find(o=>o.id===target)!;assert.equal(observed.runtimeState,'active');assert.equal(observed.positionSource,'live');
 const missing=await fact('object.presence',{target:'0'.repeat(32)});assert.equal(missing.state,'missing');assert.equal(missing.saved,false);assert.equal(missing.revision,0);

 const absentSpatial=await fact('object.spatialBounds',{target:'0'.repeat(32)});assert.equal(absentSpatial.source,'missing');assert.equal((absentSpatial.visual as {known:boolean}).known,false);

 const saved=await invoke('world.region.save',{id:'',revision:0,name:'Learner garden',members:[]});const id=String(saved.output!.id);
 const assigned=await invoke('object.region.assign',{target,revision:before.revision,regionId:id,regionRevision:saved.output!.revision});
 const bound=await fact('object.region',{target}),members=await fact('world.region.members',{id,offset:0});
 assert.equal(bound.regionId,id);assert.deepEqual(members.members,[target]);assert.equal(bound.homeRegionId,before.homeRegionId);
 const retention=await fact('world.region.retention',{id});assert.equal(retention.memberCount,1);assert.equal(retention.residentCount,1);assert.equal(retention.unloadingSupported,false);assert.ok(Array.isArray(retention.reasons));
 const sameDefinition=await fact('object.definition',{target});delete definition.revision;delete sameDefinition.revision;assert.deepEqual(sameDefinition,definition,'Region assignment moved or rebuilt authored content');
 const renamed=await invoke('world.region.save',{id,revision:members.revision,name:'Café garden',members:[target]});
 const named=await fact('world.region',{id});assert.equal(named.name,'Café garden');assert.equal(named.memberCount,1);
 await execute([{action:'undo'}]);assert.equal((await fact('world.region',{id})).name,'Learner garden');
 await execute([{action:'undo'}]);assert.equal((await fact('object.region',{target})).regionId,before.regionId);
 const empty=await fact('world.region',{id});const removed=await invoke('world.region.remove',{id,revision:empty.revision});
 await writeFile(join(directory,'world-regions.json'),JSON.stringify({boundary:'Actual shared native transport, saved authored areas, assignment, rename and Undo. No region streaming, enlarged capacity, provider or headset claim.',before,presence,spatial,absentSpatial,missing,observed,saved,assigned,bound,members,retention,renamed,named,removed},null,2));
}
