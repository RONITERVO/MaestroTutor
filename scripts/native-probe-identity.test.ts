// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it,vi} from 'vitest';
import {readNativeObjectIdentity,validNativeObjectIdentity,assertResumedNativeIdentity,readLearnerResumeReceipt,isNativeIdentityRead} from './native-probe-identity';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import type {CatalogView} from '../shared/roomCatalog';
import fixture from '../test-fixtures/browser/objectReferences.json';
const session='1'.repeat(32);
const pages=fixture.pages.map(p=>p.view as unknown as CatalogView);
function receipts(){return [...pages,...pages].map(catalog=>({version:1,session,revision:4,sceneRevision:4,ack:1,ok:true,status:'Ready',created:[],canUndo:false,canRedo:false,physicsRunning:false,workspaceReady:true,objects:[],catalog:structuredClone(catalog)}) as RoomAgentState);}
async function identity(){const values=receipts(),execute=vi.fn();for(const value of values)execute.mockResolvedValueOnce(value);return {value:await readNativeObjectIdentity(execute),values,execute};}
it('captures both complete directory passes without hydrating periodic receipts',async()=>{
 const {value,values,execute}=await identity();expect(validNativeObjectIdentity(value)).toBe(true);expect(value.ids).toEqual(fixture.pages.flatMap(p=>p.view.value.entries.map(e=>e.id)));
 expect(value.worldId).toBe(fixture.pages[0].view.value.worldId);expect(value.sceneRevision).toBe(4);expect(execute).toHaveBeenCalledTimes(4);
 expect(values.every(v=>v.objects.length===0)).toBe(true);
});
it.each(['session','world','unavailable','temporary','pending','workspace'] as const)('refuses %s changes instead of claiming a complete saved world',async change=>{
 const values=receipts(),bad=values[2];
 if(change==='session')bad.session='2'.repeat(32);
 if(change==='workspace')bad.workspaceReady=false;
 if(change==='temporary'||change==='pending')bad.temporaryRoom={active:change==='temporary',pending:change==='pending'} as RoomAgentState['temporaryRoom'];
 if(bad.catalog?.operation==='inspect'&&bad.catalog.category==='facts'){
  if(change==='world')(bad.catalog.value as {worldId:string}).worldId='3'.repeat(32);
  if(change==='unavailable')bad.catalog.available=false;
 }
 const execute=vi.fn();for(const value of values)execute.mockResolvedValueOnce(value);await expect(readNativeObjectIdentity(execute)).rejects.toThrow();
});
it('permits advancing pose revisions when saved membership stays unchanged',async()=>{
 const values=receipts(),execute=vi.fn();values.forEach((value,i)=>{value.sceneRevision=4+i;execute.mockResolvedValueOnce(value);});
 const result=await readNativeObjectIdentity(execute);expect(result.sceneRevision).toBe(7);expect(result.ids).toHaveLength(5);
});
it('checks saved identity across a new native session without requiring equal runtime revisions',async()=>{
 const {value}=await identity(),restored={...value,session:'2'.repeat(32),sceneRevision:1};expect(()=>assertResumedNativeIdentity(value,restored)).not.toThrow();
 expect(()=>assertResumedNativeIdentity(value,value)).toThrow(/new native session/);
 expect(()=>assertResumedNativeIdentity(value,{...restored,worldId:'3'.repeat(32)})).toThrow(/world identity/);
 expect(()=>assertResumedNativeIdentity(value,{...restored,ids:restored.ids.filter(id=>id!=='book')})).toThrow();
 const changed=[...restored.ids];changed[0]='0'.repeat(32);expect(()=>assertResumedNativeIdentity(value,{...restored,ids:changed})).toThrow(/exact original/);
});
it('refuses inferred, incomplete, duplicated and malformed identity receipts',async()=>{
 const {value}=await identity();
 for(const invalid of [{...value,source:'periodic'},{...value,ids:[]},{...value,ids:[...value.ids,value.ids[0]]},{...value,sceneRevision:-1},{...value,ids:[...value.ids].reverse()},{...value,extra:true}])expect(validNativeObjectIdentity(invalid)).toBe(false);
 const receipt={version:1,sourceRun:'4'.repeat(32),accessMode:'byok',nativeIdentity:value,profileSha256:'a'.repeat(64),boundary:'Closed native run'};
 expect(readLearnerResumeReceipt(receipt)).toEqual(receipt);
 for(const patch of [{nativeIdentity:undefined,nativeObjectIds:value.ids},{sourceRun:'../other'},{profileSha256:'short'},{accessMode:'unknown'}])expect(()=>readLearnerResumeReceipt({...receipt,...patch})).toThrow();
});

it('permits only saved-directory reads in an identity-only native run',()=>{
 expect(isNativeIdentityRead({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'world.objects',version:1,arguments:{query:'',kind:'all',cursor:''}}})).toBe(true);
 expect(isNativeIdentityRead({action:'execution',execution:{operation:'start',call:{id:'audio.source.edit',version:1,arguments:{}}}})).toBe(false);
 expect(isNativeIdentityRead({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:'book'}}})).toBe(false);
 expect(isNativeIdentityRead({action:'undo'})).toBe(false);
});
