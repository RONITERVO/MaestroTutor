// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface ActivityChoice { motionId:string; weight:number; speed:number; cooldown:number; loop:boolean }
export type ActivityEdit = {operation:'assign';role:number;choice:ActivityChoice}|{operation:'remove';role:number;motionId:string}|{operation:'clear';role:number};
export type AvatarActivityRequest = {modelHash:string;revision:number}&({operation:'edit';edits:ActivityEdit[]}|{operation:'undo'|'redo'});
export interface ActivityProfile {
  modelHash:string; revision?:number; status:string; canAssign:boolean; readOnly:boolean; canUndo:boolean; canRedo:boolean;
  roles:{role:number;choices:(ActivityChoice&{name:string;available:boolean})[]}[];
}
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const exact=(v:Record<string,unknown>,keys:string[])=>Object.keys(v).length===keys.length&&keys.every(k=>Object.prototype.hasOwnProperty.call(v,k));
const range=(v:unknown,min:number,max:number)=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
const integer=(v:unknown,min:number,max:number)=>Number.isInteger(v)&&range(v,min,max);
const hash=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{64}$/.test(v);
const id=(v:unknown)=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
const choice=(v:unknown):v is ActivityChoice=>record(v)&&id(v.motionId)&&integer(v.weight,1,10)&&range(v.speed,.25,2)&&range(v.cooldown,0,60)&&typeof v.loop==='boolean';
export function validAvatarActivityRequest(v:unknown):v is AvatarActivityRequest {
 if(!record(v)||!hash(v.modelHash)||!integer(v.revision,1,2147483647))return false;
 if(v.operation==='undo'||v.operation==='redo')return exact(v,['operation','modelHash','revision']);
 return v.operation==='edit'&&exact(v,['operation','modelHash','revision','edits'])&&Array.isArray(v.edits)&&v.edits.length>0&&v.edits.length<=16&&v.edits.every(e=>record(e)&&integer(e.role,0,3)&&(
  e.operation==='clear'?exact(e,['operation','role']):e.operation==='remove'?exact(e,['operation','role','motionId'])&&id(e.motionId):
  e.operation==='assign'&&exact(e,['operation','role','choice'])&&choice(e.choice)&&exact(e.choice as unknown as Record<string,unknown>,['motionId','weight','speed','cooldown','loop'])));
}
export function validActivityProfile(v:unknown,requireRevision=false):v is ActivityProfile {
 return record(v)&&(v.modelHash===''||hash(v.modelHash))&&(v.revision===undefined&&!requireRevision||integer(v.revision,1,2147483647))&&typeof v.status==='string'&&v.status.length<=2048&&
 ['canAssign','readOnly','canUndo','canRedo'].every(k=>typeof v[k]==='boolean')&&Array.isArray(v.roles)&&v.roles.length===4&&v.roles.every((r,i)=>record(r)&&r.role===i&&Array.isArray(r.choices)&&r.choices.length<=4&&
 r.choices.every(c=>choice(c)&&record(c)&&typeof c.name==='string'&&c.name.length<=100&&typeof c.available==='boolean')&&new Set(r.choices.map(c=>c.motionId)).size===r.choices.length);
}
