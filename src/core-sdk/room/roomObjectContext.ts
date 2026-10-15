// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readObjectReference} from '../../../shared/objectReference';
import {capabilityResources} from '../../../shared/capabilities';
import {validObjectPhysics,validAvatarMovement} from '../../../shared/roomControls';
import {validPigment} from './recipe';
import type {RoomAgentState,RoomCommand,RoomObjectObservation,RoomObjectRevision} from './roomAgent';
const record=(v:unknown):v is Record<string,unknown>=>v!==null&&typeof v==='object'&&!Array.isArray(v);
const revision=(v:unknown)=>typeof v==='number'&&Number.isInteger(v)&&v>=1&&v<=2147483647;
const vector=(v:unknown)=>record(v)&&['x','y','z'].every(k=>typeof v[k]==='number'&&Number.isFinite(v[k]));
export const roomObjectId=(v:unknown):v is string=>typeof v==='string'&&/^(book|maestro|[a-f0-9]{32})$/.test(v);
export function validRoomObjectObservation(o:unknown):o is RoomObjectObservation {
 if(!record(o)||!roomObjectId(o.id)||typeof o.name!=='string'||o.name.length>80||typeof o.kind!=='string'||!vector(o.position)||typeof o.scale!=='number'||!Number.isFinite(o.scale)||!validPigment(o.color)||typeof o.animated!=='boolean'||o.objectRevision!==undefined&&!revision(o.objectRevision))return false;
 if(o.runtimeState!==undefined&&!['unavailable','inactive','active'].includes(o.runtimeState as string)||o.positionSource!==undefined&&!['saved','live'].includes(o.positionSource as string)||o.positionSource==='live'&&o.runtimeState!==undefined&&o.runtimeState!=='active')return false;
 return !(o.physics!==undefined&&!validObjectPhysics(o.physics)||o.movement!==undefined&&o.movement!==null&&!validAvatarMovement(o.movement)||['held','simulating'].some(key=>o[key]!==undefined&&typeof o[key]!=='boolean'));
}
/** The native inspection is self-contained. The list is only an older-host fallback. */
export function inspectedRoomObject(state:RoomAgentState):RoomObjectObservation|undefined {
 const view=state.inspection;if(!view)return undefined;
 return view.item??state.objects.find(o=>o.id===view.id);
}
/** Capture exact revision evidence from the state the author saw, never refresh at dispatch. */
export function roomRevisionBasis(state:RoomAgentState,earlier:readonly RoomAgentState[]=[]):RoomObjectRevision[] {
 const result=new Map<string,RoomObjectRevision>();
 for(const observed of [...earlier,state]){
  if(observed.session!==state.session||observed.temporaryRoom?.id!==state.temporaryRoom?.id)throw new Error('Object reads belong to another room session.');
  const fact=observed.catalog;
  if(observed.ok&&fact?.operation==='inspect'&&fact.category==='facts'&&fact.capability==='object.definition'&&roomObjectId(fact.arguments?.target)){
   const target=fact.arguments.target as string,read=readObjectReference(fact,target);
   if(read)result.set(target,{id:target,objectRevision:read.revision});
   else result.delete(target);
  }
  for(const {id,objectRevision} of observed.objects)result.set(id,{id,objectRevision});
  const view=observed.inspection;if(view)result.set(view.id,{id:view.id,objectRevision:view.objectRevision});
 }
 return [...result.values()];
}
/** Only external exact objects need evidence; earlier creation aliases belong to this batch. */
export function roomCommandTargets(commands:readonly RoomCommand[]):{targets:Set<string>;required:Set<string>} {
 const targets=new Set<string>(),required=new Set<string>(),aliases=new Set<string>();
 for(const command of commands){
  if(command.action==='create'){if(command.reference)aliases.add(command.reference);continue;}
  const references=command.action==='execution'&&command.execution?.operation==='start'?capabilityResources(command.execution.call.id,command.execution.call.arguments):command.target?[command.target]:[];
  for(const target of references)if(!aliases.has(target)){
   targets.add(target);if(!['inspect','motions'].includes(command.action))required.add(target);
  }
 }
 return {targets,required};
}
/** Missing evidence is not permission to omit a native target precondition. */
export function roomCommandConditions(commands:readonly RoomCommand[],basis:readonly RoomObjectRevision[],reconcile=false):{version:1|2;conditions:{id:string;revision:number}[]} {
 const modern=basis.every(o=>revision(o.objectRevision)),{targets,required}=roomCommandTargets(commands);
 // Zero cannot authorize a new edit. It permits the native receipt lane to
 // reconcile an old ID before checking targets that may no longer exist.
 if(reconcile)return {version:2,conditions:[...targets].map(id=>({id,revision:0}))};
 const conditions:{id:string;revision:number}[]=[];
 for(const target of targets){
  const found=basis.filter(o=>o.id===target);
  if(modern&&required.has(target)&&!found.length)throw new Error('Read the current saved object before editing: '+target);
  if(found.length){
   if(found.some(o=>o.objectRevision!==found[0].objectRevision))throw new Error('Conflicting saved object revisions. Read the object again: '+target);
   if(revision(found[0].objectRevision))conditions.push({id:target,revision:found[0].objectRevision!});
  }
 }
 return {version:modern?2:1,conditions};
}
