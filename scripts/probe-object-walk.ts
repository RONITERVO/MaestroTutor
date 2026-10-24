// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {nativeProbeObjects} from './native-probe-objects';
import {completeProbeAction} from './native-probe-contract';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand,RoomAgentState} from '../src/core-sdk/room/roomAgent';
type Execute=(commands:RoomCommand[])=>Promise<RoomAgentState>;
export type GaitFixture={target:string;hash:string};
export async function gaitFixture(directory:string):Promise<GaitFixture|null>{return readFile(join(directory,'gait-fixture.json'),'utf8').then(text=>JSON.parse(text) as GaitFixture).catch(error=>{if(error.code==='ENOENT')return null;throw error;});}
export async function prepareObjectWalk(execute:Execute,directory:string){
 const fixture=await gaitFixture(directory);if(!fixture)return null;const {target,hash}=fixture;
 const fact=async(id:string,args:Record<string,unknown>)=>{
  const state=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,arguments:args}}]);
  assert.ok(state.catalog?.operation==='inspect'&&state.catalog.category==='facts');assert.equal(state.catalog.available,true,JSON.stringify(state.catalog));return state.catalog!.value as Record<string,unknown>;
 };
 const readyGeometry=async()=>{const deadline=Date.now()+30000;let value=await fact('object.model.geometry',{target});while(value.ready!==true&&Date.now()<deadline){await new Promise(r=>setTimeout(r,100));value=await fact('object.model.geometry',{target});}assert.equal(value.ready,true,JSON.stringify(value));return value;};
 const geometry=await readyGeometry();
 await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.model.geometry.set',version:1,arguments:{target,revision:geometry.revision,settings:{...geometry.settings as object,pivot:'base'}}}}}]));
 await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.layout.apply',version:1,arguments:{placements:[{target,position:{x:-3,y:0,z:-4},rotation:{x:0,y:0,z:0,w:1},scale:1}]}}}}]));
 await readyGeometry();
 const before=await fact('object.walk.settings',{target}),clips=await fact('object.walk.clips',{target,modelHash:hash,offset:0});assert.equal(clips.total,1);
 const saved=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.walk.select',version:1,arguments:{target,revision:before.revision,modelHash:hash,source:'embedded',clipIndex:0}}}}]));assert.equal(saved.execution?.selected?.phase,'completed');
 const after=await fact('object.walk.settings',{target});assert.equal((after.selection as {source:string}).source,'embedded');assert.equal((after.selection as {playbackStatus:string}).playbackStatus,'inactive');
 const feetBefore=await fact('object.feet.settings',{target});assert.equal(feetBefore.modelHash,hash);
 const footArgs={target,revision:feetBefore.revision,mode:'grounded',modelHash:hash,leftClearance:.01,rightClearance:.005};
 const feetSaved=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.feet.configure',version:1,arguments:footArgs}}}]));
 const feetAfter=await fact('object.feet.settings',{target});
 assert.equal(feetAfter.mode,'grounded');assert.equal(feetAfter.modelHash,hash);
 assert.equal(feetAfter.leftClearance,.01);assert.equal(feetAfter.rightClearance,.005);
 assert.equal(feetSaved.execution?.selected?.output?.revision,feetAfter.revision);
 await writeFile(join(directory,'native-object-feet.json'),JSON.stringify({boundary:'Actual native shared catalog, exact model, ordered save completion and passive readback. Full solver/authoring and deformation checks are in PlayMode. No provider or headset proof.',fixture,before:feetBefore,arguments:footArgs,receipt:feetSaved.execution?.selected,after:feetAfter},null,2));
 await writeFile(join(directory,'native-object-walk-selection.json'),JSON.stringify({boundary:'Explicit synthetic skinned character, real shared settings/clip-list/action/save path; selection alone does not move or play.',fixture,before,clips,receipt:saved.execution?.selected,after},null,2));return fixture;
}
export async function probeObjectWalk(execute:Execute,directory:string,fixture:GaitFixture|null){
 const objects=nativeProbeObjects(execute);
 if(!fixture)return;const target=fixture.target;
 const call={id:'object.navigate',version:1,arguments:{target,mode:'walk',destination:{x:-1,y:0,z:-4},speed:.5}};
 const start=await execute([{action:'execution',execution:{operation:'start',call}}]);const runId=start.execution?.selected?.id;assert.ok(runId);
 let state=start;const samples:Array<{position:unknown;selection:unknown}>=[],deadline=Date.now()+30000;
 while(Date.now()<deadline){
  const fact=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.walk.settings',version:1,arguments:{target}}}]);
  assert.ok(fact.catalog?.operation==='inspect'&&fact.catalog.category==='facts');assert.equal(fact.catalog.available,true);samples.push({position:(await objects.observation(target)).position,selection:(fact.catalog!.value as {selection:unknown}).selection});
  state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);if(state.execution?.selected?.phase==='completed')break;
  assert.ok(!['failed','cancelled'].includes(state.execution?.selected?.phase??''),JSON.stringify(state.execution?.selected));await new Promise(r=>setTimeout(r,60));
 }
 assert.equal(state.execution?.selected?.phase,'completed',JSON.stringify(state.execution?.selected));assert.ok(samples.some(s=>(s.selection as {playbackStatus:string}).playbackStatus==='walking'),'No native gait playback observed');
 const position=(await objects.observation(target)).position;assert.ok(Math.abs(position.x+1)<.02&&Math.abs(position.z+4)<.02);
 await writeFile(join(directory,'native-object-walk.json'),JSON.stringify({boundary:'Actual native movement and assigned-pose ownership observed through headless facts/receipts. Full joint/root-neutralization proof is in PlayMode tests. No provider/headset or foot-planting claim.',fixture,samples,receipt:state.execution?.selected,position},null,2));
}

