// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';

export type MotionReceipt=NonNullable<NonNullable<RoomAgentState['execution']>['selected']>;
export interface MotionMaintenanceDriver {
 fact(id:string,args?:Record<string,unknown>):Promise<{available:boolean;value?:unknown}>;
 invoke(id:string,args:Record<string,unknown>):Promise<MotionReceipt>;
 /** Refused before dispatch or a terminal failed receipt; never a pending action. */
 reject(id:string,args:Record<string,unknown>):Promise<RoomAgentState>;
 replay?(receipt:MotionReceipt):Promise<MotionReceipt>;
 capture?(phase:string):Promise<void>;
}
type Entry={motionId:string;entryRevision:string;payloadHash:string;exists:boolean;tagCount:number;favourite:boolean;archived:boolean;removed:boolean;downloaded:boolean;payloadPresent:boolean};
type Usage={total:number;history:boolean;saved:boolean;uncertain:boolean;playing:boolean;protection:string};
type Search={total:number;next:number;entries:{motionId:string;name:string;entryRevision:string}[]};
type Metadata={name:string;tags:string[];favourite:boolean};
type IncludedPackage={available:boolean;busy:boolean;package:{manifestHash:string};counts:{motions:number;catalogued:number;removed:number}};
const editId='motion.library.edit';

/** Observe only the already-started first-use copy. A missing/failed package is
 * evidence, never permission to install, retry or synthesize fixture content. */
export async function awaitIncludedMotionPackage(driver:Pick<MotionMaintenanceDriver,'fact'>,observe:(pack:IncludedPackage)=>Promise<void>,timeoutMs=120000):Promise<IncludedPackage>{
 assert.ok(Number.isFinite(timeoutMs)&&timeoutMs>0);const deadline=Date.now()+timeoutMs;let manifestHash:string|undefined;
 for(;;){
  const result=await driver.fact('motion.pack.included');assert.equal(result.available,true,'Included-package fact unavailable');
  const pack=result.value as IncludedPackage;await observe(pack);
  assert.equal(pack.available,true,'The native included animation package is unavailable');
  if(manifestHash===undefined)manifestHash=pack.package.manifestHash;else assert.equal(pack.package.manifestHash,manifestHash,'Included package identity changed while copying');
  if(!pack.busy)return pack;
  const remaining=deadline-Date.now();assert.ok(remaining>0,'The native included animation copy did not finish within the read-only readiness deadline');
  await new Promise(resolve=>setTimeout(resolve,Math.min(250,remaining)));
 }
}

/** Preserve every exact tag from one revision. Never merge pages after an edit. */
export async function readMotionMetadata(driver:Pick<MotionMaintenanceDriver,'fact'>,entry:Entry):Promise<Metadata>{
 assert.ok(Number.isInteger(entry.tagCount)&&entry.tagCount>=0&&entry.tagCount<=16,'Invalid animation tag count');
 const name=await driver.fact('motion.library.name',{motionId:entry.motionId});assert.equal(name.available,true);
 const named=name.value as {entryRevision:string;name:string};assert.equal(named.entryRevision,entry.entryRevision);assert.equal(typeof named.name,'string');
 const tags:string[]=[];let offset=0;
 for(let page=0;page<8;page++){
  const result=await driver.fact('motion.library.tags',{motionId:entry.motionId,entryRevision:entry.entryRevision,offset});assert.equal(result.available,true,'Animation tags changed while reading');
  const value=result.value as {entryRevision:string;total:number;next:number;tags:string[]};assert.equal(value.entryRevision,entry.entryRevision);assert.equal(value.total,entry.tagCount);
  assert.ok(Array.isArray(value.tags)&&value.tags.length<=2&&value.tags.every(tag=>typeof tag==='string'));
  tags.push(...value.tags);assert.ok(tags.length<=entry.tagCount);
  if(value.next===-1){assert.equal(tags.length,entry.tagCount);return {name:named.name,tags,favourite:entry.favourite};}
  assert.ok(value.tags.length>0);assert.equal(value.next,tags.length);assert.ok(value.next>offset&&value.next<entry.tagCount);offset=value.next;
 }
 throw new Error('Animation tags exceeded the eight-page contract');
}

/** Both clients exercise the actual native private library through public actions.
 * Only an unused bundled entry is reclaimed. The exact offline package restores
 * its payload and stable packaged identity; this is not ordinary file reimport.
 * A failure leaves evidence and current native state for inspection, never an
 * automatic retry or cleanup mutation after an uncertain outcome. */
