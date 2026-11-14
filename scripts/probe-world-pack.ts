// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {lstat,readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {createHash} from 'node:crypto';
import {awaitRoomPersistence} from './probe-room-persistence';
import {capabilityDefinition,validateCapabilityOutput} from '../shared/capabilities';

type Value=Record<string,unknown>;
export type WorldPackAdapter={
 fact:(id:string,args?:Value)=>Promise<Value>;
 action:(id:string,args:Value)=>Promise<Value|undefined>;
 undo:()=>Promise<void>;
 settled:(previousSession?:string)=>Promise<void>;
 session:()=>Promise<string>;
 checkpoint?:(phase:string)=>Promise<void>;
};
const sleep=(ms:number)=>new Promise(resolve=>setTimeout(resolve,ms));
const id=(v:unknown)=>{assert.equal(typeof v,'string');assert.match(v as string,/^[a-f0-9]{32}$/);return v as string;};
const hash=(v:unknown)=>{assert.equal(typeof v,'string');assert.match(v as string,/^[a-f0-9]{64}$/);return v as string;};
const object=(v:unknown):Value=>{assert.ok(v&&typeof v==='object'&&!Array.isArray(v));return v as Value;};
/** Saved membership is keyed by exact ID. Only the outer ordering may change
 * during canonical saving; every nested field and array stays strict. */
export function worldPackObjects(value:unknown):Value[]{
 assert.ok(Array.isArray(value),'Expected saved object records');const records=value.map(object),ids=new Set<string>();
 for(const entry of records){assert.equal(typeof entry.id,'string');const identity=entry.id as string;assert.match(identity,/^(book|maestro|[a-f0-9]{32})$/);assert.ok(!ids.has(identity),'Duplicate saved object ID');ids.add(identity);}
 return records.sort((a,b)=>String(a.id)<String(b.id)?-1:String(a.id)>String(b.id)?1:0);
}
export function assertWorldPackObjectsEqual(actual:unknown,expected:unknown,message:string){
 assert.deepEqual(worldPackObjects(actual),worldPackObjects(expected),message);
}

/** Completion is established by each adapter's exact native receipt checks.
 * Validate its actual output against the shared contract without manufacturing a
 * value for a successful void action (for example object.color.set). */
export function worldPackActionOutput(id:string,version:number,output:unknown):Value|undefined {
 const definition=capabilityDefinition(id);assert.ok(definition,'Unknown action output contract');assert.equal(definition.version,version,'Action output version differs');
 if(!definition.output){assert.ok(output===null||output===undefined,'Void action returned an unexpected output');return undefined;}
 assert.equal(validateCapabilityOutput(id,version,output),null,'Action output does not match the shared capability contract');
 return object(output);
}
/** Activation is acknowledged once in the old session. Do not poll commands
 * into its deliberate reset: wait for a different ready session, then inspect
 * the same host-owned request. A timeout or unexpected reset is never success. */
export async function activateWorldPackSelection(adapter:WorldPackAdapter,selection:Value,selectionRequestId:string,expectedRevision:unknown){
 const previousSession=await adapter.session();id(previousSession);
 const started=object(await adapter.action('workspace.archive.activate',{selectionRequestId,generationId:id(selection.generationId),manifestHash:hash(selection.manifestHash),expectedRevision}));
 const requestId=id(started.requestId);
 await adapter.settled(previousSession);assert.notEqual(await adapter.session(),previousSession,'Activation did not change the ready workspace session');
 const deadline=Date.now()+120000;let result:Value;
 for(;;){
  result=await adapter.fact('workspace.archive.activation',{requestId});
  assert.equal(result.requestId,requestId);assert.equal(result.selectionRequestId,selectionRequestId);assert.equal(result.originRevision,expectedRevision);
  assert.equal(object(result.preview).generationId,selection.generationId);assert.equal(object(result.preview).manifestHash,selection.manifestHash);
  if(result.phase==='review')break;
  assert.ok(['preserving','activating','committed'].includes(String(result.phase)),JSON.stringify(result));
  assert.ok(Date.now()<deadline,'Timed out observing the tracked workspace activation');await sleep(150);
 }
 const current=await adapter.fact('workspace.current');
 assert.equal(current.generationId,selection.generationId);assert.equal(current.revision,result.committedRevision);
 assert.equal(current.activationRequestId,requestId);assert.equal(current.reviewRequired,true);assert.equal(current.available,true);assert.equal(current.changing,false);
 return {result,current};
}

