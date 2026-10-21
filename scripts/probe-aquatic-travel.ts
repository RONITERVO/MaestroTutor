// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeAquaticTravel(execute:Execute,directory:string){
 // Parameterless facts omit arguments entirely; an empty object is a different contract.
 const read=async(capability:string,args?:Record<string,unknown>)=>{
  const version=behaviourFact(capability)!.version;
  const result=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version,arguments:args}}]));
  assert.equal(result.available,true,result.status);return result.value as Record<string,unknown>;
 };
 const invoke=async(id:string,args:Record<string,unknown>)=>{
  const state=await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]));
  assert.equal(state.execution?.selected?.phase,'completed',JSON.stringify(state.execution?.selected));return state.execution!.selected!;
 };
 const create=async(name:string,y:number,scale:number)=>{
  const result=await invoke('object.create',{...capabilityDefinition('object.create')!.example!,shape:'block',name,x:-1.5,y,z:2,scale});
  const target=result.output!.objectId as string;
  const current=await read('object.physics.settings',{target});
  await invoke('object.physics.configure',{target,revision:current.revision,mode:'fixed',shape:'box',mass:1});return target;
 };
 // The earlier walking/catch/history journey finishes paused. Add these fixtures
 // only after its original one-shot Undo assertions have completed.
 const initialPhysics=await read('physics.simulation');assert.equal(initialPhysics.running,false);
 const pool=await create('Aquatic journey pool',1,1),actor=await create('Aquatic journey actor',1.4,.5);
 const container=await read('object.container',{target:pool});
 await invoke('object.container.edit',{operation:'configure',target:pool,revision:container.revision,definition:{
  ...(container.definition as Record<string,unknown>),frame:{position:{x:0,y:.2,z:0},rotation:{x:0,y:0,z:0,w:1}},
  rectangle:{width:1,depth:1},height:.6,capacityMl:600000,amountMl:400000
 }});
 const preparedPhysics=await read('physics.simulation');
 await invoke('physics.simulation.set',{operation:'start',stateId:preparedPhysics.stateId});
 const call={id:'object.navigate',version:1,arguments:{target:actor,mode:'swim',habitat:pool,destination:{x:-1.2,y:1.45,z:2},speed:.3,facing:'keep'}};
 const check=await execute([{action:'catalog',catalog:{operation:'check',call}}]);assert.ok(check.catalog?.operation==='check');assert.equal(check.catalog.available,true,JSON.stringify(check.catalog));
 let state=await execute([{action:'execution',execution:{operation:'start',call}}]);const runId=state.execution?.selected?.id;assert.ok(runId);
 const samples:Record<string,unknown>[]=[];const deadline=Date.now()+30000;
 while(state.execution?.selected?.phase==='preparing'&&Date.now()<deadline){
  samples.push(await read('object.navigation',{target:actor}));
  await new Promise(r=>setTimeout(r,80));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);
 }
 const receipt=state.execution?.selected;assert.equal(receipt?.phase,'completed',JSON.stringify(receipt));
 const after=await read('object.position',{target:actor});
 assert.ok(Math.abs(Number(after.x)+1.2)<.006&&Math.abs(Number(after.y)-1.45)<.006&&Math.abs(Number(after.z)-2)<.006);
 assert.ok(samples.some(s=>s.mode==='swim'&&s.active===true));
 assert.ok(samples.some(s=>{const p=s.position as {x:number};return p.x>-1.49&&p.x<-1.21;}),'No observed intermediate aquatic position');
 const replay=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);assert.deepEqual(replay.execution?.selected,receipt);
 const finalPhysics=await read('physics.simulation');
 const paused=await invoke('physics.simulation.set',{operation:'pause',stateId:finalPhysics.stateId});assert.equal(paused.output!.running,false);
 await writeFile(join(directory,'native-aquatic-travel.json'),JSON.stringify({boundary:'Shared native action/readiness/status/receipt and replay against an authored finite liquid volume. Real gradual submerged movement; no provider or headset acceptance.',pool,actor,check:check.catalog,samples,receipt,after,physicsBefore:initialPhysics,paused},null,2));
}
