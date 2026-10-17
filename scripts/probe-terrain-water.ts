// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {createHash} from 'node:crypto';
import {join} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeTerrainWater(execute:Execute,directory:string){
 const read=async(capability:string,args:Record<string,unknown>)=>{const fact=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}]));assert.equal(fact.available,true);return fact.value as Record<string,unknown>;};
 const invoke=async(id:string,args:Record<string,unknown>)=>{const result=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]));assert.equal(result.execution?.selected?.phase,'completed',JSON.stringify(result.execution?.selected));return result.execution!.selected!;};
 const hash=createHash('sha256').update(await readFile('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/snow-patch.json')).digest('hex');
 // Keep this live-water fixture in the stationary viewer's frustum.
 const created=await invoke('object.create',{kind:'template',templateHash:hash,name:'Terrain reservoir probe',x:0,y:1,z:3,scale:1}),target=created.output!.objectId as string;
 const field=await read('object.field',{target}),example=capabilityDefinition('object.field.edit')!.example!;
 await invoke('object.field.edit',{...structuredClone(example),target,revision:field.revision,definition:{...(example.definition as Record<string,unknown>),cells:8,width:2,depth:1,maxHeight:.5,material:'Earth',frame:{position:{x:0,y:.2,z:0},rotation:{x:0,y:0,z:0,w:1}}},fillHeight:.1});
 const before=await read('object.container',{target});
 const configured=await invoke('object.container.edit',{operation:'configure',target,revision:before.revision,definition:{...(before.definition as Record<string,unknown>),terrainFloor:true,height:.5,capacityMl:1,amountMl:200000}});
 const saved=await read('object.container',{target}),definition=saved.definition as Record<string,unknown>;
 assert.equal(definition.terrainFloor,true);assert.equal(definition.amountMl,200000);assert.ok(Math.abs(Number(definition.capacityMl)-800000)<.1);assert.deepEqual(definition.rectangle,{width:2,depth:1});
 const wet=await read('world.medium',{position:{x:-.5,y:1.35,z:3},target:'maestro'}),ground=await read('world.medium',{position:{x:-.5,y:1.25,z:3},target:'maestro'});
 assert.equal(wet.found,true);assert.equal(wet.bodyId,target);assert.ok(Math.abs(Number(wet.depthMetres)-.05)<.0001);assert.equal(ground.found,false);
 const sculpted=await invoke('object.field.sculpt',{target,revision:saved.revision,mode:'lower',radius:.4,height:.05,path:[{x:0,z:0}]});
 const changed=await read('object.container',{target});assert.equal((changed.definition as Record<string,unknown>).amountMl,200000);assert.ok(Number((changed.definition as Record<string,unknown>).capacityMl)>Number(definition.capacityMl));
 await execute([{action:'undo'}]);const restored=await read('object.container',{target});assert.deepEqual(restored.definition,saved.definition);
 for(let i=0;i<3;i++)await execute([{action:'undo'}]);
 const removed=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target}}}]));assert.equal(removed.available,false);
 await writeFile(join(directory,'terrain-water.json'),JSON.stringify({boundary:'Actual native shared catalog authoring, medium queries and terrain sculpting with quantity-preserving Undo. Physics and rendered shorelines are covered separately in PlayMode. Scripted offline run, no provider or headset acceptance.',created,configured,saved,wet,ground,sculpted,changed,restored},null,2));
}
