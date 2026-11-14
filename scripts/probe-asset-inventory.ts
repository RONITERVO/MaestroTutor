// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {access,lstat,mkdir,open,readFile,unlink,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
type Receipt=NonNullable<NonNullable<RoomAgentState['execution']>['selected']>;
type Summary={ready:boolean;complete:boolean;enumerationComplete:boolean;issueCount:number;error:string};
type Page={ready?:boolean;error?:string;total:number;next?:number;offset?:number;entries:unknown[]};
export interface AssetInventoryDriver {
 invoke(id:string,args:Record<string,unknown>):Promise<Receipt>;
 fact(id:string,args:Record<string,unknown>):Promise<{available:boolean;value?:unknown}>;
 capture?(kind:string,phase:'partial'|'restored'):Promise<void>;
}
const kinds=[{kind:'models',extension:'.glb',action:'model.library.inspect',args:{offset:0}},
 {kind:'images',extension:'.image',action:'image.import',args:{operation:'refresh'}},
 {kind:'audio',extension:'.wav',action:'audio.import',args:{operation:'refresh'}}] as const;
const comparable=(page:Page)=>{const {error:_,...value}=page;return value;};
/** Deliberate corruption is confined to newly created files in this fresh, owned
 * offline probe workspace. Native discovery uses ordinary user/agent actions;
 * there is no replacement response, automatic repair or edit to existing data. */
