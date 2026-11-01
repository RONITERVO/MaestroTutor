// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Development replay only. Native geometry/edit/Undo are tested by Run-QuestRoomProbe.
import {createRoot} from 'react-dom/client';
const native=await (await fetch('./patternAuthoring.json')).json();
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {RoomWorkspace} from '../../src/platform/quest/RoomWorkspace';
import '../../src/app/index.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const client=new RoomAgentClient(),requests:unknown[]=[];
let state=structuredClone(native.before) as unknown as RoomAgentState;
state.visible=true;let revision=state.revision;
if(!client.receive(state))throw new Error('Invalid captured native pattern state');
Object.assign(window,{maestroPatternRequests:requests,maestroPatternSnapshot:()=>client.snapshot()});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));
  const command=request.commands[0];
  if(command.action!=='execution'||command.execution?.operation!=='start'||command.execution.call.id!=='object.recipe.edit')throw new Error('Unexpected replay command');
  state=structuredClone(native.after) as unknown as RoomAgentState;state.visible=true;state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected native replay state');
},200);
createRoot(document.getElementById('root')!).render(<RoomWorkspace client={client}/>);
