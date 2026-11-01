// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {parseRecipe,type RoomRecipe} from './roomRecipe';
import {validateCapabilityArguments,type CapabilityInvocation} from './capabilities';
import {boundedCapabilityCall} from './roomCatalog';

/** Project a reviewed visual draft into the same native patch used by programs and agents. */
export function recipeEditCall(target:string,revision:number,before:RoomRecipe,after:RoomRecipe):CapabilityInvocation {
 if(!parseRecipe(before)||!parseRecipe(after))throw new Error('The recipe needs valid parts and animation keys.');
 const changed=<T,>(old:T[],next:T[],id:(value:T)=>string)=>next.filter(value=>{
  const previous=old.find(entry=>id(entry)===id(value));return !previous||JSON.stringify(previous)!==JSON.stringify(value);
 });
 const removed=<T,>(old:T[],next:T[],id:(value:T)=>string)=>old.filter(value=>!next.some(entry=>id(entry)===id(value))).map(id);
 const call:CapabilityInvocation={id:'object.recipe.edit',version:1,arguments:{target,revision,
  parts:changed(before.parts,after.parts,p=>p.id),removeParts:removed(before.parts,after.parts,p=>p.id),
  tracks:changed(before.tracks,after.tracks,t=>t.part),removeTracks:removed(before.tracks,after.tracks,t=>t.part),
  duration:after.duration,loop:after.loop}};
 const error=validateCapabilityArguments(call.id,call.version,call.arguments);
 if(error||!boundedCapabilityCall(call))throw new Error(error||'This patch exceeds the room message limit. Apply fewer part or track edits at a time.');
 return call;
}