export async function probeAssetInventory(driver:AssetInventoryDriver,directory:string,client:'headless'|'book'){
 directory=resolve(directory);
 const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8'));
 const ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert.equal(owner.version,1);assert.match(owner.id,/^[a-f0-9]{32}$/);assert.equal(ready.id,owner.id);
 assert.equal(resolve(ready.workspaceDirectory),join(directory,'workspace'));assert.ok(!ready.restartOf);
 assert.ok(!owner.providerUsed&&!owner.providerScenario&&!owner.resumedFrom&&!owner.nativeRestart,'Corruption fixture requires a fresh offline workspace');
 const appRoot=join(directory,'workspace'),workspace=join(appRoot,'room');
 // The probe ready receipt names the app storage root. Asset libraries belong
 // to its selected room, not beside it. This fixture requires the original
 // fresh room and refuses a later generation instead of guessing its location.
 let hasSelection=true;try{await access(join(appRoot,'workspace-generations.v1','current.v1.json'));}catch(error){if((error as NodeJS.ErrnoException).code==='ENOENT')hasSelection=false;else throw error;}
 assert.equal(hasSelection,false,'Inventory corruption fixture requires the original room, before generation switching');
 for(const path of [directory,appRoot,workspace]){const info=await lstat(path);assert.ok(info.isDirectory()&&!info.isSymbolicLink(),'Probe root must be an ordinary owned directory');}
 const evidence:{client:string;boundary:string;cases:unknown[];complete?:boolean}={client,boundary:'Actual desktop Unity ordinary inventory actions and facts, with one explicitly malformed synthetic file per asset library in this fresh owned workspace. Existing files remain untouched. Book uses visible generated controls. No real provider, headset or quota/reclamation proof.',cases:[]};
 const checkpoint=()=>writeFile(join(directory,client+'-asset-inventory.json'),JSON.stringify(evidence,null,2));
 const invoke=async(id:string,args:Record<string,unknown>)=>{
  const receipt=await driver.invoke(id,args);assert.equal(receipt.capability,id);assert.equal(receipt.phase,'completed',receipt.status);
  const inventory=receipt.output?.inventory as Summary|undefined;assert.ok(inventory,'New inventory result omitted its completeness summary');
  const page=(id==='model.library.inspect'?{total:receipt.output!.total,offset:receipt.output!.offset,entries:receipt.output!.entries}:receipt.output!.library) as Page;
  assert.ok(Number.isInteger(page.total)&&page.total>=0&&Array.isArray(page.entries));return {receipt,inventory,page};
 };
 const collect=async(item:typeof kinds[number],first:Awaited<ReturnType<typeof invoke>>,partial:boolean)=>{
  const pages:unknown[]=[first.receipt],entries=[...first.page.entries];let offset=item.kind==='models'?entries.length:(first.page.next??-1);
  for(let count=0;offset>=0&&offset<first.page.total;count++){
   assert.ok(count<32,'Inventory pagination exceeded its library bound');assert.ok(offset>0);
   let page:Page;
   if(item.kind==='models'){
    const result=await invoke(item.action,{offset});assert.deepEqual(result.inventory,first.inventory);page=result.page;pages.push(result.receipt);
   }else{
    const result=await driver.fact(item.kind==='images'?'image.library':'audio.library',{offset});assert.equal(result.available,true);page=result.value as Page;pages.push(result);
    assert.equal(page.ready,true);assert.equal(Boolean(page.error),partial);if(partial)await driver.capture?.(item.kind+'-page-'+offset,'partial');
   }
   assert.equal(page.total,first.page.total);assert.ok(page.entries.length>0);entries.push(...page.entries);assert.ok(entries.length<=first.page.total);
   offset=item.kind==='models'?entries.length:(page.next??-1);
  }
  assert.equal(entries.length,first.page.total,'Inventory paging lost healthy entries');
  // End pages also keep the warning; a zero-length slice is not a complete-empty claim.
  if(item.kind!=='models'){
   const result=await driver.fact(item.kind==='images'?'image.library':'audio.library',{offset:first.page.total});assert.equal(result.available,true);const page=result.value as Page;
   assert.equal(page.ready,true);assert.equal(Boolean(page.error),partial);assert.deepEqual(page.entries,[]);assert.equal(page.next,-1);assert.equal(page.total,first.page.total);pages.push(result);
   if(partial)await driver.capture?.(item.kind+'-end-page','partial');
  }
  return {entries,pages};
 };
 for(const item of kinds){
  const before=await invoke(item.action,item.args);assert.deepEqual(before.inventory,{ready:true,complete:true,enumerationComplete:true,issueCount:0,error:''});
  if(client==='book')assert.ok(before.page.total>0,'Book fixture must have a healthy '+item.kind+' entry before corruption');
  const beforePages=await collect(item,before,false);
  const folder=join(workspace,item.kind);await mkdir(folder,{recursive:true});const info=await lstat(folder);assert.ok(info.isDirectory()&&!info.isSymbolicLink());
  const hash=createHash('sha256').update('Maestro explicit inventory corruption '+owner.id+' '+item.kind).digest('hex');
  const path=join(folder,hash+item.extension),bytes=Buffer.from([0]);
  const handle=await open(path,'wx');try{await handle.writeFile(bytes);await handle.sync();}finally{await handle.close();}
  try{
   const partial=await invoke(item.action,item.args);
   assert.equal(partial.inventory.ready,true);assert.equal(partial.inventory.complete,false);assert.equal(partial.inventory.enumerationComplete,true);assert.equal(partial.inventory.issueCount,1);assert.ok(partial.inventory.error.length>0);
   assert.deepEqual(comparable(partial.page),comparable(before.page),'Damaged sibling changed healthy inventory identities');
   if(item.kind!=='models'){assert.equal(partial.page.ready,true);assert.ok(partial.page.error);}
   await driver.capture?.(item.kind,'partial');
   const partialPages=await collect(item,partial,true);assert.deepEqual(partialPages.entries,beforePages.entries,'Damaged sibling changed later healthy inventory pages');
   assert.deepEqual(await readFile(path),bytes,'A passive inventory modified the malformed fixture');
   evidence.cases.push({kind:item.kind,before,beforePages,partial,partialPages});await checkpoint();
  }finally{
   const current=await lstat(path);assert.ok(current.isFile()&&!current.isSymbolicLink());assert.deepEqual(await readFile(path),bytes,'Only this probe’s unchanged malformed file may be removed');await unlink(path);
  }
  const restored=await invoke(item.action,item.args);assert.deepEqual(restored.inventory,before.inventory);assert.deepEqual(comparable(restored.page),comparable(before.page));
  await driver.capture?.(item.kind,'restored');const restoredPages=await collect(item,restored,false);assert.deepEqual(restoredPages.entries,beforePages.entries);
  Object.assign(evidence.cases.at(-1) as Record<string,unknown>,{restored,restoredPages});await checkpoint();
 }
 evidence.complete=true;await checkpoint();
}
