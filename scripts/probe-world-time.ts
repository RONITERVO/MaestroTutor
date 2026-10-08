// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Frame={second:number;ambientColor:string;sunColor:string;ambientIntensity:number;sunIntensity:number;azimuth:number;elevation:number};
type Settings={running:boolean;rate:number;cycleEnabled:boolean;frames:Frame[]};
type Clock={revision:number;worldId:string;regionId:string;day:number;second:number;settings:Settings;advancing:boolean;temporary:boolean};
export async function probeWorldTime(execute:Execute,directory:string){
 const fact=async(capability:string)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1}}])).value;
 const read=async()=>await fact('world.time') as Clock;
 const invoke=async(id:string,args:Record<string,unknown>)=>{const state=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);assert.equal(state.execution?.selected?.phase,'completed',JSON.stringify(state.execution?.selected));return state;};
 const before=await read(),view=await fact('world.presentation'),physics=await fact('physics.environment'),manual=await fact('world.lighting');
 const frame=(second:number,ambientColor:string,ambientIntensity:number,sunIntensity:number,elevation:number):Frame=>({second,ambientColor,ambientIntensity,sunColor:'#FFFFFF',sunIntensity,elevation,azimuth:0});
 const settings:Settings={running:false,rate:60,cycleEnabled:true,frames:[frame(0,'#0000FF',.2,0,-60),frame(43200,'#FFFFFF',.6,1,60)]};
 await invoke('world.time.configure',{revision:before.revision,settings});const configured=await read();assert.equal(configured.second,before.second);assert.equal(configured.day,before.day);
 await invoke('world.time.seek',{revision:configured.revision,day:2,second:0});const midnight=await read(),night=await fact('world.illumination') as {ambient:{r:number;g:number;b:number};sun:{r:number;g:number;b:number};elevation:number};
 assert.equal(midnight.day,2);assert.equal(midnight.second,0);assert.equal(night.ambient.r,0);assert.ok(Math.abs(night.ambient.b-.2)<1e-6);assert.equal(night.sun.r,0);assert.equal(night.elevation,-60);
 await execute([{action:'undo'}]);const undo=await read();assert.equal(undo.day,before.day);assert.equal(undo.second,before.second);
 await execute([{action:'redo'}]);const redo=await read();assert.equal(redo.day,2);assert.equal(redo.second,0);
 await invoke('world.time.configure',{revision:redo.revision,settings:{...settings,running:true}});const running=await read();await new Promise(resolve=>setTimeout(resolve,200));const advanced=await read();assert.ok(advanced.second>running.second);assert.equal(advanced.revision,running.revision);
 await invoke('world.time.configure',{revision:advanced.revision,settings});const stopped=await read();await new Promise(resolve=>setTimeout(resolve,100));assert.equal((await read()).second,stopped.second);
 await invoke('world.time.seek',{revision:stopped.revision,day:before.day,second:before.second});const positioned=await read();await invoke('world.time.configure',{revision:positioned.revision,settings:before.settings});const restored=await read();assert.deepEqual(restored.settings,before.settings);
 assert.deepEqual(await fact('world.lighting'),manual);assert.deepEqual(await fact('world.presentation'),view);assert.deepEqual(await fact('physics.environment'),physics);
 await writeFile(join(directory,'world-time.json'),JSON.stringify({boundary:'Native shared clock, saved settings, projection and Undo; no headset appearance claim.',before,configured,midnight,night,undo,redo,running,advanced,stopped,restored},null,2));
}
