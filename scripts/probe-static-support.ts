// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {awaitRoomPersistence} from './probe-room-persistence';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';

export type SupportReceipt=NonNullable<NonNullable<RoomAgentState['execution']>['selected']>;
type RecordValue=Record<string,unknown>;
export interface SupportSaved {version:number;sha256:string;objects:RecordValue[];}
export interface StaticSupportDriver {
 fact(id:string,args?:RecordValue):Promise<{available:boolean;value?:unknown}>;
 invoke(id:string,args:RecordValue):Promise<SupportReceipt>;
 reject(id:string,args:RecordValue):Promise<RoomAgentState>;
 undo():Promise<void>;
 redo():Promise<void>;
 saved():Promise<SupportSaved>;
 replay(receipt:SupportReceipt):Promise<SupportReceipt>;
 capture?(phase:string):Promise<void>;
}
type Support={target:string;revision:number;enabled:boolean;ready:boolean;reason:string;temporary:boolean};
const record=(value:unknown):value is RecordValue=>!!value&&typeof value==='object'&&!Array.isArray(value);
const stale='Object settings changed; inspect their current revision before editing';
const incompatible='Static support requires a fixed created object without scanned anchoring or recorded root motion';

/** A transport error or old selected receipt is not a refusal of this action. */
export function assertSupportRefusal(state:RoomAgentState,id:string,args:RecordValue,kind:'stale'|'incompatible'){
 const selected=state.execution?.selected;
 assert.ok(state.ok===false||selected?.phase==='failed','Expected refused static support edit');
 if(state.ok!==false){
  assert.equal(selected!.capability,id);assert.equal(selected!.version,1);
  assert.deepEqual(selected!.call,{id,version:1,arguments:args});
 }
 assert.equal(state.ok===false?state.status:selected!.status,kind==='stale'?stale:incompatible,'Expected the actual native support refusal');
}

/** Matching visible and physical geometry; two separate boxes are deliberately
 * retained, not replaced by a single aggregate bounding box. Support is omitted. */
export function staticSupportFixture(){
 const shapes=[-.3,.3].map((x,i)=>({id:'Deck'+i,shape:'box',position:{x,y:.04,z:0},size:{x:.4,y:.08,z:.8},rotation:{x:0,y:0,z:0,w:1}}));
 return {kind:'recipe',name:'Static support compound proof',x:2,y:1,z:2,scale:1,
  recipe:{version:1,parts:shapes.map(shape=>({...structuredClone(shape),parent:null,color:{r:.6,g:.4,b:.2,a:1}})),tracks:[],duration:2,playing:false,loop:false},
  collision:{version:1,shapes},physics:{mode:'fixed',shape:'automatic',mass:1}};
}
/** Unity stores these coordinates as float32. Permit only its tiny projection
 * error when checking submitted geometry. Source components remain exact; the
 * live root position has its own bounded frame-roundtrip check below. */
export function assertSupportCollisionShape(actual:unknown,expected:ReturnType<typeof staticSupportFixture>['collision']['shapes'][number]){
 assert.ok(record(actual));assert.equal(actual.id,expected.id);assert.equal(actual.shape,expected.shape);
 for(const field of ['position','size','rotation'] as const){
  const value=actual[field];assert.ok(record(value));
  for(const [axis,wanted] of Object.entries(expected[field])){
   const found=value[axis];assert.ok(typeof found==='number'&&Number.isFinite(found)&&Math.abs(found-wanted)<=1e-6,'Native collision '+field+'.'+axis+' differs from the submitted source');
  }
 }
 assert.equal(actual.segments,0);assert.equal(actual.innerRadius,0);
}
/** Settings edits capture the current native pose. A fixed Rigidbody's world/local
 * roundtrip can change position by float32 rounding; do not confuse that with an
 * authored geometry edit. Refusals and historical replay still require exact bytes. */
