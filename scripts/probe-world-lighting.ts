// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Lighting={revision:number;worldId:string;regionId:string;settings:{version:1;enabled:boolean;ambientColor:string;sunColor:string;ambientIntensity:number;sunIntensity:number;azimuth:number;elevation:number};temporary:boolean};
function sameSettings(actual:Lighting['settings'],expected:Lighting['settings']){
 for(const key of Object.keys(expected) as (keyof Lighting['settings'])[]){const a=actual[key],b=expected[key];if(typeof a==='number'&&typeof b==='number')assert.ok(Math.abs(a-b)<1e-6,`${key}: ${a} differs from ${b}`);else assert.equal(a,b,key);}
}
export async function probeWorldLighting(execute:Execute,directory:string){
 const inspect=async(capability:string)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1}}])).value;
 const read=async()=>await inspect('world.lighting') as Lighting;
 const before=await read(),physics=await inspect('physics.environment'),view=await inspect('world.presentation');
 assert.equal(before.settings.enabled,false);
 const settings={...before.settings,enabled:true,ambientColor:'#223344',sunColor:'#FFCC99',ambientIntensity:.2,sunIntensity:.7,azimuth:40,elevation:25};
 const set=await execute([{action:'execution',execution:{operation:'start',call:{id:'world.lighting.set',version:1,arguments:{revision:before.revision,settings}}}}]);
 assert.equal(set.execution?.selected?.phase,'completed',JSON.stringify(set.execution?.selected));const after=await read();sameSettings(after.settings,settings);assert.ok(after.revision>before.revision);assert.equal(after.worldId,before.worldId);assert.equal(after.regionId,before.regionId);
 assert.deepEqual(await inspect('physics.environment'),physics);assert.deepEqual(await inspect('world.presentation'),view);
 await execute([{action:'undo'}]);const undo=await read();assert.deepEqual(undo.settings,before.settings);assert.ok(undo.revision>after.revision);
 await execute([{action:'redo'}]);const redo=await read();sameSettings(redo.settings,settings);assert.ok(redo.revision>undo.revision);
 const restored=await execute([{action:'execution',execution:{operation:'start',call:{id:'world.lighting.set',version:1,arguments:{revision:redo.revision,settings:before.settings}}}}]);assert.equal(restored.execution?.selected?.phase,'completed');assert.deepEqual((await read()).settings,before.settings);
 await writeFile(join(directory,'world-lighting.json'),JSON.stringify({boundary:'Real native transport, persisted lighting and Undo/Redo; no headset or real-provider appearance claim.',before,set:set.execution?.selected,after,undo,redo,restored:restored.execution?.selected},null,2));
}
