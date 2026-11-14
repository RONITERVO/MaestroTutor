// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {boundedCapabilityCall,validCatalogView} from '../../../shared/roomCatalog';
import {validExecutionView} from '../../../shared/roomExecutions';
import {invocationResourceLimit,programResourceLimit} from '../../../shared/programLimits';
import {RoomAgentClient} from './roomAgentClient';
import {roomCommandConditions} from './roomObjectContext';
import type {RoomAgentState,RoomCommand} from './roomAgent';
import {ROOM_OBJECT_OVERVIEW_FEATURE} from '../../../shared/roomObjectOverview';
const members=Array.from({length:256},(_,i)=>i.toString(16).padStart(32,'0'));
const capabilities=['appearance.save','visibility.layer.save'];
it.each(capabilities)('%s accepts the complete saved-world membership through the common form and receipt schema',id=>{
 const definition=capabilityDefinition(id)!;
 expect(definition.input.properties!.members.maxItems).toBe(invocationResourceLimit);
 const args={...structuredClone(definition.example!),id:'e'.repeat(32),revision:1,members};
 expect(validateCapabilityArguments(id,1,args)).toBeNull();
 const call={id,version:1,arguments:args};expect(boundedCapabilityCall(call)).toBe(true);
 expect(validCatalogView({operation:'check',call,valid:true,available:true,occupied:false,resources:members,status:'Ready'})).toBe(true);
 const receipt={id:'b'.repeat(32),capability:id,version:1,resources:members,phase:'completed',status:'Saved'};
 expect(validExecutionView({selected:{...receipt,call},running:[],outcomes:[receipt]})).toBe(true);
 const overflow=Array.from({length:invocationResourceLimit+1},(_,i)=>i.toString(16).padStart(32,'0'));
 expect(validateCapabilityArguments(id,1,{...args,members:overflow})).not.toBeNull();
 expect(programResourceLimit).toBe(66);
});
it.each(capabilities)('%s sends every member revision using the existing bounded user/agent transport',async id=>{
 const args={...structuredClone(capabilityDefinition(id)!.example!),id:'e'.repeat(32),revision:1,members};
 const command:RoomCommand={action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}};
 const basis=members.map((id,i)=>({id,objectRevision:i+1}));
 const state:RoomAgentState={version:1,session:'f'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',created:[],
  canUndo:false,canRedo:false,physicsRunning:false,workspaceReady:true,objects:[],
  capabilities:['execution.v1','appearances.v1','visibilityLayers.v1','objectSearch.v1',ROOM_OBJECT_OVERVIEW_FEATURE],
  objectOverview:{version:1,scope:'context',exhaustive:false,limit:4}};
 const client=new RoomAgentClient();expect(client.receive(state)).toBe(true);
 expect(()=>roomCommandConditions([command],basis.slice(0,255))).toThrow('Read the current saved object');
 const result=client.request([command],state,basis);
 try{const wire=client.snapshot();expect(wire.request?.conditions).toEqual(basis.map(({id,objectRevision})=>({id,revision:objectRevision})));
  expect(JSON.stringify(wire).length).toBeLessThanOrEqual(32768);
  expect(client.receive({...state,revision:2,ack:wire.request!.sequence})).toBe(true);await result;
 }finally{client.cancel();await result.catch(()=>{});}
});
