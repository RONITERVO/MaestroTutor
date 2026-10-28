// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {createHeadlessClient,type HeadlessClient} from '../src/headless/client';
import {HeadlessRoomAgent} from '../src/headless/roomJourney';
import {selectHeadlessLanguage,runHeadlessChatTurn} from '../src/headless/chatJourney';
import {runHeadlessSuggestionAftersteps} from '../src/headless/suggestionJourney';
import type {RoomAgentLease,RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {RoomTaskRecord} from '../src/core-sdk/room/roomTaskHandoff';
import {validExecutionView} from '../shared/roomExecutions';
import {readNativeObjects} from './native-probe-read-set';
import {assertSameRoomObjects} from './agent-provider-contract';
import {bookProviderStage} from './book-provider-evidence';

export const WAVE_REQUEST='Please ask Maestro to wave hello for thirty seconds while I practise saying hola. Tell me when it has finished.';
const proposal='Voy a ayudarte.\n[EN]I will help with that.\n```maestro-tool\n{"tool":"agent"}\n```';
const verifier=JSON.stringify({suggestions:[{target:'Hola.',native:'Hello.'}],toolRequest:{tool:'agent'}});
export const WAVE_CALL={id:'animation.play',version:1,arguments:{target:'maestro',seconds:30,source:{kind:'gesture',gesture:'greeting'},channel:'wholeTarget'}};
type Dispatch={commands:RoomCommand[];receipt:RoomAgentState};
const start=(command:RoomCommand)=>command.action==='execution'&&command.execution?.operation==='start'?command.execution:null;
/** The harness saw a real running receipt, but the task must retain only its
 * durable intent. Never repair the journal by injecting the withheld evidence. */
export function assertLostAction(record:RoomTaskRecord,boundary:Dispatch) {
 assert.equal(record.phase,'interrupted');assert.equal(record.reply,undefined);assert.match(record.note,/unconfirmed/i);
 assert.equal(boundary.commands.length,1);const issued=start(boundary.commands[0]);assert(issued?.runId);assert.deepEqual(issued.call,WAVE_CALL);
 assert.equal(boundary.receipt.ok,true);assert(validExecutionView(boundary.receipt.execution));
 const selected=boundary.receipt.execution!.selected;assert(selected);assert.equal(selected.id,issued.runId);assert.deepEqual(selected.call,issued.call);
 assert.equal(selected.phase,'running','The interruption must happen before the native action completes');
 assert.equal(record.handoff.input.prompt,WAVE_REQUEST);assert.equal(record.handoff.nativeSession,boundary.receipt.session);
 assert.equal(record.operations.length,3);const pending=record.operations.at(-1)!;
 assert.deepEqual(pending.commands,boundary.commands);assert.equal(pending.receipt,undefined);
 assert(record.operations.slice(0,-1).every(op=>op.commands.length===1&&op.commands[0].action==='catalog'&&op.receipt?.ok===true));
 assert(record.operations[0].commands[0].catalog?.operation==='inspect'&&record.operations[0].commands[0].catalog.capability==='animation.play');
 assert(record.operations[1].commands[0].catalog?.operation==='inspect'&&record.operations[1].commands[0].catalog.capability==='object.definition');
 return selected;
}
export function assertWriteAheadAction(before:RoomTaskRecord,after:RoomTaskRecord,boundary:Dispatch) {
 assertLostAction(after,boundary);assert.equal(before.phase,'working');assert.equal(before.reply,undefined);
 assert.equal(before.id,after.id);assert.deepEqual(before.handoff,after.handoff);assert.deepEqual(before.operations,after.operations);
 assert(before.updatedAt<=after.updatedAt);assert.equal(before.startedAt,after.startedAt);
}
export function assertActionLossRecovery(original:RoomTaskRecord,restored:RoomTaskRecord,boundary:Dispatch,inspected:RoomAgentState,cancelled:RoomAgentState) {
 const expected=assertLostAction(original,boundary);assert.deepEqual(restored,original);
 for(const [state,phase] of [[inspected,'running'],[cancelled,'cancelled']] as const){
  assert.equal(state.session,boundary.receipt.session);assert.equal(state.ok,true);assert(validExecutionView(state.execution));
  const selected=state.execution!.selected;assert(selected);assert.equal(selected.id,expected.id);assert.deepEqual(selected.call,expected.call);assert.equal(selected.phase,phase);
 }
}

/** Deliberate loss ONLY at the host delivery boundary after real Unity accepts
 * one timed action. The task, durable writes and native handlers are unmodified.
 * A fresh profile/host load exercises recovery; neither OS process is crashed. */
export async function probeNativeActionLoss(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,lease:()=>RoomAgentLease,directory:string) {
 const baseline=await readNativeObjects(execute);assert.equal(baseline.temporary,false);const requests:unknown[]=[],dispatches:Dispatch[]=[];
 const originalFetch=globalThis.fetch;let networkAttempts=0;
 globalThis.fetch=async()=>{networkAttempts++;throw new Error('Offline action-loss fixture cannot use the network');};
 const evidence:Record<string,unknown>={version:1,phase:'setup',baseline,providerUsed:false,physicalDevice:false,
  boundary:'Actual production headless chat/verifier/task/native execution, with scripted provider streams and one intentionally withheld native start receipt. Fresh host/profile recovery, not an OS crash, native restart, physical input or real provider proof.'};
 let first:HeadlessClient|undefined,reopened:HeadlessClient|undefined,lost:Dispatch|undefined,cleaned=false;
 let stage:'wave'|'inspect'|'cancel'='wave',step=0;
 const profileOptions={accessMode:'managed' as const,profileName:'action-loss',dataRoot:join(directory,'profiles')};
 try{
  const provider:HeadlessClient['ai']={models:{generateContentStream:async(request:Parameters<HeadlessClient['ai']['models']['generateContentStream']>[0])=>{
   const kind=bookProviderStage(request);step++;let text:string;
   if(step===1){assert.equal(kind,'chat');text=proposal;}
   else if(step===2){assert.equal(kind,'verifier');text=verifier;}
   else {
    const commands:RoomCommand[]=[];
    if(stage==='wave'){
     assert.equal(kind,'planner');
     if(step===3)commands.push({action:'catalog',catalog:{operation:'inspect',capability:'animation.play',version:1}});
     else if(step===4)commands.push({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:'maestro'}}});
     else {assert.equal(step,5,'The lost action must not trigger another provider call');commands.push({action:'execution',execution:{operation:'start',call:WAVE_CALL}});}
    }else if(step===3){
     assert.equal(kind,'planner');assert(lost);const id=lost.receipt.execution!.selected!.id;
     // This ID is exposed by the actual native scene; no invented action ID.
     const serialized=JSON.stringify(request);assert(serialized.includes(id));
     commands.push({action:'execution',execution:{operation:'inspect',runId:id}});
    }else if(stage==='cancel'&&step===4){assert.equal(kind,'planner');assert(lost);commands.push({action:'execution',execution:{operation:'cancel',runId:lost.receipt.execution!.selected!.id}});}
    else if(step===(stage==='cancel'?6:5)){
     assert.equal(kind,'chat');text=stage==='cancel'?'El saludo se ha detenido.\n[EN]The wave has stopped.':'Maestro sigue saludando.\n[EN]The wave is still running. The earlier request was interrupted.';
    }else {assert.equal(kind,'planner');assert.equal(step,stage==='cancel'?5:4);}
    text??=JSON.stringify({commands});
   }
   requests.push({stage,step,kind,request,response:text,syntheticTokens:20});
   return(async function*(){yield {text,modelVersion:'offline-action-loss-fixture',usageMetadata:{totalTokenCount:20},candidates:[{content:{role:'model',parts:[{text}]}}]};})();
  }}} as unknown as HeadlessClient['ai'];
  const attach=(client:HeadlessClient,withhold:boolean)=>{
   client.accessMode='byok';client.ai=provider;
   const agent:HeadlessRoomAgent=new HeadlessRoomAgent(client,()=>{const native=lease();return {...native,execute:async(...args)=>{
    const losing=withhold&&args[0].some(c=>start(c)?.call.id==='animation.play');
    if(losing){
     const records=(await agent.store.list()).filter(record=>agent.tasks.running(record.id));assert.equal(records.length,1);
     const before=records[0];assert.equal(before.phase,'working');assert.deepEqual(before.operations.at(-1)?.commands,args[0]);assert.equal(before.operations.at(-1)?.receipt,undefined);
     evidence.beforeDispatch=structuredClone(before);
    }
    const receipt=await native.execute(...args),dispatch={commands:structuredClone(args[0]),receipt:structuredClone(receipt)};dispatches.push(dispatch);
    if(losing){
     assert.equal(lost,undefined,'A start must never repeat');lost=dispatch;
     assert.equal(receipt.execution?.selected?.phase,'running');
     throw new DOMException('Deliberately lost native acknowledgement after acceptance.','AbortError');
    }
    return receipt;
   }};});client.roomAgent=agent;return agent;
  };
  first=await createHeadlessClient(profileOptions);const agent=attach(first,true);
  await selectHeadlessLanguage(first,{targetLanguageCode:'es-ES',nativeLanguageCode:'en-US'});
  const turn=await runHeadlessChatTurn(first,{text:WAVE_REQUEST,useGoogleSearch:false});
  await runHeadlessSuggestionAftersteps(first,{assistantMessageId:turn.assistantMessage.id});
  assert.equal(step,5);assert(lost);const id=`room-task:${turn.assistantMessage.id}`,record=await agent.store.get(id);assert(record);
  const selected=assertLostAction(record,lost);assertWriteAheadAction(evidence.beforeDispatch as RoomTaskRecord,record,lost);evidence.lost=lost;evidence.original=record;
  assert.equal(agent.tasks.running(id),false);assert.equal(first.state.chats[record.handoff.conversationId].find(m=>m.id===id)?.agentTask?.phase,'interrupted');
  assert.deepEqual(await agent.start(turn.assistantMessage.id),record);assert.equal(step,5);assert.equal(dispatches.length,3);
  await agent.disconnect();first.roomAgent=undefined;
  // Read the real persisted profile using a new client and store. No state copy.
  reopened=await createHeadlessClient(profileOptions);const restoredAgent=attach(reopened,false);await restoredAgent.restore();
  const restored=await restoredAgent.store.get(id);assert(restored);assert.deepEqual(restored,record);
  assert.equal(reopened.state.chats[record.handoff.conversationId].find(m=>m.id===id)?.agentTask?.phase,'interrupted');
  assert.equal((await restoredAgent.store.claim(record)).claimed,false);
  await assert.rejects(restoredAgent.start(turn.assistantMessage.id),/no longer available/);
  assert.equal(step,5);assert.equal(dispatches.length,3);assert.equal(restoredAgent.usage.length,0);
  evidence.restored=restored;
  stage='inspect';step=0;
  const check=await runHeadlessChatTurn(reopened,{text:'Did the wave actually start? Please only check what happened. Do not repeat it.',useGoogleSearch:false});
  await runHeadlessSuggestionAftersteps(reopened,{assistantMessageId:check.assistantMessage.id});
  const inspectedTask=await restoredAgent.store.get(`room-task:${check.assistantMessage.id}`);assert(inspectedTask?.phase==='completed');assert.equal(inspectedTask.operations.length,1);
  const inspected=inspectedTask.operations[0].receipt;assert(inspected);assert.equal(step,5);evidence.inspectionTask=inspectedTask;
  stage='cancel';step=0;
  const stop=await runHeadlessChatTurn(reopened,{text:'Please stop that wave now. Leave everything else alone.',useGoogleSearch:false});
  await runHeadlessSuggestionAftersteps(reopened,{assistantMessageId:stop.assistantMessage.id});
  const cancelledTask=await restoredAgent.store.get(`room-task:${stop.assistantMessage.id}`);assert(cancelledTask?.phase==='completed');assert.equal(cancelledTask.operations.length,2);
  const cancelled=cancelledTask.operations[1].receipt;assert(cancelled);assert.equal(step,6);evidence.cancellationTask=cancelledTask;
  assertActionLossRecovery(record,restored,lost,inspected,cancelled);cleaned=true;
  assert.deepEqual(await restoredAgent.store.get(id),record,'Later inspection must not rewrite the uncertain historical intent');
  const commands=dispatches.flatMap(d=>d.commands),starts=commands.filter(c=>start(c));assert.equal(starts.length,1);assert.equal(start(starts[0])!.runId,selected.id);
  assert.deepEqual(commands.slice(3),[
   {action:'execution',execution:{operation:'inspect',runId:selected.id}},
   {action:'execution',execution:{operation:'inspect',runId:selected.id}},
   {action:'execution',execution:{operation:'cancel',runId:selected.id}},
  ]);
  assertSameRoomObjects(baseline,await readNativeObjects(execute));assert.equal(networkAttempts,0);
  evidence.phase='passed';evidence.semantics={nativeActionActuallyRunning:true,writeAheadIntent:true,missingReceiptStaysUnconfirmed:true,interruptedStatusInChat:true,duplicateHandoffNoReplay:true,freshProfileHost:true,noProviderOnRestore:true,explicitReadBeforeRetry:true,sameActionCancelled:true,originalJournalUnchanged:true};
 }catch(error){evidence.phase='failed';evidence.error=String(error);throw error;}
 finally{
  try{
   await first?.roomAgent?.disconnect();await reopened?.roomAgent?.disconnect();
   if(!cleaned&&lost?.receipt.execution?.selected?.id)await execute([{action:'execution',execution:{operation:'cancel',runId:lost.receipt.execution.selected.id}}]);
  }catch(error){evidence.phase='failed';evidence.cleanupError=String(error);throw error;}
  finally{globalThis.fetch=originalFetch;await writeFile(join(directory,'native-action-loss.json'),JSON.stringify({...evidence,requests,dispatches,networkAttempts},null,2));}
 }
 return evidence;
}