/** Reads only this explicitly owned offline run. It never repairs files or follows a fallback. */
async function saved(directory:string,generation:string){
 const ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8')),owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8'));
 assert.equal(ready.id,owner.id);assert.equal(owner.providerUsed,false);
 const workspace=join(resolve(directory),'workspace');assert.equal(resolve(ready.workspaceDirectory),workspace);
 const parts=generation==='original'?['room']:['workspace-generations.v1','generations',id(generation),'data'];
 let path=workspace;for(const part of ['',...parts]){path=part?join(path,part):path;const info=await lstat(path);assert.ok(info.isDirectory()&&!info.isSymbolicLink());}
 const read=async(name:string,maximum:number)=>{const file=join(path,name),info=await lstat(file);assert.ok(info.isFile()&&!info.isSymbolicLink()&&info.size>0&&info.size<=maximum);const bytes=await readFile(file);assert.ok(bytes.length<=maximum);return {value:JSON.parse(bytes.toString('utf8')),sha256:createHash('sha256').update(bytes).digest('hex')};};
 const room=await read('room.v47.json',4*1024*1024);assert.equal(room.value.version,47);
 return {room,provenance:async()=>read('world-packs.v1.json',1024*1024)};
}
/** The same workflow drives CLI calls and ordinary visible Book controls.
 * No provider reply, native acknowledgement or saved generation is synthesized. */
