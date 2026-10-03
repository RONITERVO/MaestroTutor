// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Replays exact full-app native catalog observations, without simulating liquid in the browser.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState,RoomCommand} from '../../src/core-sdk/room/roomAgent';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./containerPouring.json')).json() as {before:RoomAgentState;steps:{request:RoomCommand;response:RoomAgentState}[]};
const client=new RoomAgentClient(),requests:unknown[]=[];
const canonical=(value:unknown):string=>JSON.stringify(value,(_,v)=>v&&typeof v==='object'&&!Array.isArray(v)?Object.fromEntries(Object.keys(v).sort().map(key=>[key,v[key]])):v);
let state=structuredClone(native.before),revision=state.revision;state.visible=true;
if(!client.receive(state))throw new Error('Invalid native pouring state');
Object.assign(window,{maestroPouringRequests:requests,maestroPouringSnapshot:()=>client.snapshot()});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const step=native.steps.find(s=>canonical(s.request)===canonical(request.commands[0]));
  if(!step)throw new Error('No exact native pouring response');
  state=structuredClone(step.response);state.visible=true;state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native pouring replay');
},200);
createRoot(document.getElementById('root')!).render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
