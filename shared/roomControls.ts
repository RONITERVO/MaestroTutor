// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
export interface ObjectPhysicsSettings { mode:'fixed'|'solid'|'bouncy'; mass:number; shape:'automatic'|'box'|'sphere' }
export interface AvatarMovementSettings { distance:number; speed:number }
export interface AvatarWalkObservation {source:'included'|'embedded'|'library';motionId:string;modelHash:string;clipIndex:number;name:string;available:boolean;status:string;playbackStatus:string}
export interface PhysicsObservation { ready:boolean; running:boolean; status:string }
export interface AvatarMovementObservation { active:boolean; mode:'look'|'follow'|'manual'|'stopped'; status:string; canLook:boolean; canFollow:boolean; lookReason:string; followReason:string; distance:number; speed:number }
import {behaviourEvent} from './behaviourEvents';
import {capabilityDefinition,capabilityInput} from './capabilities';
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
    case 'avatarWalk':return c.target==='maestro'&&typeof c.motionId==='string'&&(c.motionId===''||/^[a-f0-9]{32}$/.test(c.motionId));
    case 'avatarSettings':return c.target==='maestro'&&validAvatarMovement(c.movement);
    case 'physicsRun':return c.operation==='start'||c.operation==='pause';
    case 'avatarMotion':return c.target==='maestro'&&['look','follow','stop'].includes(c.operation as string);
    default:return false;
  }
}
export function requireRoomCapabilities(commands:{action:string;rule?:unknown;execution?:unknown}[],scene:{capabilities?:string[]}) {
  const hasResults=(value:unknown):boolean=>Array.isArray(value)?value.some(hasResults):record(value)?value.op==='invoke'&&(value.results!==undefined||Object.keys(capabilityDefinition(String(value.capability))?.output?.properties??{}).length>0)||Object.values(value).some(hasResults):false;
  const hasRecipe=(value:unknown):boolean=>Array.isArray(value)?value.some(hasRecipe):record(value)?value.op==='invoke'&&record(value.arguments)&&capabilityInput(String(value.capability),value.arguments)?.['x-features']?.includes('recipeCreation.v1')===true||Object.values(value).some(hasRecipe):false;
  const hasEdit=(value:unknown):boolean=>Array.isArray(value)?value.some(hasEdit):record(value)?value.op==='invoke'&&['object.position.set','object.scale.set','object.color.set','object.delete'].includes(String(value.capability))||Object.values(value).some(hasEdit):false;
  const needs=(value:unknown,features:Set<string>)=>{
    if(Array.isArray(value)){value.forEach(x=>needs(x,features));return;}
    if(!record(value))return;
    if(value.op==='awaitEvent'){
      if(value.fields!==undefined)features.add('eventFields.v1');
      for(const feature of behaviourEvent(String(value.event))?.features??[])features.add(feature);
    }
    const capability=value.op==='invoke'?value.capability:value.id;
    if(typeof capability==='string'&&record(value.arguments))for(const feature of capabilityInput(capability,value.arguments)?.['x-features']??[])features.add(feature);
    Object.values(value).forEach(x=>needs(x,features));
  };
  for(const command of commands) {
    if(command.action==='execution'&&record(command.execution)&&command.execution.operation==='recover'&&!scene.capabilities?.includes('actionRecovery.v1'))throw new Error('Update the native app to recover action history.');
    if(command.action==='rules'&&record(command.rule)&&Array.isArray(command.rule.edits)&&command.rule.edits.some(e=>record(e)&&record(e.sequence)&&typeof e.sequence.program==='string'&&hasEdit(JSON.parse(e.sequence.program)))&&!scene.capabilities?.includes('objectEdits.v1'))
      throw new Error('Update the native app to edit objects in programs.');
    if(command.action==='rules'&&record(command.rule)&&Array.isArray(command.rule.edits)&&command.rule.edits.some(e=>record(e)&&record(e.sequence)&&typeof e.sequence.program==='string'&&hasRecipe(JSON.parse(e.sequence.program)))&&!scene.capabilities?.includes('recipeCreation.v1'))
      throw new Error('Update the native app to create recipe objects in programs.');
    if(command.action==='rules'&&record(command.rule)&&Array.isArray(command.rule.edits)&&command.rule.edits.some(e=>record(e)&&record(e.sequence)&&typeof e.sequence.program==='string'&&hasResults(JSON.parse(e.sequence.program)))&&!scene.capabilities?.includes('actionResults.v1'))
      throw new Error('Update the native app to use action results and creation programs.');
    if((Object.prototype.hasOwnProperty.call(roomControlFields,command.action)||command.action==='catalog'||command.action==='execution'||command.action==='motions'||command.action==='avatarActivities')&&!scene.capabilities?.includes(command.action+'.v1'))
      throw new Error('This room does not support '+command.action+'. Update or connect a compatible native app.');
    if(command.action==='rules'&&record(command.rule)&&(command.rule.action==='signal'||command.rule.action==='stop'&&typeof command.rule.target==='string'||Array.isArray(command.rule.edits)&&command.rule.edits.some(e=>record(e)&&record(e.sequence)&&typeof e.sequence.program==='string'&&JSON.parse(e.sequence.program).version===3))&&!scene.capabilities?.includes('eventPrograms.v1'))
      throw new Error('Update the native app to use event programs.');
    if(command.action==='rules'&&record(command.rule)&&Array.isArray(command.rule.edits)&&command.rule.edits.some(e=>record(e)&&record(e.sequence)&&e.sequence.program)&&!scene.capabilities?.includes('behaviourPrograms.v3'))
      throw new Error('This room does not support behaviour programs. Update or connect a compatible native app.');
    const features=new Set<string>();needs(command.execution,features);
    if(command.action==='rules'&&record(command.rule)&&Array.isArray(command.rule.edits))for(const edit of command.rule.edits)if(record(edit)&&record(edit.sequence)&&typeof edit.sequence.program==='string')needs(JSON.parse(edit.sequence.program),features);
    for(const feature of features)if(!scene.capabilities?.includes(feature))throw new Error('This action requires '+feature+'. Update or connect a compatible native app.');
  }
}
const text=(v:unknown)=>typeof v==='string'&&v.length<=2048;
export const validPhysicsObservation=(v:unknown):v is PhysicsObservation=>record(v)&&typeof v.ready==='boolean'&&typeof v.running==='boolean'&&(!v.running||v.ready)&&text(v.status);
export const validAvatarObservation=(v:unknown):v is AvatarMovementObservation=>record(v)&&['active','canLook','canFollow'].every(key=>typeof v[key]==='boolean')&&['look','follow','manual','stopped'].includes(v.mode as string)&&v.active===(v.mode!=='stopped')&&['status','lookReason','followReason'].every(key=>text(v[key]))&&between(v.distance,.8,2.5)&&between(v.speed,.2,1.2);

export const validAvatarWalkObservation=(v:unknown):v is AvatarWalkObservation=>record(v)&&['included','embedded','library'].includes(v.source as string)&&typeof v.available==='boolean'&&['name','status','playbackStatus'].every(k=>text(v[k]))&&typeof v.modelHash==='string'&&(v.modelHash===''||/^[a-f0-9]{64}$/.test(v.modelHash))&&(
 v.source==='library'?typeof v.motionId==='string'&&/^[a-f0-9]{32}$/.test(v.motionId)&&v.clipIndex===-1:
 v.motionId===''&&(v.source==='included'?v.clipIndex===-1:Number.isInteger(v.clipIndex)&&between(v.clipIndex,0,31)));