export async function probeWorldPack(adapter:WorldPackAdapter,directory:string,route:'headless'|'book'){
 const evidence:Value={complete:false,route,boundary:'Actual Unity workspace preparation, cancellation, activation, authored edit/Undo, archive-covered provenance and previous-workspace restoration. CLI and visible Book use the shared catalog. No device/provider/performance or complete Cosmos acceptance.'};
 const checkpoint=async(phase:string)=>{evidence.phase=phase;await writeFile(join(directory,route+'-world-pack.json'),JSON.stringify(evidence,null,2));await adapter.checkpoint?.(phase);};
 const wait=async(name:string,args:Value,phases:string[],terminal:string[])=>{const end=Date.now()+(name==='workspace.archive.selection'?600000:120000);for(;;){await adapter.settled();const value=await adapter.fact(name,args);if(terminal.includes(String(value.phase)))return value;assert.ok(phases.includes(String(value.phase)),JSON.stringify(value));assert.ok(Date.now()<end,'Timed out observing '+name);await sleep(150);}};
 await adapter.settled();await awaitRoomPersistence(()=>adapter.fact('room.persistence'));
 const origin=await adapter.fact('workspace.current');assert.equal(origin.available,true);assert.equal(origin.changing,false);
 const before=(await saved(directory,String(origin.generationId))).room.value;
 const listing=await adapter.fact('workspace.packs',{offset:0}),entries=listing.entries as Value[];assert.ok(Array.isArray(entries)&&entries.length===1);const pack=entries[0];
 const args={packId:pack.id,packVersion:pack.version,packHash:hash(pack.hash),expectedRevision:origin.revision};
 const prepare=async()=>{const startedAt=Date.now();const started=object(await adapter.action('workspace.pack.prepare',args)),requestId=id(started.requestId);const selection=await wait('workspace.archive.selection',{requestId},['preparing'],['prepared']);
  const summary=object(selection.summary);for(const [name,count] of Object.entries(summary))if(name.startsWith('missing')||name==='unavailablePrograms')assert.equal(count,0,name);
  const preview=await adapter.fact('workspace.pack.preview',{requestId});assert.equal(preview.packHash,pack.hash);assert.equal(preview.originRevision,origin.revision);assert.notEqual(id(preview.worldId),before.world.worldId);id(preview.instanceId);
  assert.deepEqual(await adapter.fact('workspace.current'),origin,'Preparing changed the current world');return {requestId,selection,preview,elapsedMilliseconds:Date.now()-startedAt};};
 const cancelled=await prepare();await checkpoint('preview-before-cancel');await adapter.action('workspace.archive.cancel',{requestId:cancelled.requestId});
 await wait('workspace.archive.selection',{requestId:cancelled.requestId},['cancelling'],['cancelled']);assert.deepEqual(await adapter.fact('workspace.current'),origin);
 const prepared=await prepare();assert.notEqual(prepared.preview.instanceId,cancelled.preview.instanceId);evidence.cancelled=cancelled;evidence.prepared=prepared;await checkpoint('prepared');
 const activate=(selection:Value,selectionRequestId:string,expectedRevision:unknown)=>activateWorldPackSelection(adapter,selection,selectionRequestId,expectedRevision);
 const completeReview=async(current:Value)=>{
  const started=object(await adapter.action('workspace.review.prepare',{expectedRevision:current.revision})),requestId=id(started.requestId);
  const prepared=await wait('workspace.review',{requestId},['preparing'],['prepared']);
  const summary=object(prepared.summary);for(const [name,count] of Object.entries(summary))if(name.startsWith('missing')||name==='unavailablePrograms')assert.equal(count,0,name);
  const completed=object(await adapter.action('workspace.review.complete',{requestId,manifestHash:hash(prepared.manifestHash),expectedRevision:current.revision}));assert.equal(completed.requestId,requestId);
  const result=await wait('workspace.review',{requestId},['completing'],['completed']);assert.equal((await adapter.fact('workspace.current')).reviewRequired,false);return {prepared,result};
 };
 const opened=await activate(prepared.selection,prepared.requestId,origin.revision);evidence.opened=opened;
 await awaitRoomPersistence(()=>adapter.fact('room.persistence'));
 const installed=await saved(directory,String(opened.current.generationId)),provenance=await installed.provenance();
 assert.equal(installed.room.value.world.worldId,prepared.preview.worldId);assert.equal(provenance.value.worldId,prepared.preview.worldId);assert.equal(provenance.value.instances.length,1);
 const instance=provenance.value.instances[0];assert.equal(instance.instanceId,prepared.preview.instanceId);assert.equal(instance.sourceHash,pack.hash);
 assert.ok(instance.objects.length>=3);const ids=new Set(installed.room.value.objects.map((item:Value)=>item.id));
 for(const mapping of instance.objects)assert.ok(ids.has(mapping.id));assert.equal(ids.size,instance.objects.length+2);assert.ok(ids.has('book')&&ids.has('maestro'));
 const observed=await adapter.fact('workspace.pack.instance',{offset:0});assert.equal(observed.total,1);assert.equal((observed.entries as Value[]).length,1);assert.equal((observed.entries as Value[])[0].instanceId,instance.instanceId);assert.equal(observed.worldId,provenance.value.worldId);
 evidence.installed={room:installed.room,provenance,observed};await checkpoint('opened');
 evidence.review=await completeReview(opened.current);
 const target=id(instance.objects[0].id),source=installed.room.value.objects.find((item:Value)=>item.id===target);
 await adapter.action('object.color.set',{target,red:.13,green:.72,blue:.21});await awaitRoomPersistence(()=>adapter.fact('room.persistence'));
 const paintedRoom=(await saved(directory,String(opened.current.generationId))).room.value,painted=paintedRoom.objects.find((item:Value)=>item.id===target);assert.ok(painted);
 const binding=painted.appearanceBindings.find((entry:Value)=>entry.kind==='root');
 if(binding)assert.equal(binding.tint.toUpperCase(),'#21B836');else for(const [axis,expected] of Object.entries({r:.13,g:.72,b:.21}))assert.ok(Math.abs(painted.color[axis]-expected)<.000001,'Paint did not set '+axis);
 assertWorldPackObjectsEqual(worldPackObjects(paintedRoom.objects).filter(item=>item.id!==target),worldPackObjects(installed.room.value.objects).filter(item=>item.id!==target),'Paint changed another object');
 const unpainted=structuredClone(painted);if(binding)unpainted.appearanceBindings.find((entry:Value)=>entry.kind==='root').tint=source.appearanceBindings.find((entry:Value)=>entry.kind==='root').tint;unpainted.color=source.color;
 assert.deepEqual(unpainted,source,'Paint changed more than the selected object color');
 await adapter.undo();await awaitRoomPersistence(()=>adapter.fact('room.persistence'));
 const undone=await saved(directory,String(opened.current.generationId));assertWorldPackObjectsEqual(undone.room.value.objects,installed.room.value.objects,'Undo changed saved objects');assert.equal((await undone.provenance()).sha256,provenance.sha256);
 evidence.edit={target,painted,undone:undone.room};await checkpoint('edited-and-undone');
 const previous=await adapter.fact('workspace.previous');assert.equal(previous.available,true);assert.equal(previous.generationId,object(opened.result.retained).generationId);assert.equal(previous.manifestHash,object(opened.result.retained).manifestHash);
 const selected=object(await adapter.action('workspace.previous.select',{expectedRevision:previous.revision,generationId:previous.generationId,manifestHash:previous.manifestHash}));
 const selectionRequestId=id(selected.requestId),selection=await wait('workspace.archive.selection',{requestId:selectionRequestId},['preparing'],['prepared']);
 const restored=await activate(selection,selectionRequestId,previous.revision);await awaitRoomPersistence(()=>adapter.fact('room.persistence'));
 const original=(await saved(directory,String(restored.current.generationId))).room.value;
 assert.equal(original.world.worldId,before.world.worldId);assertWorldPackObjectsEqual(original.objects,before.objects,'Restoring changed the original saved objects');
 const noPack=await adapter.fact('workspace.pack.instance',{offset:0});assert.equal(noPack.total,0,'Pack provenance leaked into original workspace');
 evidence.restored={...restored,room:original,review:await completeReview(restored.current)};evidence.complete=true;await checkpoint('restored-original');
}
