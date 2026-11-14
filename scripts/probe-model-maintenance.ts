// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {lstat,mkdir,open,readdir,readFile,unlink,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';

export type ModelMaintenanceReceipt=NonNullable<NonNullable<RoomAgentState['execution']>['selected']>;
export interface ModelMaintenanceDriver {
 invoke(id:string,args:Record<string,unknown>):Promise<ModelMaintenanceReceipt>;
 fact(id:string,args:Record<string,unknown>,version?:number):Promise<{available:boolean;value?:unknown}>;
 reject(id:string,args:Record<string,unknown>):Promise<RoomAgentState>;
 replay?(receipt:ModelMaintenanceReceipt):Promise<ModelMaintenanceReceipt>;
 history?(receipt:ModelMaintenanceReceipt):Promise<ModelMaintenanceReceipt>;
 capture?(phase:string):Promise<void>;
}
type Entry={modelHash:string;inspectionRevision:string;name:string;payloadPresent:boolean;metadataPresent:boolean;contentValid:boolean;included:boolean;payloadBytes:number};
type Usage={current:boolean;history:boolean;saved:boolean;runtime:boolean;inUse:boolean;uncertain:boolean;canRemove:boolean;protection:string};
type RemovalArguments={modelHash:string;inspectionRevision:string};
const interruption='Model maintenance stopped waiting. Before dispatch it is cancelled; an already dispatched deletion may still finish. Inspect the current file and this receipt before another explicit request.';
const causes={
 stale:['Inspect this model and load its current file revision before removing the download','This model changed. Inspect it again before removing its download.','This model changed before deletion. Its current files are preserved.'],
 retained:['A current, dormant or temporary room, avatar profile or model-specific setting still uses this model.','The asset is in use. Release its live users and wait for pending work to finish.'],
 included:['The included Maestro model is protected; its private copy can be recreated on use'],
};
/** Neither a read-set failure nor an unrelated selected receipt is a native
 * removal refusal. Accept only the exact cause and matching attempted call. */
export function assertModelRemovalRejected(state:RoomAgentState,args:RemovalArguments,kind:keyof typeof causes){
 const selected=state.execution?.selected;
 assert.ok(state.ok===false||selected?.phase==='failed','Model removal was accepted');
 if(selected?.phase==='failed'){
  assert.equal(selected.capability,'model.download.edit');assert.equal(selected.version,1);
  assert.deepEqual(selected.call,{id:'model.download.edit',version:1,arguments:args});
 }
 const status=state.ok===false?state.status:selected!.status;
 const decorated=selected?.phase==='failed'&&status===selected.status&&causes[kind].some(cause=>status===cause+'. '+interruption);
 assert.ok(causes[kind].includes(status)||decorated,'Expected native model '+kind+' refusal; received: '+status);
}
const digest=(bytes:Buffer)=>createHash('sha256').update(bytes).digest('hex');
const delay=()=>new Promise(resolve=>setTimeout(resolve,100));
async function absent(path:string){try{await lstat(path);return false;}catch(error){if((error as NodeJS.ErrnoException).code==='ENOENT')return true;throw error;}}
async function ordinaryDirectory(path:string){const entry=await lstat(path);assert.ok(entry.isDirectory()&&!entry.isSymbolicLink(),'Fixture directory must be ordinary: '+path);}
async function exclusive(path:string,bytes:Buffer){const file=await open(path,'wx');try{await file.writeFile(bytes);await file.sync();}finally{await file.close();}}

/** Original valid glTF triangle; owner/client extras make its exact content
 * identity unique. The actual native inspector and importer must still accept it. */
