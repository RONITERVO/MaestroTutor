// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function readWaterActorPose(execute:Execute,target:string){
 const saved=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target}}}]));
 const live=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.position',version:2,arguments:{target}}}]));
 assert.equal(saved.available,true);assert.equal(live.available,true);
 const data=saved.value as Record<string,unknown>;
 return {target,saved:{position:data.position,rotation:data.rotation,scale:data.scale},live:live.value};
}
export async function probeWaterTraversal(execute:Execute,directory:string){
 const target='maestro',fact='object.water.traversal.settings';
 const read=async()=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:fact,version:1,arguments:{target}}}])).value as {target:string;revision:number;mode:string;effectiveMode:string;maxDepthMetres:number;temporary:boolean};
 const placement=async()=>readWaterActorPose(execute,target);
 const before=await read(),pose=await placement();
 const result=await execute([{action:'execution',execution:{operation:'start',call:{id:'object.water.traversal.configure',version:1,arguments:{target,revision:before.revision,mode:'wade',maxDepthMetres:.15}}}}]);
 assert.equal(result.execution?.selected?.phase,'completed',JSON.stringify(result.execution?.selected));
 const after=await read();assert.equal(after.mode,'wade');assert.equal(after.effectiveMode,'wade');assert.equal(after.maxDepthMetres,.15);assert.ok(after.revision>before.revision);
 const unchanged=await placement();assert.deepEqual(unchanged,pose);
 await execute([{action:'undo'}]);const undone=await read();assert.equal(undone.mode,before.mode);assert.equal(undone.maxDepthMetres,before.maxDepthMetres);
 await execute([{action:'redo'}]);assert.equal((await read()).mode,'wade');await execute([{action:'undo'}]);
 const egress=await probeShallowEgress(execute);
 assert.deepEqual(await placement(),pose,'The egress fixture must leave Maestro placement unchanged');
 await writeFile(join(directory,'water-traversal.json'),JSON.stringify({boundary:'Native shared action/fact transport, exact revisions, placement preservation, directional shallow-egress queries and Undo/Redo. Actual walking/solid clearance have separate PlayMode coverage; no headset or provider claim.',before,after,undone,receipt:result.execution?.selected,egress},null,2));
}

async function probeShallowEgress(execute:Execute){
 const read=async(capability:string,args:Record<string,unknown>)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}])).value as Record<string,unknown>;
 const invoke=async(id:string,args:Record<string,unknown>)=>{const result=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);assert.equal(result.execution?.selected?.phase,'completed',JSON.stringify(result.execution?.selected));return result.execution!.selected!;};
 const create=async(name:string,x:number,y:number)=>(await invoke('object.create',{...capabilityDefinition('object.create')!.example!,shape:'block',name,x,y,z:0,scale:1})).output!.objectId as string;
 const pool=await create('Egress probe pool',3,1);
 const container=await read('object.container',{target:pool});
 await invoke('object.container.edit',{operation:'configure',target:pool,revision:container.revision,definition:{...(container.definition as Record<string,unknown>),frame:{position:{x:0,y:.2,z:0},rotation:{x:0,y:0,z:0,w:1}},rectangle:{width:1,depth:1},height:.6,capacityMl:600000,amountMl:400000}});
 // The standard native block is .13 m tall. Its bottom at 1.555 m is
 // shallowly immersed beneath the pool's 1.6 m live surface. This is a
 // controlled placement for passive water queries, not a walking claim.
 const actor=await create('Egress probe actor',3.25,1.62);
 const settings=await read('object.water.traversal.settings',{target:actor});
 await invoke('object.water.traversal.configure',{target:actor,revision:settings.revision,mode:'avoid',maxDepthMetres:.25});
 const path=async(x:number,y=1.62)=>await read('object.water.traversal.path',{target:actor,position:{x,y,z:0}});
 const toward=await path(3.3),inward=await path(3.2),across=await path(2),deeper=await path(3.3,1.6);
 assert.equal(toward.allowed,true,JSON.stringify(toward));assert.equal(toward.bodyId,pool);assert.equal(toward.availability,'ready');
 assert.equal(inward.allowed,false);assert.equal(across.allowed,false);assert.equal(deeper.allowed,false);
 const current=await read('object.water.traversal.settings',{target:actor});
 await invoke('object.water.traversal.configure',{target:actor,revision:current.revision,mode:'avoid',maxDepthMetres:.005});
 const limited=await path(3.3);assert.equal(limited.allowed,false);
 await invoke('object.position.set',{target:actor,x:4,y:1.62,z:0});
 const dryEntry=await path(3.3);assert.equal(dryEntry.allowed,false);
 for(let i=0;i<6;i++)await execute([{action:'undo'}]);
 const removed=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:actor}}}]);
 assert.equal(factReply(removed).available,false,'Fixture cleanup must remove its actor');
 return {toward,inward,across,deeper,limited,dryEntry};
}
