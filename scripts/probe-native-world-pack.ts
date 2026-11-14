// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {open,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {createHash} from 'node:crypto';
import {capabilityDefinition} from '../shared/capabilities';
import {HeadlessRoomTransport} from '../src/headless/roomTransport';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {factReply,checkedProbeReply} from './native-probe-contract';
import {readNativeObjectIdentity} from './native-probe-identity';
import {probeWorldPack,worldPackActionOutput} from './probe-world-pack';

/** A bounded append-only audit of this deliberately separate workspace/session
 * boundary. Existing ordinary-journey observations remain intact. No replies,
 * identities or receipts are synthesized, and an incomplete trace cannot pass. */
export async function probeNativeWorldPack(transport:HeadlessRoomTransport,directory:string,checkState:(state:RoomAgentState)=>void){
 const traceFile='headless-world-pack-trace.jsonl',summaryFile='headless-world-pack-trace.json';
 const handle=await open(join(directory,traceFile),'wx'),digest=createHash('sha256');
 const maximumBytes=128*1024*1024,maximumRecords=16384,maximumRecordBytes=2*1024*1024;
 let bytes=0,records=0,exchanges=0,complete=false,failure:{name:string;message:string}|undefined;
 let initialIdentity:Awaited<ReturnType<typeof readNativeObjectIdentity>>|undefined,finalIdentity:typeof initialIdentity;
 let initial:RoomAgentState|undefined,final:RoomAgentState|undefined;
 const append=async(value:unknown)=>{const line=Buffer.from(JSON.stringify(value)+'\n');assert.ok(line.length<=maximumRecordBytes,'World-pack trace record exceeds its bounded evidence limit');assert.ok(records<maximumRecords&&bytes+line.length<=maximumBytes,'World-pack trace exceeds its bounded evidence limit');await handle.writeFile(line);digest.update(line);bytes+=line.length;records++;};
 const settled=async(previousSession?:string)=>{const end=Date.now()+(previousSession?120000:60000);for(;;){transport.checkHealth();const state=transport.client.getSnapshot().state;if(transport.client.lease()&&state?.workspaceReady!==false&&(!previousSession||state?.session!==previousSession))return;assert.ok(Date.now()<end,'Workspace handshake did not settle'+JSON.stringify({previousSession,session:state?.session,workspaceReady:state?.workspaceReady,status:state?.status}));await new Promise(resolve=>setTimeout(resolve,100));}};
 const execute=async<const C extends RoomCommand[]>(commands:C)=>{
  await settled();const lease=transport.lease(),prepared=await transport.client.prepare(commands,lease.state());assert.ok(lease.valid());checkState(prepared.state);
  const index=++exchanges;await append({kind:'prepared',index,commands,readSet:{session:prepared.state.session,sceneRevision:prepared.state.sceneRevision,objects:prepared.objects}});
  const result=await lease.execute(commands,prepared.state.sceneRevision,prepared.objects);
  await append({kind:'reply',index,result});checkState(result);assert.equal(result.ok,true,result.status);assert.equal(result.session,prepared.state.session,'Workspace changed during command exchange');
  return checkedProbeReply(commands,result);
 };
 try{
  await settled();initial=structuredClone(transport.lease().state());checkState(initial);await append({kind:'initial',state:initial});
  initialIdentity=await readNativeObjectIdentity(execute);
  await probeWorldPack({settled,session:async()=>transport.lease().state().session,
   fact:async(id,args={})=>{const view=factReply(await execute([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,arguments:args}}]));assert.equal(view.available,true,view.status);return view.value as Record<string,unknown>;},
   action:async(id,args)=>{
    const definition=capabilityDefinition(id);assert.ok(definition);await settled();const origin=transport.lease().state(),workspace=definition.domain==='workspace',runId=(workspace?origin.execution?.workspace:origin.execution)?.nextRunId;assert.ok(runId);
    const call={id,version:definition.version,arguments:args};let state=await execute([{action:'execution',execution:{operation:'start',runId,call}}]);
    const receipt=()=>definition.domain==='workspace'?state.execution?.workspace?.selected:state.execution?.selected;
    const check=()=>{assert.equal(state.session,origin.session);assert.equal(receipt()?.id,runId);assert.equal(receipt()?.capability,id);assert.equal(receipt()?.version,definition.version);assert.deepEqual(receipt()?.call,call);};check();const end=Date.now()+40000;
    while(['preparing','running'].includes(receipt()!.phase)){assert.ok(Date.now()<end);await new Promise(resolve=>setTimeout(resolve,100));state=await execute([{action:'execution',execution:{operation:'inspect',runId}}]);check();}
    assert.equal(receipt()?.phase,'completed',receipt()?.status);return worldPackActionOutput(id,definition.version,receipt()?.output);
   },undo:async()=>{await execute([{action:'undo'}]);},
  },directory,'headless');
  finalIdentity=await readNativeObjectIdentity(execute);final=structuredClone(transport.lease().state());checkState(final);
  await append({kind:'complete',initialIdentity,finalIdentity,state:final});complete=true;
 }catch(error){failure={name:error instanceof Error?error.name:'Error',message:(error instanceof Error?error.message:String(error)).slice(0,1024)};throw error;}
 finally{
  try{await handle.sync();}finally{await handle.close();}
  await writeFile(join(directory,summaryFile),JSON.stringify({version:1,complete,traceFile,sha256:digest.digest('hex'),bytes,records,exchanges,maximumBytes,maximumRecords,maximumRecordBytes,initialSession:initial?.session,finalSession:final?.session,initialIdentity,finalIdentity,failure},null,2),{flag:'wx'});
 }
 assert.ok(complete&&initial&&final&&initialIdentity&&finalIdentity);
 return {summaryFile,traceFile,complete,initialSession:initial.session,finalSession:final.session,initialIdentity,finalIdentity,exchanges,records,bytes};
}
