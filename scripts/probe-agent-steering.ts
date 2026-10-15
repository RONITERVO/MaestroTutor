// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import type {HeadlessClient} from '../src/headless/client';
import {HeadlessRoomAgent,runHeadlessRoomTurn} from '../src/headless/roomJourney';
import {dispatchHeadlessMethod} from '../src/headless/dispatcher';
import {isRoomQuery,type RoomAgentLease,type RoomAgentState,type RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {RoomTaskRecord} from '../src/core-sdk/room/roomTaskHandoff';
import {isDeepStrictEqual} from 'node:util';
import {readNativeObjects,type NativeObjectReadSet} from './native-probe-read-set';
import {assertPaintedParityBall,assertSameRoomObjects} from './agent-provider-contract';

export type ReceiptObserver=(commands:RoomCommand[],receipt:RoomAgentState)=>Promise<void>;
const pause=(ms:number)=>new Promise(resolve=>setTimeout(resolve,ms));
export const STOP_PAINT_REQUEST='Ask the room agent to paint the existing ParityBall red, inspect that result, and only then resize it to scale 0.75. Use separate native edits so I can stop between them. Keep its identity, position and every other object unchanged. Do not add programs, buttons or objects.';

export interface PaintBoundary {commands:RoomCommand[];receipt:RoomAgentState}
const red=(r:unknown,g:unknown,b:unknown)=>typeof r==='number'&&typeof g==='number'&&typeof b==='number'&&[r,g,b].every(n=>Number.isFinite(n)&&n>=0&&n<=1)&&r>=.8&&g<=.2&&b<=.2;
const redCall=(command:RoomCommand,target:string)=>command.action==='execution'&&command.execution?.operation==='start'
 &&command.execution.call.id==='object.color.set'&&command.execution.call.version===1&&command.execution.call.arguments.target===target
 &&red(command.execution.call.arguments.red,command.execution.call.arguments.green,command.execution.call.arguments.blue);
/** Select an acknowledged paint, never a requested/failed/preparing action or a
 * stale selected receipt attached to an unrelated query. No inventory is read. */
export function confirmedRedPaint(operations:ReadonlyArray<{commands:RoomCommand[];receipt?:RoomAgentState}>,target:string):PaintBoundary|null {
 const issued=new Map<string,Extract<NonNullable<RoomCommand['execution']>,{operation:'start'}>>();
 for(const operation of operations){
  const {commands,receipt}=operation;if(!receipt?.ok)continue;
  for(const command of commands){
   if(command.action==='paint'&&command.target===target&&command.color?.a===1&&red(command.color.r,command.color.g,command.color.b))return {commands,receipt};
   if(redCall(command,target)&&command.execution?.operation==='start'&&command.execution.runId)issued.set(command.execution.runId,command.execution);
   if(command.action!=='execution'||!command.execution||!(command.execution.operation==='start'||command.execution.operation==='inspect'))continue;
   const id=command.execution.runId,expected=id&&issued.get(id),actual=receipt.execution?.selected;
   if(expected&&actual?.id===id&&actual.phase==='completed'&&actual.capability===expected.call.id&&actual.version===expected.call.version
     &&isDeepStrictEqual(actual.call,expected.call))return {commands,receipt};
  }
 }
 return null;
}
export function assertStoppedPaint(record:RoomTaskRecord,before:NativeObjectReadSet,painted:NativeObjectReadSet,target:string,boundary:PaintBoundary) {
 assert.equal(record.phase,'stopped');assert.equal(record.reply,undefined);
 assert.ok(record.operations.length>0&&record.operations.every(op=>op.receipt?.ok),'An action outcome is unconfirmed');
 const changes=record.operations.filter(op=>op.commands.some(command=>!isRoomQuery(command)));
 assert.equal(changes.length,1,'An extra edit ran before Stop');
 const edits=changes[0].commands.filter(command=>!isRoomQuery(command));assert.equal(edits.length,1,'An extra edit shared the paint batch');
 assert.ok(edits[0].action==='paint'&&edits[0].target===target||redCall(edits[0],target),'The acknowledged edit is not the requested paint');
 assert.deepEqual(confirmedRedPaint(record.operations,target),boundary,'The journal did not retain the exact acknowledged paint');
 assert.equal(boundary.receipt.session,painted.session);assert.equal(boundary.receipt.sceneRevision,painted.sceneRevision);
 assertPaintedParityBall(before,painted,target); // Explicit state also refuses the unfinished resize.
}
export function assertSteeringLink(record:RoomTaskRecord,parent:RoomTaskRecord,action:'revise'|'continue') {
 assert.equal(record.phase,'completed');assert.notEqual(record.id,parent.id);
 assert.deepEqual(record.directive,{action,taskId:parent.id});
 const related=record.relatedTask;assert.ok(related);
 assert.equal(related.id,parent.id);assert.equal(related.action,action);assert.equal(related.phase,parent.phase);
 assert.equal(related.wasRunning,false);assert.equal(related.unconfirmed,false);
 assert.deepEqual(related.requests,[...(parent.relatedTask?.requests??[]),parent.handoff.input.prompt]);
 assert.deepEqual(related.operations,parent.operations);
 assert.equal(record.handoff.nativeSession,parent.handoff.nativeSession);
 assert.equal(record.handoff.accessScope,parent.handoff.accessScope);
 assert.equal(record.handoff.conversationId,parent.handoff.conversationId);
}

/** Stops at a real acknowledgement boundary without changing any provider response
 * or native receipt. "Manual" edits invoke the same native edit handlers; this
 * does not simulate physical grip, a crashed process, or a lost acknowledgement. */
export async function runAgentSteeringProof(input:{client:HeadlessClient;before:RoomAgentState;target:string;directory:string;
 providerMode:'live'|'scripted';execute:(commands:RoomCommand[])=>Promise<RoomAgentState>;lease:()=>RoomAgentLease;observe:(observer?:ReceiptObserver)=>void}) {
 const {client,before,target,directory,execute,lease,observe,providerMode}=input,agent=client.roomAgent!;assert.ok(agent);
 const beforeObjects=await readNativeObjects(execute);
 const evidence:Record<string,unknown>={phase:'stopping',providerMode,beforeObjects,boundary:(providerMode==='live'?'Real provider':'Scripted provider responses, no paid-provider acceptance')+' through actual chat/verifier/task/native handlers; deterministic Stop immediately after a native paint acknowledgement. Manual edits use ordinary native handlers. Reopen uses a fresh host/store instance, not an OS crash or Quest input.'};
 const save=()=>writeFile(join(directory,'provider-steering.json'),JSON.stringify(evidence,null,2));
 let boundary:PaintBoundary|undefined,stoppedId:string|undefined;
 const observed:Array<PaintBoundary>=[];
 observe(async(commands,receipt)=>{
  observed.push({commands:structuredClone(commands),receipt:structuredClone(receipt)});
  if(boundary)return;const confirmed=confirmedRedPaint(observed,target);if(!confirmed)return;
  boundary=structuredClone(confirmed);observe();evidence.paintReceipt=boundary.receipt;
  assert.equal(boundary.receipt.session,before.session);
  const active=(await agent.store.list()).filter(record=>record.handoff.input.prompt===STOP_PAINT_REQUEST&&agent.tasks.running(record.id));
  assert.equal(active.length,1);stoppedId=active[0].id;
  evidence.stop=await dispatchHeadlessMethod(client,'room.stop',{taskId:stoppedId});
  assert.deepEqual(evidence.stop,{taskId:stoppedId,stopRequested:true});
  // Return the untouched receipt for durable recording before the abort fence.
  // Inspection and manual edits wait until the task has left this command lease.
 });
 try {
  let stoppedEvidence:Awaited<ReturnType<typeof runHeadlessRoomTurn>>|undefined;
  try {await runHeadlessRoomTurn(client,{text:STOP_PAINT_REQUEST});throw new Error('The stopped task unexpectedly completed');}
  catch(error) {
   stoppedEvidence=(error as {evidence?:typeof stoppedEvidence}).evidence;
   if(!stoppedEvidence)throw error;
  }
  evidence.stoppedJourney=stoppedEvidence;await save();
  assert.ok(boundary&&stoppedId,'The native paint/Stop boundary was not reached');
  assert.equal(agent.tasks.running(stoppedId),false);
  const painted=await readNativeObjects(execute);evidence.paintedObjects=painted;
  // The ordinary positive journey correctly rejects a stopped result: no result
  // narration is generated, and its status remains projected into the source chat state.
  for(const [key,value] of Object.entries(stoppedEvidence.coverage))assert.equal(value,!['completed','replyInChat','providerUsage'].includes(key),key);
  assert.ok(stoppedEvidence.usage.some(row=>row.stage==='planning'&&Number((row.usageMetadata as {totalTokenCount?:number})?.totalTokenCount)>0));
  assert.ok(!stoppedEvidence.usage.some(row=>row.stage==='reply'));
  const stopped=(await agent.store.get(stoppedId))!;assertStoppedPaint(stopped,beforeObjects,painted,target,boundary);
  evidence.stopped=stopped;
  const history=client.state.chats[stopped.handoff.conversationId];
  assert.ok(history.some(message=>message.id===stoppedId&&message.role==='status'&&message.agentTask?.phase==='stopped'));
  const ball=painted.objects.find(object=>object.id===target)!;
  await execute([{action:'paint',target,color:{r:0,g:1,b:0,a:1}}]);
  await execute([{action:'move',target,position:{...ball.position,x:ball.position.x+.25}}]);
  const manual=await readNativeObjects(execute);evidence.manualObjects=manual;await save();
  await pause(1200);assertSameRoomObjects(manual,await readNativeObjects(execute));
  const usage=agent.usage.length,revision=lease().state().sceneRevision;
  assert.equal((await agent.start(stopped.handoff.sourceAssistantId)).phase,'stopped');
  assert.equal(agent.usage.length,usage);assert.equal(lease().state().sceneRevision,revision);
  evidence.phase='revising';await save();
  const revisedJourney=await runHeadlessRoomTurn(client,{text:'Revise that stopped painting task: paint the existing ParityBall blue instead. Cancel its unfinished resize; keep the current size and the position I just set by hand. Do not repeat the red paint, move it, undo anything, add or delete objects.'});
  evidence.revisedJourney=revisedJourney;await save();
  const revised=(await agent.store.get(revisedJourney.task!.id))!;assertSteeringLink(revised,stopped,'revise');
  const afterRevision=await readNativeObjects(execute),blue=afterRevision.objects.find(object=>object.id===target)!;
  assert.ok(blue.color.b>.8&&blue.color.r<.2&&blue.color.g<.2);
  assertSameRoomObjects({...manual,objects:manual.objects.map(object=>object.id===target?{...object,color:blue.color}:object)},afterRevision);
  assert.deepEqual(await agent.store.get(stopped.id),stopped);
  evidence.revised=revised;evidence.afterRevision=afterRevision;evidence.phase='continuing';await save();
  const continuedJourney=await runHeadlessRoomTurn(client,{text:'Continue the completed revised painting task by inspecting the current ParityBall and reporting its current colour, scale and position. Read only. Do not repaint, resize, move, undo or create anything.',requireActions:false});
  evidence.continuedJourney=continuedJourney;await save();
  const continued=(await agent.store.get(continuedJourney.task!.id))!;assertSteeringLink(continued,revised,'continue');
  assert.ok(continued.operations.every(op=>op.commands.every(isRoomQuery)));assertSameRoomObjects(afterRevision,await readNativeObjects(execute));
  const translated=continued.reply!.parsed.translations.map(pair=>pair.native).join(' ');
  assert.match(translated,/blue/i);
  evidence.continued=continued;evidence.phase='reopening';await save();
  const records=await agent.store.list(),usageBefore=agent.usage.length,sceneRevision=lease().state().sceneRevision;
  const restored=new HeadlessRoomAgent(client,lease);
  await restored.restore();
  assert.deepEqual(await restored.store.list(),records);
  for(const record of [stopped,revised,continued]){
   assert.equal((await restored.store.claim(record)).claimed,false);
   await assert.rejects(restored.start(record.handoff.sourceAssistantId),/no longer available/);
   assert.ok(client.state.chats[record.handoff.conversationId].some(message=>message.id===record.id&&message.agentTask?.phase===record.phase));
  }
  await pause(700);assert.equal(restored.usage.length,0);assert.equal(agent.usage.length,usageBefore);
  assert.equal(lease().state().sceneRevision,sceneRevision);assertSameRoomObjects(afterRevision,await readNativeObjects(execute));
  evidence.phase='passed';evidence.semantics={realFirstEdit:true,stopViaPublicControl:true,confirmedReceiptPreserved:true,unfinishedResizeCancelled:true,
   stoppedStatusInChatState:true,manualEditPreserved:true,duplicateStoppedHandoffNoReplay:true,exactRevisionParent:true,revisionPreservesManualPlacement:true,
   exactContinuationParent:true,readOnlyContinuation:true,originalRequestChain:true,inspectionAfterTaskIdle:true,freshHostRestoresStatuses:true,durableClaimsPreventReplay:true,noRestoreProviderUsage:true};
 }catch(error){evidence.phase='failed';evidence.error=String(error);throw error;}
 finally{observe();await save();}
 return evidence;
}
