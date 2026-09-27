// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface ObjectPhysicsSettings { mode:'fixed'|'solid'|'bouncy'; mass:number; shape:'automatic'|'box'|'sphere' }
export interface AvatarMovementSettings { distance:number; speed:number }
export interface PhysicsObservation { ready:boolean; running:boolean; status:string }
export interface AvatarMovementObservation { active:boolean; mode:'look'|'follow'|'manual'|'stopped'; status:string; canLook:boolean; canFollow:boolean; lookReason:string; followReason:string; distance:number; speed:number }
import {roomControlFields} from './prompts/roomcontrols';
export {roomControlFields} from './prompts/roomcontrols';
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const exact=(v:Record<string,unknown>,keys:string[])=>Object.keys(v).length===keys.length&&keys.every(key=>Object.prototype.hasOwnProperty.call(v,key));
const between=(v:unknown,min:number,max:number)=>typeof v==='number'&&Number.isFinite(v)&&v>=min&&v<=max;
export const validObjectPhysics=(v:unknown):v is ObjectPhysicsSettings=>record(v)&&exact(v,['mode','mass','shape'])&&['fixed','solid','bouncy'].includes(v.mode as string)&&['automatic','box','sphere'].includes(v.shape as string)&&between(v.mass,.05,20);
export const validAvatarMovement=(v:unknown):v is AvatarMovementSettings=>record(v)&&exact(v,['distance','speed'])&&between(v.distance,.8,2.5)&&between(v.speed,.2,1.2);
export function validRoomControl(c:Record<string,unknown>):boolean {
  switch(c.action) {
    case 'physicsSettings':return typeof c.target==='string'&&/^[a-zA-Z0-9_]{1,32}$/.test(c.target)&&!['book','maestro'].includes(c.target)&&validObjectPhysics(c.physics);
    case 'avatarSettings':return c.target==='maestro'&&validAvatarMovement(c.movement);
    case 'physicsRun':return c.operation==='start'||c.operation==='pause';
    case 'avatarMotion':return c.target==='maestro'&&['look','follow','stop'].includes(c.operation as string);
    default:return false;
  }
}
export function requireRoomCapabilities(commands:{action:string;rule?:unknown}[],scene:{capabilities?:string[]}) {
  for(const command of commands) {
    if(Object.prototype.hasOwnProperty.call(roomControlFields,command.action)&&!scene.capabilities?.includes(command.action+'.v1'))
      throw new Error('This room does not support '+command.action+'. Update or connect a compatible native app.');
    if(command.action==='rules'&&record(command.rule)&&Array.isArray(command.rule.edits)&&command.rule.edits.some(e=>record(e)&&record(e.sequence)&&e.sequence.program)&&!scene.capabilities?.includes('behaviourPrograms.v1'))
      throw new Error('This room does not support behaviour programs. Update or connect a compatible native app.');
  }
}
const text=(v:unknown)=>typeof v==='string'&&v.length<=2048;
export const validPhysicsObservation=(v:unknown):v is PhysicsObservation=>record(v)&&typeof v.ready==='boolean'&&typeof v.running==='boolean'&&(!v.running||v.ready)&&text(v.status);
export const validAvatarObservation=(v:unknown):v is AvatarMovementObservation=>record(v)&&['active','canLook','canFollow'].every(key=>typeof v[key]==='boolean')&&['look','follow','manual','stopped'].includes(v.mode as string)&&v.active===(v.mode!=='stopped')&&['status','lookReason','followReason'].every(key=>text(v[key]))&&between(v.distance,.8,2.5)&&between(v.speed,.2,1.2);
