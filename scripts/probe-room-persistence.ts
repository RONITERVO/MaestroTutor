// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {validFactValue} from '../shared/behaviourFacts';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Store={phase:string;pending:boolean;dirty:boolean;dispatchHeld:boolean;error:string};
type Checkpoints={room:Store;behaviours:Store};
export async function probeRoomPersistence(execute:Execute,directory:string){
 const samples:Checkpoints[]=[];
 const end=Date.now()+20000;
 do{
  const value=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.persistence',version:1,arguments:{}}}])).value;
  assert.ok(validFactValue('room.persistence',value),'Native checkpoint status must match the shared catalog');
  const state=value as Checkpoints;samples.push(state);
  for(const store of Object.values(state))assert.ok(!['unconfirmed','unavailable','temporary'].includes(store.phase),JSON.stringify(state));
  if(Object.values(state).every(store=>store.phase==='idle'&&!store.pending&&!store.dirty&&!store.dispatchHeld&&store.error===''))break;
  await new Promise(resolve=>setTimeout(resolve,100));
 }while(Date.now()<end);
 assert.ok(Object.values(samples.at(-1)!).every(store=>store.phase==='idle'&&!store.pending&&!store.dirty&&!store.dispatchHeld&&store.error===''),'Accepted journal edits did not reach confirmed idle');
 await writeFile(join(directory,'room-persistence.json'),JSON.stringify({samples,boundary:'Real Unity catalog observations after accepted edits; no forced process termination, headset or provider claim. Pause coalescing and failure ordering are exercised by native PlayMode tests.'},null,2));
}
