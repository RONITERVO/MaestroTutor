// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
export async function probeAvatarAttention(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string){
 const definition=await execute([{action:'catalog',catalog:{operation:'inspect',capability:'avatar.look',version:1}}]);
 const call={id:'avatar.look',version:1,arguments:{target:'maestro',focus:{kind:'object',objectId:'book'},seconds:1.5}};
 const check=await execute([{action:'catalog',catalog:{operation:'check',call}}]);assert.ok(check.catalog?.operation==='check');assert.equal(check.catalog.available,true,JSON.stringify(check.catalog));
 const before=check.objects.find(o=>o.id==='maestro')!.position;
 const started=await execute([{action:'execution',execution:{operation:'start',call}}]);const runId=started.execution?.selected?.id;assert.ok(runId);
 await new Promise(r=>setTimeout(r,250));
 const observed=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'avatar.attention',version:1,arguments:{target:'maestro'}}}]);
 assert.ok(observed.catalog?.operation==='inspect'&&observed.catalog.category==='facts');
 const sample=observed.catalog.value as {active:boolean;applied:boolean;focus:string;objectId:string};assert.equal(sample.active,true);assert.equal(sample.applied,true);assert.equal(sample.objectId,'book');
 let state=started;const deadline=Date.now()+8000;
 while(state.execution?.selected?.phase==='running'&&Date.now()<deadline){await new Promise(r=>setTimeout(r,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);}
 const receipt=state.execution?.selected;assert.equal(receipt?.phase,'completed',JSON.stringify(receipt));assert.equal(receipt.output?.focus,'object');assert.equal(receipt.output?.applied,true);
 assert.deepEqual(state.objects.find(o=>o.id==='maestro')!.position,before);
 const replay=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);assert.deepEqual(replay.execution?.selected,receipt);
 const stopped=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'avatar.attention',version:1,arguments:{target:'maestro'}}}]);assert.ok(stopped.catalog?.operation==='inspect'&&stopped.catalog.category==='facts');assert.equal((stopped.catalog.value as {active:boolean}).active,false);
 await writeFile(join(directory,'native-avatar-attention.json'),JSON.stringify({boundary:'Shared catalog, readiness, execution, passive native sampled gaze, timed completion and replay against actual Unity. Native PlayMode tests separately measure the rendered head. No provider or physical headset acceptance.',definition:definition.catalog,check:check.catalog,sample,receipt,replay:replay.execution?.selected,stopped:stopped.catalog},null,2));
}
