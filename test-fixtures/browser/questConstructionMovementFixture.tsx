// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Development replay of actual native observations; no browser physics simulator.
import {createRoot} from 'react-dom/client';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomAgentState} from '../../src/core-sdk/room/roomAgent';
import {RoomWorkspace} from '../../src/platform/quest/RoomWorkspace';
import '../../src/app/index.css';
if(!import.meta.env.DEV)throw new Error('Development fixture only');
const native=await (await fetch('./constructionMovementAuthoring.json')).json() as {before:RoomAgentState;shown:RoomAgentState;hidden:RoomAgentState;search:RoomAgentState;definition:RoomAgentState;transformed:RoomAgentState;undo:RoomAgentState;facts:RoomAgentState[]};
const client=new RoomAgentClient(),requests:unknown[]=[];
const canonical=(value:unknown):string=>JSON.stringify(value,(_,entry)=>entry&&typeof entry==='object'&&!Array.isArray(entry)?Object.fromEntries(Object.keys(entry).sort().map(key=>[key,entry[key]])):entry);
let state=structuredClone(native.before),revision=state.revision;
state.visible=true;state.workspaceView='objects';
if(!client.receive(state))throw new Error('Invalid native construction movement state');
Object.assign(window,{maestroConstructionMovementRequests:requests,maestroConstructionMovementSnapshot:()=>({...client.getSnapshot(),...client.snapshot()})});
setInterval(()=>{
 const request=client.snapshot().request;
 if(request&&request.sequence>state.ack){
  requests.push(structuredClone(request));const command=request.commands[0];let replay:RoomAgentState|undefined;
  if(command.action==='undo')replay=native.undo;
  else if(command.action==='catalog'&&command.catalog?.operation==='search'&&native.search.catalog?.operation==='search'&&command.catalog.query===native.search.catalog.query)replay=native.search;
  else if(command.action==='catalog'&&command.catalog?.operation==='inspect'){
   const query=command.catalog;replay=query.category==='facts'?native.facts.find(f=>f.catalog?.operation==='inspect'&&f.catalog.category==='facts'&&f.catalog.capability===query.capability&&canonical(f.catalog.arguments)===canonical(query.arguments)):query.capability==='object.layout.transform'?native.definition:undefined;
  }else if(command.action==='execution'&&command.execution?.operation==='start'){
   const execution=command.execution;replay=[native.shown,native.transformed,native.hidden].find(s=>canonical(s.execution?.selected?.call)===canonical(execution.call)&&s.execution?.selected?.id===execution.runId);
  }
  if(!replay)throw new Error('No exact native response for this construction movement request');
  state=structuredClone(replay);state.visible=true;state.workspaceView='objects';state.ack=request.sequence;
 }
 state={...state,revision:++revision};if(!client.receive(state))throw new Error('Rejected construction movement replay state');
},200);
createRoot(document.getElementById('root')!).render(<RoomWorkspace client={client}/>);
