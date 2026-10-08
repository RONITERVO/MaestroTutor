// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function readWaterActorPose(execute:Execute,target:string){
 const saved=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target}}}]));
 const live=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.position',version:2,arguments:{target}}}]));
 assert.equal(saved.available,true);assert.equal(live.available,true);
 const data=saved.value as Record<string,unknown>;
 return {target,saved:{position:data.position,rotation:data.rotation,scale:data.scale},live:live.value};
}
export async function probeWaterTraversal(execute:Execute,directory:string){
 const target='maestro',fact='object.water.traversal.settings';
 const read=async()=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact,version:1,arguments:{target}}}])).value as {target:string;revision:number;mode:string;effectiveMode:string;maxDepthMetres:number;temporary:boolean};
 const placement=async()=>readWaterActorPose(execute,target);
 const before=await read(),pose=await placement();
 const result=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.water.traversal.configure',version:1,arguments:{target,revision:before.revision,mode:'wade',maxDepthMetres:.15}}}}]);
 assert.equal(result.execution?.selected?.phase,'completed',JSON.stringify(result.execution?.selected));
 const after=await read();assert.equal(after.mode,'wade');assert.equal(after.effectiveMode,'wade');assert.equal(after.maxDepthMetres,.15);assert.ok(after.revision>before.revision);
 const unchanged=await placement();assert.deepEqual(unchanged,pose);
 await execute([{action:'undo'}]);const undone=await read();assert.equal(undone.mode,before.mode);assert.equal(undone.maxDepthMetres,before.maxDepthMetres);
 await execute([{action:'redo'}]);assert.equal((await read()).mode,'wade');await execute([{action:'undo'}]);
 await writeFile(join(directory,'water-traversal.json'),JSON.stringify({boundary:'Native shared action/fact transport, exact revisions, placement preservation and Undo/Redo. Water collision and traversal have separate PlayMode coverage; no headset claim.',before,after,undone,receipt:result.execution?.selected},null,2));
}