export function assertSupportSavedSource(before:RecordValue,after:RecordValue){
 const omitPoseAndSupport=(value:RecordValue)=>Object.fromEntries(Object.entries(value).filter(([key])=>key!=='staticSupport'&&key!=='position'));
 assert.deepEqual(omitPoseAndSupport(after),omitPoseAndSupport(before),'Support edit changed unrelated saved source');
 if(!Object.hasOwn(before,'position')&&!Object.hasOwn(after,'position'))return;
 assert.ok(record(before.position)&&record(after.position),'Saved position missing');
 for(const value of [before.position,after.position])assert.deepEqual(Object.keys(value).sort(),['x','y','z']);
 for(const axis of ['x','y','z']){
  const a=before.position[axis],b=after.position[axis];
  assert.ok(typeof a==='number'&&typeof b==='number'&&Number.isFinite(a)&&Number.isFinite(b)&&Math.abs(a-b)<=1e-6,'Support edit moved saved position.'+axis);
 }
}
function savedObject(snapshot:SupportSaved,target:string){
 assert.equal(snapshot.version,47);assert.match(snapshot.sha256,/^[a-f0-9]{64}$/);
 const matches=snapshot.objects.filter(item=>item.id===target);assert.equal(matches.length,1,'Expected one durable source object');return matches[0];
}

/** This is an offline client scenario, not a collision/route or physical-device
 * test. Native save receipts, exact files and passive readiness are independent
 * proof boundaries; the unit-test driver only tests these assertions. */
