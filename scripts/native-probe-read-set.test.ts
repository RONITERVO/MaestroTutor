// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import type {DataValue} from '../shared/programValues';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {behaviourFact} from '../shared/behaviourCatalog';
import {readNativeObjects} from './native-probe-read-set';
import {assertSameRoomObjects} from './agent-provider-contract';
const target='a'.repeat(32),worldId='b'.repeat(32);
const physics={mode:'bouncy' as const,shape:'sphere' as const,mass:2};
const observed=(id:string)=>({id,objectRevision:3,name:id==='book'?'Book':'Ball',kind:id==='book'?'Book':'Ball',position:{x:1,y:1,z:0},scale:.5,color:{r:0,g:0,b:1,a:1},animated:false,held:false,simulating:false,runtimeState:'active',positionSource:'live'});
function fixture(change?:(s:RoomAgentState,c:RoomCommand,n:number)=>void){
 let n=0;const receipts:RoomAgentState[]=[];
 const execute=async(commands:RoomCommand[])=>{
  const c=commands[0],s:RoomAgentState={version:1,session:'c'.repeat(32),revision:++n,sceneRevision:3,ack:n,ok:true,status:'Read',created:[],objects:[],physicsRunning:false,canUndo:false,canRedo:false};
  if(c.action==='inspect')s.inspection={id:'book',objectRevision:3,recipe:null,item:{...observed('book'),runtimeState:'active',positionSource:'live',physics:{mode:'fixed',shape:'automatic',mass:1}}};
  else if(c.catalog?.operation==='inspect'){
   const q=c.catalog,id=q.capability,value=id==='world.objects'?{worldId,offset:0,total:2,next:'',entries:[{id:target,name:'Ball',kind:'Ball'},{id:'book',name:'Book',kind:'Book'}]}:id==='object.observation'?observed(String(q.arguments?.target)):{target,revision:3,temporary:false,...physics};
   s.catalog={operation:'inspect',category:'facts',capability:id,version:1,arguments:q.arguments,available:true,definition:behaviourFact(id)!,value:value as DataValue,status:'Read'};
  }
  change?.(s,c,n);receipts.push(s);return s;
 };return {execute,receipts};
}
it('reads exact settings including built-ins without hydrating periodic objects',async()=>{
 const f=fixture(),set=await readNativeObjects(f.execute);expect(set.source).toBe('explicit-native-reads');expect(set.worldId).toBe(worldId);
 expect(set.objects[0].physics).toEqual(physics);expect(set.objects[0].physicsEvidence).toBe('component-fact');
 expect(set.objects[1].physics).toEqual({mode:'fixed',shape:'automatic',mass:1});expect(set.objects[1].physicsEvidence).toBe('native-inspection');
 expect(f.receipts.every(s=>s.objects.length===0)).toBe(true);
 const changed=structuredClone(set);changed.objects[0].physics.mass=3;expect(()=>assertSameRoomObjects(set,changed)).toThrow(/unrelated/);
});
it('rejects changed sessions, saved revisions and world membership',async()=>{
 for(const field of ['session','sceneRevision','world']){
  const f=fixture((s,c,n)=>{if(n<2)return;if(field==='session')s.session='d'.repeat(32);else if(field==='sceneRevision')s.sceneRevision++;else if(c.catalog?.operation==='inspect'&&c.catalog.capability==='world.objects'&&s.catalog?.operation==='inspect'&&s.catalog.category==='facts')(s.catalog.value as {worldId:string}).worldId='d'.repeat(32);});
  await expect(readNativeObjects(f.execute)).rejects.toThrow(/changed/);
 }
});
it('never drops unavailable, foreign, mismatched-revision or invalid physics',async()=>{
 for(const patch of [{revision:4},{target:'d'.repeat(32)},{temporary:true},{mass:NaN},null] as Array<Record<string,DataValue>|null>){
  const f=fixture(s=>{const v=s.catalog;if(v?.operation==='inspect'&&v.category==='facts'&&v.capability==='object.physics.settings'){if(patch===null){v.available=false;v.value=null;}else v.value={...(v.value as object),...patch} as DataValue;}});
  await expect(readNativeObjects(f.execute)).rejects.toThrow();
 }
});
it('requires actual built-in physics instead of inventing defaults',async()=>{
 const f=fixture(s=>{if(s.inspection?.item)delete s.inspection.item.physics;});await expect(readNativeObjects(f.execute)).rejects.toThrow(/built-in/);
});
