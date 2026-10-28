// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Additional restart gate. The wrapper separately requires all Live continuity gates.
import assert from 'node:assert/strict';
import {lstat,readFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {restartCheckpoint,validateRestartProcess,assertRestartedDefinition} from './first-learner-native-restart';
import {validNativeObjectIdentity,assertResumedNativeIdentity} from './native-probe-identity';
import {objectSearchRequest,readObjectSearchPage} from '../shared/objectSearch';
import {validCatalogView} from '../shared/roomCatalog';

const guid=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
const array=(v:unknown,min:number,max:number):any[]=>{assert(Array.isArray(v)&&v.length>=min&&v.length<=max);return v;};
const fact=(v:any,id:string)=>{
 assert(validCatalogView(v)&&v.operation==='inspect'&&v.category==='facts'&&v.capability===id&&v.version===1&&v.available);
 return v.value;
};
function checkpoint(v:any) {return restartCheckpoint(fact(v,'room.persistence'));}
function directory(point:any,expected:string[]) {
 const id=point.identity;assert(validNativeObjectIdentity(id));assert.deepEqual(id.ids,expected);
 const views=array(point.identityPages,2,512);let at=0;
 // Verify both complete native passes, following each exact cursor; never infer
 // saved membership from the periodic native object sample.
 for(let pass=0;pass<2;pass++){
  const ids:string[]=[];let cursor='';
  do{
   const page=readObjectSearchPage(views[at++],objectSearchRequest('', 'all',cursor));assert(page);
   assert.equal(page.worldId,id.worldId);assert.equal(page.offset,ids.length);assert.equal(page.total,expected.length);
   ids.push(...page.entries.map(x=>x.id));cursor=page.next;
  }while(cursor);
  assert.deepEqual(ids,expected);
 }
 assert.equal(at,views.length);
}
function identity(point:any,expected:string[]) {
 directory(point,expected);
 assert.equal(point.state.session,point.identity.session);assert.equal(point.state.workspaceReady,true);
 assert(!point.state.temporaryRoom?.active&&!point.state.temporaryRoom?.pending);
 checkpoint(point.persistence);
}
function capture(v:any,restored:boolean) {
 const tracks=array(v.tracks,restored?0:1,restored?0:256),attempts=array(v.attempts,0,256);
 for(const track of tracks){assert.equal(track.kind,'audio');assert.equal(track.readyState,'ended');assert.equal(typeof track.enabled,'boolean');}
 for(const attempt of attempts){assert.equal(attempt.audio,true);assert.equal(attempt.video,false);assert(restored?attempt.state==='rejected':['resolved','rejected'].includes(attempt.state));}
}
/** JSON report checks are independently testable without launching Unity or a provider. */
export function assertNativeRestartResult(result:any,root:string,id:string,requestBytes:string) {
 assert(guid(id));assert.equal(result.status,'passed');assert.equal(result.nativeProbeId,id);
 assert.equal(result.productionComposition,true);assert.equal(result.nativeExecution,true);assert.equal(result.realProvider,false);assert.equal(result.physicalDevice,false);
 const n=result.native,r=n.restart;assert.equal(n.nativeProbeId,id);assert.equal(n.qualified,true);assert.equal(r.version,1);assert.equal(r.qualified,true);assert.equal(r.sourceProbeId,id);
 const q=r.request;assert.deepEqual(q,JSON.parse(requestBytes));assert.equal(q.version,1);assert.equal(q.id,id);assert.equal(q.operation,'restart');
 for(const name of ['clientId','nativeSession','appleId','treeId'])assert(guid(q[name]));assert.notEqual(q.appleId,q.treeId);
 assert.equal(q.appleId,n.appleId);assert.equal(q.treeId,n.treeId);assert.deepEqual(restartCheckpoint(q.checkpoint),checkpoint(r.before.persistence));
 const process=validateRestartProcess(r.process,r.sourceReady,r.resumedReady,root,id,requestBytes);
 assert.equal(r.childProbeId,process.resumed.id);
 for(const ready of [r.sourceReady,r.resumedReady]){assert.equal(ready.version,1);assert.equal(ready.syntheticPhysics,true);assert.equal(ready.syntheticScan,false);}
 assert.equal(r.sourceReady.restartOf,'');
 assert(validNativeObjectIdentity(r.initialWorld.identity));
 directory(r.initialWorld,r.initialWorld.identity.ids);
 assert.equal(r.initialWorld.identity.session,q.nativeSession);assert.equal(r.initialWorld.identity.worldId,r.before.identity.worldId);
 assert(!r.initialWorld.identity.ids.includes(q.appleId)&&!r.initialWorld.identity.ids.includes(q.treeId));
 const expected=[...r.initialWorld.identity.ids,q.appleId].sort();identity(r.before,expected);identity(r.after,expected);
 assertResumedNativeIdentity(r.before.identity,r.after.identity);assert.equal(q.nativeSession,r.before.identity.session);
 assertRestartedDefinition(n.appleDefinition,n.restored.apple,q.appleId);
 assert.equal(n.restored.apple.arguments.target,q.appleId);assert.equal(n.appleDefinition.arguments.target,q.appleId);
 const gone=readObjectSearchPage(n.restored.directory,objectSearchRequest(q.treeId));assert(gone&&gone.total===0&&gone.entries.length===0);
 assert.equal(gone.worldId,r.after.identity.worldId);
 const receipts=array(n.receipts,3,3);assert.equal(new Set(receipts.map(x=>x.id)).size,3);
 for(const [i,receipt] of receipts.entries()){
  assert(guid(receipt.id));assert.equal(receipt.phase,'completed');assert.equal(receipt.version,1);
  assert.equal(receipt.capability,i<2?'object.create':'object.delete');assert.equal(receipt.call.id,receipt.capability);
  assert.equal(i<2?receipt.output.objectId:receipt.call.arguments.target,i===1?q.appleId:q.treeId);
 }
 assert.deepEqual(r.before.receipts,receipts);assert.deepEqual(r.after.receipts,receipts);
 capture(r.before.capture,false);capture(r.after.capture,true);
 assert.deepEqual(r.before.capture,n.beforeReloadCapture);assert.deepEqual(r.after.capture,n.restoredCapture);
 const clients=array(n.clients,2,2);assert(guid(clients[0])&&guid(clients[1]));assert.notEqual(clients[0],clients[1]);assert.equal(q.clientId,clients[0]);
 const requests=array(n.requests,4,2048),split=r.requestsBeforeRestart;
 assert(Number.isSafeInteger(split)&&split>0&&split<requests.length);
 const starts:any[]=[],inspected=new Set<string>();
 requests.forEach((item:any,index:number)=>{
  const resumed=index>=split;assert.equal(item.clientId,clients[resumed?1:0]);assert.equal(item.request.session,resumed?r.after.identity.session:q.nativeSession);
  for(const c of array(item.request.commands,1,64)){
   if(c.action==='execution'&&c.execution.operation==='start')starts.push(c.execution.runId);
   if(!resumed)continue;
   if(c.action==='catalog'){assert(['search','inspect'].includes(c.catalog.operation));continue;}
   if(c.action==='workspace'){assert.equal(typeof c.visible,'boolean');assert.deepEqual(Object.keys(c).sort(),['action','visible']);continue;}
   assert.equal(c.action,'execution');assert.equal(c.execution.operation,'inspect');
   assert(receipts.some(x=>x.id===c.execution.runId));inspected.add(c.execution.runId);
  }
 });
 assert.deepEqual(starts,receipts.map(x=>x.id));assert.deepEqual([...inspected].sort(),receipts.map(x=>x.id).sort());
 const observations=array(n.observations,4,10000);
 for(const [index,point] of [r.before,r.after].entries()){
  assert(observations.some((x:any)=>x.clientId===clients[index]&&x.session===point.identity.session&&x.ok===true));
  for(const receipt of receipts)assert(observations.some((x:any)=>x.clientId===clients[index]&&x.session===point.identity.session&&x.ok===true&&x.execution?.id===receipt.id&&x.execution.phase==='completed'&&x.execution.capability===receipt.capability));
 }
}
async function readEvidence(path:string,maximum=16384) {
 const info=await lstat(path);assert(info.isFile()&&!info.isSymbolicLink()&&info.size>0&&info.size<=maximum);
 const bytes=await readFile(path);assert(bytes.length<=maximum);return bytes.toString('utf8').replace(/^\uFEFF/,'');
}
async function missing(path:string) {
 try{await lstat(path);}catch(error){if((error as NodeJS.ErrnoException).code==='ENOENT')return;throw error;}
 assert.fail('Forbidden restart evidence exists: '+path);
}
/** Cross-check report claims against the parent's retained process receipts. */
export async function assertNativeRestartFiles(root:string) {
 root=resolve(root);const child=join(root,'restart');
 for(const path of [root,child,join(root,'workspace')]){const info=await lstat(path);assert(info.isDirectory()&&!info.isSymbolicLink());}
 const json=async(path:string)=>JSON.parse(await readEvidence(path));
 const owner=await json(join(root,'owner.json')),childOwner=await json(join(child,'owner.json'));
 const result=JSON.parse(await readEvidence(join(root,'first-learner/result.json'),32*1024*1024)),r=result.native.restart;
 for(const value of [owner,childOwner]){assert.equal(value.version,1);assert.equal(value.nativeRestart,true);assert.equal(value.firstLearner,true);assert.equal(value.liveContinuity,true);}
 assert.equal(owner.restartOf,undefined);assert.equal(childOwner.restartOf,owner.id);assert.equal(childOwner.id,r.childProbeId);
 for(const [path,expected] of [[join(root,'ready.json'),r.sourceReady],[join(child,'ready.json'),r.resumedReady],[join(root,'restart.json'),r.process]] as const)assert.deepEqual(await json(path),expected);
 await missing(join(root,'terminal.json'));await missing(join(child,'workspace'));await missing(join(child,'learner-resume.json'));
 const terminal=await json(join(child,'terminal.json'));assert.equal(terminal.id,childOwner.id);assert.equal(terminal.exitCode,0);assert.equal(terminal.version,1);
 assertNativeRestartResult(result,root,owner.id,await readEvidence(join(root,'restart-request.json'),8192));
 return {id:owner.id,child:childOwner.id,status:'passed'};
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){
 assert(process.argv[2]);console.log(JSON.stringify(await assertNativeRestartFiles(process.argv[2])));
}
