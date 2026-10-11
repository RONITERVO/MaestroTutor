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
export async function runAgentLightingProof({client,initial,execute,directory,read}:{client:HeadlessClient;initial:RoomAgentState;execute:Execute;directory:string;read:()=>RoomAgentState}){
 const fact=async(capability:string)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1}}])).value as Record<string,unknown>;
 const changed=await fact('world.lighting');const light=changed.settings as {enabled:boolean;ambientColor:string;sunColor:string;ambientIntensity:number;sunIntensity:number};
 assert.equal(light.enabled,true);assert.ok(light.ambientIntensity>0&&light.ambientIntensity<.35);assert.ok(light.sunIntensity>0);
 const rgb=(color:string)=>[1,3,5].map(i=>Number.parseInt(color.slice(i,i+2),16));const ambient=rgb(light.ambientColor),sun=rgb(light.sunColor);
 assert.ok(ambient[2]>ambient[0],'Evening ambient should be blue');assert.ok(sun[0]>sun[2],'Evening sun should be warm');
 assertSameRoomObjects(initial,read());const view=await fact('world.presentation'),physics=await fact('physics.environment');assert.equal(view.backdropOpacity,0);assert.equal(view.realDepth,true);assert.equal(physics.realCollisions,true);
 const restoredJourney=await runHeadlessRoomTurn(client,{text:'Thanks! Switch back to the original illustrated lighting now, but keep the evening lighting settings saved so I can turn them on again later. Please leave all my objects and the room view alone.'});
 const restored=await fact('world.lighting'),restoredSettings=restored.settings as typeof light;assert.equal(restoredSettings.enabled,false);assert.deepEqual({...restoredSettings,enabled:true},light);
 assertSameRoomObjects(initial,read());assert.deepEqual(await fact('world.presentation'),view);assert.deepEqual(await fact('physics.environment'),physics);
 const result={scenario:'WorldLighting',phase:'passed',boundary:'Real original chat and provider handoff to desktop Unity; pixel rendering has separate PlayMode tests. No Quest headset lighting acceptance.',semantics:{eveningLight:true,restoredIllustration:true,savedPresetRetained:true,unrelatedObjectsPreserved:true,viewAndCollisionIndependent:true},changed,restoredJourney,restored};
 await writeFile(join(directory,'provider-scenarios.json'),JSON.stringify(result,null,2));return result;
}
