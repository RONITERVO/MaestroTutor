// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {lstat,mkdir,open,readdir,readFile,unlink,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';

export type AudioMaintenanceReceipt=NonNullable<NonNullable<RoomAgentState['execution']>['selected']>;
export interface AudioMaintenanceDriver {
 invoke(id:string,args:Record<string,unknown>):Promise<AudioMaintenanceReceipt>;
 fact(id:string,args:Record<string,unknown>,version?:number):Promise<{available:boolean;value?:unknown}>;
 reject(id:string,args:Record<string,unknown>):Promise<RoomAgentState>;
 offer(audio:{dataUrl:string;hash:string;name:string}):Promise<void>;
 replay?(receipt:AudioMaintenanceReceipt):Promise<AudioMaintenanceReceipt>;
 history?(receipt:AudioMaintenanceReceipt):Promise<AudioMaintenanceReceipt>;
 capture?(phase:string):Promise<void>;
}
/** A transport/read-set failure cannot prove that native sound revision checks
 * ran. Pre-dispatch refusal may retain the preceding completed selection; a
 * failed receipt, when present, must belong to this exact removal attempt. */
const soundInterruptionNotice='Sound maintenance stopped waiting. Before dispatch it is cancelled; an already dispatched deletion may still finish. Inspect the current file and this receipt before another explicit request.';
function assertRemovalRejected(state:RoomAgentState,args:{assetHash:string;inspectionRevision:string},statuses:string[],expected:string){
 const selected=state.execution?.selected;
 assert.ok(state.ok===false||selected?.phase==='failed','Stale removal was accepted');
 if(selected?.phase==='failed'){
  assert.equal(selected.capability,'audio.library.edit','Stale rejection selected another capability');
  assert.equal(selected.version,1);
  assert.deepEqual(selected.call,{id:'audio.library.edit',version:1,arguments:args},'Stale rejection selected another removal call');
 }
 const status=state.ok===false?state.status:selected!.status;
 // Native failed-action cleanup appends this exact notice with ". ", even
 // when the cause already ends in a period. Decoration is evidence only on
 // the matching failed receipt, never a transport-level string by itself.
 const decorated=selected?.phase==='failed'&&status===selected.status&&statuses.some(reason=>status===reason+'. '+soundInterruptionNotice);
 assert.ok(statuses.includes(status)||decorated,expected+'; received: '+status);
}
export function assertStaleAudioRemovalRejected(state:RoomAgentState,args:{assetHash:string;inspectionRevision:string}){
 assertRemovalRejected(state,args,[
  'Inspect this sound and load its current file revision before removing the download',
  'This sound changed. Inspect it again before removing its download.',
  'This sound changed before deletion. Its current files are preserved.',
 ],'Expected a native sound revision rejection');
}
export function assertRetainedAudioRemovalRejected(state:RoomAgentState,args:{assetHash:string;inspectionRevision:string}){
 assertRemovalRejected(state,args,['A current or saved clip source still uses this sound, including unbound definitions.'],'Expected native clip-source retention refusal');
}

type Entry={assetHash:string;inspectionRevision:string;name:string;payloadPresent:boolean;metadataPresent:boolean;contentValid:boolean;payloadBytes:number};
type Usage={current:boolean;history:boolean;saved:boolean;runtime:boolean;inUse:boolean;uncertain:boolean;canRemove:boolean;protection:string};
const digest=(bytes:Buffer)=>createHash('sha256').update(bytes).digest('hex');
const delay=()=>new Promise(resolve=>setTimeout(resolve,100));
async function absent(path:string){try{await lstat(path);return false;}catch(error){if((error as NodeJS.ErrnoException).code==='ENOENT')return true;throw error;}}
async function ordinaryDirectory(path:string){const entry=await lstat(path);assert.ok(entry.isDirectory()&&!entry.isSymbolicLink(),'Fixture directory must be ordinary: '+path);}
async function exclusive(path:string,bytes:Buffer){const file=await open(path,'wx');try{await file.writeFile(bytes);await file.sync();}finally{await file.close();}}

