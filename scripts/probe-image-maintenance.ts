// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {lstat,mkdir,open,readdir,readFile,unlink,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {deflateSync} from 'node:zlib';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';

export type ImageMaintenanceReceipt=NonNullable<NonNullable<RoomAgentState['execution']>['selected']>;
export interface ImageMaintenanceDriver {
 invoke(id:string,args:Record<string,unknown>):Promise<ImageMaintenanceReceipt>;
 fact(id:string,args:Record<string,unknown>,version?:number):Promise<{available:boolean;value?:unknown}>;
 reject(id:string,args:Record<string,unknown>):Promise<RoomAgentState>;
 offer(image:{dataUrl:string;hash:string;name:string}):Promise<void>;
 replay?(receipt:ImageMaintenanceReceipt):Promise<ImageMaintenanceReceipt>;
 history?(receipt:ImageMaintenanceReceipt):Promise<ImageMaintenanceReceipt>;
 capture?(phase:string):Promise<void>;
}
/** A transport/read-set failure cannot prove that native image revision checks
 * ran. Pre-dispatch refusal may retain the preceding completed selection; a
 * failed receipt, when present, must belong to this exact removal attempt. */
export function assertStaleImageRemovalRejected(state:RoomAgentState,args:{imageHash:string;inspectionRevision:string}){
 const selected=state.execution?.selected;
 assert.ok(state.ok===false||selected?.phase==='failed','Stale removal was accepted');
 if(selected?.phase==='failed'){
  assert.equal(selected.capability,'image.library.edit','Stale rejection selected another capability');
  assert.equal(selected.version,1);
  assert.deepEqual(selected.call,{id:'image.library.edit',version:1,arguments:args},'Stale rejection selected another removal call');
 }
 const status=state.ok===false?state.status:selected!.status;
 assert.ok([
  'Inspect this image and load its current file revision before removing the download',
  'This image changed. Inspect it again before removing its download.',
  'This image changed before deletion. Its current files are preserved.',
 ].includes(status),'Expected a native image revision rejection; received: '+status);
}

type Entry={imageHash:string;inspectionRevision:string;name:string;payloadPresent:boolean;metadataPresent:boolean;contentValid:boolean;payloadBytes:number};
type Usage={current:boolean;history:boolean;saved:boolean;runtime:boolean;inUse:boolean;uncertain:boolean;canRemove:boolean;protection:string};
const digest=(bytes:Buffer)=>createHash('sha256').update(bytes).digest('hex');
const delay=()=>new Promise(resolve=>setTimeout(resolve,100));
async function absent(path:string){try{await lstat(path);return false;}catch(error){if((error as NodeJS.ErrnoException).code==='ENOENT')return true;throw error;}}
async function ordinaryDirectory(path:string){const entry=await lstat(path);assert.ok(entry.isDirectory()&&!entry.isSymbolicLink(),'Fixture directory must be ordinary: '+path);}
async function exclusive(path:string,bytes:Buffer){const file=await open(path,'wx');try{await file.writeFile(bytes);await file.sync();}finally{await file.close();}}

/** A unique, ordinary 8-bit RGBA PNG. Native SurfaceImage still validates its
 * actual bytes; this fixture constructor is not decoder or rendering evidence. */
export function maintenanceImage(ownerId:string,client:string){
 const colors=createHash('sha256').update('Maestro image maintenance '+ownerId+' '+client).digest();
 const pixels=Buffer.alloc(8*(1+8*4));for(let y=0;y<8;y++)for(let x=0;x<8;x++){
  const at=y*33+1+x*4;for(let channel=0;channel<3;channel++)pixels[at+channel]=colors[(y*8*3+x*3+channel)%colors.length];pixels[at+3]=255;
 }
 const chunk=(type:string,bytes:Buffer)=>{const body=Buffer.concat([Buffer.from(type),bytes]);let crc=0xffffffff;for(const value of body){crc^=value;for(let bit=0;bit<8;bit++)crc=(crc>>>1)^((crc&1)?0xedb88320:0);}const result=Buffer.alloc(bytes.length+12);result.writeUInt32BE(bytes.length);body.copy(result,4);result.writeUInt32BE((crc^0xffffffff)>>>0,result.length-4);return result;};
 const header=Buffer.alloc(13);header.writeUInt32BE(8,0);header.writeUInt32BE(8,4);header[8]=8;header[9]=6;
 const bytes=Buffer.concat([Buffer.from([137,80,78,71,13,10,26,10]),chunk('IHDR',header),chunk('IDAT',deflateSync(pixels)),chunk('IEND',Buffer.alloc(0))]);
 return {bytes,hash:digest(bytes),dataUrl:'data:image/png;base64,'+bytes.toString('base64'),name:'Maintenance orphan '+ownerId.slice(0,8)+' '+client+'.png'};
}
async function inventory(folder:string){
 const names=(await readdir(folder)).sort();assert.ok(names.length<=128,'Unexpected image fixture directory size');
 const result:Record<string,string>={};let bytes=0;
 for(const name of names){const path=join(folder,name),entry=await lstat(path);assert.ok(entry.isFile()&&!entry.isSymbolicLink(),'Unexpected non-file in image fixture');assert.ok(entry.size<=16*1024*1024);bytes+=entry.size;assert.ok(bytes<=130*1024*1024);result[name]=digest(await readFile(path));}
 return result;
}