/** Focused imported-gait residency regression, also useful without the long
 * room journey. Uses ordinary area demand rather than forcing native state. */
export async function probeGaitRestoration(execute:Execute,directory:string){
 const fixture=await prepareObjectWalk(execute,directory);assert.ok(fixture,'Synthetic gait fixture required');const target=fixture.target;
 const fact=async(id:string,args?:Record<string,unknown>)=>{const state=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,...(args===undefined?{}:{arguments:args})}}]);assert.ok(state.catalog?.operation==='inspect'&&state.catalog.category==='facts'&&state.catalog.available,JSON.stringify(state.catalog));return state.catalog.value as Record<string,unknown>;};
 const invoke=async(id:string,args:Record<string,unknown>)=>(await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]))).execution!.selected!;
 const area=await invoke('world.region.save',{id:'',revision:0,name:'Imported gait recovery',members:[],loading:'independent'}),id=String(area.output!.id);
 await invoke('object.region.assign',{target,revision:(await fact('object.region',{target})).revision,regionId:id,regionRevision:area.output!.revision});
 let retention=await fact('world.region.retention',{id});const deadline=Date.now()+25000;
 while(retention.retirementState!=='dormant'&&Date.now()<deadline){await new Promise(r=>setTimeout(r,200));retention=await fact('world.region.retention',{id});}
 assert.equal(retention.retirementState,'dormant',JSON.stringify(retention));
 const companion=await invoke('object.create',{kind:'primitive',shape:'ball',name:'Gait companion',x:0,y:1,z:2,scale:.2,red:1,green:1,blue:1}),anchor=String(companion.output!.objectId);
 await invoke('object.region.assign',{target:anchor,revision:(await fact('object.region',{target:anchor})).revision,regionId:id,regionRevision:(await fact('world.region',{id})).revision});
 await invoke('world.region.save',{id,revision:(await fact('world.region',{id})).revision,name:'Imported gait recovery',members:[target,anchor].sort(),loading:'together'});
 let presence=await fact('object.presence',{target});const restoreDeadline=Date.now()+18000;
 while(!presence.active&&Date.now()<restoreDeadline){await new Promise(r=>setTimeout(r,100));presence=await fact('object.presence',{target});}assert.equal(presence.active,true);
 const recovery=await fact('object.environment.recovery',{target}),geometry=await fact('object.model.geometry',{target}),inputs=await fact('object.physics.inputs',{target}),placement=await fact('object.placement',{target});
 await writeFile(join(directory,'native-gait-restoration.json'),JSON.stringify({fixture,retention,presence,recovery,geometry,inputs,placement},null,2));
 assert.equal(recovery.held,false,JSON.stringify({recovery,geometry,inputs}));
 const physics=await fact('physics.simulation');await invoke('physics.simulation.set',{operation:'start',stateId:physics.stateId});
 await probeObjectWalk(execute,directory,fixture);
 const running=await fact('physics.simulation');await invoke('physics.simulation.set',{operation:'pause',stateId:running.stateId});
 await invoke('object.delete',{target:anchor});
 await invoke('object.region.assign',{target,revision:(await fact('object.region',{target})).revision,regionId:'',regionRevision:0});
 await invoke('world.region.remove',{id,revision:(await fact('world.region',{id})).revision});
}
