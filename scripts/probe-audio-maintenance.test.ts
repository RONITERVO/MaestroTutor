// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {createHash} from 'node:crypto';
import {mkdtemp,mkdir,readdir,readFile,writeFile,unlink,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {basename,dirname,join,resolve} from 'node:path';
import {afterEach,expect,it,vi} from 'vitest';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {assertStaleAudioRemovalRejected,assertRetainedAudioRemovalRejected,maintenanceAudio,probeAudioMaintenance,type AudioMaintenanceDriver,type AudioMaintenanceReceipt} from './probe-audio-maintenance';

// These synthetic drivers validate fixture orchestration and assertions only.
// Actual C# decoding, retained-reference proof and replay are separate journeys.
const owned:string[]=[];
const retainedCause='A current or saved clip source still uses this sound, including unbound definitions.';
// Exact native failed-action status from run 36f9f3cf7b33415286609e79c0a8bb65.
// Keep independent of the assertion's constant so malformed decoration cannot pass.
const retainedFailure=retainedCause+'. Sound maintenance stopped waiting. Before dispatch it is cancelled; an already dispatched deletion may still finish. Inspect the current file and this receipt before another explicit request.';
function retainedState(args:{assetHash:string;inspectionRevision:string},status=retainedFailure):RoomAgentState{
 return {ok:true,status:'Waiting for action completion',execution:{selected:{id:'e'.repeat(32),capability:'audio.library.edit',version:1,resources:[],phase:'failed',status,call:{id:'audio.library.edit',version:1,arguments:{...args}}}}} as unknown as RoomAgentState;
}
afterEach(async()=>{for(const path of owned.splice(0)){expect(dirname(resolve(path))).toBe(resolve(tmpdir()));expect(basename(path)).toMatch(/^MaestroAudioMaintenanceTest-/);await rm(path,{recursive:true,force:true});}});
async function fixture(roomExists=true){
 const directory=await mkdtemp(join(tmpdir(),'MaestroAudioMaintenanceTest-'));owned.push(directory);const appRoot=join(directory,'workspace'),room=join(appRoot,'room'),audios=join(room,'audio'),id='a'.repeat(32);
 await mkdir(appRoot);await writeFile(join(directory,'owner.json'),JSON.stringify({version:1,id}));await writeFile(join(directory,'ready.json'),JSON.stringify({id,workspaceDirectory:appRoot}));
 if(roomExists){await mkdir(audios,{recursive:true});await writeFile(join(audios,'existing.wav'),'An existing asset that the fixture must preserve');await writeFile(join(audios,'existing.txt'),'Existing name\n');}
 return {directory,appRoot,room,audios,id};
}
async function files(folder:string){const result:Record<string,string>={};for(const name of (await readdir(folder)).sort())result[name]=(await readFile(join(folder,name))).toString('hex');return result;}
function syntheticDriver(audios:string,options:{captureFailure?:boolean;replayDeletes?:boolean;editMetadata?:boolean;unrelatedSceneRejection?:boolean;unexpectedPlayback?:boolean;ignoreRetention?:boolean}={}){
 let offered:Parameters<AudioMaintenanceDriver['offer']>[0]|undefined,accepted=false,index=0;const phases:string[]=[];
 const record=async(hash:string)=>{
  let payload:Buffer|undefined;try{payload=await readFile(join(audios,hash+'.wav'));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
  const metadata=await readFile(join(audios,hash+'.txt'));
  return {assetHash:hash,inspectionRevision:createHash('sha256').update(payload??'missing').update(metadata).digest('hex'),name:metadata.toString('utf8').trim(),payloadPresent:!!payload,metadataPresent:true,contentValid:!!payload,payloadBytes:payload?.length??0};
 };
 const receipt=(id:string,args:Record<string,unknown>,output:Record<string,unknown>):AudioMaintenanceReceipt=>({id:(++index).toString(16).padStart(32,'0'),capability:id,version:1,resources:[],phase:'completed',status:'Synthetic fixture driver',call:{id,version:1,arguments:args},output});
 const driver:AudioMaintenanceDriver={
  invoke:vi.fn<AudioMaintenanceDriver['invoke']>(async(id,args)=>{
   const hash=String(args.assetHash);
   if(id==='audio.library.inspect')return receipt(id,args,{entry:await record(hash),usage:{current:accepted,history:false,saved:false,runtime:false,inUse:false,uncertain:false,canRemove:!accepted,protection:accepted?'Clip source retains it':''}});
   if(id==='audio.library.edit'){await unlink(join(audios,hash+'.wav'));if(options.editMetadata)await writeFile(join(audios,hash+'.txt'),'Damaged name');return receipt(id,args,{removed:true,previousRevision:args.inspectionRevision,entry:await record(hash)});}
   if(args.operation==='selectChat')return receipt(id,args,{requestId:'c'.repeat(32)});
   expect(args.operation).toBe('accept');expect(offered?.hash).toBe(hash);await writeFile(join(audios,hash+'.wav'),Buffer.from(offered!.dataUrl.split(',')[1],'base64'),{flag:'wx'});accepted=true;
   return receipt(id,args,{assetHash:hash,sourceId:'d'.repeat(32)});
  }),
  fact:vi.fn<AudioMaintenanceDriver['fact']>(async(id,args)=>{
   if(id==='audio.instances')return {available:true,value:{entries:accepted&&options.unexpectedPlayback?[{instance:'f'.repeat(32)}]:[]}};
   if(id==='audio.source.definition')return {available:true,value:{definition:{clip:{assetHash:offered!.hash}}}};
   if(id==='audio.library.review')return {available:true,value:await record(String(args.assetHash))};
   if(id==='audio.chat.library')return {available:true,value:{offerSet:'b'.repeat(32),entries:offered?[{assetHash:offered.hash}]:[]}};
   expect(id).toBe('audio.import.selection');return {available:true,value:{requestId:'c'.repeat(32),phase:'preview',error:'',preview:{assetHash:offered!.hash}}};
  }),
  reject:vi.fn<AudioMaintenanceDriver['reject']>(async(_id,args)=>accepted&&!options.ignoreRetention?retainedState(args as {assetHash:string;inspectionRevision:string}):({ok:accepted&&options.ignoreRetention?true:false,status:options.unrelatedSceneRejection?'The scene changed; inspect again':'Inspect this sound and load its current file revision before removing the download'} as RoomAgentState)),
  offer:vi.fn<AudioMaintenanceDriver['offer']>(async audio=>{offered=audio;}),
  replay:vi.fn<NonNullable<AudioMaintenanceDriver['replay']>>(async previous=>{if(options.replayDeletes)await unlink(join(audios,String(previous.call.arguments.assetHash)+'.wav'));return previous;}),
  history:vi.fn<NonNullable<AudioMaintenanceDriver['history']>>(async previous=>previous),
  capture:vi.fn<NonNullable<AudioMaintenanceDriver['capture']>>(async phase=>{phases.push(phase);if(options.captureFailure&&phase==='orphan-seeded')throw new Error('Synthetic pre-dispatch capture failure');}),
 };
 return {driver,phases};
}
it('creates bounded canonical PCM WAV bytes whose identity varies with owner and client',()=>{
 const audio=maintenanceAudio('a'.repeat(32),'headless'),bytes=audio.bytes;
 expect(bytes).toHaveLength(9644);expect(audio.hash).toMatch(/^[a-f0-9]{64}$/);expect(bytes.toString('ascii',0,4)).toBe('RIFF');expect(bytes.readUInt32LE(4)).toBe(bytes.length-8);expect(bytes.toString('ascii',8,16)).toBe('WAVEfmt ');
 expect(bytes.readUInt32LE(16)).toBe(16);expect(bytes.readUInt16LE(20)).toBe(1);expect(bytes.readUInt16LE(22)).toBe(1);expect(bytes.readUInt32LE(24)).toBe(24000);expect(bytes.readUInt32LE(28)).toBe(48000);expect(bytes.readUInt16LE(32)).toBe(2);expect(bytes.readUInt16LE(34)).toBe(16);expect(bytes.toString('ascii',36,40)).toBe('data');expect(bytes.readUInt32LE(40)).toBe(9600);
 expect(maintenanceAudio('a'.repeat(32),'headless').bytes).toEqual(bytes);expect(maintenanceAudio('b'.repeat(32),'headless').hash).not.toBe(audio.hash);expect(maintenanceAudio('a'.repeat(32),'book').hash).not.toBe(audio.hash);
});
it.each(['headless','book'] as const)('uses the actual room path, preserves unrelated files and proves the round trip assertions (%s)',async client=>{
 const f=await fixture(),before=await files(f.audios),{driver,phases}=syntheticDriver(f.audios);if(client==='book')delete driver.replay;else delete driver.history;
 await probeAudioMaintenance(driver,f.directory,client);const audio=maintenanceAudio(f.id,client),after=await files(f.audios);
 expect(after['existing.wav']).toBe(before['existing.wav']);expect(after['existing.txt']).toBe(before['existing.txt']);expect(after[audio.hash+'.wav']).toBe(audio.bytes.toString('hex'));expect(after[audio.hash+'.txt']).toBe(Buffer.from(audio.name+'\n').toString('hex'));expect(Object.keys(after)).toHaveLength(4);
 const result=JSON.parse(await readFile(join(f.directory,client+'-audio-maintenance.json'),'utf8'));expect(result.complete).toBe(true);expect(result.phase).toBe('complete');expect(result.receipts.map((r:AudioMaintenanceReceipt)=>r.capability)).toEqual(['audio.library.inspect','audio.library.edit','audio.import','audio.import','audio.library.inspect','audio.library.inspect']);
 expect(phases).toContain('stale-refused');expect(phases).toContain('unbound-source-protected');expect(driver.offer).toHaveBeenCalledOnce();expect(driver.reject).toHaveBeenCalledTimes(2);expect(client==='headless'?result.duplicateStart:result.historicalRevisit).toMatchObject({capability:'audio.library.edit',phase:'completed',output:{removed:true}});
});
it.each(['missing room','selected generation','provider workspace'] as const)('refuses %s before invoking or writing a fixture',async mode=>{
 const f=await fixture(mode!=='missing room');
 if(mode==='selected generation'){const generations=join(f.appRoot,'workspace-generations.v1');await mkdir(generations);await writeFile(join(generations,'current.v1.json'),'{}');}
 if(mode==='provider workspace')await writeFile(join(f.directory,'owner.json'),JSON.stringify({version:1,id:f.id,providerUsed:true}));
 const before=mode==='missing room'?null:await files(f.audios),{driver}=syntheticDriver(f.audios);
 await expect(probeAudioMaintenance(driver,f.directory,'headless')).rejects.toThrow();expect(driver.invoke).not.toHaveBeenCalled();if(before)expect(await files(f.audios)).toEqual(before);expect(await readdir(f.directory)).not.toContain('headless-audio-maintenance.json');
});
it('refuses a colliding existing identity without overwriting or removing it',async()=>{
 const f=await fixture(),audio=maintenanceAudio(f.id,'headless');await writeFile(join(f.audios,audio.hash+'.wav'),'Existing user bytes');const before=await files(f.audios),{driver}=syntheticDriver(f.audios);
 await expect(probeAudioMaintenance(driver,f.directory,'headless')).rejects.toThrow('must not already exist');expect(await files(f.audios)).toEqual(before);expect(driver.invoke).not.toHaveBeenCalled();
});
it('cleans only its own unchanged seed files if setup fails before native dispatch',async()=>{
 const f=await fixture(),before=await files(f.audios),{driver}=syntheticDriver(f.audios,{captureFailure:true});
 await expect(probeAudioMaintenance(driver,f.directory,'headless')).rejects.toThrow('pre-dispatch');expect(await files(f.audios)).toEqual(before);expect(driver.invoke).not.toHaveBeenCalled();
});
it.each(['replayDeletes','editMetadata','unexpectedPlayback','ignoreRetention'] as const)('fails visibly if the synthetic native driver violates %s and preserves evidence',async flaw=>{
 const f=await fixture(),before=await files(f.audios),{driver}=syntheticDriver(f.audios,{[flaw]:true});
 await expect(probeAudioMaintenance(driver,f.directory,'headless')).rejects.toThrow();const after=await files(f.audios);expect(after['existing.wav']).toBe(before['existing.wav']);expect(after['existing.txt']).toBe(before['existing.txt']);
 const evidence=JSON.parse(await readFile(join(f.directory,'headless-audio-maintenance.json'),'utf8'));expect(evidence.phase).toBe('failed');expect(evidence.complete).not.toBe(true);expect(evidence.error).toBeTruthy();
});

it('accepts exact audio revision refusal before dispatch while a completed historical receipt remains selected',()=>{
 const args={assetHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 const state={ok:false,status:'Inspect this sound and load its current file revision before removing the download',execution:{selected:{phase:'completed',capability:'audio.library.inspect'}}} as unknown as RoomAgentState;
 expect(()=>assertStaleAudioRemovalRejected(state,args)).not.toThrow();
});
it('accepts the actual decorated native retained-source failure and the exact direct refusal',()=>{
 const args={assetHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 expect(()=>assertRetainedAudioRemovalRejected(retainedState(args),args)).not.toThrow();
 expect(()=>assertRetainedAudioRemovalRejected(retainedState(args,retainedCause),args)).not.toThrow();
 expect(()=>assertRetainedAudioRemovalRejected({ok:false,status:retainedCause} as RoomAgentState,args)).not.toThrow();
});
it.each([
 retainedCause+'. The scene changed; inspect again',
 retainedFailure+' Retry now.',
 retainedFailure.replace('definitions..','definitions.'),
 retainedFailure.replace('Before dispatch it is cancelled;','Before dispatch it succeeds;'),
 'The scene changed; inspect again. '+retainedFailure.slice(retainedCause.length+2),
])('refuses malformed or unrelated decorated retained failure: %s',status=>{
 const args={assetHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 expect(()=>assertRetainedAudioRemovalRejected(retainedState(args,status),args)).toThrow('Expected native clip-source retention refusal');
});
it.each(['missing receipt','completed receipt','transport error','capability','version','call id','asset hash','inspection revision'] as const)('rejects decorated retention evidence with %s',flaw=>{
 const args={assetHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)},state=retainedState(args),selected=state.execution!.selected!;
 if(flaw==='missing receipt'){state.ok=false;state.status=retainedFailure;state.execution!.selected=null;}
 else if(flaw==='completed receipt'){state.ok=false;state.status=retainedFailure;selected.phase='completed';}
 else if(flaw==='transport error'){state.ok=false;state.status='The scene changed; inspect again';}
 else if(flaw==='capability')selected.capability='audio.library.inspect';
 else if(flaw==='version')selected.version=2;
 else if(flaw==='call id')selected.call.id='audio.library.inspect';
 else if(flaw==='asset hash')selected.call.arguments.assetHash='c'.repeat(64);
 else selected.call.arguments.inspectionRevision='c'.repeat(64);
 expect(()=>assertRetainedAudioRemovalRejected(state,args)).toThrow();
});
it.each(['This sound changed. Inspect it again before removing its download.','This sound changed before deletion. Its current files are preserved.'])('accepts the exact failed removal receipt for %s',status=>{
 const args={assetHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 const selected={capability:'audio.library.edit',version:1,phase:'failed',status,call:{id:'audio.library.edit',version:1,arguments:args}};
 expect(()=>assertStaleAudioRemovalRejected({ok:true,execution:{selected}} as unknown as RoomAgentState,args)).not.toThrow();
});
it.each(['capability','version','call id','audio hash','inspection revision'] as const)('refuses an audio-specific failed receipt with the wrong %s',mismatch=>{
 const args={assetHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 const selected={capability:'audio.library.edit',version:1,phase:'failed',status:'This sound changed. Inspect it again before removing its download.',call:{id:'audio.library.edit',version:1,arguments:{...args}}};
 if(mismatch==='capability')selected.capability='audio.library.inspect';
 else if(mismatch==='version')selected.version=2;
 else if(mismatch==='call id')selected.call.id='audio.library.inspect';
 else if(mismatch==='audio hash')selected.call.arguments.assetHash='c'.repeat(64);
 else selected.call.arguments.inspectionRevision='c'.repeat(64);
 for(const ok of [true,false])expect(()=>assertStaleAudioRemovalRejected({ok,status:selected.status,execution:{selected}} as unknown as RoomAgentState,args)).toThrow();
});
it('refuses an unrelated scene rejection before offering or reimporting the fixture',async()=>{
 const f=await fixture(),{driver,phases}=syntheticDriver(f.audios,{unrelatedSceneRejection:true});
 await expect(probeAudioMaintenance(driver,f.directory,'headless')).rejects.toThrow('Expected a native sound revision rejection');
 expect(driver.offer).not.toHaveBeenCalled();expect(phases).not.toContain('stale-refused');
 const evidence=JSON.parse(await readFile(join(f.directory,'headless-audio-maintenance.json'),'utf8'));
 expect(evidence.phase).toBe('failed');expect(evidence.complete).toBe(false);expect(evidence.error).toContain('The scene changed; inspect again');
});

it.each(['phase','call'] as const)('refuses corrupted historical Book %s despite matching receipt ID and output',async flaw=>{
 const f=await fixture(),{driver}=syntheticDriver(f.audios);delete driver.replay;
 driver.history=async previous=>{const changed=structuredClone(previous);if(flaw==='phase')changed.phase='failed';else changed.call.arguments.inspectionRevision='f'.repeat(64);return changed;};
 await expect(probeAudioMaintenance(driver,f.directory,'book')).rejects.toThrow();
 const evidence=JSON.parse(await readFile(join(f.directory,'book-audio-maintenance.json'),'utf8'));expect(evidence.complete).toBe(false);expect(evidence.phase).toBe('failed');
});
