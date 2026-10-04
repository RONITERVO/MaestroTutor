// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Exact native observation replay for the shared form. No browser physics or catch implementation.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import type {CapabilityInvocation} from '../../shared/capabilities';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./physicalCatching.json')).json() as {call:CapabilityInvocation;search:RoomAgentState;readyDefinition:RoomAgentState;readyCheck:RoomAgentState;waiting:RoomAgentState;after:RoomAgentState};
const client=new RoomAgentClient(),requests:unknown[]=[];
const canonical=(value:unknown):string=>JSON.stringify(value,(_,v)=>v&&typeof v==='object'&&!Array.isArray(v)?Object.fromEntries(Object.keys(v).sort().map(key=>[key,v[key]])):v);
let state=structuredClone(native.readyDefinition),revision=state.revision,completeAt=0;state.visible=true;
if(!client.receive(state))throw new Error('Invalid native catch state');
Object.assign(window,{maestroCatchRequests:requests,maestroCatchSnapshot:()=>client.snapshot(),maestroCatchState:()=>client.getSnapshot().state});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let replay:RoomAgentState|undefined;
  if(command.action==='catalog'&&command.catalog?.operation==='inspect'&&command.catalog.capability==='object.physics.catch')replay=native.readyDefinition;
  else if(command.action==='catalog'&&command.catalog?.operation==='check'&&canonical(command.catalog.call)===canonical(native.call))replay=native.readyCheck;
  else if(command.action==='execution'&&command.execution?.operation==='start'&&canonical(command.execution.call)===canonical(native.call)&&command.execution.runId===native.waiting.execution?.selected?.id){replay=native.waiting;completeAt=Date.now()+1000;}
  else if(command.action==='execution'&&command.execution?.operation==='inspect'&&command.execution.runId===native.after.execution?.selected?.id)replay=native.after;
  if(!replay)throw new Error('No exact native catch response for this request');
  state=structuredClone(replay);state.visible=true;state.ack=request.sequence;
 }
 if(completeAt&&Date.now()>=completeAt){const ack=state.ack;state=structuredClone(native.after);state.visible=true;state.ack=ack;completeAt=0;}
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native catch replay');
},200);
createRoot(document.getElementById('root')!).render(<CapabilityBrowser client={client} initialCall={native.call} onClose={()=>{}}/>);
