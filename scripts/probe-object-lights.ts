// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
type Lamp={target:string;revision:number;stateId:string;settings:Record<string,unknown>;effective:{enabled:boolean;intensity:number;color:string;overridden:boolean};active:boolean};
export async function probeObjectLights(execute:Execute,directory:string){
 const action=async(id:string,args:Record<string,unknown>)=>{const s=await execute([{action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}}]);assert.equal(s.execution?.selected?.phase,'completed',JSON.stringify(s.execution?.selected));return s;};
 const definition=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'object.create',version:1}}]);
 const example=(definition.catalog.definition as {example:Record<string,unknown>}).example;
 const created=await action('object.create',{...example,name:'Native lamp'}),target=String(created.execution!.selected!.output!.objectId);
 const list=async()=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.lights',version:1,arguments:{target}}}])).value as {revision:number;lights:{id:string}[]};
 const read=async()=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.light',version:1,arguments:{target,light:'Lamp'}}}])).value as Lamp;
 const before=await list();assert.equal(before.lights.length,0);
 const settings={version:1,kind:'spot',part:'',position:{x:0,y:.2,z:0},rotation:{x:0,y:0,z:0,w:1},enabled:true,color:'#FFDDAA',intensity:1,range:3,angle:70,shadows:'hard'};
 await action('object.light.edit',{operation:'save',target,revision:before.revision,light:'Lamp',settings});const saved=await read();assert.equal(saved.active,true);assert.equal(saved.effective.overridden,false);
 const controlled=await action('object.light.control',{operation:'set',target,light:'Lamp',stateId:saved.stateId,enabled:false,intensity:.25,color:'#3366FF'});const off=await read();assert.equal(off.active,false);assert.equal(off.effective.overridden,true);assert.equal(off.revision,saved.revision);assert.deepEqual(off.settings,saved.settings);
 const stateId=off.stateId;assert.equal((await read()).stateId,stateId,'Facts must be passive');
 await action('object.light.control',{operation:'reset',target,light:'Lamp',stateId});const reset=await read();assert.equal(reset.active,true);assert.deepEqual(reset.effective,saved.effective);
 await execute([{action:'undo'}]);assert.equal((await list()).lights.length,0,'Live controls add no Undo entries');
 await execute([{action:'redo'}]);const redo=await read();assert.equal(redo.active,true);assert.equal(redo.effective.overridden,false);
 await execute([{action:'undo'}]);await execute([{action:'undo'}]);
 await writeFile(join(directory,'object-lights.json'),JSON.stringify({boundary:'Native shared catalog, saved definition, transient control, passive readback and Undo/Redo. Offline scripted journey; no provider or physical Quest acceptance.',target,saved,controlled:controlled.execution?.selected,off,reset,redo},null,2));
}
