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
import type {Weather} from './probe-world-weather';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function runAgentWorldWeatherProof({client,initial,execute,directory,read}:{client:HeadlessClient;initial:RoomAgentState;execute:Execute;directory:string;read:()=>RoomAgentState}){
 const fact=async(capability:string)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1}}])).value;
 const weather=async()=>await fact('world.weather') as Weather;
 const rain=await weather(),view=await fact('world.presentation'),physics=await fact('physics.environment'),time=await fact('world.time');
 assert.ok(rain.current.rainMmPerHour>0&&rain.current.rainMmPerHour<=30);assert.ok(rain.current.cloudCover>0);assert.ok(rain.current.fogDensity>0&&rain.current.fogDensity<=.1);assert.equal(rain.transition.progress,1);
 assertSameRoomObjects(initial,read());
 const clearJourney=await runHeadlessRoomTurn(client,{text:'Thanks! Now stop the rain and clear the clouds and fog immediately. Keep all of our objects, the world clock, my room view and collisions exactly as they were.'});
 const clear=await weather();assert.equal(clear.current.rainMmPerHour,0);assert.equal(clear.current.cloudCover,0);assert.equal(clear.current.fogDensity,0);
 assertSameRoomObjects(initial,read());assert.deepEqual(await fact('world.presentation'),view);assert.deepEqual(await fact('physics.environment'),physics);assert.deepEqual(await fact('world.time'),time);
 const result={scenario:'WorldWeather',phase:'passed',boundary:'Fresh original English/Spanish chat and real provider to desktop Unity. Does not verify headset weather or physical rain collection.',semantics:{rainCloudFog:true,clearByOrdinaryChat:true,objectsClockViewCollisionsPreserved:true},rain,clearJourney,clear};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
