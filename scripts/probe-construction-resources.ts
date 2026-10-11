// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand,RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {parseProgram} from '../src/core-sdk/room/programs';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeConstructionResources(execute:Execute,directory:string){
 const cases=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/construction-resources-contract.json','utf8')) as {arguments:Record<string,unknown>}[];
 const finish=async(state:RoomAgentState)=>{const id=state.execution?.selected?.id;assert.ok(id);const end=Date.now()+15000;
  while(state.execution?.selected?.phase!=='completed'&&Date.now()<end){assert.notEqual(state.execution?.selected?.phase,'failed',JSON.stringify(state.execution?.selected));await new Promise(r=>setTimeout(r,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId:id}}]);}
  assert.equal(state.execution?.selected?.phase,'completed',JSON.stringify(state.execution?.selected));return state;
 };
 const libraries=async()=>{
  const values=[];for(const capability of ['appearance.library','audio.source.list','environment.profiles']){
   const state=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability,version:1,arguments:{offset:0}}}]);values.push(factReply(state).value);
  }return values;
 };
 const beforeLibraries=await libraries();
 const created=await finish(await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:cases[0].arguments}}}]));
 const ids=created.execution!.selected!.output!.objectIds as string[];assert.equal(ids.length,2);
 const capture=await finish(await execute([{action:'execution',execution:{operation:'start',call:{id:'program.module.captureConstruction',version:1,arguments:{name:'Portable styled sound pair',members:ids.map((target,i)=>({target,revision:created.objects.find(o=>o.id===target)!.objectRevision,slot:'piece_'+i}))}}}}]));
 const hash=capture.execution!.selected!.output!.hash;assert.equal(typeof hash,'string');
 const read=await execute([{action:'catalog',catalog:{operation:'inspect',category:'modules',capability:hash as string,version:1}}]);
 const module=read.catalog.definition;assert.ok(module);ids.forEach(id=>assert.ok(!JSON.stringify(module).includes(id)));
 const parsed=parseProgram(JSON.stringify(module.program));const node=parsed.program?.functions.find(f=>f.name==='create')?.body[0];assert.ok(node?.op==='invoke'&&node.capability==='object.batch.create',parsed.error??'Missing constructor');
 const removed=await execute([{action:'undo'}]);assert.ok(ids.every(id=>!removed.objects.some(o=>o.id===id)));
 const rebuilt=await finish(await execute([{action:'execution',execution:{operation:'start',call:{id:'object.batch.create',version:1,arguments:node.arguments}}}]));
 const fresh=rebuilt.execution!.selected!.output!.objectIds as string[];assert.equal(fresh.length,2);assert.ok(fresh.every(id=>!ids.includes(id)));
 const facts=[];const styleIds:string[]=[],profileIds:string[]=[],soundIds:string[]=[];
 for(const target of fresh){
  const style=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.appearances',version:1,arguments:{target,offset:0}}}]);
  const binding=(factReply(style).value as {bindings:{appearanceId:string;tint:string}[]}).bindings[0];assert.equal(binding.tint,'#BBEEFF');styleIds.push(binding.appearanceId);
  const profile=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.environment',version:1,arguments:{target}}}]);
  const bound=factReply(profile).value as {profileId:string;state:{effectiveRealCollisions:boolean}};assert.equal(bound.state.effectiveRealCollisions,false);profileIds.push(bound.profileId);
  const sound=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.audioEmitter',version:1,arguments:{target,emitter:'bell'}}}]);
  const emitter=factReply(sound).value as {configured:boolean;definition:{source:string}};assert.equal(emitter.configured,true);assert.equal(typeof emitter.definition.source,'string');soundIds.push(emitter.definition.source);const playback=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'audio.playback',version:1,arguments:{target,emitter:'bell'}}}]);
  assert.equal((factReply(playback).value as {playing:boolean}).playing,false);facts.push({style,profile,sound,playback});
 }
 for(const ids of [styleIds,profileIds,soundIds]){assert.equal(new Set(ids).size,1);assert.notEqual(ids[0],'0'.repeat(31)+'1');}
 const undo=await execute([{action:'undo'}]);assert.ok(fresh.every(id=>!undo.objects.some(o=>o.id===id)));
 const afterLibraries=await libraries();assert.deepEqual(afterLibraries,beforeLibraries,'Undo must remove only the new resource definitions');
 const cleanup=await finish(await execute([{action:'execution',execution:{operation:'start',call:{id:'program.module.remove',version:1,arguments:{hash}}}}]));
 await writeFile(join(directory,'construction-resources.json'),JSON.stringify({boundary:'Real Unity shared transport: creation, library capture, original removal, re-instantiation, style/sound/collision readback and Undo. No provider or headset proof.',beforeLibraries,created,capture,read,removed,rebuilt,facts,undo,afterLibraries,cleanup},null,2));
 return {capturedAfterCreation:true,survivedOriginalRemoval:true,sharedResourcesVerified:true,undoVerified:true};
}
