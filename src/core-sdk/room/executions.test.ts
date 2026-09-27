// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {validExecutionRequest,validExecutionView,type ExecutionView} from '../../../shared/roomExecutions';
import {parseRoomCommands,isRoomQuery,type RoomAgentState} from './roomAgent';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import nativeCreation from '../../../test-fixtures/browser/creationResult.json';
import nativeProgram from '../../../test-fixtures/browser/programBookState.json';
const id='a'.repeat(32),prop='b'.repeat(32);
const call={id:'animation.play',version:1,arguments:{source:{kind:'recording'},channel:'wholeTarget',target:'maestro',seconds:1,loop:false,prop:{
 objectId:prop,avatarHash:'',hand:'right',release:'return',offset:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},releaseAt:1}}};
const start={operation:'start' as const,call};
it('validates exact one-off commands and distinguishes inspection from starts or cancellation',()=>{
 expect(validExecutionRequest(start)).toBe(true);
 for(const bad of [{...start,runId:"invalid"},{operation:'cancel',call},{operation:'inspect',runId:'unknown'},
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

it('binds a consumed start identity before dispatch and preserves recovered uncertainty',async()=>{
 const client=new RoomAgentClient(),state=JSON.parse(JSON.stringify(nativeProgram)) as RoomAgentState;
 const execution:ExecutionView={selected:null,running:[],outcomes:[],nextRunId:id,storageError:null};
 state.capabilities=['execution.v1','executionReceipts.v1'];state.execution=execution;state.revision=1;state.ack=0;
 expect(client.receive(state)).toBe(true);
 const promise=client.request([{action:'execution',execution:{operation:'start',call:{id:'time.wait',version:1,arguments:{seconds:1}}}}]);
 expect(client.snapshot().request?.commands[0].execution).toMatchObject({operation:'start',runId:id});
 const interrupted={id,capability:'time.wait',version:1,resources:[],phase:'interrupted' as const,status:'Some effects may have happened; not replayed'};
 client.receive({...state,revision:2,ack:1,execution:{...execution,nextRunId:prop,selected:{...interrupted,call:{id:'time.wait',version:1,arguments:{seconds:1}}},outcomes:[interrupted]}});
 expect((await promise).execution!.selected!.phase).toBe('interrupted');
 client.cancel();expect(client.snapshot().request).toBeNull();
 expect(validExecutionView({...execution,nextRunId:null,storageError:'Receipt storage unavailable'})).toBe(true);
 expect(validExecutionView({...execution,nextRunId:id,storageError:'Contradictory'})).toBe(false);
 client.receive({...state,session:'c'.repeat(32),execution:{...execution,nextRunId:null,storageError:'Receipt storage unavailable'}});
 expect(()=>client.lease()!.execute([{action:'execution',execution:start}],state.sceneRevision)).toThrow('Receipt storage unavailable');
 expect(client.snapshot().request).toBeNull();client.cancel();
});

import nativeRecovery from '../../../test-fixtures/browser/actionReceiptStates.json';
it('accepts actual native disk recovery and unsaved-completion observations without replay',()=>{
 const client=new RoomAgentClient();let revision=0;
 for(const execution of Object.values(nativeRecovery)) {
  expect(validExecutionView(execution)).toBe(true);
  expect(client.receive({...nativeProgram,revision:++revision,execution,capabilities:['execution.v1','executionReceipts.v1']})).toBe(true);
  expect(client.snapshot().request).toBeNull();
 }
 expect(nativeRecovery.interrupted.selected.phase).toBe('interrupted');
 expect(nativeRecovery.completed.selected.phase).toBe('completed');
 expect(nativeRecovery['unsaved-completion'].selected.phase).toBe('completed');
 expect(nativeRecovery['unsaved-completion'].nextRunId).toBeNull();
 expect(nativeRecovery['unsaved-completion'].storageError).toContain('storage failed');client.cancel();
});

it('validates exact typed creation results without treating a receipt as current object existence',()=>{
 const call={id:'object.create',version:1,arguments:{kind:'primitive',shape:'ball',name:'Ball',x:.3,y:1.3,z:.65,scale:1,red:.2,green:.6,blue:.9}};
 const done={id,capability:call.id,version:1,resources:[],phase:'completed' as const,status:'Action completed',output:{objectId:prop}};
 const view={selected:{...done,call},running:[],outcomes:[done]};
 expect(validExecutionView(view)).toBe(true);
 expect(validExecutionView({...view,selected:{...view.selected,output:{objectId:'c'.repeat(32)}}})).toBe(false);
 for(const output of [{objectId:'maestro'},{objectId:'invalid'},{objectId:prop,extra:1}]){
  expect(validExecutionView({...view,selected:{...view.selected,output},outcomes:[{...done,output}]})).toBe(false);
 }
 expect(validExecutionView({...view,selected:{...view.selected,phase:'failed'},outcomes:[{...done,phase:'failed'}]})).toBe(false);
});

it('accepts the real persisted native creation result through the existing room observation adapter',()=>{
 expect(validExecutionView(nativeCreation)).toBe(true);
 const state=JSON.parse(JSON.stringify(nativeProgram)) as RoomAgentState;
 state.execution=nativeCreation as ExecutionView;
 expect(new RoomAgentClient().receive(state)).toBe(true);
});

it('keeps recovery a gated mutation with an exact observed identity',()=>{
 const recovery={operation:'recover' as const,recoveryId:id},command={action:'execution' as const,execution:recovery};
 expect(validExecutionRequest(recovery)).toBe(true);
 for(const invalid of [{operation:'recover'},{...recovery,runId:id},{...recovery,recoveryId:'../file'},{...recovery,call}])expect(validExecutionRequest(invalid)).toBe(false);
 expect(parseRoomCommands({commands:[command]})).toEqual([command]);expect(isRoomQuery(command)).toBe(false);
 expect(()=>requireRoomCapabilities([command],{capabilities:['execution.v1']})).toThrow('recover');
 expect(()=>requireRoomCapabilities([command],{capabilities:['execution.v1','actionRecovery.v1']})).not.toThrow();
 const broken={selected:null,running:[],outcomes:[],nextRunId:null,storageError:'Storage unavailable',recovery:{id,status:'Archive old history; no replay'}};
 expect(validExecutionView(broken)).toBe(true);
 expect(validExecutionView({...broken,nextRunId:prop,storageError:null})).toBe(false);
 expect(validExecutionView({...broken,recovery:{id:'bad',status:'bad'}})).toBe(false);
 expect(validExecutionView({...broken,recovery:{id,status:'x'.repeat(2049)}})).toBe(false);
});

import historyRecovery from '../../../test-fixtures/browser/actionHistoryRecoveryStates.json';
it('reads actual Unity error and recovered observations through the same bridge',()=>{
 for(const state of Object.values(historyRecovery)){
  expect(validExecutionView(state.execution)).toBe(true);const client=new RoomAgentClient();expect(client.receive(state)).toBe(true);client.cancel();
 }
 expect(historyRecovery.error.execution.recovery!.id).toMatch(/^[a-f0-9]{32}$/);
 expect(historyRecovery.success.execution.recovery).toBeNull();expect(historyRecovery.success.execution.nextRunId).toMatch(/^[a-f0-9]{32}$/);
 expect(historyRecovery.success.execution.running).toEqual([]);expect(historyRecovery.success.execution.outcomes).toEqual([]);
});
