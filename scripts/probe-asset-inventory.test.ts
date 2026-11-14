// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {mkdtemp,mkdir,readdir,readFile,writeFile,rm} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {basename,dirname,join,resolve} from 'node:path';
import {afterEach,expect,it,vi} from 'vitest';
import {probeAssetInventory,type AssetInventoryDriver} from './probe-asset-inventory';

// These drivers test fixture ownership, location, paging and cleanup only. Their
// fabricated metadata is not evidence of Unity decoding or native integration.
const owned:string[]=[];
const specifications=[{kind:'models',extension:'.glb',count:10},{kind:'images',extension:'.image',count:5},{kind:'audio',extension:'.wav',count:5}] as const;
type Kind=typeof specifications[number]['kind'];
afterEach(async()=>{
 for(const path of owned.splice(0)){
  expect(dirname(resolve(path))).toBe(resolve(tmpdir()));expect(basename(path)).toMatch(/^MaestroInventoryProbeTest-/);
  await rm(path,{recursive:true,force:true});
 }
});
async function tree(directory:string):Promise<Record<string,string>> {
 const result:Record<string,string>={};
 async function visit(folder:string,relative=''){
  for(const entry of await readdir(folder,{withFileTypes:true})){
   const key=relative+entry.name,path=join(folder,entry.name);
   if(entry.isDirectory()){result[key+'/']='directory';await visit(path,key+'/');}
   else result[key]=(await readFile(path)).toString('hex');
  }
 }
 await visit(directory);return result;
}
async function fixture(createRoom=true){
 const directory=await mkdtemp(join(tmpdir(),'MaestroInventoryProbeTest-'));owned.push(directory);
 const appRoot=join(directory,'workspace'),room=join(appRoot,'room'),id='a'.repeat(32);
 await mkdir(appRoot);await writeFile(join(directory,'owner.json'),JSON.stringify({version:1,id}));
 await writeFile(join(directory,'ready.json'),JSON.stringify({version:1,id,workspaceDirectory:appRoot}));
 if(createRoom){
  await mkdir(room);
  for(const spec of specifications){
   await mkdir(join(room,spec.kind));
   for(let index=0;index<spec.count;index++)await writeFile(join(room,spec.kind,index.toString(16).padStart(64,'0')+spec.extension),'Existing '+spec.kind+' '+index);
  }
 }
 return {directory,appRoot,room};
}
function syntheticDriver(room:string,options:{failPartial?:boolean;omitLaterWarning?:boolean}={}){
 const observations:{kind:Kind;damaged:number;offset:number;fact:boolean}[]=[],captures:string[]=[];let receiptId=0;
 const scan=async(kind:Kind,offset:number,fact=false)=>{
  const spec=specifications.find(item=>item.kind===kind)!;const names=(await readdir(join(room,kind))).filter(name=>name.endsWith(spec.extension)).sort();
  const entries:Record<string,unknown>[]=[];let damaged=0;
  for(const name of names){
   const bytes=await readFile(join(room,kind,name));
   if(bytes.length===1&&bytes[0]===0){damaged++;continue;}
   const hash=name.slice(0,-spec.extension.length),label=bytes.toString('utf8');
   entries.push(kind==='models'?{modelHash:hash,name:label,bytes:bytes.length}:kind==='images'?{id:hash,name:label,width:1,height:1,format:'png'}:{id:hash,name:label,seconds:1,sampleRate:24000,channels:1});
  }
  observations.push({kind,damaged,offset,fact});
  if(damaged&&options.failPartial)throw new Error('Synthetic driver failed while fixture exists');
  return {entries,error:damaged?'Synthetic damaged sibling':'',inventory:{ready:true,complete:damaged===0,enumerationComplete:true,issueCount:damaged,error:damaged?'Synthetic damaged sibling':''}};
 };
 const page=(entries:Record<string,unknown>[],error:string,offset:number,size:number)=>({ready:true,error,total:entries.length,next:offset+size<entries.length?offset+size:-1,entries:entries.slice(offset,offset+size)});
 const driver:AssetInventoryDriver={
  invoke:vi.fn<AssetInventoryDriver['invoke']>(async(id,args)=>{
   const kind:Kind=id==='model.library.inspect'?'models':id==='image.import'?'images':'audio';
   const offset=kind==='models'?Number(args.offset):0,reading=await scan(kind,offset);
   return {id:(++receiptId).toString(16).padStart(32,'0'),capability:id,version:1,resources:[],phase:'completed',status:'Synthetic fixture driver',call:{id,version:1,arguments:args},
    output:kind==='models'?{total:reading.entries.length,offset,entries:reading.entries.slice(offset,offset+8),inventory:reading.inventory}:{library:page(reading.entries,reading.error,0,1),inventory:reading.inventory}};
  }),
  fact:vi.fn<AssetInventoryDriver['fact']>(async(id,args)=>{
   const kind:Kind=id==='image.library'?'images':'audio',offset=Number(args.offset),reading=await scan(kind,offset,true);
   return {available:true,value:page(reading.entries,options.omitLaterWarning&&kind==='images'&&reading.error?'':reading.error,offset,2)};
  }),
  capture:vi.fn<NonNullable<AssetInventoryDriver['capture']>>(async(kind,phase)=>{captures.push(kind+':'+phase);}),
 };
 return {driver,observations,captures};
}
it.each(['headless','book'] as const)('places only owned malformed files in the actual room and preserves every healthy page (%s)',async client=>{
 const {directory,appRoot,room}=await fixture(),before=await tree(room),{driver,observations,captures}=syntheticDriver(room);
 await probeAssetInventory(driver,directory,client);
 expect(await tree(room)).toEqual(before);
 expect((await readdir(appRoot)).sort()).toEqual(['room']);
 const evidence=JSON.parse(await readFile(join(directory,client+'-asset-inventory.json'),'utf8'));
 expect(evidence.complete).toBe(true);expect(evidence.cases).toHaveLength(3);
 for(const spec of specifications){
  const row=evidence.cases.find((value:{kind:string})=>value.kind===spec.kind);
  expect(row.beforePages.entries).toHaveLength(spec.count);expect(row.partialPages.entries).toEqual(row.beforePages.entries);expect(row.restoredPages.entries).toEqual(row.beforePages.entries);
  expect(observations.some(value=>value.kind===spec.kind&&value.damaged===1&&value.offset>0)).toBe(true);
  expect(captures).toContain(spec.kind+':partial');expect(captures).toContain(spec.kind+':restored');
  if(spec.kind!=='models')expect(observations).toContainEqual({kind:spec.kind,damaged:1,offset:spec.count,fact:true});
 }
});
it.each(['missing original room','selected generation'])('refuses %s before invoking a driver or writing fixtures',async kind=>{
 const {directory,appRoot,room}=await fixture(kind!=='missing original room');
 if(kind==='selected generation'){const generations=join(appRoot,'workspace-generations.v1');await mkdir(generations);await writeFile(join(generations,'current.v1.json'),'{}');}
 const before=await tree(directory),{driver}=syntheticDriver(room);
 await expect(probeAssetInventory(driver,directory,'headless')).rejects.toThrow();
 expect(driver.invoke).not.toHaveBeenCalled();expect(driver.fact).not.toHaveBeenCalled();expect(await tree(directory)).toEqual(before);
});
it('removes its malformed file after a driver failure without changing existing room assets',async()=>{
 const {directory,room}=await fixture(),before=await tree(room),{driver}=syntheticDriver(room,{failPartial:true});
 await expect(probeAssetInventory(driver,directory,'headless')).rejects.toThrow('Synthetic driver failed while fixture exists');
 expect(await tree(room)).toEqual(before);expect(await readdir(directory)).not.toContain('headless-asset-inventory.json');
});
it('refuses a later page that drops the partial warning and still removes its malformed file',async()=>{
 const {directory,room}=await fixture(),before=await tree(room),{driver}=syntheticDriver(room,{omitLaterWarning:true});
 await expect(probeAssetInventory(driver,directory,'headless')).rejects.toThrow();
 expect(await tree(room)).toEqual(before);
 const evidence=JSON.parse(await readFile(join(directory,'headless-asset-inventory.json'),'utf8'));
 expect(evidence.complete).not.toBe(true);expect(evidence.cases.map((value:{kind:string})=>value.kind)).toEqual(['models']);
});