/** The sole file seeding is an explicitly owned orphan in a fresh offline
 * workspace. Actual removal/reimport uses ordinary shared capabilities. Once a
 * native request begins, failures preserve the fixture and evidence rather
 * than guessing whether a worker still owns it or deleting accepted assets. */
export async function probeImageMaintenance(driver:ImageMaintenanceDriver,directory:string,client:'headless'|'book'){
 assert.ok(client==='headless'?driver.replay:driver.history,'The client must verify duplicate execution or visible historical receipt inspection');
 directory=resolve(directory);const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8')),ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert.equal(owner.version,1);assert.match(owner.id,/^[a-f0-9]{32}$/);assert.equal(ready.id,owner.id);
 const appRoot=join(directory,'workspace'),room=join(appRoot,'room'),folder=join(room,'images');
 assert.equal(resolve(ready.workspaceDirectory),appRoot);assert.ok(!ready.restartOf);
 assert.ok(!owner.providerUsed&&!owner.providerScenario&&!owner.resumedFrom&&!owner.nativeRestart,'Image maintenance requires a fresh offline workspace');
 for(const path of [directory,appRoot,room])await ordinaryDirectory(path);
 assert.equal(await absent(join(appRoot,'workspace-generations.v1','current.v1.json')),true,'Image maintenance requires the original room');
 await mkdir(folder,{recursive:true});await ordinaryDirectory(folder);
 const before=await inventory(folder),image=maintenanceImage(owner.id,client),payload=join(folder,image.hash+'.image'),metadata=join(folder,image.hash+'.txt'),nameBytes=Buffer.from(image.name+'\n');
 assert.equal(await absent(payload),true,'The orphan identity must not already exist');assert.equal(await absent(metadata),true,'The orphan metadata must not already exist');
 let payloadCreated=false,metadataCreated=false,dispatched=false;
 const receipts:ImageMaintenanceReceipt[]=[];
 const evidence:Record<string,unknown>={client,phase:'preparing',hash:image.hash,name:image.name,receipts,before,
  boundary:'Actual desktop Unity shared image inspect/review/removal and exact chat-image transfer/acceptance. A unique valid PNG orphan is explicitly seeded only in this fresh owned offline room. Book uses visible generated controls and historical receipt inspection; exact duplicate-start transport proof is Headless only. No user workspace, provider, headset, picker or rendered-pixel claim. After native dispatch, failures preserve evidence and accepted files.'};
 const checkpoint=async(phase:string)=>{evidence.phase=phase;await writeFile(join(directory,client+'-image-maintenance.json'),JSON.stringify(evidence,null,2));await driver.capture?.(phase);};
 const invoke=async(id:string,args:Record<string,unknown>)=>{dispatched=true;const receipt=await driver.invoke(id,args);assert.equal(receipt.capability,id);assert.equal(receipt.phase,'completed',receipt.status);assert.deepEqual(receipt.call.arguments,args);receipts.push(receipt);return receipt;};
 const fact=async<T>(id:string,args:Record<string,unknown>={},version=1)=>{const result=await driver.fact(id,args,version);assert.equal(result.available,true,id);return result.value as T;};
 const assertFiles=async(present:boolean)=>{assert.equal(await absent(payload),!present);if(present)assert.deepEqual(await readFile(payload),image.bytes);assert.deepEqual(await readFile(metadata),nameBytes);const current=await inventory(folder);delete current[image.hash+'.image'];delete current[image.hash+'.txt'];assert.deepEqual(current,before,'Maintenance changed an existing image file');};
 try {
  await exclusive(payload,image.bytes);payloadCreated=true;await exclusive(metadata,nameBytes);metadataCreated=true;await checkpoint('orphan-seeded');
  const inspection=await invoke('image.library.inspect',{imageHash:image.hash}),original=inspection.output!.entry as Entry,usage=inspection.output!.usage as Usage;
  assert.equal(original.imageHash,image.hash);assert.match(original.inspectionRevision,/^[a-f0-9]{64}$/);assert.equal(original.name,image.name);assert.equal(original.payloadPresent,true);assert.equal(original.metadataPresent,true);assert.equal(original.contentValid,true);assert.equal(original.payloadBytes,image.bytes.length);
  assert.deepEqual(usage,{current:false,history:false,saved:false,runtime:false,inUse:false,uncertain:false,canRemove:true,protection:''});
  assert.deepEqual(await fact<Entry>('image.library.review',{imageHash:image.hash}),original);await assertFiles(true);await checkpoint('unused-reviewed');
  const removal=await invoke('image.library.edit',{imageHash:image.hash,inspectionRevision:original.inspectionRevision});
  assert.equal(removal.output!.removed,true);assert.equal(removal.output!.previousRevision,original.inspectionRevision);const removed=removal.output!.entry as Entry;
  assert.equal(removed.imageHash,image.hash);assert.equal(removed.payloadPresent,false);assert.equal(removed.metadataPresent,true);assert.equal(removed.name,image.name);assert.equal(removed.contentValid,false);assert.equal(removed.payloadBytes,0);assert.notEqual(removed.inspectionRevision,original.inspectionRevision);
  assert.deepEqual(await fact<Entry>('image.library.review',{imageHash:image.hash}),removed);await assertFiles(false);await checkpoint('download-removed');
  const staleArgs={imageHash:image.hash,inspectionRevision:original.inspectionRevision},stale=await driver.reject('image.library.edit',staleArgs);
  assertStaleImageRemovalRejected(stale,staleArgs);evidence.stale=stale;await assertFiles(false);await checkpoint('stale-refused');
  await driver.offer(image);
  let offers=await fact<{offerSet:string;entries:{imageHash:string}[]}>('image.chat.library',{offset:0});const deadline=Date.now()+45000;
  while(!offers.entries.some(value=>value.imageHash===image.hash)&&Date.now()<deadline){await delay();offers=await fact('image.chat.library',{offset:0});}
  assert.ok(offers.entries.some(value=>value.imageHash===image.hash),'Synthetic original image did not enter the ordinary chat source');
  const selected=await invoke('image.import',{operation:'selectChat',offerSet:offers.offerSet,imageHash:image.hash});
  let preview=await fact<{requestId:string;phase:string;error:string;preview:{imageHash:string}}>('image.import.selection');
  while(preview.phase!=='preview'&&Date.now()<deadline){assert.ok(!['failed','cancelled'].includes(preview.phase),preview.error);await delay();preview=await fact('image.import.selection');}
  assert.equal(preview.phase,'preview');assert.equal(preview.requestId,selected.output!.requestId);assert.equal(preview.preview.imageHash,image.hash);evidence.preview=preview;
  const imported=await invoke('image.import',{operation:'accept',requestId:preview.requestId,imageHash:image.hash});
  assert.equal(imported.output!.imageHash,image.hash);assert.match(String(imported.output!.appearanceId),/^[a-f0-9]{32}$/);await assertFiles(true);await checkpoint('exact-reimport');
  const restored=await invoke('image.library.inspect',{imageHash:image.hash}),restoredEntry=restored.output!.entry as Entry;
  assert.deepEqual(restoredEntry,original,'Exact reimport must restore the same payload/name fingerprint');assert.equal((restored.output!.usage as Usage).current,true,'Acceptance creates an unbound appearance that protects the download');assert.equal((restored.output!.usage as Usage).canRemove,false);
  if(driver.replay){const repeated=await driver.replay(removal);assert.equal(repeated.id,removal.id);assert.equal(repeated.phase,'completed');assert.deepEqual(repeated.call,removal.call);assert.deepEqual(repeated.output,removal.output);evidence.duplicateStart=repeated;await assertFiles(true);}
  if(driver.history){const previous=await driver.history(removal);assert.equal(previous.id,removal.id);assert.equal(previous.phase,'completed');assert.deepEqual(previous.call,removal.call);assert.deepEqual(previous.output,removal.output);evidence.historicalRevisit=previous;await assertFiles(true);}
  const finalInspection=await invoke('image.library.inspect',{imageHash:image.hash});assert.deepEqual(finalInspection.output!.entry,original,'A fresh native file read must still see the reimport after historical execution');await assertFiles(true);
  assert.deepEqual(await fact<Entry>('image.library.review',{imageHash:image.hash}),restoredEntry);evidence.complete=true;await checkpoint('complete');
 } catch(error) {
  evidence.complete=false;evidence.error=error instanceof Error?error.message:String(error);await checkpoint('failed');throw error;
 } finally {
  if(!dispatched)for(const [path,bytes,created] of [[metadata,nameBytes,metadataCreated],[payload,image.bytes,payloadCreated]] as const){if(!created)continue;const entry=await lstat(path);assert.ok(entry.isFile()&&!entry.isSymbolicLink());assert.deepEqual(await readFile(path),bytes,'Only unchanged exclusively created fixture bytes may be cleaned up');await unlink(path);}
 }
}
