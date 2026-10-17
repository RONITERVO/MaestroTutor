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
 await execute([{action:'execution',execution:{operation:'start',call:{id:'object.model.geometry.set',version:1,arguments:{target,revision:geometry.revision,settings:{...geometry.settings as object,pivot:'base'}}}}}]);
 await execute([{action:'execution',execution:{operation:'start',call:{id:'object.layout.apply',version:1,arguments:{placements:[{target,position:{x:-3,y:0,z:-4},rotation:{x:0,y:0,z:0,w:1},scale:1}]}}}}]);
 await readyGeometry();
 const before=await fact('object.walk.settings',{target}),clips=await fact('object.walk.clips',{target,modelHash:hash,offset:0});assert.equal(clips.total,1);
 const saved=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'object.walk.select',version:1,arguments:{target,revision:before.revision,modelHash:hash,source:'embedded',clipIndex:0}}}}]));assert.equal(saved.execution?.selected?.phase,'completed');
 const after=await fact('object.walk.settings',{target});assert.equal((after.selection as {source:string}).source,'embedded');assert.equal((after.selection as {playbackStatus:string}).playbackStatus,'inactive');
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
