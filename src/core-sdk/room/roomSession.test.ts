// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {roomSessionCall,validTemporaryRoom,type TemporaryRoomView} from '../../../shared/roomSession';
import {capabilityDefinition,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
const view=():TemporaryRoomView=>({active:false,pending:false,id:'a'.repeat(32),saveId:'',phase:'idle',error:'',savedRevision:0});
const state=(temporaryRoom:unknown)=>({version:1,session:'b'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',created:[],objects:[],canUndo:false,canRedo:false,physicsRunning:false,capabilities:['temporaryRoom.v1'],temporaryRoom});
it('uses the exact observed session identity and native async/result contracts',()=>{
 const call=roomSessionCall('begin',view());expect(call.arguments.sessionId).toBe(view().id);
 expect(capabilityDefinition(call.id)).toMatchObject({duration:'completion',input:{'x-features':['temporaryRoom.v1']}});
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 expect(validateCapabilityArguments(call.id,1,{...call.arguments,sessionId:''})).not.toBeNull();
 expect(validateCapabilityArguments(call.id,1,{...call.arguments,operation:'reset'})).not.toBeNull();
 expect(validateCapabilityOutput(call.id,1,{sessionId:'c'.repeat(32),saveId:'',savedRevision:0})).toBeNull();
});
it('rejects inconsistent temporary-room observations before publishing them',()=>{
 expect(validTemporaryRoom(view())).toBe(true);expect(new RoomAgentClient().receive(state(view()))).toBe(true);
 for(const bad of [undefined,{...view(),id:''},{...view(),pending:true},{...view(),phase:'saved'},{...view(),phase:'failed'},{...view(),extra:1},{...view(),savedRevision:-1}])
  expect(new RoomAgentClient().receive(state(bad))).toBe(false);
 const pending={...view(),active:true,pending:true,phase:'pending' as const,saveId:'c'.repeat(32)};expect(validTemporaryRoom(pending)).toBe(true);
 expect(validTemporaryRoom({...pending,pending:false,phase:'failed',error:'Storage is full'})).toBe(true);
 expect(validTemporaryRoom({...pending,pending:false,phase:'saved',savedRevision:1})).toBe(true);
});
it('enforces module-declared native features for one-off calls and program source',()=>{
 const call=roomSessionCall('begin',view());
 const oneoff=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(oneoff,{capabilities:['execution.v1']})).toThrow('temporaryRoom.v1');
 expect(()=>requireRoomCapabilities(oneoff,{capabilities:['execution.v1','temporaryRoom.v1']})).not.toThrow();
 const node={id:'begin',op:'invoke',capability:call.id,version:1,arguments:call.arguments,bindings:{sessionId:{fact:'room.sessionId'}}};
 const program={version:3,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[node]}]};
 const commands=[{action:'rules',rule:{action:'edit',edits:[{sequence:{program:JSON.stringify(program)}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','actionResults.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('temporaryRoom.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'temporaryRoom.v1']})).not.toThrow();
});