/** Unique canonical PCM WAV used only as an owned offline orphan fixture.
 * Native WaveAudio still validates it; no playback or decoder claim is implied. */
export function maintenanceAudio(ownerId:string,client:string){
 const seed=createHash('sha256').update('Maestro audio maintenance '+ownerId+' '+client).digest(),frames=4800,bytes=Buffer.alloc(44+frames*2);
 bytes.write('RIFF');bytes.writeUInt32LE(bytes.length-8,4);bytes.write('WAVEfmt ',8);bytes.writeUInt32LE(16,16);bytes.writeUInt16LE(1,20);bytes.writeUInt16LE(1,22);bytes.writeUInt32LE(24000,24);bytes.writeUInt32LE(48000,28);bytes.writeUInt16LE(2,32);bytes.writeUInt16LE(16,34);bytes.write('data',36);bytes.writeUInt32LE(frames*2,40);
 for(let frame=0;frame<frames;frame++)bytes.writeInt16LE((seed[frame%seed.length]-128)*16,44+frame*2);
 return {bytes,hash:digest(bytes),dataUrl:'data:audio/wav;base64,'+bytes.toString('base64'),name:'Maintenance orphan '+ownerId.slice(0,8)+' '+client+'.wav'};
}
async function inventory(folder:string){
 const names=(await readdir(folder)).sort();assert.ok(names.length<=128,'Unexpected audio fixture directory size');
 const result:Record<string,string>={};let bytes=0;
 for(const name of names){const path=join(folder,name),entry=await lstat(path);assert.ok(entry.isFile()&&!entry.isSymbolicLink(),'Unexpected non-file in audio fixture');assert.ok(entry.size<=32*1024*1024);bytes+=entry.size;assert.ok(bytes<=130*1024*1024);result[name]=digest(await readFile(path));}
 return result;
}

/** The sole file seeding is an explicitly owned orphan in a fresh offline
 * workspace. Actual removal/reimport uses ordinary shared capabilities. Once a
 * native request begins, failures preserve the fixture and evidence rather
 * than guessing whether a worker still owns it or deleting accepted assets. */