export function maintenanceModel(ownerId:string,client:string){
 const binary=Buffer.alloc(72);[-.1,0,0,.1,0,0,0,.2,0,0,0,1,0,0,1,0,0,1].forEach((n,i)=>binary.writeFloatLE(n,i*4));
 const gltf={asset:{version:'2.0',copyright:'Original Maestro offline test fixture; Apache-2.0'},extras:{ownerId,client},scene:0,scenes:[{nodes:[0]}],nodes:[{name:'Maintenance triangle',mesh:0}],meshes:[{primitives:[{attributes:{POSITION:0,NORMAL:1},material:0}]}],materials:[{pbrMetallicRoughness:{baseColorFactor:[.2,.6,.8,1],metallicFactor:0,roughnessFactor:1}}],buffers:[{byteLength:binary.length}],bufferViews:[{buffer:0,byteOffset:0,byteLength:36},{buffer:0,byteOffset:36,byteLength:36}],accessors:[{bufferView:0,componentType:5126,count:3,type:'VEC3',min:[-.1,0,0],max:[.1,.2,0]},{bufferView:1,componentType:5126,count:3,type:'VEC3'}]};
 let json=Buffer.from(JSON.stringify(gltf));json=Buffer.concat([json,Buffer.alloc((4-json.length%4)%4,32)]);
 const header=Buffer.alloc(20),binaryHeader=Buffer.alloc(8);header.writeUInt32LE(0x46546c67);header.writeUInt32LE(2,4);header.writeUInt32LE(28+json.length+binary.length,8);header.writeUInt32LE(json.length,12);header.writeUInt32LE(0x4e4f534a,16);binaryHeader.writeUInt32LE(binary.length);binaryHeader.writeUInt32LE(0x004e4942,4);
 const bytes=Buffer.concat([header,json,binaryHeader,binary]),hash=digest(bytes),name='Maintenance model '+ownerId.slice(0,8)+' '+client+'.glb';
 // Exceeds the media-name limit on purpose; complete model attribution is retained.
 const metadata=Buffer.from(name+'\nSHA256: '+hash+'\n\n'+('Original offline fixture attribution; preserve this complete sidecar.\n').repeat(96));
 assert.ok(bytes.length<65536&&metadata.length>1024&&metadata.length<=128*1024);
 return {bytes,hash,name,metadata};
}
async function inventory(folder:string){
 const names=(await readdir(folder)).sort();assert.ok(names.length<=128,'Unexpected model fixture directory size');
 const result:Record<string,string>={};let bytes=0;
 for(const name of names){const path=join(folder,name),entry=await lstat(path);assert.ok(entry.isFile()&&!entry.isSymbolicLink(),'Unexpected non-file in model fixture');assert.ok(entry.size<=64*1024*1024);bytes+=entry.size;assert.ok(bytes<=260*1024*1024);result[name]=digest(await readFile(path));}
 return result;
}

/** The orphan and original export are exclusively owned offline fixtures. The
 * editor-only picker substitutes just Android's document chooser; select,
 * preparation, preview, import acceptance, removal and receipts are real native
 * paths. No direct restoration of the library payload is permitted by the driver. */
