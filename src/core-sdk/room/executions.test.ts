// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {validExecutionRequest,validExecutionView,type ExecutionView} from '../../../shared/roomExecutions';
import {parseRoomCommands,isRoomQuery,type RoomAgentState} from './roomAgent';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import nativeProgram from '../../../test-fixtures/browser/programBookState.json';
const id='a'.repeat(32),prop='b'.repeat(32);
const call={id:'animation.recording.play',version:1,arguments:{target:'maestro',seconds:1,loop:false,prop:{
 objectId:prop,avatarHash:'',hand:'right',release:'return',offset:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},releaseAt:1}}};
const start={operation:'start' as const,call};
it('validates exact one-off commands and distinguishes inspection from starts or cancellation',()=>{
 expect(validExecutionRequest(start)).toBe(true);
 for(const bad of [{...start,runId:id},{operation:'cancel',call},{operation:'inspect',runId:'unknown'},
  {...start,call:{...call,version:2}},{...start,call:{...call,arguments:{...call.arguments,seconds:50}}}])
  expect(validExecutionRequest(bad)).toBe(false);
 const command={action:'execution' as const,execution:start};
 expect(parseRoomCommands({commands:[command]})).toEqual([command]);
 expect(()=>parseRoomCommands({commands:[command,{action:'rules',rule:{action:'stop'}}]})).toThrow('on its own');
 expect(()=>requireRoomCapabilities([command],{capabilities:[]})).toThrow();
 expect(isRoomQuery(command)).toBe(false);
 expect(isRoomQuery({action:'execution',execution:{operation:'inspect',runId:id}})).toBe(true);
 expect(isRoomQuery({action:'execution',execution:{operation:'cancel',runId:id}})).toBe(false);
});
it('keeps starts guarded by both target and prop revisions while cancellation needs no stale object guard',async()=>{
 const client=new RoomAgentClient();
 const state=JSON.parse(JSON.stringify(nativeProgram)) as RoomAgentState;
 state.capabilities=['execution.v1'];state.revision=1;state.ack=0;
 const maestro=state.objects.find(o=>o.id==='maestro')!;
 state.objects.push({...maestro,id:prop,objectRevision:88});
 expect(client.receive(state)).toBe(true);
 const result=client.request([{action:'execution',execution:start}]);
 expect(client.snapshot().request?.version).toBe(2);
 expect(client.snapshot().request?.conditions).toEqual(expect.arrayContaining([{id:'maestro',revision:maestro.objectRevision},{id:prop,revision:88}]));
 client.receive({...state,revision:2,ack:1});await result;
 const cancelled=client.request([{action:'execution',execution:{operation:'cancel',runId:id}}]);
 expect(client.snapshot().request?.conditions).toEqual([]);
 client.receive({...state,revision:3,ack:2});await cancelled;client.cancel();
});
const summary={id,capability:call.id,version:1,phase:'running' as const,resources:['maestro',prop],status:'Action running'};
it('rejects contradictory execution receipts and bounds the expanded native observation',()=>{
 const execution:ExecutionView={selected:{...summary,call},running:[summary],outcomes:[]};
 expect(validExecutionView(execution)).toBe(true);
 const multiline={...summary,phase:'failed' as const,status:'Model error\nDetails'};
 expect(validExecutionView({selected:{...multiline,call},running:[],outcomes:[multiline]})).toBe(true);
 expect(validExecutionView({...execution,outcomes:[{...summary,phase:'completed'}]})).toBe(false);
 expect(validExecutionView({...execution,selected:{...summary,phase:'completed',call}})).toBe(false);
 expect(validExecutionView({...execution,selected:{...summary,call:{...call,arguments:{...call.arguments,target:'book'}}}})).toBe(false);
 const state=JSON.parse(JSON.stringify(nativeProgram)) as RoomAgentState;
 state.execution={selected:{...summary,call},running:Array.from({length:8},(_,i)=>({...summary,id:i===0?id:i.toString(16).padStart(32,'0'),status:i===0?summary.status:'x'.repeat(2048),resources:i===0?summary.resources:Array.from({length:16},(_,j)=>(j+99).toString(16).padStart(32,'0'))})),
  outcomes:Array.from({length:16},(_,i)=>({...summary,id:(i+9).toString(16).padStart(32,'0'),phase:'completed',status:'y'.repeat(2048)}))};
 state.rules!.selected!.program=state.rules!.selected!.program.padEnd(24000,' ');
 const run=state.rules!.running[0];state.rules!.running=Array.from({length:8},(_,i)=>({...run,id:(i+33).toString(16).padStart(32,'0'),locals:Array.from({length:24},(_,j)=>({name:'value_'+j,type:'text',value:'x'.repeat(128)}))}));
 const client=new RoomAgentClient();expect(client.receive(state)).toBe(true);
 expect(client.receive({...state,revision:state.revision+1,padding:'x'.repeat(327680)})).toBe(false);client.cancel();
});

import nativeExecutions from '../../../test-fixtures/browser/executionStates.json';
it('accepts actual Unity execution observations through the shared bridge',()=>{
 const client=new RoomAgentClient();
 for(const state of Object.values(nativeExecutions).sort((a,b)=>a.revision-b.revision)){
  expect(validExecutionView(state.execution)).toBe(true);expect(client.receive(state)).toBe(true);
 }
 expect(nativeExecutions.running.execution.selected.phase).toBe('running');
 expect(nativeExecutions.cancelled.execution.selected.phase).toBe('cancelled');
 expect(nativeExecutions.completed.execution.selected.phase).toBe('completed');client.cancel();
});
