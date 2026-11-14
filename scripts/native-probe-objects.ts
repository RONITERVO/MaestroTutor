// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {readRoomObjectObservation} from '../src/core-sdk/room/roomObjectContext';
import {objectReferenceRequest,readObjectReference} from '../shared/objectReference';
import {objectSearchRequest,readObjectSearchPage} from '../shared/objectSearch';
import {validFactValue} from '../shared/behaviourFacts';
import {checkedProbeReply,factReply} from './native-probe-contract';

/** Native facts only. Never hydrate or replace the raw periodic observation. */
export function nativeProbeObjects(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>){
 const observation=async(target:string)=>{
  const commands:RoomCommand[]=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.observation',version:1,arguments:{target}}}];
  const state=checkedProbeReply(commands,await execute(commands)),value=readRoomObjectObservation(state.catalog,target);
  if(!state.ok||!value||value.objectRevision===undefined||value.runtimeState===undefined||value.positionSource===undefined)throw new Error('Expected an exact native object observation: '+target);
  return {...value,objectRevision:value.objectRevision,runtimeState:value.runtimeState,positionSource:value.positionSource};
 };
 const revision=async(target:string)=>{
  const commands:RoomCommand[]=[{action:'catalog',catalog:objectReferenceRequest(target)}],state=checkedProbeReply(commands,await execute(commands));
  const read=state.ok?readObjectReference(state.catalog,target):null;if(!read)throw new Error('Expected a saved object revision: '+target);return read.revision;
 };
 const exists=async(target:string)=>{
  const commands:RoomCommand[]=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.presence',version:1,arguments:{target}}}],state=checkedProbeReply(commands,await execute(commands)),view=factReply(state);
  if(!state.ok||!view.available||!validFactValue('object.presence',view.value))throw new Error('Expected explicit saved-object presence: '+target);
  const value=view.value as {target:string;saved:boolean};if(value.target!==target)throw new Error('Native presence targets another object');return value.saved;
 };
 const page=async(cursor='')=>{
  const query=objectSearchRequest('','all',cursor),commands:RoomCommand[]=[{action:'catalog',catalog:query}],state=checkedProbeReply(commands,await execute(commands));
  const value=state.ok?readObjectSearchPage(state.catalog,query):null;if(!value)throw new Error('Saved-object directory is unavailable or changed; do not infer deletion');return value;
 };
 const count=async()=>(await page()).total;
 const ids=async()=>{
  const result:string[]=[];let cursor='',worldId='',total=-1;
  for(let pages=0;pages<256;pages++){
   const value=await page(cursor);
   if(pages===0){worldId=value.worldId;total=value.total;}
   if(value.worldId!==worldId||value.total!==total||value.offset!==result.length||value.entries.some(e=>result.includes(e.id))||result.length&&value.entries.length&&result[result.length-1]>=value.entries[0].id)throw new Error('Saved-object directory changed while reading its pages');
   result.push(...value.entries.map(e=>e.id));cursor=value.next;
   if(!cursor){if(result.length!==total)throw new Error('Incomplete saved-object directory');return result;}
  }
  throw new Error('Native probe directory exceeded its explicit page budget');
 };
 const any=async(targets:readonly string[])=>{for(const target of targets)if(await exists(target))return true;return false;};
 const all=async(targets:readonly string[])=>{for(const target of targets)if(!await exists(target))return false;return true;};
 return {observation,revision,exists,count,ids,any,all};
}
