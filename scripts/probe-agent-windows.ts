// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import type {RoomCommand,RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {assertSameRoomObjects} from './agent-provider-contract';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Windows={revision:number;physicalAnchor:boolean;renderingReady:boolean;windows:{id:string;surface:string;shape:string;reveal:number}[]};
export async function runAgentWindowProof({client,initial,execute,directory,read}:{client:HeadlessClient;initial:RoomAgentState;execute:Execute;directory:string;read:()=>RoomAgentState}){
 const fact=async(capability:string,args?:Record<string,unknown>)=>{const reply=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,...args?{arguments:args}:{}}}]));assert.equal(reply.available,true,capability);return reply.value;};
 const added=read().objects.filter(o=>!initial.objects.some(v=>v.id===o.id));assert.equal(added.length,1,'Create one reusable frame');const target=added[0].id;
 assert.equal(added[0].name,'RoomWindow');const before=await fact('object.windows',{target}) as Windows;assert.equal(before.windows.length,1);assert.equal(before.windows[0].shape,'rectangle');assert.equal(before.windows[0].reveal,1);assert.equal(before.physicalAnchor,false);assert.equal(before.renderingReady,false,'Desktop is not physical headset evidence');
 const view=await fact('world.presentation'),physics=await fact('physics.environment');
 const frame=await fact('object.surface',{target,surface:before.windows[0].surface});
 const halfJourney=await runHeadlessRoomTurn(client,{text:"Thanks! Could you make that opening reveal only half as much of my real room? Keep the frame and its position, and leave the rest of the room alone."});
 const half=await fact('object.windows',{target}) as Windows;assert.equal(half.windows.length,1);assert.equal(half.windows[0].id,before.windows[0].id);assert.equal(half.windows[0].reveal,.5);
 const removeJourney=await runHeadlessRoomTurn(client,{text:"Please remove the see-through opening component again, but keep the frame and its surface so I can reuse them later. Keep everything else as it is."});
 const removed=await fact('object.windows',{target}) as Windows;assert.equal(removed.windows.length,0);const after=await execute([{action:'rules',rule:{action:'inspect'}}]);
 assertSameRoomObjects(initial,{...after,objects:after.objects.filter(o=>o.id!==target)});assert.deepEqual(await fact('world.presentation'),view);assert.deepEqual(await fact('physics.environment'),physics);
 const remainingFrame=await fact('object.surface',{target,surface:before.windows[0].surface}) as Record<string,unknown>;assert.deepEqual({...remainingFrame,revision:0},{...(frame as object),revision:0});
 const result={scenario:'PassthroughWindow',phase:'passed',boundary:'Real original-app chat and delegated provider, native save/readback/removal. No physical passthrough, headset comfort, stereo or performance claim. Separate GPU and anchor tests cover native composition.',target,before,frame,halfJourney,half,removeJourney,removed,after};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
