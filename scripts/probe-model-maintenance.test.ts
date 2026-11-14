// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {createHash} from 'node:crypto';
import {mkdtemp,mkdir,readdir,readFile,writeFile,unlink,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {basename,dirname,join,resolve} from 'node:path';
import {afterEach,expect,it,vi} from 'vitest';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {assertModelRemovalRejected,maintenanceModel,probeModelMaintenance,type ModelMaintenanceDriver,type ModelMaintenanceReceipt} from './probe-model-maintenance';

// These synthetic drivers test the orchestrator's evidence/assertion boundaries.
// Real GLB validation, import, references and replay are checked by native runs.
const owned:string[]=[];
const retainedCause='A current, dormant or temporary room, avatar profile or model-specific setting still uses this model.';
const retainedFailure=retainedCause+'. Model maintenance stopped waiting. Before dispatch it is cancelled; an already dispatched deletion may still finish. Inspect the current file and this receipt before another explicit request.';
type Args={modelHash:string;inspectionRevision:string};
function failed(args:Args,status=retainedFailure):RoomAgentState{return {ok:true,status:'Waiting for action completion',execution:{selected:{id:'e'.repeat(32),capability:'model.download.edit',version:1,resources:[],phase:'failed',status,call:{id:'model.download.edit',version:1,arguments:{...args}}}}} as unknown as RoomAgentState;}
afterEach(async()=>{for(const path of owned.splice(0)){expect(dirname(resolve(path))).toBe(resolve(tmpdir()));expect(basename(path)).toMatch(/^MaestroModelMaintenanceTest-/);await rm(path,{recursive:true,force:true});}});
async function fixture(roomExists=true){
 const directory=await mkdtemp(join(tmpdir(),'MaestroModelMaintenanceTest-'));owned.push(directory);const appRoot=join(directory,'workspace'),room=join(appRoot,'room'),models=join(room,'models'),id='a'.repeat(32),included=maintenanceModel('b'.repeat(32),'included');
 await mkdir(appRoot);await writeFile(join(directory,'owner.json'),JSON.stringify({version:1,id,providerUsed:false,providerScenario:'',resumedFrom:'',nativeRestart:false,actionInterruption:false,motionOnly:false,transportOnly:false,liveContinuity:false,firstLearner:false,identityOnly:false,sparseObjectView:false,steeringOnly:false}));await writeFile(join(directory,'ready.json'),JSON.stringify({version:1,id,workspaceDirectory:appRoot}));
 if(roomExists){await mkdir(models,{recursive:true});await writeFile(join(models,included.hash+'.glb'),included.bytes);await writeFile(join(models,included.hash+'.txt'),included.metadata);}
 return {directory,appRoot,room,models,id,included};
}
async function files(folder:string){const result:Record<string,string>={};for(const name of (await readdir(folder)).sort())result[name]=(await readFile(join(folder,name))).toString('hex');return result;}
function syntheticDriver(f:Awaited<ReturnType<typeof fixture>>,options:{captureFailure?:boolean;replayDeletes?:boolean;editMetadata?:boolean;unrelatedRejection?:boolean;ignoreRetention?:boolean;wrongPickerHash?:boolean;wrongAcceptedHash?:boolean;missingIncludedProtection?:boolean}={}){
 let accepted=false,index=0,chosen:{ownerId:string;modelHash:string;name:string}|undefined;const phases:string[]=[];
 const record=async(hash:string)=>{
  let payload:Buffer|undefined;try{payload=await readFile(join(f.models,hash+'.glb'));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
  const metadata=await readFile(join(f.models,hash+'.txt'));
  return {modelHash:hash,inspectionRevision:createHash('sha256').update(payload??'missing').update(metadata).digest('hex'),name:metadata.toString('utf8').split('\n')[0],payloadPresent:!!payload,metadataPresent:true,contentValid:!!payload,included:hash===f.included.hash&&!options.missingIncludedProtection,payloadBytes:payload?.length??0};
 };
 const receipt=(id:string,args:Record<string,unknown>,output:Record<string,unknown>):ModelMaintenanceReceipt=>({id:(++index).toString(16).padStart(32,'0'),capability:id,version:1,resources:[],phase:'completed',status:'Synthetic fixture driver',call:{id,version:1,arguments:args},output});
 const driver:ModelMaintenanceDriver={
  invoke:vi.fn<ModelMaintenanceDriver['invoke']>(async(id,args)=>{
   const hash=String(args.modelHash),current=accepted&&hash===chosen?.modelHash,isIncluded=hash===f.included.hash;
   if(id==='model.download.inspect')return receipt(id,args,{entry:await record(hash),usage:{current,history:false,saved:false,runtime:false,inUse:false,uncertain:false,canRemove:!current&&!isIncluded,protection:current?retainedCause:isIncluded?'Included avatar is protected':''}});
   if(id==='model.download.edit'){await unlink(join(f.models,hash+'.glb'));return receipt(id,args,{removed:true,previousRevision:args.inspectionRevision,entry:await record(hash)});}
   if(args.operation==='select'){
    chosen=JSON.parse(await readFile(join(f.directory,'model-maintenance-source.json'),'utf8'));
    await writeFile(join(f.directory,'model-maintenance-picker.json'),JSON.stringify({version:1,ownerId:chosen!.ownerId,modelHash:options.wrongPickerHash?'f'.repeat(64):chosen!.modelHash,requestId:'c'.repeat(32),starts:1,releases:1,phase:'released',sparseObjectView:JSON.parse(await readFile(join(f.directory,'owner.json'),'utf8')).sparseObjectView}));
    return receipt(id,args,{requestId:'c'.repeat(32)});
   }
   expect(id).toBe('model.import');expect(args.operation).toBe('object');expect(chosen?.modelHash).toBe(hash);
   await writeFile(join(f.models,hash+'.glb'),await readFile(join(f.directory,'model-maintenance-source.glb')),{flag:'wx'});accepted=true;
   if(options.editMetadata)await writeFile(join(f.models,hash+'.txt'),chosen!.name+'\nTruncated attribution');
   return receipt(id,args,{modelHash:options.wrongAcceptedHash?'f'.repeat(64):hash,destination:'object',objectId:'d'.repeat(32)});
  }),
  fact:vi.fn<ModelMaintenanceDriver['fact']>(async(id,args)=>{
   if(id==='model.download.review')return {available:true,value:await record(String(args.modelHash))};
   if(id==='avatar.included')return {available:true,value:{modelHash:f.included.hash,bundled:true}};
   expect(id).toBe('model.import.selection');return {available:true,value:{requestId:'c'.repeat(32),phase:'preview',error:'',preview:{modelHash:chosen!.modelHash}}};
  }),
  reject:vi.fn<ModelMaintenanceDriver['reject']>(async(_id,args)=>{
   if(options.unrelatedRejection)return {ok:false,status:'The scene changed; inspect again'} as RoomAgentState;
   if(args.modelHash===f.included.hash)return {ok:false,status:'The included Maestro model is protected; its private copy can be recreated on use'} as RoomAgentState;
   if(accepted)return options.ignoreRetention?{ok:true,status:'Accepted'} as RoomAgentState:failed(args as Args);
   return {ok:false,status:'Inspect this model and load its current file revision before removing the download'} as RoomAgentState;
  }),
  replay:vi.fn<NonNullable<ModelMaintenanceDriver['replay']>>(async previous=>{if(options.replayDeletes)await unlink(join(f.models,String(previous.call.arguments.modelHash)+'.glb'));return previous;}),
  history:vi.fn<NonNullable<ModelMaintenanceDriver['history']>>(async previous=>previous),
  capture:vi.fn<NonNullable<ModelMaintenanceDriver['capture']>>(async phase=>{phases.push(phase);if(options.captureFailure&&phase==='orphan-seeded')throw new Error('Synthetic pre-dispatch failure');}),
 };
 return {driver,phases};
}
it('builds a unique, internally consistent GLB and full bounded attribution sidecar',()=>{
 const model=maintenanceModel('a'.repeat(32),'headless'),bytes=model.bytes;
 expect(bytes.readUInt32LE(0)).toBe(0x46546c67);expect(bytes.readUInt32LE(4)).toBe(2);expect(bytes.readUInt32LE(8)).toBe(bytes.length);expect(bytes.readUInt32LE(16)).toBe(0x4e4f534a);
 const length=bytes.readUInt32LE(12),gltf=JSON.parse(bytes.toString('utf8',20,20+length));expect(length%4).toBe(0);expect(bytes.readUInt32LE(20+length)).toBe(72);expect(bytes.readUInt32LE(24+length)).toBe(0x004e4942);expect(gltf.buffers[0].byteLength).toBe(72);expect(gltf.accessors[0].count).toBe(3);
 expect(model.hash).toBe(createHash('sha256').update(bytes).digest('hex'));expect(model.metadata.length).toBeGreaterThan(1024);expect(model.metadata.length).toBeLessThanOrEqual(128*1024);expect(model.metadata.toString()).toContain('SHA256: '+model.hash);
 expect(maintenanceModel('a'.repeat(32),'headless').bytes).toEqual(bytes);expect(maintenanceModel('b'.repeat(32),'headless').hash).not.toBe(model.hash);expect(maintenanceModel('a'.repeat(32),'book').hash).not.toBe(model.hash);
});
it.each(['headless','book'] as const)('checks the complete native-flow evidence without changing prior files (%s)',async client=>{
 const f=await fixture(),before=await files(f.models),{driver,phases}=syntheticDriver(f);if(client==='headless')delete driver.history;else delete driver.replay;
 await probeModelMaintenance(driver,f.directory,client);const model=maintenanceModel(f.id,client),after=await files(f.models);
 for(const [name,content] of Object.entries(before))expect(after[name]).toBe(content);
 expect(after[model.hash+'.glb']).toBe(model.bytes.toString('hex'));expect(after[model.hash+'.txt']).toBe(model.metadata.toString('hex'));expect(Object.keys(after)).toHaveLength(4);
 const result=JSON.parse(await readFile(join(f.directory,client+'-model-maintenance.json'),'utf8'));expect(result.complete).toBe(true);expect(result.metadataBytes).toBeGreaterThan(1024);expect(result.phase).toBe('complete');
 expect(result.receipts.map((r:ModelMaintenanceReceipt)=>r.capability)).toEqual(['model.download.inspect','model.download.edit','model.import','model.import','model.download.inspect','model.download.inspect','model.download.inspect']);
 expect(phases).toContain('saved-object-protected');expect(phases).toContain('included-avatar-protected');expect(driver.reject).toHaveBeenCalledTimes(3);expect(client==='headless'?result.duplicateStart:result.historicalRevisit).toMatchObject({capability:'model.download.edit',output:{removed:true}});
});
it.each(['missing room','selected generation','provider workspace'] as const)('rejects %s before writing or invoking',async mode=>{
 const f=await fixture(mode!=='missing room');if(mode==='selected generation'){await mkdir(join(f.appRoot,'workspace-generations.v1'));await writeFile(join(f.appRoot,'workspace-generations.v1','current.v1.json'),'{}');}
 if(mode==='provider workspace')await writeFile(join(f.directory,'owner.json'),JSON.stringify({version:1,id:f.id,providerUsed:true}));
 const before=mode==='missing room'?null:await files(f.models),{driver}=syntheticDriver(f);await expect(probeModelMaintenance(driver,f.directory,'headless')).rejects.toThrow();expect(driver.invoke).not.toHaveBeenCalled();if(before)expect(await files(f.models)).toEqual(before);expect(await readdir(f.directory)).not.toContain('model-maintenance-source.glb');
});
it.each(['providerUsed','nativeRestart','sparseObjectView'] as const)('requires explicit %s ownership, never inferring absent fields as offline',async field=>{
 const f=await fixture(),path=join(f.directory,'owner.json'),owner=JSON.parse(await readFile(path,'utf8'));delete owner[field];await writeFile(path,JSON.stringify(owner));const {driver}=syntheticDriver(f);await expect(probeModelMaintenance(driver,f.directory,'headless')).rejects.toThrow();expect(driver.invoke).not.toHaveBeenCalled();expect(await readdir(f.directory)).not.toContain('model-maintenance-source.glb');
});
it('keeps full maintenance coverage for an explicitly sparse offline ordinary journey',async()=>{
 const f=await fixture(),path=join(f.directory,'owner.json'),owner=JSON.parse(await readFile(path,'utf8'));owner.sparseObjectView=true;await writeFile(path,JSON.stringify(owner));const {driver}=syntheticDriver(f);await probeModelMaintenance(driver,f.directory,'headless');expect(JSON.parse(await readFile(join(f.directory,'headless-model-maintenance.json'),'utf8'))).toMatchObject({sparseObjectView:true,complete:true});
});
it.each(['payload','marker','picker evidence'] as const)('does not replace an existing %s',async collision=>{
 const f=await fixture(),model=maintenanceModel(f.id,'headless'),path=collision==='payload'?join(f.models,model.hash+'.glb'):join(f.directory,collision==='marker'?'model-maintenance-source.json':'model-maintenance-picker.json');await writeFile(path,'Existing bytes');const before=await files(f.models),{driver}=syntheticDriver(f);
 await expect(probeModelMaintenance(driver,f.directory,'headless')).rejects.toThrow();expect(await readFile(path,'utf8')).toBe('Existing bytes');expect(await files(f.models)).toEqual(before);expect(driver.invoke).not.toHaveBeenCalled();
});
it('cleans only exclusively created unchanged fixtures after a pre-dispatch failure',async()=>{
 const f=await fixture(),before=await files(f.models),{driver}=syntheticDriver(f,{captureFailure:true});await expect(probeModelMaintenance(driver,f.directory,'headless')).rejects.toThrow('pre-dispatch');expect(await files(f.models)).toEqual(before);expect(await readdir(f.directory)).not.toContain('model-maintenance-source.glb');expect(await readdir(f.directory)).not.toContain('model-maintenance-source.json');expect(driver.invoke).not.toHaveBeenCalled();
});
it.each(['replayDeletes','editMetadata','ignoreRetention','wrongPickerHash','wrongAcceptedHash','missingIncludedProtection'] as const)('rejects false evidence for %s and preserves diagnosis',async flaw=>{
 const f=await fixture(),before=await files(f.models),{driver}=syntheticDriver(f,{[flaw]:true});await expect(probeModelMaintenance(driver,f.directory,'headless')).rejects.toThrow();const after=await files(f.models);for(const [name,content] of Object.entries(before))expect(after[name]).toBe(content);
 const evidence=JSON.parse(await readFile(join(f.directory,'headless-model-maintenance.json'),'utf8'));expect(evidence.complete).toBe(false);expect(evidence.phase).toBe('failed');expect(evidence.error).toBeTruthy();expect(await readdir(f.directory)).toContain('model-maintenance-source.glb');
});
it('rejects an unrelated transport failure before selecting the original export',async()=>{
 const f=await fixture(),{driver,phases}=syntheticDriver(f,{unrelatedRejection:true});await expect(probeModelMaintenance(driver,f.directory,'headless')).rejects.toThrow('Expected native model stale refusal');expect(phases).not.toContain('stale-refused');expect(driver.invoke).not.toHaveBeenCalledWith('model.import',expect.anything());
});
it('accepts exact direct and decorated current-reference refusal, and exact native source-pin refusal',()=>{
 const args={modelHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};expect(()=>assertModelRemovalRejected(failed(args),args,'retained')).not.toThrow();expect(()=>assertModelRemovalRejected(failed(args,retainedCause),args,'retained')).not.toThrow();expect(()=>assertModelRemovalRejected({ok:false,status:'The asset is in use. Release its live users and wait for pending work to finish.'} as RoomAgentState,args,'retained')).not.toThrow();
});
it.each(['capability','version','call id','model hash','revision','transport cause','suffix','missing receipt'] as const)('rejects mismatched %s as model retention evidence',flaw=>{
 const args={modelHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)},state=failed(args),selected=state.execution!.selected!;
 if(flaw==='capability')selected.capability='model.import';else if(flaw==='version')selected.version=2;else if(flaw==='call id')selected.call.id='model.import';else if(flaw==='model hash')selected.call.arguments.modelHash='c'.repeat(64);else if(flaw==='revision')selected.call.arguments.inspectionRevision='c'.repeat(64);else if(flaw==='suffix')selected.status+=' Please retry.';else if(flaw==='missing receipt'){state.ok=false;state.status=retainedFailure;state.execution!.selected=null;}else{state.ok=false;state.status='The scene changed; inspect again';}
 expect(()=>assertModelRemovalRejected(state,args,'retained')).toThrow();
});
it.each(['phase','call','capability','status','resources'] as const)('rejects damaged historical Book %s despite matching id/output',async flaw=>{
 const f=await fixture(),{driver}=syntheticDriver(f);delete driver.replay;driver.history=async receipt=>{const wrong=structuredClone(receipt);if(flaw==='phase')wrong.phase='failed';else if(flaw==='call')wrong.call.arguments.inspectionRevision='f'.repeat(64);else if(flaw==='capability')wrong.capability='model.import';else if(flaw==='status')wrong.status='Changed receipt';else wrong.resources=['model:wrong'];return wrong;};await expect(probeModelMaintenance(driver,f.directory,'book')).rejects.toThrow();
});
