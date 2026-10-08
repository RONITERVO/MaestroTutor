// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export type WeatherSettings={rainMmPerHour:number;windX:number;windZ:number;cloudCover:number;fogDensity:number;fogColor:string};
export type Weather={revision:number;seed:number;settings:WeatherSettings;current:WeatherSettings;transition:{seconds:number;progress:number}};
export async function probeWorldWeather(execute:Execute,directory:string){
 const fact=async(capability:string,args?:Record<string,unknown>)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,...(args?{arguments:args}:{})}}])).value;
 const read=async()=>await fact('world.weather') as Weather;
 const set=async(settings:WeatherSettings,seed:number)=>{const before=await read();const s=await execute([{action:'execution',execution:{operation:'start',call:{id:'world.weather.set',version:1,arguments:{revision:before.revision,seed,settings,transitionSeconds:0}}}}]);assert.equal(s.execution?.selected?.phase,'completed',JSON.stringify(s.execution?.selected));return read();};
 const before=await read(),view=await fact('world.presentation'),physics=await fact('physics.environment'),time=await fact('world.time');
 const rain=await set({rainMmPerHour:18,windX:2,windZ:-1,cloudCover:.8,fogDensity:.04,fogColor:'#CCD6E0'},31);
 assert.equal(rain.settings.rainMmPerHour,18);assert.ok(Math.abs(rain.current.cloudCover-.8)<1e-6);assert.equal(rain.transition.progress,1);
 const cover=await fact('world.weather.exposure',{position:{x:3,y:1,z:0},target:''}) as {state:string};assert.ok(['unknown','covered','open'].includes(cover.state));
 await execute([{action:'undo'}]);const undo=await read();assert.deepEqual(undo.settings,before.settings);
 await execute([{action:'redo'}]);const redo=await read();assert.deepEqual(redo.settings,rain.settings);
 const restored=await set(before.settings,before.seed);assert.deepEqual(restored.settings,before.settings);
 assert.deepEqual(await fact('world.presentation'),view);assert.deepEqual(await fact('physics.environment'),physics);assert.deepEqual(await fact('world.time'),time);
 await writeFile(join(directory,'world-weather.json'),JSON.stringify({boundary:'Desktop Unity native saved weather/cover/Undo. No real headset weather acceptance.',before,rain,cover,undo,redo,restored},null,2));
}
