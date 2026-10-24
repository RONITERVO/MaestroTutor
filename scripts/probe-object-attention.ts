// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {completeProbeAction} from './native-probe-contract';
import {nativeProbeObjects} from './native-probe-objects';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
export async function probeObjectAttention(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string,actors:{target:string;part?:string}[]){
 const objects=nativeProbeObjects(execute),results=[];
 for(const actor of actors){
  const before=(await objects.observation(actor.target)).position;
  const call={id:'object.look',version:1,arguments:{target:actor.target,joint:actor.part?{kind:'part',part:actor.part}:{kind:'head'},focus:{kind:'object',objectId:'book'},seconds:1}};
  const started=await execute([{action:'execution',execution:{operation:'start',call}}]);
  const runId=started.execution?.selected?.id;assert.ok(runId);
  const completed=await completeProbeAction(execute,started),receipt=completed.execution?.selected;
  assert.equal(receipt?.phase,'completed',JSON.stringify(receipt));assert.equal(receipt.output?.applied,true);assert.equal(receipt.output?.objectId,'book');
  assert.deepEqual((await objects.observation(actor.target)).position,before);
  const replay=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);assert.deepEqual(replay.execution?.selected,receipt);
  const stopped=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.attention',version:1,arguments:{target:actor.target}}}]);
  assert.ok(stopped.catalog?.operation==='inspect'&&stopped.catalog.category==='facts');
  let retired:Record<string,unknown>|undefined;
  if(stopped.catalog.available)assert.equal((stopped.catalog.value as {active:boolean}).active,false);
  else{
   const presence=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.presence',version:1,arguments:{target:actor.target}}}]);
   assert.ok(presence.catalog?.operation==='inspect'&&presence.catalog.category==='facts'&&presence.catalog.available);
   retired=presence.catalog.value as Record<string,unknown>;assert.equal(retired.saved,true);
   assert.ok(retired.nativeInstance===false||retired.active===false);
  }
  results.push({actor,receipt,stopped:stopped.catalog,retired});
 }
 await writeFile(join(directory,'native-object-attention.json'),JSON.stringify({boundary:'Actual Unity shared action, exact recipe/imported actor, sampled joint presentation, completion, passive fact and replay. Rendered joints are separately asserted in PlayMode; no provider or physical-headset claim.',results},null,2));
}
