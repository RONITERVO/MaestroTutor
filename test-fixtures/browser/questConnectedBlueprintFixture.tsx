// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Development replay only; native edit/Undo and physics are verified separately.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./connectedBlueprintAuthoring.json')).json() as Record<string,RoomAgentState>;
const client=new RoomAgentClient(),requests:unknown[]=[];
let state=structuredClone(native.before);state.visible=true;let revision=state.revision;
if(!client.receive(state))throw new Error('Invalid captured native connected blueprint state');
Object.assign(window,{maestroConnectedBlueprintRequests:requests,maestroConnectedBlueprintSnapshot:()=>client.snapshot()});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let key:string;
  if(command.action==='catalog'&&command.catalog?.operation==='search')key='search';
  else if(command.action==='catalog'&&command.catalog?.operation==='inspect')key=command.catalog.category==='facts'?'current':'definition';
  else if(command.action==='execution'&&command.execution?.operation==='start'&&command.execution.call.id==='object.batch.create')key='after';
  else throw new Error('Unexpected connected blueprint replay command');
  state=structuredClone(native[key]);state.visible=true;state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native connected blueprint replay state');
},200);
createRoot(document.getElementById('root')!).render(<CapabilityBrowser client={client} onClose={()=>{}}/>);
