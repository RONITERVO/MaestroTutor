// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeVisibilityLayers(execute:Execute,directory:string){
 const fact=async(capability:string,args:Record<string,unknown>)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:args}}])).value as Record<string,unknown>;
 const invoke=async(id:string,args:Record<string,unknown>)=>{
  let state=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);
  const run=state.execution!.selected!.id,deadline=Date.now()+10000;
  while(state.execution!.selected!.phase!=='completed'&&Date.now()<deadline){assert.notEqual(state.execution!.selected!.phase,'failed');await new Promise(r=>setTimeout(r,50));state=await execute([{action:'execution',execution:{operation:'inspect',runId:run}}]);}
  assert.equal(state.execution!.selected!.phase,'completed');return state.execution!.selected!;
 };
 const physical=await fact('object.environment',{target:'maestro'});
 const saved=await invoke('visibility.layer.save',{id:'',revision:0,name:'Faded Maestro',opacity:.35,realDepth:false,members:[]});
 const id=String(saved.output!.id),before=await fact('object.visibility',{target:'maestro'}),layer=await fact('visibility.layer',{id});
 const assigned=await invoke('object.visibility.assign',{target:'maestro',revision:before.revision,layerId:id,layerRevision:layer.revision});
 const bound=await fact('object.visibility',{target:'maestro'}),members=await fact('visibility.members',{id,offset:0});
 assert.equal(bound.layerId,id);assert.ok(Math.abs(Number(bound.opacity)-.35)<1e-6);assert.equal(bound.realDepth,false);assert.deepEqual(members.members,['maestro']);
 const afterPhysical=await fact('object.environment',{target:'maestro'});delete physical.revision;delete afterPhysical.revision;assert.deepEqual(afterPhysical,physical);
 const changed=await invoke('visibility.layer.save',{id,revision:layer.revision,name:'Faded Maestro',opacity:.7,realDepth:true,members:['maestro']});
 assert.ok(Math.abs(Number((await fact('visibility.layer',{id})).opacity)-.7)<1e-6);
 await execute([{action:'undo'}]);assert.ok(Math.abs(Number((await fact('visibility.layer',{id})).opacity)-.35)<1e-6);
 await execute([{action:'undo'}]);assert.equal((await fact('object.visibility',{target:'maestro'})).layerId,'');
 const unused=await fact('visibility.layer',{id});const removed=await invoke('visibility.layer.remove',{id,revision:unused.revision});
 await writeFile(join(directory,'visibility-layers.json'),JSON.stringify({boundary:'Real shared native transport, saved definitions, membership, assignment and Undo. Rendering and input use separate PlayMode tests; no device acceptance claim.',saved,assigned,bound,members,physical,changed,removed},null,2));
}
