// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand,RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type View={stateId:string;backdropOpacity:number;realDepth:boolean;depthEligible:boolean;virtualView:boolean};
export async function probeWorldPresentation(execute:Execute,directory:string){
 const read=async()=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.presentation',version:1}}])).value as View;
 const physics=async()=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.environment',version:1}}])).value;
 const before=await read(),collision=await physics(),steps:unknown[]=[];
 const finish=async(state:RoomAgentState)=>{
  const id=state.execution?.selected?.id;assert.ok(id);const deadline=Date.now()+10000;
  while(state.execution?.selected?.phase!=='completed'&&Date.now()<deadline){
   assert.notEqual(state.execution?.selected?.phase,'failed',JSON.stringify(state.execution?.selected));
   await new Promise(r=>setTimeout(r,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId:id}}]);
  }assert.equal(state.execution?.selected?.phase,'completed');return state;
 };
 assert.equal(before.backdropOpacity,0);assert.equal(before.realDepth,true);
 for(const [opacity,depth] of [[.25,true],[.5,false],[1,true],[0,true]] as const){
  const current=await read();
  const result=await finish(await execute([{action:'execution',execution:{operation:'start',call:{id:'world.presentation.set',version:1,arguments:{stateId:current.stateId,backdropOpacity:opacity,realDepth:depth}}}}]));
  const after=await read();assert.equal(after.backdropOpacity,opacity);assert.equal(after.realDepth,depth);assert.equal(after.depthEligible,depth&&opacity<1);assert.equal(after.virtualView,opacity===1);
  assert.deepEqual(await physics(),collision,'Changing presentation changed real-room collision policy');
  steps.push({before:current,receipt:result.execution!.selected,after});
 }
 await writeFile(join(directory,'world-presentation.json'),JSON.stringify({boundary:'Actual native shared transport and view facts; no headset compositor/depth or real-provider proof.',before,collision,steps},null,2));
}
