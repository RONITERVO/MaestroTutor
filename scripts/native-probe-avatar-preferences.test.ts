// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {behaviourFact} from '../shared/behaviourCatalog';
import {readAvatarMovementSettings} from './native-probe-avatar-preferences';
function fixture(patch?:(s:RoomAgentState)=>void){
 const receipts:RoomAgentState[]=[],requests:RoomCommand[][]=[];
 const execute=async(commands:RoomCommand[])=>{
  requests.push(commands);
  const state:RoomAgentState={version:1,session:'native',revision:1,sceneRevision:1,ack:1,ok:true,status:'Read',created:[],objects:[],physicsRunning:false,canUndo:false,canRedo:false,
   catalog:{operation:'inspect',category:'facts',capability:'avatar.movement.settings',version:1,available:true,definition:behaviourFact('avatar.movement.settings')!,status:'Read',
    value:{revision:1,distance:1.5,speed:.8,temporary:false,live:{active:false,mode:'stopped',status:'Ready',canLook:true,lookReason:'',canFollow:true,followReason:''}}}};
  patch?.(state);receipts.push(state);return state;
 };return {execute,receipts,requests};
}
it('reads saved movement preferences from an explicit native fact with no periodic objects',async()=>{
 const f=fixture();expect(await readAvatarMovementSettings(f.execute)).toEqual({distance:1.5,speed:.8});
 expect(f.requests).toEqual([[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'avatar.movement.settings',version:1}}]]);
 expect(f.receipts[0].objects).toEqual([]);
});
it('rejects unavailable, wrong, invalid and mismatched temporary-room facts',async()=>{
 for(const change of ['failed','unavailable','foreign','arguments','missing','distance','speed','temporary','pending']){
  const f=fixture(s=>{const fact=s.catalog!;if(fact.operation!=='inspect'||fact.category!=='facts')throw new Error('fixture');
   const value=fact.value as Record<string,unknown>;
   if(change==='failed')s.ok=false;
   if(change==='unavailable')fact.available=false;
   if(change==='foreign')fact.capability='physics.running';
   if(change==='arguments')fact.arguments={target:'other'};
   if(change==='missing')delete value.speed;
   if(change==='distance')value.distance=NaN;
   if(change==='speed')value.speed=12;
   if(change==='temporary')value.temporary=true;
   if(change==='pending')s.temporaryRoom={active:false,pending:true,id:'a'.repeat(32),saveId:'',phase:'pending',error:'',savedRevision:1};
  });await expect(readAvatarMovementSettings(f.execute),change).rejects.toThrow();
 }
});
