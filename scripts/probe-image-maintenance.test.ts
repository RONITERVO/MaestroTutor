// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {createHash} from 'node:crypto';
import {mkdtemp,mkdir,readdir,readFile,writeFile,unlink,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {basename,dirname,join,resolve} from 'node:path';
import {inflateSync} from 'node:zlib';
import {afterEach,expect,it,vi} from 'vitest';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {assertStaleImageRemovalRejected,maintenanceImage,probeImageMaintenance,type ImageMaintenanceDriver,type ImageMaintenanceReceipt} from './probe-image-maintenance';

// These synthetic drivers validate fixture orchestration and assertions only.
// Actual C# decoding, retained-reference proof and replay are separate journeys.
const owned:string[]=[];
afterEach(async()=>{for(const path of owned.splice(0)){expect(dirname(resolve(path))).toBe(resolve(tmpdir()));expect(basename(path)).toMatch(/^MaestroImageMaintenanceTest-/);await rm(path,{recursive:true,force:true});}});
async function fixture(roomExists=true){
 const directory=await mkdtemp(join(tmpdir(),'MaestroImageMaintenanceTest-'));owned.push(directory);const appRoot=join(directory,'workspace'),room=join(appRoot,'room'),images=join(room,'images'),id='a'.repeat(32);
 await mkdir(appRoot);await writeFile(join(directory,'owner.json'),JSON.stringify({version:1,id}));await writeFile(join(directory,'ready.json'),JSON.stringify({id,workspaceDirectory:appRoot}));
 if(roomExists){await mkdir(images,{recursive:true});await writeFile(join(images,'existing.image'),'An existing asset that the fixture must preserve');await writeFile(join(images,'existing.txt'),'Existing name\n');}
 return {directory,appRoot,room,images,id};
}
async function files(folder:string){const result:Record<string,string>={};for(const name of (await readdir(folder)).sort())result[name]=(await readFile(join(folder,name))).toString('hex');return result;}
function syntheticDriver(images:string,options:{captureFailure?:boolean;replayDeletes?:boolean;editMetadata?:boolean;unrelatedSceneRejection?:boolean}={}){
 let offered:Parameters<ImageMaintenanceDriver['offer']>[0]|undefined,accepted=false,index=0;const phases:string[]=[];
 const record=async(hash:string)=>{
  let payload:Buffer|undefined;try{payload=await readFile(join(images,hash+'.image'));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
  const metadata=await readFile(join(images,hash+'.txt'));
  return {imageHash:hash,inspectionRevision:createHash('sha256').update(payload??'missing').update(metadata).digest('hex'),name:metadata.toString('utf8').trim(),payloadPresent:!!payload,metadataPresent:true,contentValid:!!payload,payloadBytes:payload?.length??0};
 };
 const receipt=(id:string,args:Record<string,unknown>,output:Record<string,unknown>):ImageMaintenanceReceipt=>({id:(++index).toString(16).padStart(32,'0'),capability:id,version:1,resources:[],phase:'completed',status:'Synthetic fixture driver',call:{id,version:1,arguments:args},output});
 const driver:ImageMaintenanceDriver={
  invoke:vi.fn<ImageMaintenanceDriver['invoke']>(async(id,args)=>{
   const hash=String(args.imageHash);
   if(id==='image.library.inspect')return receipt(id,args,{entry:await record(hash),usage:{current:accepted,history:false,saved:false,runtime:false,inUse:false,uncertain:false,canRemove:!accepted,protection:accepted?'Appearance retains it':''}});
   if(id==='image.library.edit'){await unlink(join(images,hash+'.image'));if(options.editMetadata)await writeFile(join(images,hash+'.txt'),'Damaged name');return receipt(id,args,{removed:true,previousRevision:args.inspectionRevision,entry:await record(hash)});}
   if(args.operation==='selectChat')return receipt(id,args,{requestId:'c'.repeat(32)});
   expect(args.operation).toBe('accept');expect(offered?.hash).toBe(hash);await writeFile(join(images,hash+'.image'),Buffer.from(offered!.dataUrl.split(',')[1],'base64'),{flag:'wx'});accepted=true;
   return receipt(id,args,{imageHash:hash,appearanceId:'d'.repeat(32)});
  }),
  fact:vi.fn<ImageMaintenanceDriver['fact']>(async(id,args)=>{
   if(id==='image.library.review')return {available:true,value:await record(String(args.imageHash))};
   if(id==='image.chat.library')return {available:true,value:{offerSet:'b'.repeat(32),entries:offered?[{imageHash:offered.hash}]:[]}};
   expect(id).toBe('image.import.selection');return {available:true,value:{requestId:'c'.repeat(32),phase:'preview',error:'',preview:{imageHash:offered!.hash}}};
  }),
  reject:vi.fn<ImageMaintenanceDriver['reject']>(async()=>({ok:false,status:options.unrelatedSceneRejection?'The scene changed; inspect again':'Inspect this image and load its current file revision before removing the download'} as RoomAgentState)),
  offer:vi.fn<ImageMaintenanceDriver['offer']>(async image=>{offered=image;}),
  replay:vi.fn<NonNullable<ImageMaintenanceDriver['replay']>>(async previous=>{if(options.replayDeletes)await unlink(join(images,String(previous.call.arguments.imageHash)+'.image'));return previous;}),
  history:vi.fn<NonNullable<ImageMaintenanceDriver['history']>>(async previous=>previous),
  capture:vi.fn<NonNullable<ImageMaintenanceDriver['capture']>>(async phase=>{phases.push(phase);if(options.captureFailure&&phase==='orphan-seeded')throw new Error('Synthetic pre-dispatch capture failure');}),
 };
 return {driver,phases};
}
it('creates a bounded deterministic ordinary PNG whose identity varies with its owner and client',()=>{
 const image=maintenanceImage('a'.repeat(32),'headless');expect(image.bytes.length).toBeLessThan(1024);expect(image.hash).toMatch(/^[a-f0-9]{64}$/);expect(image.bytes.subarray(0,8)).toEqual(Buffer.from([137,80,78,71,13,10,26,10]));
 const chunks:Record<string,Buffer>={};for(let at=8;at<image.bytes.length;){const length=image.bytes.readUInt32BE(at),name=image.bytes.toString('ascii',at+4,at+8);chunks[name]=image.bytes.subarray(at+8,at+8+length);at+=length+12;}
 expect(chunks.IHDR.readUInt32BE(0)).toBe(8);expect(chunks.IHDR.readUInt32BE(4)).toBe(8);expect([...chunks.IHDR.subarray(8)]).toEqual([8,6,0,0,0]);expect(inflateSync(chunks.IDAT)).toHaveLength(264);expect(chunks.IEND).toHaveLength(0);
 expect(maintenanceImage('a'.repeat(32),'headless').bytes).toEqual(image.bytes);expect(maintenanceImage('b'.repeat(32),'headless').hash).not.toBe(image.hash);expect(maintenanceImage('a'.repeat(32),'book').hash).not.toBe(image.hash);
});
it.each(['headless','book'] as const)('uses the actual room path, preserves unrelated files and proves the round trip assertions (%s)',async client=>{
 const f=await fixture(),before=await files(f.images),{driver,phases}=syntheticDriver(f.images);if(client==='book')delete driver.replay;else delete driver.history;
 await probeImageMaintenance(driver,f.directory,client);const image=maintenanceImage(f.id,client),after=await files(f.images);
 expect(after['existing.image']).toBe(before['existing.image']);expect(after['existing.txt']).toBe(before['existing.txt']);expect(after[image.hash+'.image']).toBe(image.bytes.toString('hex'));expect(after[image.hash+'.txt']).toBe(Buffer.from(image.name+'\n').toString('hex'));expect(Object.keys(after)).toHaveLength(4);
 const result=JSON.parse(await readFile(join(f.directory,client+'-image-maintenance.json'),'utf8'));expect(result.complete).toBe(true);expect(result.phase).toBe('complete');expect(result.receipts.map((r:ImageMaintenanceReceipt)=>r.capability)).toEqual(['image.library.inspect','image.library.edit','image.import','image.import','image.library.inspect','image.library.inspect']);
 expect(phases).toContain('stale-refused');expect(driver.offer).toHaveBeenCalledOnce();expect(driver.reject).toHaveBeenCalledOnce();expect(client==='headless'?result.duplicateStart:result.historicalRevisit).toMatchObject({capability:'image.library.edit',phase:'completed',output:{removed:true}});
});
it.each(['missing room','selected generation','provider workspace'] as const)('refuses %s before invoking or writing a fixture',async mode=>{
 const f=await fixture(mode!=='missing room');
 if(mode==='selected generation'){const generations=join(f.appRoot,'workspace-generations.v1');await mkdir(generations);await writeFile(join(generations,'current.v1.json'),'{}');}
 if(mode==='provider workspace')await writeFile(join(f.directory,'owner.json'),JSON.stringify({version:1,id:f.id,providerUsed:true}));
 const before=mode==='missing room'?null:await files(f.images),{driver}=syntheticDriver(f.images);
 await expect(probeImageMaintenance(driver,f.directory,'headless')).rejects.toThrow();expect(driver.invoke).not.toHaveBeenCalled();if(before)expect(await files(f.images)).toEqual(before);expect(await readdir(f.directory)).not.toContain('headless-image-maintenance.json');
});
it('refuses a colliding existing identity without overwriting or removing it',async()=>{
 const f=await fixture(),image=maintenanceImage(f.id,'headless');await writeFile(join(f.images,image.hash+'.image'),'Existing user bytes');const before=await files(f.images),{driver}=syntheticDriver(f.images);
 await expect(probeImageMaintenance(driver,f.directory,'headless')).rejects.toThrow('must not already exist');expect(await files(f.images)).toEqual(before);expect(driver.invoke).not.toHaveBeenCalled();
});
it('cleans only its own unchanged seed files if setup fails before native dispatch',async()=>{
 const f=await fixture(),before=await files(f.images),{driver}=syntheticDriver(f.images,{captureFailure:true});
 await expect(probeImageMaintenance(driver,f.directory,'headless')).rejects.toThrow('pre-dispatch');expect(await files(f.images)).toEqual(before);expect(driver.invoke).not.toHaveBeenCalled();
});
it.each(['replayDeletes','editMetadata'] as const)('fails visibly if the synthetic native driver violates %s and preserves evidence',async flaw=>{
 const f=await fixture(),before=await files(f.images),{driver}=syntheticDriver(f.images,{[flaw]:true});
 await expect(probeImageMaintenance(driver,f.directory,'headless')).rejects.toThrow();const after=await files(f.images);expect(after['existing.image']).toBe(before['existing.image']);expect(after['existing.txt']).toBe(before['existing.txt']);
 const evidence=JSON.parse(await readFile(join(f.directory,'headless-image-maintenance.json'),'utf8'));expect(evidence.phase).toBe('failed');expect(evidence.complete).not.toBe(true);expect(evidence.error).toBeTruthy();
});

it('accepts exact image revision refusal before dispatch while a completed historical receipt remains selected',()=>{
 const args={imageHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 const state={ok:false,status:'Inspect this image and load its current file revision before removing the download',execution:{selected:{phase:'completed',capability:'image.library.inspect'}}} as unknown as RoomAgentState;
 expect(()=>assertStaleImageRemovalRejected(state,args)).not.toThrow();
});
it.each(['This image changed. Inspect it again before removing its download.','This image changed before deletion. Its current files are preserved.'])('accepts the exact failed removal receipt for %s',status=>{
 const args={imageHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 const selected={capability:'image.library.edit',version:1,phase:'failed',status,call:{id:'image.library.edit',version:1,arguments:args}};
 expect(()=>assertStaleImageRemovalRejected({ok:true,execution:{selected}} as unknown as RoomAgentState,args)).not.toThrow();
});
it.each(['capability','version','call id','image hash','inspection revision'] as const)('refuses an image-specific failed receipt with the wrong %s',mismatch=>{
 const args={imageHash:'a'.repeat(64),inspectionRevision:'b'.repeat(64)};
 const selected={capability:'image.library.edit',version:1,phase:'failed',status:'This image changed. Inspect it again before removing its download.',call:{id:'image.library.edit',version:1,arguments:{...args}}};
 if(mismatch==='capability')selected.capability='image.library.inspect';
 else if(mismatch==='version')selected.version=2;
 else if(mismatch==='call id')selected.call.id='image.library.inspect';
 else if(mismatch==='image hash')selected.call.arguments.imageHash='c'.repeat(64);
 else selected.call.arguments.inspectionRevision='c'.repeat(64);
 for(const ok of [true,false])expect(()=>assertStaleImageRemovalRejected({ok,status:selected.status,execution:{selected}} as unknown as RoomAgentState,args)).toThrow();
});
it('refuses an unrelated scene rejection before offering or reimporting the fixture',async()=>{
 const f=await fixture(),{driver,phases}=syntheticDriver(f.images,{unrelatedSceneRejection:true});
 await expect(probeImageMaintenance(driver,f.directory,'headless')).rejects.toThrow('Expected a native image revision rejection');
 expect(driver.offer).not.toHaveBeenCalled();expect(phases).not.toContain('stale-refused');
 const evidence=JSON.parse(await readFile(join(f.directory,'headless-image-maintenance.json'),'utf8'));
 expect(evidence.phase).toBe('failed');expect(evidence.complete).toBe(false);expect(evidence.error).toContain('The scene changed; inspect again');
});

it.each(['phase','call'] as const)('refuses corrupted historical Book %s despite matching receipt ID and output',async flaw=>{
 const f=await fixture(),{driver}=syntheticDriver(f.images);delete driver.replay;
 driver.history=async previous=>{const changed=structuredClone(previous);if(flaw==='phase')changed.phase='failed';else changed.call.arguments.inspectionRevision='f'.repeat(64);return changed;};
 await expect(probeImageMaintenance(driver,f.directory,'book')).rejects.toThrow();
 const evidence=JSON.parse(await readFile(join(f.directory,'book-image-maintenance.json'),'utf8'));expect(evidence.complete).toBe(false);expect(evidence.phase).toBe('failed');
});
