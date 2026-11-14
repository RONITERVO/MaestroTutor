// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export type WorldLocation={stateId:string;located:boolean;position:{x:number;y:number;z:number};yaw:number};
export function assertWorldLocation(actual:Pick<WorldLocation,'position'|'yaw'>,expected:Pick<WorldLocation,'position'|'yaw'>){
 for(const axis of ['x','y','z'] as const)assert.ok(Math.abs(actual.position[axis]-expected.position[axis])<.0001,JSON.stringify({actual,expected}));
 const yaw=(actual.yaw-expected.yaw+540)%360-180;assert.ok(Math.abs(yaw)<.001,JSON.stringify({actual,expected}));
}
export async function probeWorldLocation(execute:Execute,directory:string){
 const read=async()=>{const state=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.viewpoint',version:1}}]);const reply=factReply(state);assert.equal(reply.available,true);const value=reply.value as WorldLocation;assert.equal(value.located,true);return {state,value};};
 const before=await read();const target={position:before.value.position,yaw:(before.value.yaw+25+540)%360-180};
 const started=await execute([{action:'execution',execution:{operation:'start',call:{id:'world.viewpoint.set',version:1,arguments:{stateId:before.value.stateId,...target}}}}]);
 const completed=await completeProbeAction(execute,started);const receipt=completed.execution!.selected!;
 assertWorldLocation(receipt.output as WorldLocation,target);assert.equal(completed.sceneRevision,before.state.sceneRevision,'Navigation must not create a geometry edit');
 const after=await read();assert.notEqual(after.value.stateId,before.value.stateId);assertWorldLocation(after.value,target);
 const restored=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'world.viewpoint.set',version:1,arguments:{stateId:after.value.stateId,position:before.value.position,yaw:before.value.yaw}}}}]));
 const inspection=await execute([{action:'execution',execution:{operation:'inspect',runId:receipt.id}}]);
 assert.deepEqual(inspection.execution!.selected!.output,receipt.output,'Inspecting an older completion must retain its accepted location');
 const final=await read();assertWorldLocation(final.value,before.value);assert.equal(final.state.sceneRevision,before.state.sceneRevision);
 await writeFile(join(directory,'world-location-completion.json'),JSON.stringify({before:before.value,target,started:started.execution!.selected,receipt,after:after.value,restored:restored.execution!.selected,inspection:inspection.execution!.selected,final:final.value,boundary:'Actual desktop Unity and shared headless transport: fresh guard, completion, unchanged scene revision, return and immutable older receipt inspection. Delayed IO/cancellation/retirement are separate native tests. No provider or physical headset claim.'},null,2));
}
