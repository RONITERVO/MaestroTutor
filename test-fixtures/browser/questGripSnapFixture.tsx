// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Exact replay of native observations, never a browser physics approximation.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./gripSnappingAuthoring.json')).json() as {before:RoomAgentState;search:RoomAgentState;definition:RoomAgentState;read:RoomAgentState;after:RoomAgentState};
const client=new RoomAgentClient(),requests:unknown[]=[];
const canonical=(value:unknown):string=>JSON.stringify(value,(_,v)=>v&&typeof v==='object'&&!Array.isArray(v)?Object.fromEntries(Object.keys(v).sort().map(key=>[key,v[key]])):v);
let state=structuredClone(native.before),revision=state.revision;state.visible=true;
if(!client.receive(state))throw new Error('Invalid native snap state');
Object.assign(window,{maestroGripSnapRequests:requests,maestroGripSnapSnapshot:()=>client.snapshot()});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let replay:RoomAgentState|undefined;
  if(command.action==='catalog'&&command.catalog?.operation==='search'&&native.search.catalog?.operation==='search'&&command.catalog.query===native.search.catalog.query)replay=native.search;
  else if(command.action==='catalog'&&command.catalog?.operation==='inspect'){
   const q=command.catalog;replay=q.category==='facts'?native.read.catalog?.operation==='inspect'&&native.read.catalog.category==='facts'&&q.capability==='room.selection.snapping'&&canonical(q.arguments??{})===canonical(native.read.catalog.arguments??{})?native.read:undefined:q.capability==='room.selection.snapSettings'?native.definition:undefined;
  }else if(command.action==='execution'&&command.execution?.operation==='start'&&canonical(command.execution.call)===canonical(native.after.execution?.selected?.call)&&command.execution.runId===native.after.execution?.selected?.id)replay=native.after;
  if(!replay)throw new Error('No exact native snap response');
  state=structuredClone(replay);state.visible=true;state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native snap replay');
},200);
createRoot(document.getElementById('root')!).render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
