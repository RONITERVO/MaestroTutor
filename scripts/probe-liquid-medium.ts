// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeLiquidMedium(execute:Execute,directory:string){
 const fact=async(capability:string,args:Record<string,unknown>)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}])).value;
 const invoke=async(id:string,args:Record<string,unknown>)=>{const s=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);assert.equal(s.execution?.selected?.phase,'completed',JSON.stringify(s.execution?.selected));return s.execution!.selected!;};
 const create=async(name:string,x:number)=>(await invoke('object.create',{...capabilityDefinition('object.create')!.example!,shape:'block',name,x,y:1,z:0})).output!.objectId as string;
 const pool=await create('Medium probe pool',3),cup=await create('Medium probe receiver',4);
 type Container={revision:number;definition:Record<string,unknown>&{amountMl:number;fluid:{densityKgM3:number;linearDrag:number}}};
 const read=async(target:string)=>await fact('object.container',{target}) as Container;
 const configure=async(target:string,extra:Record<string,unknown>)=>{const c=await read(target);return invoke('object.container.edit',{operation:'configure',target,revision:c.revision,definition:{...c.definition,...extra}});};
 await configure(pool,{frame:{position:{x:0,y:.2,z:0},rotation:{x:0,y:0,z:0,w:1}},rectangle:{width:1,depth:1},height:.6,capacityMl:600000,amountMl:400000,fluid:{version:1,densityKgM3:900,linearDrag:4,angularDrag:2}});
 await configure(cup,{capacityMl:200000,amountMl:0});
 const query=async()=>await fact('world.medium',{position:{x:3,y:1.4,z:0},target:''}) as {found:boolean;active:boolean;depthMetres:number;densityKgM3:number};
 const before=await query();assert.equal(before.found,true);assert.equal(before.active,false);assert.ok(Math.abs(before.depthMetres-.2)<1e-5);assert.equal(before.densityKgM3,900);
 const a=await read(pool),b=await read(cup);await invoke('object.container.transfer',{source:{target:pool,revision:a.revision},destination:{target:cup,revision:b.revision},amountMl:100000});
 const after=await query(),destination=await read(cup);assert.ok(Math.abs(after.depthMetres-.1)<1e-5);assert.equal(destination.definition.amountMl,100000);assert.equal(destination.definition.fluid.densityKgM3,900);assert.equal(destination.definition.fluid.linearDrag,4);assert.equal((await read(pool)).definition.amountMl+destination.definition.amountMl,400000);
 await execute([{action:'undo'}]);assert.ok(Math.abs((await query()).depthMetres-before.depthMetres)<1e-5);await execute([{action:'redo'}]);assert.ok(Math.abs((await query()).depthMetres-after.depthMetres)<1e-5);
 const response=await fact('object.medium',{target:cup}) as {applied:boolean};assert.equal(response.applied,false);
 for(let i=0;i<5;i++)await execute([{action:'undo'}]);
 await writeFile(join(directory,'liquid-medium.json'),JSON.stringify({boundary:'Full desktop native transport, finite-medium depth, explicit fluid transfer and Undo. Physics forces have separate PlayMode acceptance; no headset or terrain-water claim.',before,after,destination,response},null,2));
}
