// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {factReply,type NativeProbeState} from './native-probe-contract';
import {assertSameRoomObjects} from './agent-provider-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function prepareAgentImportedAudio(execute:Execute,directory:string){
 const fixture=JSON.parse(await readFile(join(directory,'sound-fixture.json'),'utf8')) as {hash:string;seconds:number;name:string};
 assert.match(fixture.hash,/^[a-f0-9]{64}$/);assert.equal(fixture.name,'Little bell.wav');assert.equal(fixture.seconds,1);
 const baseline=await execute([{action:'rules',rule:{action:'inspect'}}]);
 const sounds=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.source.list',version:1,arguments:{offset:0}}}])).value as {total:number};assert.equal(sounds.total,0);
 return {baseline,fixture};
}
export async function runAgentImportedAudioProof({client,execute,directory,baseline,fixture}:{client:HeadlessClient;execute:Execute;directory:string;baseline:NativeProbeState<RoomCommand[]>;fixture:{hash:string;seconds:number;name:string}}){
 const read=async(capability:string,args?:Record<string,unknown>,version=1)=>{
  const fact=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version,...(args===undefined?{}:{arguments:args})}}]));
  assert.equal(fact.available,true,`Expected available native fact ${capability}`);assert.notEqual(fact.value,null,`Missing native value for ${capability}`);return fact.value;
 };
 const sources=await read('audio.source.list',{offset:0}) as {total:number;entries:{id:string;kind:string}[]};assert.equal(sources.total,1);assert.equal(sources.entries[0].kind,'clip');const source=sources.entries[0].id;
 const exact=await read('audio.source.definition',{id:source},2) as {definition:{clip:{assetHash:string;seconds:number}}};assert.equal(exact.definition.clip.assetHash,fixture.hash);assert.equal(exact.definition.clip.seconds,fixture.seconds);
 const attached=await read('object.audioEmitters',{target:'book'}) as {entries:{emitter:string;source:string}[]};assert.equal(attached.entries.length,1);assert.equal(attached.entries[0].source,source);const emitter=attached.entries[0].emitter;
 const configured=await read('object.audioEmitter',{target:'book',emitter}) as {definition:{spatial:boolean;gain:number}};assert.equal(configured.definition.spatial,true);assert.ok(configured.definition.gain>0&&configured.definition.gain<=1);
 const active=await read('audio.instances') as {entries:unknown[]};assert.equal(active.entries.length,0,'Attaching a clip must not start it');
 const beforePlay=await execute([{action:'rules',rule:{action:'inspect'}}]);assertSameRoomObjects(baseline,beforePlay);assert.deepEqual(beforePlay.rules?.sequences,baseline.rules?.sequences);
 const played=await runHeadlessRoomTurn(client,{text:"Please play the book's little chime once now, then leave it quiet."});
 const task=await client.roomAgent!.store.get(played.task!.id);assert.ok(task);
 const playback=task.operations.map(o=>o.receipt?.execution?.selected).find(v=>v?.capability==='audio.play'&&v.phase==='completed'&&v.output?.source===source);
 let consumed=Number(playback?.output?.seconds??0);
 if(!playback){
  const started=task.operations.map(o=>o.receipt?.execution?.selected).find(v=>v?.capability==='audio.start'&&v.phase==='completed');assert.ok(started,'Expected a native playback receipt');
  const instance=(started.output?.identity as {instance:string})?.instance;assert.ok(instance);
  let state=await read('audio.instance',{target:'book',instance}) as {playback:{phase:string;seconds:number;loop:boolean}};
  const deadline=Date.now()+5000;while(state.playback.phase!=='completed'&&Date.now()<deadline){await new Promise(r=>setTimeout(r,100));state=await read('audio.instance',{target:'book',instance}) as typeof state;}
  assert.equal(state.playback.phase,'completed');assert.equal(state.playback.loop,false);consumed=state.playback.seconds;
 }
 assert.ok(consumed>=fixture.seconds-.0001,'Completion must follow native PCM consumption');
 const removed=await runHeadlessRoomTurn(client,{text:"Please take that chime off the book, but keep the imported sound in my world so I can use it again. Don't play anything."});
 const remaining=await read('object.audioEmitters',{target:'book'}) as {entries:unknown[]};assert.equal(remaining.entries.length,0);
 const kept=await read('audio.source.definition',{id:source},2);assert.deepEqual(kept,exact);
 const after=await execute([{action:'rules',rule:{action:'inspect'}}]);assertSameRoomObjects(baseline,after);assert.deepEqual(after.rules?.sequences,baseline.rules?.sequences);
 const result={scenario:'ImportedAudio',phase:'passed',boundary:'Fresh real-provider chat uses one synthetic one-second WAV seeded through the private AudioLibrary. Native decoder/emitter/PCM completion and exact-source retention are real; file-picker interaction and human audibility are separate checks.',fixture,source,emitter,exact,configured,played,consumed,removed,kept,after};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
