// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import {isRoomQuery,type RoomCommand} from '../src/core-sdk/room/roomAgent';
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
 const looping=await runHeadlessRoomTurn(client,{text:"Please repeat that little book chime until I ask you to stop. Keep its volume and position the same."});
 type Budget={sources:number;owners:number;retiring:number;reservedPcmBytes:number;readyPcmBytes:number};
 type Reservation={reservationId:string;worldId:string;regionId:string;sourceId:string;kind:string;assetHash:string;definitionHash:string;state:string;owners:number;reservedPcmBytes:number;readyPcmBytes:number};
 type Owner={reservationId:string;instanceId:string;worldId:string;regionId:string;target:string;role:string;sourceId:string;emitter:string;sourceRevision:number};
 const usageBefore=await read('runtime.audioBudget') as Budget;assert.equal(usageBefore.sources,1);assert.equal(usageBefore.owners,1);
 const expectedReservation=await read('runtime.audioReservation',{index:0}) as Reservation;
 assert.equal(expectedReservation.sourceId,source);assert.equal(expectedReservation.assetHash,fixture.hash);assert.equal(expectedReservation.state,'ready');assert.equal(expectedReservation.readyPcmBytes,48000);
 const expectedOwner=await read('runtime.audioOwner',{reservationId:expectedReservation.reservationId,index:0}) as Owner;assert.equal(expectedOwner.target,'book');assert.equal(expectedOwner.sourceId,source);assert.equal(expectedOwner.emitter,emitter);
 const loopingState=await read('audio.instance',{target:'book',instance:expectedOwner.instanceId}) as {playback:{phase:string;loop:boolean;lifetime:string}};
 assert.equal(loopingState.playback.phase,'playing');assert.equal(loopingState.playback.loop,true);assert.equal(loopingState.playback.lifetime,'room');
 const inspectPrompt="Can you explain which objects currently use our loaded sound and how much space its decoded audio takes? Just inspect and explain; keep the chime playing and don't change anything.";
 const inspection=await runHeadlessRoomTurn(client,{text:inspectPrompt,requireActions:false});
 const inspectionTask=(await client.roomAgent!.store.list()).find(value=>value.handoff.input.prompt===inspectPrompt);assert.ok(inspectionTask,'Audio inspection must retain its actual task');
 await writeFile(join(directory,'provider-audio-reservation-inspection.json'),JSON.stringify({looping,inspection,usageBefore,expectedReservation,expectedOwner,operations:inspectionTask.operations},null,2));
 const reservations:Reservation[]=[],owners:Owner[]=[];
 for(const operation of inspectionTask.operations){
  assert.ok(operation.commands.every(isRoomQuery),'Read-only audio request changed the world');
  const request=operation.commands[0];if(request.action!=='catalog'||request.catalog?.operation!=='inspect'||!['runtime.audioReservation','runtime.audioOwner'].includes(request.catalog.capability??''))continue;
  assert.ok(operation.receipt?.ok);const catalog=operation.receipt.catalog;assert.ok(catalog?.operation==='inspect'&&catalog.category==='facts');
  if(!request.catalog.arguments){assert.ok(catalog.definition);continue;}
  assert.ok(catalog.available);if(catalog.capability==='runtime.audioReservation')reservations.push(catalog.value as Reservation);else owners.push(catalog.value as Owner);
 }
 assert.deepEqual(reservations.find(value=>value.reservationId===expectedReservation.reservationId),expectedReservation,'Agent did not inspect the actual decoded sound reservation');
 assert.deepEqual(owners.find(value=>value.instanceId===expectedOwner.instanceId),expectedOwner,'Agent did not inspect the actual sound owner');
 assert.deepEqual(await read('runtime.audioBudget'),usageBefore);assert.deepEqual(await read('audio.source.definition',{id:source},2),exact);
 const removed=await runHeadlessRoomTurn(client,{text:"Please stop the repeating chime and take it off the book, but keep the imported sound in my world so I can use it again. Leave the room quiet."});
 const usageAfter=await read('runtime.audioBudget') as Budget;assert.deepEqual(usageAfter,{...usageBefore,sources:0,owners:0,retiring:0,reservedPcmBytes:0,readyPcmBytes:0});
 const released=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.audioOwner',version:1,arguments:{reservationId:expectedReservation.reservationId,index:0}}}]));assert.equal(released.available,false);

 const remaining=await read('object.audioEmitters',{target:'book'}) as {entries:unknown[]};assert.equal(remaining.entries.length,0);
 const kept=await read('audio.source.definition',{id:source},2);assert.deepEqual(kept,exact);
 const after=await execute([{action:'rules',rule:{action:'inspect'}}]);assertSameRoomObjects(baseline,after);assert.deepEqual(after.rules?.sequences,baseline.rules?.sequences);
 const result={scenario:'ImportedAudio',phase:'passed',boundary:'Fresh real-provider chat uses one synthetic one-second WAV seeded through the private AudioLibrary. Native decoder/emitter/PCM completion, independent loop, read-only ownership/cost inspection, final buffer release and exact-source retention are real; file-picker interaction and human audibility are separate checks.',fixture,source,emitter,exact,configured,played,consumed,looping,inspection,reservations,owners,usageBefore,usageAfter,removed,kept,after};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
