// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {lstat,readdir,readFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {capabilityDefinition} from '../shared/capabilities';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {completeProbeAction,factReply,type NativeProbeState} from './native-probe-contract';
import {probeStaticSupport,type SupportSaved} from './probe-static-support';

/** Read-only native save evidence. Never guess a user's selected generation or
 * fall back to an old primary when this fresh offline workspace is unexpected. */
export async function supportSavedReader(directory:string){
 directory=resolve(directory);
 const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8')),ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert.equal(owner.version,1);assert.match(owner.id,/^[a-f0-9]{32}$/);assert.equal(ready.version,1);assert.equal(ready.id,owner.id);
 assert.equal(owner.providerUsed,false);assert.ok(!owner.providerScenario&&!owner.resumedFrom&&!owner.restartOf&&!ready.restartOf);
 for(const mode of ['nativeRestart','actionInterruption','motionOnly','transportOnly','liveContinuity','firstLearner','identityOnly','steeringOnly'])assert.equal(owner[mode],false);
 assert.equal(typeof owner.sparseObjectView,'boolean');
 const workspace=join(directory,'workspace'),room=join(workspace,'room');assert.equal(resolve(ready.workspaceDirectory),workspace);
 for(const path of [directory,workspace,room]){const info=await lstat(path);assert.ok(info.isDirectory()&&!info.isSymbolicLink(),'Expected an ordinary owned probe directory');}
 return async():Promise<SupportSaved>=>{
  await assert.rejects(lstat(join(workspace,'workspace-generations.v1','current.v1.json')),error=>(error as NodeJS.ErrnoException).code==='ENOENT','Selected generations require their own evidence reader');
  const primaries=(await readdir(room)).filter(name=>/^room\.v[0-9]+\.json$/.test(name));
  assert.deepEqual(primaries,['room.v47.json'],'Expected one unambiguous current saved room');
  const path=join(room,primaries[0]),info=await lstat(path);assert.ok(info.isFile()&&!info.isSymbolicLink()&&info.size>0&&info.size<=4*1024*1024);
  const bytes=await readFile(path),data=JSON.parse(bytes.toString('utf8'));assert.equal(data.version,47);assert.ok(Array.isArray(data.objects));
  return {version:data.version,sha256:createHash('sha256').update(bytes).digest('hex'),objects:data.objects};
 };
}

export async function probeNativeStaticSupport(
 execute:<const C extends RoomCommand[]>(commands:C)=>Promise<NativeProbeState<C>>,
 reject:(commands:RoomCommand[])=>Promise<RoomAgentState>,directory:string){
 const saved=await supportSavedReader(directory);
 const version=(id:string)=>{const definition=capabilityDefinition(id);assert.ok(definition,'Missing native-exported capability '+id);return definition.version;};
 // Inspect the actual served definitions as well as using the shared client catalog.
 const definition=await execute([{action:'catalog',catalog:{operation:'inspect',category:'actions',capability:'object.support.set',version:1}}]);
 assert.equal(definition.catalog?.capability,'object.support.set');assert.ok(definition.catalog?.definition);
 await probeStaticSupport({
  fact:async(id,args)=>factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,arguments:args}}])),
  invoke:async(id,args)=>(await completeProbeAction(execute,await execute([{action:'execution',execution:{operation:'start',call:{id,version:version(id),arguments:args}}}]))).execution!.selected!,
  reject:async(id,args)=>{
   let state=await reject([{action:'execution',execution:{operation:'start',call:{id,version:version(id),arguments:args}}}]);
   const runId=state.execution?.selected?.id,deadline=Date.now()+35000;
   while(state.ok&&runId&&['preparing','running'].includes(state.execution!.selected!.phase)&&Date.now()<deadline){
    await new Promise(resolve=>setTimeout(resolve,100));state=await reject([{action:'execution',execution:{operation:'inspect',runId}}]);
   }
   return state;
  },
  undo:async()=>{await execute([{action:'undo'}]);},redo:async()=>{await execute([{action:'redo'}]);},saved,
  replay:async receipt=>(await execute([{action:'execution',execution:{operation:'start',runId:receipt.id,call:receipt.call}}])).execution!.selected!,
 },directory,'headless');
}
