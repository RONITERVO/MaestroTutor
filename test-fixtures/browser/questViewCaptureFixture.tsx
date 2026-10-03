// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Actual native receipt and pixels replayed through the real book controls.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./viewCapture.json')).json();
const client=new RoomAgentClient(),requests:unknown[]=[];
let state=structuredClone(native.before) as RoomAgentState;state.visible=true;let revision=state.revision,showImage=false;
if(!client.receive(state))throw new Error('Invalid captured native room view state');
Object.assign(window,{maestroCaptureRequests:requests,maestroCaptureSnapshot:()=>client.snapshot()});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let key:string;
  if(command.action==='catalog'&&command.catalog?.operation==='search')key='search';
  else if(command.action==='catalog'&&command.catalog?.operation==='inspect')key='definition';
  else if(command.action==='execution'&&command.execution?.operation==='start'&&command.execution.call.id==='room.view.capture'){key='after';showImage=true;}
  else throw new Error('Unexpected virtual view replay command');
  state=structuredClone(native[key]) as RoomAgentState;state.visible=true;state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native room view replay state');
 if(showImage&&!client.captureImage(native.payload.capture.captureId)&&!client.receiveCapture(native.payload))throw new Error('Rejected native pixels');
},200);
createRoot(document.getElementById('root')!).render(<CapabilityBrowser client={client} onClose={()=>{}} initialCall={{id:'room.view.capture',version:1,arguments:{}}}/>);
