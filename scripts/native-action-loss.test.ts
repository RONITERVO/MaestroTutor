// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {it,expect} from 'vitest';
import {assertLostAction,assertActionLossRecovery,assertWriteAheadAction,WAVE_CALL,WAVE_REQUEST} from './probe-native-action-loss';
import type {RoomTaskRecord} from '../src/core-sdk/room/roomTaskHandoff';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
const runId='a'.repeat(32);
function fixture(){
 const selected={id:runId,capability:WAVE_CALL.id,version:1,resources:['maestro'],phase:'running' as const,status:'Running',call:structuredClone(WAVE_CALL)};
 const summary={id:runId,capability:WAVE_CALL.id,version:1,resources:['maestro'],phase:'running' as const,status:'Running'};
 const state:RoomAgentState={version:1,session:'native',revision:5,sceneRevision:1,ack:3,ok:true,status:'Accepted',objects:[],created:[],canUndo:false,canRedo:false,physicsRunning:false,execution:{selected,running:[summary],outcomes:[]}};
 const commands:RoomCommand[]=[{action:'execution',execution:{operation:'start',runId,call:structuredClone(WAVE_CALL)}}];
 const boundary={commands,receipt:state};
 const original={version:1,id:'room-task:assistant',phase:'interrupted',note:'Stopped with an unconfirmed action. Inspect the room before trying again.',startedAt:1,updatedAt:2,
  handoff:{version:1,id:'room-task:assistant',sourceUserId:'user',sourceAssistantId:'assistant',conversationId:'es',nativeSession:'native',accessScope:'byok',input:{prompt:WAVE_REQUEST}},
  operations:[
   {commands:[{action:'catalog',catalog:{operation:'inspect',capability:'animation.play',version:1}}],sceneRevision:1,receipt:structuredClone(state)},
   {commands:[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:'maestro'}}}],sceneRevision:1,receipt:structuredClone(state)},
   {commands:structuredClone(commands),sceneRevision:1}
  ]} as RoomTaskRecord;
 const cancelled=structuredClone(state);cancelled.execution!.selected!.phase='cancelled';cancelled.execution!.running=[];cancelled.execution!.outcomes=[{...summary,phase:'cancelled'}];
 return {original,boundary,inspected:structuredClone(state),cancelled};
}
it('requires an actual running action, durable pending intent and the same explicit inspection/cancellation',()=>{
 const f=fixture();expect(()=>assertActionLossRecovery(f.original,structuredClone(f.original),f.boundary,f.inspected,f.cancelled)).not.toThrow();
});
it.each(['completed','receiptInjected','missingIntent','extraEdit','wrongRequest','wrongSession','successReply'])('refuses false lost-action journal evidence: %s',change=>{
 const f=fixture();
 if(change==='completed')f.original.phase='completed';
 if(change==='receiptInjected')f.original.operations[2].receipt=structuredClone(f.boundary.receipt);
 if(change==='missingIntent')f.original.operations.pop();
 if(change==='extraEdit')f.original.operations[0].commands=[{action:'undo'}];
 if(change==='wrongRequest')f.original.handoff.input.prompt='Another request';
 if(change==='wrongSession')f.original.handoff.nativeSession='elsewhere';
 if(change==='successReply')f.original.reply={} as RoomTaskRecord['reply'];
 expect(()=>assertLostAction(f.original,f.boundary)).toThrow();
});
it.each(['preparing','completed','failed','rejected','wrongId','wrongCall','extraStart','missingRunId'])('refuses an unproved native action: %s',change=>{
 const f=fixture(),selected=f.boundary.receipt.execution!.selected!;
 if(['preparing','completed','failed'].includes(change))selected.phase=change as typeof selected.phase;
 if(change==='rejected')f.boundary.receipt.ok=false;
 if(change==='wrongId')selected.id='b'.repeat(32);
 if(change==='wrongCall')selected.call.arguments.seconds=2;
 if(change==='extraStart')f.boundary.commands.push(structuredClone(f.boundary.commands[0]));
 if(change==='missingRunId')delete (f.boundary.commands[0].execution as {runId?:string}).runId;
 expect(()=>assertLostAction(f.original,f.boundary)).toThrow();
});
it.each(['journalRewritten','foreignInspection','alreadyCompleted','replacementCancelled','differentCall','stillRunning','rejectedCancel'])('refuses unsafe recovery evidence: %s',change=>{
 const f=fixture(),restored=structuredClone(f.original);
 if(change==='journalRewritten')restored.operations[2].receipt=structuredClone(f.inspected);
 if(change==='foreignInspection')f.inspected.session='different';
 if(change==='alreadyCompleted')f.inspected.execution!.selected!.phase='completed';
 if(change==='replacementCancelled')f.cancelled.execution!.selected!.id='b'.repeat(32);
 if(change==='differentCall')f.cancelled.execution!.selected!.call.arguments.seconds=5;
 if(change==='stillRunning')f.cancelled.execution!.selected!.phase='running';
 if(change==='rejectedCancel')f.cancelled.ok=false;
 expect(()=>assertActionLossRecovery(f.original,restored,f.boundary,f.inspected,f.cancelled)).toThrow();
});

it('requires the exact durable intent observed before dispatch, not a later repaired copy',()=>{
 const f=fixture(),before=structuredClone(f.original);before.phase='working';before.note='Applying the next action.';before.updatedAt=1;
 expect(()=>assertWriteAheadAction(before,f.original,f.boundary)).not.toThrow();
 for(const edit of [(r:RoomTaskRecord)=>{r.phase='completed';},(r:RoomTaskRecord)=>{r.id='different';},(r:RoomTaskRecord)=>{r.handoff.input.prompt='invented';},(r:RoomTaskRecord)=>{r.operations.pop();},(r:RoomTaskRecord)=>{r.updatedAt=3;}]){
  const wrong=structuredClone(before);edit(wrong);expect(()=>assertWriteAheadAction(wrong,f.original,f.boundary)).toThrow();
 }
});