export async function probeModelMaintenance(driver:ModelMaintenanceDriver,directory:string,client:'headless'|'book'){
 assert.ok(client==='headless'?driver.replay:driver.history,'The client must verify duplicate execution or visible history');
 directory=resolve(directory);const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8')),ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert.equal(owner.version,1);assert.match(owner.id,/^[a-f0-9]{32}$/);assert.equal(ready.version,1);assert.equal(ready.id,owner.id);
 const appRoot=join(directory,'workspace'),room=join(appRoot,'room'),folder=join(room,'models');
 assert.equal(resolve(ready.workspaceDirectory),appRoot);assert.ok(!ready.restartOf);
 assert.equal(owner.providerUsed,false,'Model maintenance requires explicit offline ownership');assert.ok(!owner.providerScenario&&!owner.resumedFrom&&!owner.restartOf,'Model maintenance requires a fresh offline workspace');
 for(const mode of ['nativeRestart','actionInterruption','motionOnly','transportOnly','liveContinuity','firstLearner','identityOnly','steeringOnly'])assert.equal(owner[mode],false,'Model maintenance requires an ordinary offline journey: '+mode);
 assert.equal(typeof owner.sparseObjectView,'boolean','Sparse observation must be explicit in the offline owner');
 for(const path of [directory,appRoot,room])await ordinaryDirectory(path);
 assert.equal(await absent(join(appRoot,'workspace-generations.v1','current.v1.json')),true,'Model maintenance requires the original room');
 await mkdir(folder,{recursive:true});await ordinaryDirectory(folder);
 const before=await inventory(folder),model=maintenanceModel(owner.id,client),payload=join(folder,model.hash+'.glb'),metadata=join(folder,model.hash+'.txt');
 const source=join(directory,'model-maintenance-source.glb'),marker=join(directory,'model-maintenance-source.json');
 const markerBytes=Buffer.from(JSON.stringify({version:1,ownerId:owner.id,modelHash:model.hash,name:model.name}));
 const seeds:[string,Buffer][]=[[payload,model.bytes],[metadata,model.metadata],[source,model.bytes],[marker,markerBytes]],created:[string,Buffer][]=[];
 for(const [path] of seeds)assert.equal(await absent(path),true,'The model fixture identity/source must not already exist');
 assert.equal(await absent(join(directory,'model-maintenance-picker.json')),true,'Picker evidence must be fresh');
 let dispatched=false;const receipts:ModelMaintenanceReceipt[]=[];
 const evidence:Record<string,unknown>={client,sparseObjectView:owner.sparseObjectView,phase:'preparing',hash:model.hash,name:model.name,metadataBytes:model.metadata.length,metadataSha256:digest(model.metadata),receipts,before,
  boundary:'Actual desktop Unity shared model inspection/removal, model.import selection/native preview/object acceptance and immutable receipts. The exclusively generated GLB is offered by an editor-only IModelPicker substitute, not the Android document chooser. Full attribution metadata is retained. Headless duplicate-start and Book visible history are distinct. No user assets, provider, headset, OS picker or physical rendering acceptance. After dispatch, failures preserve all fixtures/evidence.'};
 const checkpoint=async(phase:string)=>{evidence.phase=phase;await writeFile(join(directory,client+'-model-maintenance.json'),JSON.stringify(evidence,null,2));await driver.capture?.(phase);};
 const invoke=async(id:string,args:Record<string,unknown>)=>{dispatched=true;const receipt=await driver.invoke(id,args);assert.equal(receipt.capability,id);assert.equal(receipt.phase,'completed',receipt.status);assert.deepEqual(receipt.call.arguments,args);receipts.push(receipt);return receipt;};
 const fact=async<T>(id:string,args:Record<string,unknown>={},version=1)=>{const result=await driver.fact(id,args,version);assert.equal(result.available,true,id);return result.value as T;};
 const assertFiles=async(present:boolean)=>{assert.equal(await absent(payload),!present);if(present)assert.deepEqual(await readFile(payload),model.bytes);assert.deepEqual(await readFile(metadata),model.metadata);assert.deepEqual(await readFile(source),model.bytes);assert.deepEqual(await readFile(marker),markerBytes);const current=await inventory(folder);delete current[model.hash+'.glb'];delete current[model.hash+'.txt'];assert.deepEqual(current,before,'Maintenance changed an existing model file');};
 try{
  for(const [path,bytes] of seeds){await exclusive(path,bytes);created.push([path,bytes]);}await checkpoint('orphan-seeded');
  const inspection=await invoke('model.download.inspect',{modelHash:model.hash}),original=inspection.output!.entry as Entry,usage=inspection.output!.usage as Usage;
  assert.equal(original.modelHash,model.hash);assert.match(original.inspectionRevision,/^[a-f0-9]{64}$/);assert.equal(original.name,model.name);assert.equal(original.payloadPresent,true);assert.equal(original.metadataPresent,true);assert.equal(original.contentValid,true);assert.equal(original.included,false);assert.equal(original.payloadBytes,model.bytes.length);
  assert.deepEqual(usage,{current:false,history:false,saved:false,runtime:false,inUse:false,uncertain:false,canRemove:true,protection:''});
  assert.deepEqual(await fact<Entry>('model.download.review',{modelHash:model.hash}),original);await assertFiles(true);await checkpoint('unused-reviewed');
  const removal=await invoke('model.download.edit',{modelHash:model.hash,inspectionRevision:original.inspectionRevision}),removed=removal.output!.entry as Entry;
  assert.equal(removal.output!.removed,true);assert.equal(removal.output!.previousRevision,original.inspectionRevision);assert.equal(removed.modelHash,model.hash);assert.equal(removed.payloadPresent,false);assert.equal(removed.metadataPresent,true);assert.equal(removed.name,model.name);assert.equal(removed.contentValid,false);assert.equal(removed.included,false);assert.equal(removed.payloadBytes,0);assert.notEqual(removed.inspectionRevision,original.inspectionRevision);
  assert.deepEqual(await fact<Entry>('model.download.review',{modelHash:model.hash}),removed);await assertFiles(false);await checkpoint('download-removed');
  const staleArgs={modelHash:model.hash,inspectionRevision:original.inspectionRevision},stale=await driver.reject('model.download.edit',staleArgs);
  assertModelRemovalRejected(stale,staleArgs,'stale');evidence.stale=stale;await assertFiles(false);await checkpoint('stale-refused');
  const selected=await invoke('model.import',{operation:'select'}),deadline=Date.now()+45000;
  let preview=await fact<{requestId:string;phase:string;error:string;preview:{modelHash:string}}>('model.import.selection');
  while(preview.phase!=='preview'&&Date.now()<deadline){assert.ok(!['failed','cancelled'].includes(preview.phase),preview.error);await delay();preview=await fact('model.import.selection');}
  assert.equal(preview.phase,'preview');assert.equal(preview.requestId,selected.output!.requestId);assert.equal(preview.preview.modelHash,model.hash);evidence.preview=preview;
  const imported=await invoke('model.import',{operation:'object',requestId:preview.requestId,modelHash:model.hash});
  assert.equal(imported.output!.modelHash,model.hash);assert.equal(imported.output!.destination,'object');assert.match(String(imported.output!.objectId),/^[a-f0-9]{32}$/);await assertFiles(true);
  const picker=JSON.parse(await readFile(join(directory,'model-maintenance-picker.json'),'utf8'));assert.equal(picker.version,1);assert.equal(picker.ownerId,owner.id);assert.equal(picker.modelHash,model.hash);assert.equal(picker.requestId,preview.requestId);assert.equal(picker.starts,1);assert.equal(picker.releases,1);assert.equal(picker.phase,'released');assert.equal(picker.sparseObjectView,owner.sparseObjectView);evidence.picker=picker;await checkpoint('exact-reimport');
  const restored=await invoke('model.download.inspect',{modelHash:model.hash}),restoredEntry=restored.output!.entry as Entry;
  assert.deepEqual(restoredEntry,original,'Exact reimport must restore the same payload/full-metadata fingerprint');assert.equal((restored.output!.usage as Usage).current,true);assert.equal((restored.output!.usage as Usage).canRemove,false);
  const retainedArgs={modelHash:model.hash,inspectionRevision:restoredEntry.inspectionRevision},retained=await driver.reject('model.download.edit',retainedArgs);
  assertModelRemovalRejected(retained,retainedArgs,'retained');evidence.retained=retained;await assertFiles(true);await checkpoint('saved-object-protected');
  if(driver.replay){const repeated=await driver.replay(removal);assert.equal(repeated.id,removal.id);assert.equal(repeated.phase,'completed');assert.deepEqual(repeated.call,removal.call);assert.deepEqual(repeated.output,removal.output);assert.deepEqual(repeated,removal);evidence.duplicateStart=repeated;await assertFiles(true);}
  if(driver.history){const previous=await driver.history(removal);assert.equal(previous.id,removal.id);assert.equal(previous.phase,'completed');assert.deepEqual(previous.call,removal.call);assert.deepEqual(previous.output,removal.output);assert.deepEqual(previous,removal);evidence.historicalRevisit=previous;await assertFiles(true);}
  const included=await fact<{modelHash:string;bundled:boolean}>('avatar.included');assert.equal(included.bundled,true,'This full probe requires the packaged avatar');assert.match(included.modelHash,/^[a-f0-9]{64}$/);
  const includedInspection=await invoke('model.download.inspect',{modelHash:included.modelHash}),includedEntry=includedInspection.output!.entry as Entry;
  assert.equal(includedEntry.included,true);assert.equal((includedInspection.output!.usage as Usage).canRemove,false);const includedArgs={modelHash:included.modelHash,inspectionRevision:includedEntry.inspectionRevision};
  const protectedIncluded=await driver.reject('model.download.edit',includedArgs);assertModelRemovalRejected(protectedIncluded,includedArgs,'included');evidence.included=protectedIncluded;await assertFiles(true);await checkpoint('included-avatar-protected');
  const finalInspection=await invoke('model.download.inspect',{modelHash:model.hash});assert.deepEqual(finalInspection.output!.entry,original,'Fresh native read must still see the exact reimport after historical execution');assert.deepEqual(await fact<Entry>('model.download.review',{modelHash:model.hash}),original);await assertFiles(true);
  evidence.complete=true;await checkpoint('complete');
 }catch(error){evidence.complete=false;evidence.error=error instanceof Error?error.message:String(error);await checkpoint('failed');throw error;}
 finally{if(!dispatched)for(const [path,bytes] of created.reverse()){const entry=await lstat(path);assert.ok(entry.isFile()&&!entry.isSymbolicLink());assert.deepEqual(await readFile(path),bytes,'Only unchanged exclusively created fixture bytes may be cleaned up');await unlink(path);}}
}
