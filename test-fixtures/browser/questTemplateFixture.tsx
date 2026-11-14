// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Development presentation adapter; native edit/Undo and physics are verified separately.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import {simulatedTemplateReply,templateReplayBoundary,templateReplayState,type TemplateCapture} from './questTemplateReplay';
import {CapabilityBrowser} from '../../src/platform/quest/CapabilityBrowser';
import '../../src/app/index.css';
import '../../src/platform/quest/roomWorkspace.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./templateAuthoring.json')).json() as TemplateCapture;
const client=new RoomAgentClient(),requests:unknown[]=[];
let state=templateReplayState(native.before);
if(!client.receive(state))throw new Error('Invalid captured native template state');
Object.assign(window,{maestroTemplateRequests:requests,maestroTemplateSnapshot:()=>client.snapshot(),maestroTemplateReplayBoundary:templateReplayBoundary});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  const next=simulatedTemplateReply(native,state,request);
  requests.push(structuredClone(request));state=next;
 }
 state={...state,revision:state.revision+1};if(!client.receive(state))throw new Error('Rejected native template replay state');
},200);
createRoot(document.getElementById('root')!).render(<div style={{position:"fixed",inset:0,display:"grid",gridTemplateRows:"auto minmax(0, 1fr)"}}><aside aria-label="Development fixture boundary" style={{padding:"6px 12px",background:"#fff",color:"#222",font:"12px system-ui"}}>{templateReplayBoundary}</aside><main style={{position:"relative",minHeight:0}}><CapabilityBrowser client={client} onClose={()=>{}}/></main></div>);
