// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import type {RoomCommand,RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {factReply,assertSamePlacement,type NativeProbeState} from './native-probe-contract';
import {assertSameRoomObjects} from './agent-provider-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
const readFact=async(execute:Execute,capability:string)=>{
 const reply=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1}}]));assert.equal(reply.available,true);assert.notEqual(reply.value,null);return reply.value as Record<string,unknown>;
};
type Geometry={revision:number;target:string;modelHash:string;settings:{version:number;scaleMode:string;metresPerUnit:number;pivot:string;meshCollision:boolean;walkable:boolean};ready:boolean;reason:string;sourceSize:{x:number;y:number;z:number};size:{x:number;y:number;z:number}};
const readGeometry=async(execute:Execute,target:string)=>{
 const reply=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.model.geometry',version:1,arguments:{target}}}]));
 assert.equal(reply.available,true);assert.notEqual(reply.value,null);return reply.value as Geometry;
};
export async function prepareAgentModelGeometry(execute:Execute,directory:string){
 const fixture=JSON.parse(await readFile(join(directory,'model-fixture.json'),'utf8')) as {target:string;hash:string};
 assert.match(fixture.target,/^[a-f0-9]{32}$/);assert.match(fixture.hash,/^[a-f0-9]{64}$/);
 let before=await readGeometry(execute,fixture.target);const deadline=Date.now()+15000;
 while(!before.ready&&Date.now()<deadline){await new Promise(r=>setTimeout(r,100));before=await readGeometry(execute,fixture.target);}
 assert.equal(before.ready,true,before.reason);assert.equal(before.settings.scaleMode,'fitted');assert.equal(before.settings.meshCollision,false);
 const baseline=await execute([{action:'rules',rule:{action:'inspect'}}]);
 const placement=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target:fixture.target}}}]);
 const policies={view:await readFact(execute,'world.presentation'),environment:await readFact(execute,'physics.environment'),simulation:await readFact(execute,'physics.simulation')};
 return {fixture,before,baseline,placement,policies};
}
export async function runAgentModelGeometryProof({client,execute,directory,fixture,before,baseline,placement,policies}:{client:HeadlessClient;execute:Execute;directory:string;fixture:{target:string;hash:string};before:Geometry;baseline:RoomAgentState;placement:RoomAgentState;policies:{view:Record<string,unknown>;environment:Record<string,unknown>;simulation:Record<string,unknown>}}){
 const checkPlacement=async()=>{
  assertSamePlacement(placement,await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.placement',version:1,arguments:{target:fixture.target}}}]),'Model geometry edit');
  assert.deepEqual(await readFact(execute,'world.presentation'),policies.view);
  const environment=await readFact(execute,'physics.environment'),simulation=await readFact(execute,'physics.simulation');
  assert.equal(environment.realCollisions,policies.environment.realCollisions);assert.equal(environment.scope,policies.environment.scope);assert.equal(simulation.running,policies.simulation.running);
 };
 const source=await readGeometry(execute,fixture.target);assert.equal(source.modelHash,fixture.hash);assert.equal(source.ready,true,source.reason);
 assert.deepEqual(source.settings,{version:1,scaleMode:'source',metresPerUnit:1,pivot:'source',meshCollision:true,walkable:true});
 await checkPlacement();assert.ok(Math.abs(source.size.x-6)<.001);assert.ok(Math.abs(source.size.y-3.1)<.001);
 const followup=await runHeadlessRoomTurn(client,{text:"Keep that building solid at its current size, but please stop using its floors for walking. Leave its position and everything else alone."});
 const solid=await readGeometry(execute,fixture.target);assert.equal(solid.ready,true,solid.reason);assert.deepEqual(solid.settings,{...source.settings,walkable:false});assert.deepEqual(solid.size,source.size);await checkPlacement();
 const restore=await runHeadlessRoomTurn(client,{text:"Please turn that building back into the usual small centred display model, with the ordinary bounding-box collision again. Keep its saved model, its placement and everything else as they were."});
 const restored=await readGeometry(execute,fixture.target);assert.equal(restored.ready,true,restored.reason);assert.deepEqual(restored.settings,before.settings);assert.deepEqual(restored.size,before.size);await checkPlacement();
 const after=await execute([{action:'rules',rule:{action:'inspect'}}]);assertSameRoomObjects(baseline,after);assert.deepEqual(after.rules?.sequences,baseline.rules?.sequences);
 const result={scenario:'ModelGeometry',phase:'passed',boundary:'Real original-app chat and delegated provider against native GLB import, saved settings, readiness and restoration. Original synthetic model; no real headset, file picker, live collision or navigation performance claim.',fixture,policies,before,source,followup,solid,restore,restored,after};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
