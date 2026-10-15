// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {nativeProbeObjects} from './native-probe-objects';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeWorldRegions(execute:Execute,directory:string){
 const objects=nativeProbeObjects(execute);
 const fact=async(capability:string,args?:Record<string,unknown>)=>{
  const reply=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}]));
  assert.equal(reply.available,true,JSON.stringify(reply));return reply.value as Record<string,unknown>;
 };
 const invoke=async(id:string,args:Record<string,unknown>)=>{
  let state=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);
  const runId=state.execution!.selected!.id,deadline=Date.now()+15000;
  while(['preparing','running'].includes(state.execution!.selected!.phase)&&Date.now()<deadline){
   await new Promise(r=>setTimeout(r,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);
  }
  assert.equal(state.execution!.selected!.phase,'completed',JSON.stringify(state.execution!.selected));return state.execution!.selected!;
 };
 await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.regions',version:1,arguments:{offset:0}}}]);
 const target=(await objects.ids()).find(id=>/^[a-f0-9]{32}$/.test(id));assert.ok(target,'Expected a saved creation');
 const originalPosition={...(await objects.observation(target)).position};
 const viewpoint=await fact('world.viewpoint');assert.equal(viewpoint.located,true);
 const floor=viewpoint.position as {x:number;y:number;z:number},yaw=Number(viewpoint.yaw)*Math.PI/180;
 const nearView={x:floor.x+Math.sin(yaw)*3,y:floor.y+.8,z:floor.z+Math.cos(yaw)*3};
 await invoke('object.position.set',{target,...nearView});
 await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.presence',version:1,arguments:{target}}}]);
 const before=await fact('object.region',{target}),definition=await fact('object.definition',{target});
 const presence=await fact('object.presence',{target});assert.equal(presence.state,'active');assert.equal(presence.saved,true);assert.equal(presence.nativeInstance,true);assert.equal(presence.active,true);assert.equal(presence.revision,definition.revision);
 const spatial=await fact('object.spatialBounds',{target});assert.equal(spatial.source,'native');assert.equal(spatial.coordinates,'room');assert.equal(spatial.revision,definition.revision);
 const visual=spatial.visual as {known:boolean;hasBounds:boolean;min:{x:number;y:number;z:number};max:{x:number;y:number;z:number}};assert.equal(visual.known,true);assert.equal(visual.hasBounds,true);for(const axis of ['x','y','z'] as const){assert.ok(Number.isFinite(visual.min[axis]));assert.ok(visual.min[axis]<=visual.max[axis]);}
 const observed=await objects.observation(target);assert.equal(observed.runtimeState,'active');assert.equal(observed.positionSource,'live');
 const missing=await fact('object.presence',{target:'0'.repeat(32)});assert.equal(missing.state,'missing');assert.equal(missing.saved,false);assert.equal(missing.revision,0);

 const absentSpatial=await fact('object.spatialBounds',{target:'0'.repeat(32)});assert.equal(absentSpatial.source,'missing');assert.equal((absentSpatial.visual as {known:boolean}).known,false);

 const saved=await invoke('world.region.save',{id:'',revision:0,name:'Learner garden',members:[]});const id=String(saved.output!.id);
 const assigned=await invoke('object.region.assign',{target,revision:before.revision,regionId:id,regionRevision:saved.output!.revision});
 const bound=await fact('object.region',{target}),members=await fact('world.region.members',{id,offset:0});
 assert.equal(bound.regionId,id);assert.deepEqual(members.members,[target]);assert.equal(bound.homeRegionId,before.homeRegionId);
 const retention=await fact('world.region.retention',{id});assert.equal(retention.memberCount,1);assert.equal(retention.residentCount,1);assert.equal(retention.unloadingSupported,true);assert.ok(Array.isArray(retention.reasons));
 const sameDefinition=await fact('object.definition',{target});delete definition.revision;delete sameDefinition.revision;assert.deepEqual(sameDefinition,definition,'Region assignment moved or rebuilt authored content');
 const renamed=await invoke('world.region.save',{id,revision:members.revision,name:'Café garden',members:[target]});
 const named=await fact('world.region',{id});assert.equal(named.name,'Café garden');assert.equal(named.memberCount,1);
 await execute([{action:'undo'}]);assert.equal((await fact('world.region',{id})).name,'Learner garden');
 await execute([{action:'undo'}]);assert.equal((await fact('object.region',{target})).regionId,before.regionId);
 const empty=await fact('world.region',{id});const removed=await invoke('world.region.remove',{id,revision:empty.revision});
 await invoke('object.position.set',{target,...nearView});
 const viewArea=await invoke('world.region.save',{id:'',revision:0,name:'Viewer demand check',members:[]});
 const viewBinding=await fact('object.region',{target});
 await invoke('object.region.assign',{target,revision:viewBinding.revision,regionId:viewArea.output!.id,regionRevision:viewArea.output!.revision});
 const viewerDemand=await fact('world.region.retention',{id:viewArea.output!.id});
 assert.ok((viewerDemand.reasons as string[]).includes('approachingView'),JSON.stringify(viewerDemand));
 assert.ok((viewerDemand.reasons as string[]).includes('recentObservation'),JSON.stringify(viewerDemand));
 const behindView={x:floor.x-Math.sin(yaw)*6,y:floor.y+.8,z:floor.z-Math.cos(yaw)*6};
 await invoke('object.position.set',{target,...behindView});
 const dormantDefinition=await fact('object.definition',{target});
 let dormant=await fact('world.region.retention',{id:viewArea.output!.id});const retirementDeadline=Date.now()+18000;
 while(dormant.retirementState!=='dormant'&&Date.now()<retirementDeadline){await new Promise(r=>setTimeout(r,150));dormant=await fact('world.region.retention',{id:viewArea.output!.id});}
 assert.equal(dormant.retirementState,'dormant',JSON.stringify(dormant));assert.equal(dormant.residentCount,0);
 const dormantPresence=await fact('object.presence',{target});assert.equal(dormantPresence.saved,true);assert.equal(dormantPresence.nativeInstance,false);
 assert.deepEqual(await fact('object.definition',{target}),dormantDefinition,'Automatic retirement changed authored content');
 const restored=await invoke('object.position.set',{target,...nearView});const restoredPresence=await fact('object.presence',{target});assert.equal(restoredPresence.active,true);
 await writeFile(join(directory,'automatic-area-retirement.json'),JSON.stringify({boundary:'Real Unity workspace automatically retires an idle out-of-view area; passive facts preserve exact saved content; the ordinary agent action acquires it and restores it to view. Physics remains paused. No increased capacity, physical headset, provider or per-area physics claim.',target,area:viewArea.output!.id,viewerDemand,behindView,dormantDefinition,dormant,dormantPresence,restored,restoredPresence},null,2));
 await invoke('object.position.set',{target,...originalPosition});
 const restoredBinding=await fact('object.region',{target});
 await invoke('object.region.assign',{target,revision:restoredBinding.revision,regionId:'',regionRevision:0});
 const viewAreaCurrent=await fact('world.region',{id:viewArea.output!.id});await invoke('world.region.remove',{id:viewArea.output!.id,revision:viewAreaCurrent.revision});
 await writeFile(join(directory,'viewer-demand.json'),JSON.stringify({boundary:'Shared client uses ordinary placement and area actions, then reads frame-owned observer demand from the actual Unity camera. No headset or automatic-streaming proof.',viewpoint,nearView,viewerDemand},null,2));
 await writeFile(join(directory,'world-regions.json'),JSON.stringify({boundary:'Actual shared native transport, saved authored areas, assignment, rename and Undo. No region streaming, enlarged capacity, provider or headset claim.',before,presence,spatial,absentSpatial,missing,observed,saved,assigned,bound,members,retention,renamed,named,removed},null,2));
}
