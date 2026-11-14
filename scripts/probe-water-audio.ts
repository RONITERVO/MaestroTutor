// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type SoundTreatment={revision:number;configured:boolean;settings:{version:number;enabled:boolean;attenuationDbPerMetre:number;mufflingPerMetre:number}};
type Container={revision:number;definition:Record<string,unknown>&{amountMl:number}};
export async function probeWaterAudio(execute:Execute,directory:string){
 const read=async(id:string,target:string)=>{const fact=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,arguments:{target}}}]));assert.equal(fact.available,true,fact.status);return fact.value;};
 const invoke=async(id:string,args:Record<string,unknown>)=>{const state=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]));assert.equal(state.execution?.selected?.phase,'completed',JSON.stringify(state.execution?.selected));return state.execution!.selected!;};
 const created=await invoke('object.create',{...capabilityDefinition('object.create')!.example!,shape:'block',name:'Water sound probe',x:0,y:1,z:3});
 const target=created.output!.objectId as string;
 let container=await read('object.container',target) as Container;
 await invoke('object.container.edit',{operation:'configure',target,revision:container.revision,definition:{...container.definition,amountMl:100}});
 const before=await read('object.container.audio',target) as SoundTreatment;assert.equal(before.configured,true);assert.equal(before.settings.enabled,false);
 const settings={version:1,enabled:true,attenuationDbPerMetre:3,mufflingPerMetre:2};
 const configured=await invoke('object.container.audio.configure',{target,revision:before.revision,settings});
 const after=await read('object.container.audio',target) as SoundTreatment;assert.equal(after.settings.enabled,true);assert.equal(after.settings.attenuationDbPerMetre,settings.attenuationDbPerMetre);assert.ok(Math.abs(after.settings.mufflingPerMetre-settings.mufflingPerMetre)<1e-6);
 container=await read('object.container',target) as Container;assert.equal(Object.hasOwn(container.definition,'waterAudio'),false,'The original typed record must retain its shape');
 await invoke('object.container.edit',{operation:'configure',target,revision:container.revision,definition:{...container.definition,amountMl:75}});
 const retained=await read('object.container.audio',target) as SoundTreatment;assert.deepEqual(retained.settings,after.settings);
 await execute([{action:'undo'}]);assert.equal((await read('object.container',target) as Container).definition.amountMl,100);
 await execute([{action:'undo'}]);const undone=await read('object.container.audio',target) as SoundTreatment;assert.equal(undone.settings.enabled,false);
 await execute([{action:'redo'}]);const redone=await read('object.container.audio',target) as SoundTreatment;assert.deepEqual(redone.settings,after.settings);
 await execute([{action:'undo'}]);await execute([{action:'undo'}]);assert.equal((await read('object.container.audio',target) as SoundTreatment).configured,false);
 await execute([{action:'undo'}]);
 const removed=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target}}}]));assert.equal(removed.available,false);
 await writeFile(join(directory,'water-audio.json'),JSON.stringify({boundary:'Actual shared native transport, saved sound settings completion/readback, preservation by ordinary fill edits and Undo/Redo. PCM rendering and finite water paths have separate PlayMode evidence. Offline scripted journey, no provider, physical camera or headset acceptance.',created,configured,before,after,retained,undone,redone},null,2));
}
