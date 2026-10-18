// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Native-state replay for generated book controls; physical input is tested in Unity.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const kind=new URLSearchParams(location.search).get('kind');
const fixture=kind==='slider'?'sliderAuthoring.json':kind==='fixed'?'connectionAuthoring.json':'hingeAuthoring.json';
const native=await (await fetch('./'+fixture)).json() as {before:RoomAgentState;search:RoomAgentState;definition:RoomAgentState;facts:RoomAgentState[];directory:RoomAgentState[];started:RoomAgentState;after:RoomAgentState};
const client=new RoomAgentClient(),requests:unknown[]=[];
const canonical=(value:unknown):string=>JSON.stringify(value,(_,v)=>v&&typeof v==='object'&&!Array.isArray(v)?Object.fromEntries(Object.keys(v).sort().map(key=>[key,v[key]])):v);
let state=structuredClone(native.before);state.visible=true;let revision=state.revision,pendingTicks=0;
if(!client.receive(state))throw new Error('Invalid captured native hinge state');
Object.assign(window,{maestroHingeRequests:requests,maestroHingeSnapshot:()=>client.snapshot(),maestroHingeState:()=>client.getSnapshot().state});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let replay:RoomAgentState|undefined;
  if(command.action==='catalog'&&command.catalog?.operation==='search'&&native.search.catalog?.operation==='search'&&command.catalog.query===native.search.catalog.query)replay=native.search;
  else if(command.action==='catalog'&&command.catalog?.operation==='inspect'){
   const q=command.catalog;replay=q.category==='facts'?[...native.facts,...native.directory].find(f=>f.catalog?.operation==='inspect'&&f.catalog.category==='facts'&&f.catalog.capability===q.capability&&canonical(f.catalog.arguments)===canonical(q.arguments)):q.capability==='object.connection.edit'?native.definition:undefined;
  }else if(command.action==='execution'&&command.execution?.operation==='start'&&canonical(command.execution.call)===canonical(native.after.execution?.selected?.call)&&command.execution.runId===native.after.execution?.selected?.id){replay=native.started;pendingTicks=native.started.execution?.selected?.phase==='preparing'?15:0;}
  if(!replay)throw new Error('No exact native connection response');
  state=structuredClone(replay);state.visible=true;state.ack=request.sequence;
 }else if(pendingTicks>0&&--pendingTicks===0){const ack=state.ack;state=structuredClone(native.after);state.visible=true;state.ack=ack;}
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native hinge replay state');
},200);
createRoot(document.getElementById('root')!).render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
