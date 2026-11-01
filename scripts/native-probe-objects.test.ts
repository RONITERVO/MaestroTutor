// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it,vi} from 'vitest';
import {nativeProbeObjects} from './native-probe-objects';
import {behaviourFact} from '../shared/behaviourCatalog';
import {roomRevisionBasis,readRoomObjectObservation} from '../src/core-sdk/room/roomObjectContext';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import type {CatalogView} from '../shared/roomCatalog';
import fixture from '../test-fixtures/browser/objectReferences.json';
const target='a'.repeat(32);
const state=(catalog?:CatalogView):RoomAgentState=>({version:1,session:'1'.repeat(32),revision:1,sceneRevision:1,ack:1,ok:true,status:'Ready',created:[],canUndo:false,canRedo:false,physicsRunning:false,objects:[],catalog});
const sample=()=>({id:target,objectRevision:7,name:'Apple',kind:'Ball',position:{x:1,y:2,z:3},scale:1,color:{r:1,g:0,b:0,a:1},animated:false,held:false,simulating:false,runtimeState:'unavailable',positionSource:'saved'});
const fact=(id:string,value:unknown):CatalogView=>({operation:'inspect',category:'facts',capability:id,version:1,arguments:{target},available:true,definition:behaviourFact(id)!,value,status:'Read'});
const pages=fixture.pages.map(p=>p.view as unknown as CatalogView);
it('reads saved directory pages with an empty periodic view and never hydrates it',async()=>{
 const receipts=pages.map(state),execute=vi.fn().mockResolvedValueOnce(receipts[0]).mockResolvedValueOnce(receipts[1]);
 const ids=await nativeProbeObjects(execute).ids();expect(ids).toEqual(fixture.pages.flatMap(p=>p.view.value.entries.map(e=>e.id)));
 expect(execute.mock.calls[1][0][0].catalog.arguments.cursor).toBe(fixture.pages[0].view.value.next);
 expect(receipts.every(r=>r.objects.length===0)).toBe(true);
});
it('counts saved objects using the native total rather than a page length',async()=>{
 const execute=vi.fn().mockResolvedValue(state(pages[0]));expect(await nativeProbeObjects(execute).count()).toBe(fixture.pages[0].view.value.total);expect(execute).toHaveBeenCalledTimes(1);
});
it('rejects changed or unavailable directory continuations without claiming deletion',async()=>{
 for(const change of ['world','offset','unavailable']){
  const next=structuredClone(pages[1]);if(next.operation!=='inspect'||next.category!=='facts')throw new Error('Bad fixture');
  if(change==='unavailable'){next.available=false;next.value=null;}
  else (next.value as Record<string,unknown>)[change==='world'?'worldId':'offset']=change==='world'?'e'.repeat(32):0;
  const execute=vi.fn().mockResolvedValueOnce(state(pages[0])).mockResolvedValueOnce(state(next));await expect(nativeProbeObjects(execute).ids()).rejects.toThrow(/changed|unavailable/);
 }
});
it('reads exact dormant metadata without selecting or synthesizing periodic objects',async()=>{
 const receipt=state(fact('object.observation',sample())),execute=vi.fn().mockResolvedValue(receipt);
 expect(await nativeProbeObjects(execute).observation(target)).toEqual(sample());expect(receipt.objects).toEqual([]);
 expect(execute.mock.calls[0][0]).toEqual([{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.observation',version:1,arguments:{target}}}]);
});
it('rejects mismatched and malformed exact observations',async()=>{
 for(const patch of [{id:'b'.repeat(32)},{runtimeState:'active',positionSource:'unknown'},{objectRevision:0},{simulating:'yes'},{position:{x:NaN,y:0,z:0}}]){
  const receipt=state(fact('object.observation',{...sample(),...patch}));await expect(nativeProbeObjects(vi.fn().mockResolvedValue(receipt)).observation(target)).rejects.toThrow();
 }
 const wrong=fact('object.observation',sample());if(wrong.operation!=='inspect'||wrong.category!=='facts')throw new Error();wrong.arguments={target:'b'.repeat(32)};expect(readRoomObjectObservation(wrong,target)).toBeNull();
});
it('distinguishes explicitly missing saved objects from dormant ones and unavailable reads',async()=>{
 for(const saved of [true,false]){
  const receipt=state(fact('object.presence',{target,revision:saved?7:0,saved,nativeInstance:false,active:false,state:saved?'unavailable':'missing'}));expect(await nativeProbeObjects(vi.fn().mockResolvedValue(receipt)).exists(target)).toBe(saved);
 }
 const view=fact('object.presence',null);if(view.operation!=='inspect'||view.category!=='facts')throw new Error();view.available=false;
 await expect(nativeProbeObjects(vi.fn().mockResolvedValue(state(view))).exists(target)).rejects.toThrow(/presence/);
});
it('retains only valid scoped object-observation evidence for subsequent guarded edits',()=>{
 const observed=state(fact('object.observation',sample()));expect(roomRevisionBasis(state(),[observed])).toEqual([{id:target,objectRevision:7}]);
 const unavailable=structuredClone(observed);if(unavailable.catalog?.operation!=='inspect'||unavailable.catalog.category!=='facts')throw new Error();unavailable.catalog.available=false;unavailable.catalog.value=null;
 expect(roomRevisionBasis(unavailable,[observed])).toEqual([]);
 expect(()=>roomRevisionBasis({...state(),session:'2'.repeat(32)},[observed])).toThrow(/session/);
});
