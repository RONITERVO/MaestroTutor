// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {validFactValue} from '../shared/behaviourFacts';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export type ControllerSettings={configurationId:string;storageReady:boolean;avatarStick:'left'|'right'|'none';userStick:'left'|'right'|'none';deadZone:number;userSpeed:number;buttons:unknown[];live:{avatarEnabled:boolean;userEnabled:boolean;virtualView:boolean}};
export function assertControllerSettings(actual:ControllerSettings,expected:ControllerSettings){
 const {configurationId:actualId,...a}=actual,{configurationId:expectedId,...b}=expected;
 assert.match(actualId,/^[a-f0-9]{32}$/);assert.match(expectedId,/^[a-f0-9]{32}$/);assert.deepEqual(a,b);
}
export async function probeControllerCompletion(execute:Execute,directory:string){
 const read=async()=>{const state=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'controller.settings',version:1}}]);const reply=factReply(state);assert.equal(reply.available,true);assert.ok(validFactValue('controller.settings',reply.value));const value=reply.value as ControllerSettings;assert.equal(value.storageReady,true);return {state,value};};
 const before=await read(),target={...before.value,deadZone:before.value.deadZone===.3?.25:.3};
 const change=async(value:ControllerSettings,current:ControllerSettings)=>completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id:'controller.configure',version:1,arguments:{configurationId:current.configurationId,operation:'movement.'+value.avatarStick,userStick:value.userStick,deadZone:value.deadZone,userSpeed:value.userSpeed}}}}]));
 const changed=await change(target,before.value),receipt=changed.execution!.selected!,after=await read();
 assertControllerSettings(after.value,target);assert.notEqual(after.value.configurationId,before.value.configurationId);
 assert.deepEqual(receipt.output,{configurationId:after.value.configurationId,saved:true});
 assert.equal(changed.sceneRevision,before.state.sceneRevision,'Personal settings must not create geometry Undo');
 const restored=await change(before.value,after.value),final=await read();assertControllerSettings(final.value,before.value);
 assert.notEqual(final.value.configurationId,after.value.configurationId);
 const inspected=await execute([{action:'execution',execution:{operation:'inspect',runId:receipt.id}}]);assert.deepEqual(inspected.execution!.selected!.output,receipt.output);
 const replayed=await execute([{action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}}]);
 assert.deepEqual(replayed.execution!.selected!.output,receipt.output);
 assert.deepEqual((await read()).value,final.value,'Duplicate receipt must not restore an older preference');
 await writeFile(join(directory,'controller-completion.json'),JSON.stringify({before:before.value,target,receipt,after:after.value,restored:restored.execution!.selected,final:final.value,inspected:inspected.execution!.selected,replayed:replayed.execution!.selected,boundary:'Actual desktop Unity and shared headless transport; save completion, restored preferences, immutable receipt and duplicate replay without reapplying. No provider or headset input proof.'},null,2));
}
