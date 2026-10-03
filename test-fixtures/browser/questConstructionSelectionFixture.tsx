// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Development replay of real native observations; this does not simulate physics.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {RoomWorkspace} from '../../src/platform/quest/RoomWorkspace';
import '../../src/app/index.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./constructionSelectionAuthoring.json')).json() as {before:RoomAgentState;first:RoomAgentState;both:RoomAgentState;located:RoomAgentState;definition:RoomAgentState;captured:RoomAgentState;facts:RoomAgentState[]};
const client=new RoomAgentClient(),requests:unknown[]=[];
const canonical=(value:unknown):string=>JSON.stringify(value,(_,entry)=>entry&&typeof entry==='object'&&!Array.isArray(entry)?Object.fromEntries(Object.keys(entry).sort().map(key=>[key,entry[key]])):entry);
let state=structuredClone(native.before),revision=state.revision;
state.visible=true;state.workspaceView='objects';
if(!client.receive(state))throw new Error('Invalid native construction selection state');
Object.assign(window,{maestroConstructionSelectionRequests:requests,maestroConstructionSelectionSnapshot:()=>({...client.getSnapshot(),...client.snapshot()})});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let replay:RoomAgentState|undefined;
  if(command.action==='inspect'&&command.target===native.located.selectedId)replay=native.located;
  else if(command.action==='catalog'&&command.catalog?.operation==='inspect'){
   const query=command.catalog;replay=query.category==='facts'?native.facts.find(f=>f.catalog?.operation==='inspect'&&f.catalog.category==='facts'&&f.catalog.capability===query.capability&&JSON.stringify(f.catalog.arguments)===JSON.stringify(query.arguments)):query.capability==='program.module.captureConstruction'?native.definition:undefined;
  }else if(command.action==='execution'&&command.execution?.operation==='start'){
   const execution=command.execution,call=execution.call;
   replay=[native.first,native.both,native.captured].find(s=>canonical(s.execution?.selected?.call)===canonical(call)&&s.execution?.selected?.id===execution.runId);
  }
  if(!replay)throw new Error('No exact native response for this construction selection request');
  state=structuredClone(replay);state.visible=true;state.workspaceView='objects';state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected construction selection replay state');
},200);
createRoot(document.getElementById('root')!).render(<RoomWorkspace client={client}/>);