export async function probeMotionMaintenance(driver:MotionMaintenanceDriver,directory:string,client:'headless'|'book'){
 const evidence:Record<string,unknown>={client,boundary:'Actual desktop Unity motion library via ordinary client actions. Offline included package only; no provider, headset, playback or arbitrary imported-source recovery proof.'};
 const receipts:MotionReceipt[]=[];evidence.receipts=receipts;
 const checkpoint=async(phase:string)=>{evidence.phase=phase;await writeFile(join(directory,client+'-motion-maintenance.json'),JSON.stringify(evidence,null,2));await driver.capture?.(phase);};
 const fact=async<T>(id:string,args?:Record<string,unknown>):Promise<T>=>{const result=await driver.fact(id,args);assert.equal(result.available,true,id);assert.ok(result.value&&typeof result.value==='object');return result.value as T;};
 const invoke=async(id:string,args:Record<string,unknown>)=>{const receipt=await driver.invoke(id,args);assert.equal(receipt.capability,id);assert.equal(receipt.phase,'completed',receipt.status);assert.deepEqual(receipt.call.arguments,args);receipts.push(receipt);return receipt;};
 const current=(motionId:string)=>fact<Entry>('motion.library.entry',{motionId});
 const inspect=async(entry:Entry)=>{const receipt=await invoke('motion.library.inspect',{motionId:entry.motionId,entryRevision:entry.entryRevision});const output=receipt.output as unknown as {entry:Entry;usage:Usage};assert.deepEqual(output.entry,entry);return output.usage;};
 const search=(query:string,archived=false,offset=0)=>fact<Search>('motion.library.search',{query,archived,offset});
 const edit=async(entry:Entry,operation:string,details:Partial<Metadata>={})=>{
  const receipt=await invoke(editId,{operation,motionId:entry.motionId,entryRevision:entry.entryRevision,...details});
  const output=receipt.output as unknown as {operation:string;previousRevision:string;entry:Entry};assert.equal(output.operation,operation);assert.equal(output.previousRevision,entry.entryRevision);assert.equal(output.entry.motionId,entry.motionId);
  if(operation!=='forgetMotion'){assert.equal(output.entry.payloadHash,entry.payloadHash);assert.deepEqual(await current(entry.motionId),output.entry);}
  else {assert.equal(output.entry.exists,false);assert.equal(output.entry.entryRevision,'');assert.equal(output.entry.payloadHash,'');}
  return {entry:output.entry,receipt};
 };
 const rejected=async(args:Record<string,unknown>,pattern:RegExp)=>{
  const state=await driver.reject(editId,args),receipt=state.execution?.selected;
  assert.ok(!state.ok||receipt?.phase==='failed','Expected an actual refusal or terminal failed action');
  assert.match(state.ok?receipt!.status:state.status,pattern);return state;
 };
 const manifestText=await readFile(new URL('../unity/MaestroQuest/Assets/Maestro/Resources/Avatars/IncludedMotions.json',import.meta.url),'utf8');
 const manifest=JSON.parse(manifestText) as {catalogue:{entries:{id:string;hash:string}[]}};
 const initializationSamples:IncludedPackage[]=[];evidence.initializationSamples=initializationSamples;
 const pack=await awaitIncludedMotionPackage(driver,async sample=>{initializationSamples.push(sample);evidence.initialPack??=sample;await checkpoint('package-observed');});
 assert.equal(pack.available,true);assert.equal(pack.busy,false);assert.equal(pack.package.manifestHash,createHash('sha256').update(manifestText).digest('hex'),'Probe source and native included package differ');
 assert.equal(pack.counts.catalogued,pack.counts.motions,'This round trip requires the complete preinstalled native fixture');assert.equal(pack.counts.removed,0);
 const bundled=new Map(manifest.catalogue.entries.map(entry=>[entry.id,entry.hash]));
 let original:Entry|undefined,originalUsage:Usage|undefined,offset=0;
 const candidates:unknown[]=[];evidence.candidates=candidates;
 for(let page=0;page<12;page++){
  const found=await search('',false,offset);assert.ok(found.entries.length<=1,'Search must stay bounded to one exact entry');
  const result=found.entries[0];if(!result)break;
  const entry=await current(result.motionId);assert.equal(entry.entryRevision,result.entryRevision);
  if(entry.downloaded&&!entry.archived&&!entry.removed&&bundled.get(entry.motionId)===entry.payloadHash){
   const usage=await inspect(entry);candidates.push({entry,usage});
   if(usage.total===0&&!usage.history&&!usage.saved&&!usage.uncertain&&!usage.playing&&!usage.protection){original=entry;originalUsage=usage;break;}
  }
  if(found.next===-1)break;assert.equal(found.next,offset+1);offset=found.next;
 }
 assert.ok(original,'No unreferenced included motion found within twelve candidates');
 const metadata=await readMotionMetadata(driver,original);Object.assign(evidence,{pack,original,metadata});await checkpoint('selected');
 const replacement:Metadata={name:'Motion maintenance '+client+' '+original.motionId.slice(0,8),tags:['probe-'+client,'quoted "tag"','back\\slash'],favourite:!metadata.favourite};
 const saved=await edit(original,'save',replacement);let entry=saved.entry;assert.notEqual(entry.entryRevision,original.entryRevision);assert.deepEqual(await readMotionMetadata(driver,entry),replacement);
 const staleTags=await driver.fact('motion.library.tags',{motionId:entry.motionId,entryRevision:original.entryRevision,offset:0});assert.equal(staleTags.available,false);
 evidence.stale=await rejected({operation:'save',motionId:entry.motionId,entryRevision:original.entryRevision,...metadata},/changed|missing/i);
 assert.deepEqual(await current(entry.motionId),entry);assert.deepEqual(await readMotionMetadata(driver,entry),replacement);await checkpoint('saved-and-stale-refused');
 entry=(await edit(entry,'archive')).entry;assert.equal(entry.archived,true);assert.equal(entry.downloaded,true);
 assert.equal((await search(replacement.name)).total,0);const archived=await search(replacement.name,true);assert.equal(archived.total,1);assert.deepEqual(archived.entries,[{motionId:entry.motionId,name:replacement.name,entryRevision:entry.entryRevision}]);
 entry=(await edit(entry,'restore')).entry;assert.equal(entry.archived,false);assert.equal((await search(replacement.name)).total,1);
 // Replay an older completed save after a newer metadata save; the historical
 // receipt must return its original output without restoring the old draft.
 const later={...replacement,name:replacement.name+' newer'};entry=(await edit(entry,'save',later)).entry;
 if(driver.replay){const replayed=await driver.replay(saved.receipt);assert.equal(replayed.id,saved.receipt.id);assert.equal(replayed.phase,'completed');assert.deepEqual(replayed.output,saved.receipt.output);assert.deepEqual(await current(entry.motionId),entry);assert.deepEqual(await readMotionMetadata(driver,entry),later);evidence.replay=replayed;}
 entry=(await edit(entry,'archive')).entry;const beforeRemoval=await inspect(entry);assert.equal(beforeRemoval.protection,'');
 entry=(await edit(entry,'removeDownload')).entry;assert.equal(entry.removed,true);assert.equal(entry.archived,true);assert.equal(entry.downloaded,false);assert.equal(entry.payloadPresent,false);await checkpoint('download-removed');
 const restored=await invoke('motion.pack.install',{operation:'restore',manifestHash:pack.package.manifestHash,motionId:entry.motionId});assert.equal(restored.output?.restored,1);
 entry=await current(entry.motionId);assert.equal(entry.removed,false);assert.equal(entry.archived,true);assert.equal(entry.downloaded,true);assert.equal(entry.payloadPresent,true);assert.deepEqual(await readMotionMetadata(driver,entry),later);
 entry=(await edit(entry,'removeDownload')).entry;
 const forgotten=await edit(entry,'forgetMotion');assert.equal(forgotten.entry.payloadPresent,false);assert.equal((await driver.fact('motion.library.entry',{motionId:entry.motionId})).available,false);assert.equal((await search(later.name,true)).total,0);await checkpoint('identity-forgotten');
 const added=await invoke('motion.pack.install',{operation:'add',manifestHash:pack.package.manifestHash});assert.equal(added.output?.added,1,'Cleanup must add only the one deliberately forgotten packaged identity');
 entry=await current(original.motionId);assert.equal(entry.payloadHash,original.payloadHash);assert.equal(entry.downloaded,true);assert.equal(entry.payloadPresent,true);
 entry=(await edit(entry,'save',metadata)).entry;assert.deepEqual(await readMotionMetadata(driver,entry),metadata);
 assert.deepEqual(entry,original,'The packaged entry must end with its exact original revision, metadata and payload state');
 assert.deepEqual(await inspect(entry),originalUsage,'Maintenance must leave the selected motion unassigned and unpinned');
 const afterPack=await fact<typeof pack>('motion.pack.included');assert.deepEqual(afterPack.counts,pack.counts);
 Object.assign(evidence,{restored:entry,afterPack});await checkpoint('complete');
}
