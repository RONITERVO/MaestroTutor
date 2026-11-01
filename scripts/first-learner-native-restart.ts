// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// A fixed, owned file handshake with the parent process supervisor. It never
// launches, terminates or synthesizes a native process or native observation.
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {lstat,readFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {readObjectReference} from '../shared/objectReference';
import type {CatalogView} from '../shared/roomCatalog';
import {RoomProbeChannel,publishRoomProbeFile} from '../src/headless/roomTransport';

const record=(v:unknown):v is Record<string,any>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const guid=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
const integer=(v:unknown)=>Number.isSafeInteger(v)&&Number(v)>0;
export function restartCheckpoint(value:unknown) {
 assert(record(value));assert.deepEqual(Object.keys(value).sort(),['behaviours','room']);
 const result:Record<string,{phase:string;pending:boolean;dirty:boolean;dispatchHeld:boolean;error:string}>={};
 for(const key of ['room','behaviours']) {
  const s:unknown=value[key];assert(record(s));assert.deepEqual(Object.keys(s).sort(),['dirty','dispatchHeld','error','pending','phase']);
  assert.equal(s.phase,'idle');assert.equal(s.pending,false);assert.equal(s.dirty,false);assert.equal(s.dispatchHeld,false);assert.equal(s.error,'');
  result[key]={phase:s.phase,pending:s.pending,dirty:s.dirty,dispatchHeld:s.dispatchHeld,error:s.error};
 }
 return result;
}
export function validateRestartProcess(process:unknown,sourceReady:Record<string,any>,resumedReady:Record<string,any>,root:string,rootId:string,requestBytes:string) {
 assert(record(process));const p=process;
 assert.equal(p.version,1);assert.equal(p.id,rootId);assert.equal(p.method,'terminate-after-checkpoint');
 assert.equal(resolve(p.workspace),join(resolve(root),'workspace'));
 assert.equal(p.requestSha256,createHash('sha256').update(requestBytes).digest('hex'));
 assert(record(p.source)&&record(p.resumed));const {source,resumed}=p;
 assert.equal(source.id,rootId);assert.equal(source.terminalExisted,false);
 assert(integer(source.processId)&&integer(source.startedAt)&&integer(source.exitedAt));assert(Number.isSafeInteger(source.exitCode));
 assert(source.exitedAt>=source.startedAt);assert.equal(source.processId,sourceReady.processId);assert.equal(source.startedAt,sourceReady.processStartedAt);
 assert.equal(sourceReady.id,rootId);assert.equal(resolve(sourceReady.workspaceDirectory),resolve(p.workspace));
 assert(guid(resumed.id)&&resumed.id!==rootId);assert.equal(resolve(resumed.directory),join(resolve(root),'restart'));
 assert(integer(resumed.processId)&&integer(resumed.startedAt));assert.notEqual(resumed.processId,source.processId);assert(resumed.startedAt>=source.exitedAt);
 assert.equal(resumedReady.id,resumed.id);assert.equal(resumedReady.processId,resumed.processId);assert.equal(resumedReady.processStartedAt,resumed.startedAt);
 assert.equal(resumedReady.restartOf,rootId);assert.equal(resolve(resumedReady.workspaceDirectory),resolve(p.workspace));
 assert.equal(resumedReady.syntheticPhysics,true);assert.equal(resumedReady.syntheticScan,false);
 return p;
}
async function boundedJson(path:string) {
 const file=await lstat(path);assert(file.isFile()&&!file.isSymbolicLink()&&file.size<=16384,'Invalid restart handshake file');
 const bytes=await readFile(path);assert(bytes.length<=16384);return JSON.parse(bytes.toString('utf8'));
}
export async function restartNativeLearner(channel:RoomProbeChannel,sourceReady:Record<string,any>,request:Record<string,unknown>) {
 const root=channel.directory,child=join(root,'restart');
 assert.equal(request.id,channel.id);assert.equal(request.version,1);assert.equal(request.operation,'restart');
 for(const key of ['clientId','nativeSession','appleId','treeId'])assert(guid(request[key]));
 assert.notEqual(request.appleId,request.treeId);restartCheckpoint(request.checkpoint);
 const bytes=JSON.stringify(request);await publishRoomProbeFile(join(root,'restart-request.json'),bytes);
 const deadline=Date.now()+120000;let ready:Record<string,any>|undefined,process:Record<string,any>|undefined;
 while(Date.now()<deadline) {
  try {
   const proof=await boundedJson(join(root,'restart.json')),observed=await boundedJson(join(child,'ready.json'));
   if(record(proof)&&record(proof.resumed)&&integer(proof.resumed.processId)) {
    process=validateRestartProcess(proof,sourceReady,observed,root,channel.id,bytes);ready=observed;break;
   }
  }catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
  await new Promise(resolve=>setTimeout(resolve,100));
 }
 assert(ready&&process,'Parent did not establish a genuine new native process');
 const next=await RoomProbeChannel.connect(child,5000);assert.equal(next.id,process.resumed.id);
 return {channel:next,ready,process,request};
}

// RoomJournal reconstructs its in-memory revision clock when it opens a saved
// document. A new native session invalidates old edit authority; the persisted
// metadata must match, while both freshly observed revisions remain explicit.
export function assertRestartedDefinition(before:any,after:any,target:string) {
 assert(readObjectReference(before as CatalogView,target)&&readObjectReference(after as CatalogView,target));
 const {revision:beforeRevision,...beforeData}=before.value,{revision:afterRevision,...afterData}=after.value;
 assert(Number.isInteger(beforeRevision)&&Number.isInteger(afterRevision));assert.deepEqual(afterData,beforeData);
}
