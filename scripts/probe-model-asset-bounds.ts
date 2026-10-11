// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,type NativeProbeState} from './native-probe-contract';
type Execute=<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>;
export async function probeModelAssetBounds(execute:Execute,directory:string){
 const fixture=await readFile(join(directory,'model-fixture.json'),'utf8').then(text=>JSON.parse(text) as {target:string;hash:string}).catch(error=>{if(error.code==='ENOENT')return null;throw error;});
 if(!fixture)return;
 const reply=await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.model.assetBounds',version:1,arguments:{target:fixture.target}}}]);
 const fact=factReply(reply);assert.equal(fact.available,true);
 const value=fact.value as {target:string;modelHash:string;derivationVersion:number;coordinates:string;inspected:boolean;known:boolean;hasBounds:boolean;min:{x:number;y:number;z:number};max:{x:number;y:number;z:number}};
 assert.equal(value.target,fixture.target);assert.equal(value.modelHash,fixture.hash);assert.equal(value.derivationVersion,1);assert.equal(value.coordinates,'object');
 assert.equal(value.inspected,true);assert.equal(value.known,true);assert.equal(value.hasBounds,true);
 assert.ok(Math.abs(value.max.x-value.min.x-.35)<.0001);
 assert.ok(Object.values(value.min).every(Number.isFinite)&&Object.values(value.max).every(Number.isFinite));
 await writeFile(join(directory,'model-asset-bounds.json'),JSON.stringify({boundary:'Actual shared headless client reads the native validated static GLB envelope. No provider, collision-readiness, automatic streaming or headset claim.',fixture,value},null,2));
}
