// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {capabilityDefinition} from '../../shared/capabilities';
import {behaviourFact} from '../../shared/behaviourCatalog';
import {validCatalogView} from '../../shared/roomCatalog';
import type {RoomAgentState,RoomCommand} from '../../src/core-sdk/room/roomAgent';

export const templateReplayBoundary='Development fixture: current shared definitions, historical native room/result, simulated acknowledgements. No native action runs.';
export type TemplateCapture=Record<'before'|'search'|'definition'|'after',RoomAgentState>;
const equal=(a:unknown,b:unknown):boolean=>{
 if(a===b)return true;
 if(Array.isArray(a)&&Array.isArray(b))return a.length===b.length&&a.every((v,i)=>equal(v,b[i]));
 if(a!==null&&b!==null&&typeof a==='object'&&typeof b==='object'&&!Array.isArray(a)&&!Array.isArray(b)){
  const x=a as Record<string,unknown>,y=b as Record<string,unknown>;
  return Object.keys(x).length===Object.keys(y).length&&Object.keys(x).every(k=>Object.prototype.hasOwnProperty.call(y,k)&&equal(x[k],y[k]));
 }
 return false;
};
/** Presentation adapter only. Do not overwrite the historical capture or its receipts. */
export function templateReplayState(captured:RoomAgentState):RoomAgentState {
 const state=structuredClone(captured);state.visible=true;
 const catalog=state.catalog;
 if(catalog?.operation==='inspect'&&catalog.definition!==null){
  if(catalog.category==='facts')catalog.definition=behaviourFact(catalog.capability);
  else if(catalog.category===undefined||catalog.category==='actions')catalog.definition=capabilityDefinition(catalog.capability);
  else throw new Error('Unsupported template fixture definition category');
  if(!catalog.definition||catalog.definition.version!==catalog.version)throw new Error('Template fixture definition is unavailable');
 }
 if(catalog&&!validCatalogView(catalog))throw new Error('Invalid current template fixture catalog');
 return state;
}
/** One recorded journey only; never turn a different request into a recorded success.
 * This presentation adapter does not simulate native scene/object-condition guards. */
export function simulatedTemplateReply(captured:TemplateCapture,current:RoomAgentState,request:{sequence:number;commands:RoomCommand[]}):RoomAgentState {
 if(request.commands.length!==1||request.sequence<=current.ack)throw new Error('Unexpected template replay request');
 const command=request.commands[0],search=captured.search.catalog,selected=captured.after.execution?.selected;
 let source:RoomAgentState|undefined;
 if(command.action==='catalog'&&command.catalog?.operation==='search'&&search?.operation==='search'&&
   equal(command.catalog,{operation:'search',query:search.query,offset:search.offset,...(search.category?{category:search.category}:{})}))source=captured.search;
 else if(command.action==='catalog'&&command.catalog?.operation==='inspect'&&command.catalog.capability==='object.create'&&command.catalog.version===1&&
   (command.catalog.category===undefined||command.catalog.category==='actions')&&command.catalog.arguments===undefined)source=captured.definition;
 else if(command.action==='execution'&&command.execution?.operation==='start'&&selected&&
   command.execution.runId===selected.id&&current.execution?.nextRunId===selected.id&&equal(command.execution.call,selected.call))source=captured.after;
 if(!source)throw new Error('Unsupported request: this fixture only replays the exact recorded template action');
 const projected=templateReplayState(source);
 // Catalog browsing must not restore an old world, outcome or next run identity.
 const reply=command.action==='catalog'?{...structuredClone(current),catalog:projected.catalog,status:projected.status,ok:projected.ok}:projected;
 return {...reply,revision:current.revision+1,ack:request.sequence};
}