export async function probeStaticSupport(driver:StaticSupportDriver,directory:string,client:'headless'|'book'){
 const evidence:RecordValue={client,complete:false,boundary:'Ordinary catalog calls and persisted preparation in the native desktop app, with read-only inspection of its owned saved room. This leaf proves saved intent, current revisions and readiness only; no physical collision, navigation route, headset, provider, prototype roundtrip or Book UI claim follows from Headless execution.'};
 const receipts:SupportReceipt[]=[],refusals:RecordValue[]=[],snapshots:RecordValue[]=[];
 const historyCheckpoints:unknown[]=[];
 Object.assign(evidence,{receipts,refusals,snapshots,historyCheckpoints});
 const history=async(direction:'undo'|'redo')=>{
  await driver[direction]();
  // Undo/Redo acknowledge journal acceptance; disk publication is asynchronous.
  // Observe it without repeating the edit, flushing or rewriting saved evidence.
  const samples=await awaitRoomPersistence(async()=>{const reply=await driver.fact('room.persistence',{});assert.equal(reply.available,true);return reply.value;});
  historyCheckpoints.push({direction,samples});
 };
 const checkpoint=async(phase:string)=>{evidence.phase=phase;await writeFile(join(directory,client+'-static-support.json'),JSON.stringify(evidence,null,2));await driver.capture?.(phase);};
 const fact=async(id:string,args?:RecordValue)=>{const result=await driver.fact(id,args);assert.equal(result.available,true,id+' unavailable');assert.ok(record(result.value),id+' missing value');return result.value;};
 const invoke=async(id:string,args:RecordValue)=>{
  const receipt=await driver.invoke(id,args);receipts.push(structuredClone(receipt));
  assert.equal(receipt.phase,'completed',receipt.status);assert.equal(receipt.capability,id);assert.equal(receipt.version,1);
  assert.deepEqual(receipt.call,{id,version:1,arguments:args});return receipt;
 };
 try{
  const before=await driver.saved(),fixture=staticSupportFixture();
  const simulation=await fact('physics.simulation');assert.equal(simulation.running,false,'Support fixture expects paused physics');
  const created=await invoke('object.create',fixture),target=String(created.output?.objectId);
  assert.match(target,/^[a-f0-9]{32}$/);assert.ok(!before.objects.some(item=>item.id===target));evidence.target=target;
  const createdSaved=await driver.saved(),source=savedObject(createdSaved,target);assert.deepEqual(source.staticSupport,{version:1,enabled:false});
  // A completed creation drains earlier queued saves. Use its durable snapshot
  // for later unrelated-source preservation, rather than a prior physics save.
  const otherSources=createdSaved.objects.filter(item=>item.id!==target);assert.deepEqual(otherSources.map(item=>item.id).sort(),before.objects.map(item=>item.id).sort());
  const read=async(enabled:boolean)=>{
   const value=await fact('object.support',{target}) as Support;
   assert.equal(value.target,target);assert.ok(Number.isSafeInteger(value.revision)&&value.revision>0);
   assert.equal(value.enabled,enabled);assert.equal(typeof value.ready,'boolean');assert.equal(typeof value.reason,'string');assert.equal(value.temporary,false);
   const definition=await fact('object.definition',{target});assert.equal(definition.revision,value.revision);
   const saved=await driver.saved(),object=savedObject(saved,target);assertSupportSavedSource(source,object);assert.deepEqual(object.staticSupport,{version:1,enabled});
   assert.equal((await fact('physics.simulation')).running,false,'Support actions or passive reads started physics');
   snapshots.push({value:structuredClone(value),definition,savedSha256:saved.sha256,source:object});return value;
  };
  const initial=await read(false);assert.equal(initial.ready,false);assert.equal(initial.reason,'Static support is disabled');
  const collision=await fact('object.collision',{target});assert.equal(collision.revision,initial.revision);assert.equal(collision.shapes,2);assert.equal(collision.pieces,2);assert.equal(collision.customActive,true);
  for(let index=0;index<2;index++){
   const part=await fact('object.collision.shape',{target,revision:initial.revision,index});
   assert.equal(part.revision,initial.revision);assert.equal(part.index,index);assert.equal(part.total,2);assert.ok(record(part.shape));
   assertSupportCollisionShape(part.shape,fixture.collision.shapes[index]);
  }
  Object.assign(evidence,{fixture,initial,collision,source,createdSavedSha256:createdSaved.sha256});await checkpoint('default-off');
  const enabledReceipt=await invoke('object.support.set',{target,revision:initial.revision,enabled:true});
  let enabled=await read(true);assert.ok(enabled.revision>initial.revision);assert.equal(enabledReceipt.output?.revision,enabled.revision);assert.equal(enabledReceipt.output?.target,target);assert.equal(enabledReceipt.output?.temporary,false);
  // Passive bounded polling never repeats the action or silently activates geometry.
  for(let attempt=0;!enabled.ready&&attempt<40;attempt++){
   await new Promise(resolve=>setTimeout(resolve,100));const next=await read(true);assert.equal(next.revision,enabled.revision);enabled=next;
  }
  assert.equal(enabled.ready,true,enabled.reason);assert.equal(enabled.reason,'');evidence.enabled=enabled;await checkpoint('enabled-ready');
  const refused=async(id:string,args:RecordValue,kind:'stale'|'incompatible')=>{
   const saved=await driver.saved(),value=await read(true),state=await driver.reject(id,args);
   refusals.push({kind,id,args,state});assertSupportRefusal(state,id,args,kind);
   assert.equal((await driver.saved()).sha256,saved.sha256,'A refused edit changed saved bytes');assert.deepEqual(await read(true),value,'A refused edit changed intent, revision or readiness');
  };
  await refused('object.support.set',{target,revision:initial.revision,enabled:false},'stale');
  await refused('object.physics.configure',{target,revision:enabled.revision,mode:'solid',shape:'automatic',mass:1},'incompatible');
  await checkpoint('refusals-preserved');
  const disabledReceipt=await invoke('object.support.set',{target,revision:enabled.revision,enabled:false});
  const disabled=await read(false);assert.ok(disabled.revision>enabled.revision);assert.equal(disabledReceipt.output?.revision,disabled.revision);assert.equal(disabled.ready,false);
  const disabledSaved=await driver.saved(),historical=await driver.replay(enabledReceipt);
  assert.deepEqual(historical,enabledReceipt,'Historical support receipt changed');assert.equal((await driver.saved()).sha256,disabledSaved.sha256,'Historical receipt replay enabled support again');assert.deepEqual(await read(false),disabled);
  evidence.historical=historical;await checkpoint('disabled-history');
  await history('undo');const undo=await read(true);assert.equal(undo.ready,true,undo.reason);assert.ok(undo.revision>disabled.revision);
  await history('redo');const redo=await read(false);assert.equal(redo.ready,false);assert.ok(redo.revision>undo.revision);
  // The first Undo reverses disable; the second enable; the third creation.
  // Refusals, reads and duplicate-start must not introduce extra history entries.
  await history('undo');assert.equal((await read(true)).ready,true);
  await history('undo');assert.equal((await read(false)).ready,false);
  await history('undo');const removed=await driver.fact('object.support',{target});assert.equal(removed.available,false);assert.ok(removed.value==null);
  const final=await driver.saved();assert.ok(!final.objects.some(item=>item.id===target));
  assert.deepEqual(final.objects,otherSources,'Support editing or cleanup changed another saved object');
  Object.assign(evidence,{disabled,undo,redo,removed,finalSavedSha256:final.sha256,complete:true});await checkpoint('complete');
 }catch(error){evidence.complete=false;evidence.error=error instanceof Error?error.message:String(error);await checkpoint('failed');throw error;}
}
