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
type Clock={revision:number;day:number;second:number;settings:{running:boolean;rate:number;cycleEnabled:boolean;frames:{second:number;ambientColor:string;sunColor:string;ambientIntensity:number;sunIntensity:number}[]};advancing:boolean};
export async function runAgentWorldTimeProof({client,initial,execute,directory,read}:{client:HeadlessClient;initial:RoomAgentState;execute:Execute;directory:string;read:()=>RoomAgentState}){
 const fact=async(capability:string)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1}}])).value as Record<string,unknown>;
 const clock=async()=>await fact('world.time') as unknown as Clock;
 const noon=await clock(),light=await fact('world.illumination'),view=await fact('world.presentation'),physics=await fact('physics.environment');
 assert.equal(noon.settings.cycleEnabled,true);assert.equal(noon.settings.running,false);assert.equal(noon.settings.rate,60);assert.equal(noon.second,43200);assert.equal(light.enabled,true);
 const nightFrame=noon.settings.frames.find(f=>f.second===0),dayFrame=noon.settings.frames.find(f=>f.second===43200);assert.ok(nightFrame&&dayFrame,'Cycle must include midnight and noon as requested');
 const rgb=(s:string)=>[1,3,5].map(i=>Number.parseInt(s.slice(i,i+2),16));assert.ok(rgb(nightFrame.ambientColor)[2]>rgb(nightFrame.ambientColor)[0]);assert.ok(rgb(dayFrame.sunColor)[0]>rgb(dayFrame.sunColor)[2]);assert.ok(nightFrame.ambientIntensity<dayFrame.ambientIntensity);assert.ok(nightFrame.sunIntensity<dayFrame.sunIntensity);
 assertSameRoomObjects(initial,read());assert.equal(view.backdropOpacity,0);assert.equal(view.realDepth,true);assert.equal(physics.realCollisions,true);
 const startJourney=await runHeadlessRoomTurn(client,{text:'Please start that world clock now. Keep the day and night lighting we just made and leave everything else alone.'});const started=await clock();assert.equal(started.settings.running,true);assert.equal(started.advancing,true);assert.deepEqual(started.settings.frames,noon.settings.frames);assert.equal(started.settings.rate,60);
 await new Promise(resolve=>setTimeout(resolve,200));const advanced=await clock();assert.ok(advanced.day*86400+advanced.second>started.day*86400+started.second);assert.equal(advanced.revision,started.revision);
 const pauseJourney=await runHeadlessRoomTurn(client,{text:'Now pause the world clock and set it to midnight. Keep our lighting cycle saved, and keep all objects, my room view and collisions unchanged.'});const paused=await clock(),midnight=await fact('world.illumination');assert.equal(paused.settings.running,false);assert.equal(paused.settings.cycleEnabled,true);assert.equal(paused.second,0);assert.deepEqual(paused.settings.frames,noon.settings.frames);assert.equal(paused.settings.rate,60);
 await new Promise(resolve=>setTimeout(resolve,100));assert.equal((await clock()).second,0);const energy=midnight.ambient as {r:number;b:number};assert.ok(energy.b>energy.r);assertSameRoomObjects(initial,read());assert.deepEqual(await fact('world.presentation'),view);assert.deepEqual(await fact('physics.environment'),physics);
 const result={scenario:'WorldTime',phase:'passed',boundary:'Fresh original chat and real provider to desktop Unity; no Quest appearance/timing acceptance.',semantics:{authoredCycle:true,retainedTimeOnConfigure:true,activeProgress:true,pausedMidnight:true,settingsRetained:true,objectsViewCollisionsPreserved:true},noon,light,startJourney,started,advanced,pauseJourney,paused,midnight};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
