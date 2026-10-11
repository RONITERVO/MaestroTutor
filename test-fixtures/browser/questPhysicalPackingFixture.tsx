// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Exact replay of native physical packing observations; no browser surface simulation.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./physicalPackingAuthoring.json')).json() as {created:RoomAgentState;read:RoomAgentState;idle:RoomAgentState;search:RoomAgentState;definition:RoomAgentState;set:RoomAgentState};
const client=new RoomAgentClient(),requests:unknown[]=[];
const canonical=(value:unknown):string=>JSON.stringify(value,(_,v)=>v&&typeof v==='object'&&!Array.isArray(v)?Object.fromEntries(Object.keys(v).sort().map(key=>[key,v[key]])):v);
let state=structuredClone(native.created),revision=state.revision;state.visible=true;
if(!client.receive(state))throw new Error('Invalid native physical-packing state');
Object.assign(window,{maestroPhysicalPackingRequests:requests,maestroPhysicalPackingSnapshot:()=>client.snapshot()});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let replay:RoomAgentState|undefined;
  if(command.action==='catalog'&&command.catalog?.operation==='search'&&native.search.catalog?.operation==='search'&&command.catalog.query===native.search.catalog.query)replay=native.search;
  else if(command.action==='catalog'&&command.catalog?.operation==='inspect'){
   const q=command.catalog;replay=q.category==='facts'?[native.read,native.idle].find(f=>f.catalog?.operation==='inspect'&&f.catalog.category==='facts'&&f.catalog.capability===q.capability&&canonical(f.catalog.arguments)===canonical(q.arguments)):q.capability==='material.pack.tool.set'?native.definition:undefined;
  }else if(command.action==='execution'&&command.execution?.operation==='start'&&canonical(command.execution.call)===canonical(native.set.execution?.selected?.call)&&command.execution.runId===native.set.execution?.selected?.id)replay=native.set;
  if(!replay)throw new Error('No exact native physical-packing response');
  state=structuredClone(replay);state.visible=true;state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native physical-packing replay');
},200);
createRoot(document.getElementById('root')!).render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