export async function probeAudioMaintenance(driver:AudioMaintenanceDriver,directory:string,client:'headless'|'book'){
 assert.ok(client==='headless'?driver.replay:driver.history,'The client must verify duplicate execution or visible historical receipt inspection');
 directory=resolve(directory);const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8')),ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert.equal(owner.version,1);assert.match(owner.id,/^[a-f0-9]{32}$/);assert.equal(ready.id,owner.id);
 const appRoot=join(directory,'workspace'),room=join(appRoot,'room'),folder=join(room,'audio');
 assert.equal(resolve(ready.workspaceDirectory),appRoot);assert.ok(!ready.restartOf);
 assert.ok(!owner.providerUsed&&!owner.providerScenario&&!owner.resumedFrom&&!owner.nativeRestart,'Audio maintenance requires a fresh offline workspace');
 for(const path of [directory,appRoot,room])await ordinaryDirectory(path);
 assert.equal(await absent(join(appRoot,'workspace-generations.v1','current.v1.json')),true,'Audio maintenance requires the original room');
 await mkdir(folder,{recursive:true});await ordinaryDirectory(folder);
 const before=await inventory(folder),audio=maintenanceAudio(owner.id,client),payload=join(folder,audio.hash+'.wav'),metadata=join(folder,audio.hash+'.txt'),nameBytes=Buffer.from(audio.name+'\n');
 assert.equal(await absent(payload),true,'The orphan identity must not already exist');assert.equal(await absent(metadata),true,'The orphan metadata must not already exist');
 let payloadCreated=false,metadataCreated=false,dispatched=false;
 const receipts:AudioMaintenanceReceipt[]=[];
 const evidence:Record<string,unknown>={client,phase:'preparing',hash:audio.hash,name:audio.name,receipts,before,
  boundary:'Actual desktop Unity shared audio inspect/review/removal and exact chat-audio transfer/acceptance, without starting playback. A unique canonical WAV orphan is explicitly seeded only in this fresh owned offline room. Book uses visible generated controls and historical receipt inspection; exact duplicate-start transport proof is Headless only. No user workspace, provider, headset, picker or audible-output claim. After native dispatch, failures preserve evidence and accepted files.'};
 const checkpoint=async(phase:string)=>{evidence.phase=phase;await writeFile(join(directory,client+'-audio-maintenance.json'),JSON.stringify(evidence,null,2));await driver.capture?.(phase);};
 const invoke=async(id:string,args:Record<string,unknown>)=>{dispatched=true;const receipt=await driver.invoke(id,args);assert.equal(receipt.capability,id);assert.equal(receipt.phase,'completed',receipt.status);assert.deepEqual(receipt.call.arguments,args);receipts.push(receipt);return receipt;};
 const fact=async<T>(id:string,args:Record<string,unknown>={},version=1)=>{const result=await driver.fact(id,args,version);assert.equal(result.available,true,id);return result.value as T;};
 const assertFiles=async(present:boolean)=>{assert.equal(await absent(payload),!present);if(present)assert.deepEqual(await readFile(payload),audio.bytes);assert.deepEqual(await readFile(metadata),nameBytes);const current=await inventory(folder);delete current[audio.hash+'.wav'];delete current[audio.hash+'.txt'];assert.deepEqual(current,before,'Maintenance changed an existing audio file');};
 try {
  await exclusive(payload,audio.bytes);payloadCreated=true;await exclusive(metadata,nameBytes);metadataCreated=true;await checkpoint('orphan-seeded');
  const inspection=await invoke('audio.library.inspect',{assetHash:audio.hash}),original=inspection.output!.entry as Entry,usage=inspection.output!.usage as Usage;
  assert.equal(original.assetHash,audio.hash);assert.match(original.inspectionRevision,/^[a-f0-9]{64}$/);assert.equal(original.name,audio.name);assert.equal(original.payloadPresent,true);assert.equal(original.metadataPresent,true);assert.equal(original.contentValid,true);assert.equal(original.payloadBytes,audio.bytes.length);
  assert.deepEqual(usage,{current:false,history:false,saved:false,runtime:false,inUse:false,uncertain:false,canRemove:true,protection:''});
  const soundsBefore=await fact<{entries:{instance:string}[]}>('audio.instances');evidence.soundsBefore=soundsBefore;
  assert.deepEqual(await fact<Entry>('audio.library.review',{assetHash:audio.hash}),original);await assertFiles(true);await checkpoint('unused-reviewed');
  const removal=await invoke('audio.library.edit',{assetHash:audio.hash,inspectionRevision:original.inspectionRevision});
  assert.equal(removal.output!.removed,true);assert.equal(removal.output!.previousRevision,original.inspectionRevision);const removed=removal.output!.entry as Entry;
  assert.equal(removed.assetHash,audio.hash);assert.equal(removed.payloadPresent,false);assert.equal(removed.metadataPresent,true);assert.equal(removed.name,audio.name);assert.equal(removed.contentValid,false);assert.equal(removed.payloadBytes,0);assert.notEqual(removed.inspectionRevision,original.inspectionRevision);
  assert.deepEqual(await fact<Entry>('audio.library.review',{assetHash:audio.hash}),removed);await assertFiles(false);await checkpoint('download-removed');
  const staleArgs={assetHash:audio.hash,inspectionRevision:original.inspectionRevision},stale=await driver.reject('audio.library.edit',staleArgs);
  assertStaleAudioRemovalRejected(stale,staleArgs);evidence.stale=stale;await assertFiles(false);await checkpoint('stale-refused');
  await driver.offer(audio);
  let offers=await fact<{offerSet:string;entries:{assetHash:string}[]}>('audio.chat.library',{offset:0});const deadline=Date.now()+45000;
  while(!offers.entries.some(value=>value.assetHash===audio.hash)&&Date.now()<deadline){await delay();offers=await fact('audio.chat.library',{offset:0});}
  assert.ok(offers.entries.some(value=>value.assetHash===audio.hash),'Synthetic original audio did not enter the ordinary chat source');
  const selected=await invoke('audio.import',{operation:'selectChat',offerSet:offers.offerSet,assetHash:audio.hash});
  let preview=await fact<{requestId:string;phase:string;error:string;preview:{assetHash:string}}>('audio.import.selection');
  while(preview.phase!=='preview'&&Date.now()<deadline){assert.ok(!['failed','cancelled'].includes(preview.phase),preview.error);await delay();preview=await fact('audio.import.selection');}
  assert.equal(preview.phase,'preview');assert.equal(preview.requestId,selected.output!.requestId);assert.equal(preview.preview.assetHash,audio.hash);evidence.preview=preview;
  const imported=await invoke('audio.import',{operation:'accept',requestId:preview.requestId,assetHash:audio.hash});
  assert.equal(imported.output!.assetHash,audio.hash);assert.match(String(imported.output!.sourceId),/^[a-f0-9]{32}$/);await assertFiles(true);await checkpoint('exact-reimport');
  const definition=await fact<{definition:{clip:{assetHash:string}}}>('audio.source.definition',{id:imported.output!.sourceId},2);
  assert.equal(definition.definition.clip.assetHash,audio.hash);evidence.acceptedSource=definition;
  const soundsAfter=await fact<{entries:{instance:string}[]}>('audio.instances');
  assert.ok(soundsAfter.entries.every(sound=>soundsBefore.entries.some(previous=>previous.instance===sound.instance)),'Maintenance or import unexpectedly started room playback');evidence.soundsAfter=soundsAfter;
  const restored=await invoke('audio.library.inspect',{assetHash:audio.hash}),restoredEntry=restored.output!.entry as Entry;
  assert.deepEqual(restoredEntry,original,'Exact reimport must restore the same payload/name fingerprint');assert.equal((restored.output!.usage as Usage).current,true,'Acceptance creates an unbound clip source that protects the download');assert.equal((restored.output!.usage as Usage).canRemove,false);
  const retainedArgs={assetHash:audio.hash,inspectionRevision:restoredEntry.inspectionRevision},protectedResult=await driver.reject('audio.library.edit',retainedArgs);
  assertRetainedAudioRemovalRejected(protectedResult,retainedArgs);evidence.retained=protectedResult;await assertFiles(true);await checkpoint('unbound-source-protected');
  if(driver.replay){const repeated=await driver.replay(removal);assert.equal(repeated.id,removal.id);assert.equal(repeated.phase,'completed');assert.deepEqual(repeated.call,removal.call);assert.deepEqual(repeated.output,removal.output);evidence.duplicateStart=repeated;await assertFiles(true);}
  if(driver.history){const previous=await driver.history(removal);assert.equal(previous.id,removal.id);assert.equal(previous.phase,'completed');assert.deepEqual(previous.call,removal.call);assert.deepEqual(previous.output,removal.output);evidence.historicalRevisit=previous;await assertFiles(true);}
  const finalInspection=await invoke('audio.library.inspect',{assetHash:audio.hash});assert.deepEqual(finalInspection.output!.entry,original,'A fresh native file read must still see the reimport after historical execution');await assertFiles(true);
  assert.deepEqual(await fact<Entry>('audio.library.review',{assetHash:audio.hash}),restoredEntry);evidence.complete=true;await checkpoint('complete');
 } catch(error) {
  evidence.complete=false;evidence.error=error instanceof Error?error.message:String(error);await checkpoint('failed');throw error;
 } finally {
  if(!dispatched)for(const [path,bytes,created] of [[metadata,nameBytes,metadataCreated],[payload,audio.bytes,payloadCreated]] as const){if(!created)continue;const entry=await lstat(path);assert.ok(entry.isFile()&&!entry.isSymbolicLink());assert.deepEqual(await readFile(path),bytes,'Only unchanged exclusively created fixture bytes may be cleaned up');await unlink(path);}
 }
}
