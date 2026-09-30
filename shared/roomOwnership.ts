// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface RoomOwner {id:string;label:string;role:'ambient'|'program'|'reflex'|'control'|'grab';allowsGrab:boolean;claims:{target:string;channel:string}[]}
export interface RoomOwnershipView {suspended:boolean;error:string;owners:RoomOwner[]}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const text=(v:unknown,max:number):v is string=>typeof v==='string'&&v.length>0&&v.length<=max&&!/[\u0000-\u001f]/.test(v);
const keys=(v:Record<string,unknown>,names:string[])=>Object.keys(v).length===names.length&&names.every(k=>Object.prototype.hasOwnProperty.call(v,k));
export function validRoomOwnership(v:unknown):v is RoomOwnershipView {
 if(!record(v)||!keys(v,['suspended','error','owners'])||typeof v.suspended!=='boolean'||typeof v.error!=='string'||v.error.length>512||!Array.isArray(v.owners)||v.owners.length>64)return false;
 const ids=new Set<string>();
 for(const owner of v.owners){
  if(!record(owner)||!keys(owner,['id','label','role','allowsGrab','claims'])||!text(owner.id,128)||!text(owner.label,120)||!['ambient','program','reflex','control','grab'].includes(owner.role as string)||typeof owner.allowsGrab!=='boolean'||owner.allowsGrab&&owner.role!=='control'||!Array.isArray(owner.claims)||owner.claims.length>128||ids.has(owner.id))return false;
  ids.add(owner.id);const claims=new Set<string>();
  for(const claim of owner.claims){
   if(!record(claim)||!keys(claim,['target','channel'])||!text(claim.target,128)||!text(claim.channel,64))return false;
   const key=JSON.stringify([claim.target,claim.channel]);if(claims.has(key))return false;claims.add(key);
  }
 }
 return !v.suspended||v.owners.length===0;
}
