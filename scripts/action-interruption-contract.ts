// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {join,resolve} from 'node:path';
import {validExecutionView} from '../shared/roomExecutions';
import {restartCheckpoint,validateRestartProcess} from './first-learner-native-restart';
import {assertResumedNativeIdentity} from './native-probe-identity';
import {WAVE_CALL,WAVE_REQUEST} from './probe-native-action-loss';

export function assertRunningSource(source:any) {
 assert.equal(source.version,1);assert.equal(source.phase,'awaiting-process-termination');
 assert.equal(source.providerUsed,false);assert.equal(source.networkAttempts,0);
 const original=source.original;assert.equal(original.phase,'working');assert.equal(original.reply,undefined);
 assert.equal(original.handoff.input.prompt,WAVE_REQUEST);assert.equal(original.id,source.request.taskId);
 assert.equal(original.handoff.nativeSession,source.request.nativeSession);
 assert.equal(source.request.version,2);assert.equal(source.request.operation,'restart');
 assert.equal(source.original.handoff.sourceAssistantId,source.assistantId);
 assert.equal(source.original.handoff.sourceUserId,source.userId);
 assert.equal(original.operations.length,4);
 for(const op of original.operations){assert.equal(op.commands.length,1);assert.equal(op.receipt.ok,true);assert.equal(op.receipt.session,source.request.nativeSession);}
 const ops=original.operations;
 assert.equal(ops[0].commands[0].catalog?.capability,'animation.play');
 assert.equal(ops[1].commands[0].catalog?.capability,'object.definition');
 assert.equal(ops[2].commands[0].catalog?.capability,'room.persistence');
 for(const op of ops.slice(0,3)){assert.equal(op.commands[0].action,'catalog');assert.equal(op.commands[0].catalog.operation,'inspect');}
 assert.deepEqual(restartCheckpoint(ops[2].receipt.catalog.value),restartCheckpoint(source.request.checkpoint));
 const last=ops[3];assert.deepEqual(last.commands,[{action:'execution',execution:{operation:'start',runId:source.request.runId,call:WAVE_CALL}}]);
 assert(validExecutionView(last.receipt.execution));const selected=last.receipt.execution.selected;assert(selected);
 assert.equal(selected.id,source.request.runId);assert.deepEqual(selected.call,WAVE_CALL);assert.equal(selected.phase,'running');
 assert.equal(source.identity.session,source.request.nativeSession);
 assert.deepEqual(source.dispatches,ops.map((op:any)=>({commands:op.commands,receipt:op.receipt})));
 assert.equal(source.requests.length,7);assert.equal(source.requests[6].kind,'planner');assert.equal(source.requests[6].response,undefined);
 assert.deepEqual(source.requests.map((x:any)=>x.kind),['chat','verifier','planner','planner','planner','planner','planner']);
 assert.equal(source.nodeProcessId>0,true);
 return selected;
}
export function assertInterruptedTask(original:any,restored:any) {
 assert.equal(restored.phase,'interrupted');assert.equal(restored.readOnly,true);assert.equal(restored.reply,undefined);
 assert.match(restored.note,/cannot resume.*unfinished task/i);
 const {phase:oldPhase,readOnly:oldReadonly,note:oldNote,...before}=original;
 const {phase,readOnly,note,...after}=restored;
 assert.equal(oldPhase,'working');assert(!oldReadonly);assert.deepEqual(after,before);
}
export function assertActionInterruptionResult(result:any,root:string,requestBytes:string) {
 const s=result.source;const selected=assertRunningSource(s);const q=JSON.parse(requestBytes);assert.deepEqual(q,s.request);
 assert.equal(result.version,1);assert.equal(result.status,'passed');assert.equal(result.providerUsed,false);assert.equal(result.physicalDevice,false);
 assert.equal(result.networkAttempts,0);assert.equal(result.id,q.id);
 assert.equal(resolve(s.profileDirectory),join(resolve(root),'profiles','action-interruption'));
 assert.equal(result.profileDirectory,s.profileDirectory);assert.equal(result.beforeProfileSha256,s.profileSha256);
 assert.equal(result.beforeJournalSha256,s.journalSha256);assert.equal(result.afterJournalSha256,s.journalSha256);
 for(const hash of [s.profileSha256,s.journalSha256])assert.match(hash,/^[a-f0-9]{64}$/);
 assertInterruptedTask(s.original,result.restored);
 assert.equal(result.chatProjection.phase,'interrupted');assert.equal(result.chatProjection.id,s.original.id);
 assert.equal(result.automaticProviderCalls,0);assert.equal(result.automaticDispatches,0);
 assert.equal(result.duplicateClaimed,false);assert.equal(result.oldHandoffRejected,true);
 const p=validateRestartProcess(result.nativeProcess,s.ready,result.ready,root,q.id,requestBytes),c=result.clientProcess;
 assert.equal(c.version,1);assert.equal(c.id,q.id);assert.equal(resolve(c.profileDirectory),resolve(s.profileDirectory));
 for(const side of [c.source,c.resumed]){assert(Number.isSafeInteger(side.processId)&&side.processId>0);assert(Number.isSafeInteger(side.startedAt)&&side.startedAt>0);}
 assert.equal(c.source.processId,s.nodeProcessId);assert.equal(c.resumed.processId,result.nodeProcessId);
 assert.notEqual(c.source.processId,c.resumed.processId);assert.notEqual(c.source.processId,p.source.processId);assert.notEqual(c.resumed.processId,p.resumed.processId);
 assert(Number.isSafeInteger(c.source.exitedAt)&&c.source.exitedAt>=c.source.startedAt);assert(Number.isSafeInteger(c.source.exitCode));
 assert(c.source.exitedAt<=p.source.exitedAt);assert(c.resumed.startedAt>=p.source.exitedAt);assert(c.resumed.startedAt>=c.source.exitedAt);
 assert.equal(c.source.normalExit,false);
 assertResumedNativeIdentity(s.identity,result.identity);
 assert.notEqual(result.clientId,q.clientId);
 const inspected=result.inspectionTask;assert.equal(inspected.phase,'completed');assert.equal(inspected.operations.length,1);
 const op=inspected.operations[0];assert.deepEqual(op.commands,[{action:'execution',execution:{operation:'inspect',runId:selected.id}}]);
 assert.equal(op.receipt.session,result.identity.session);assert.equal(op.receipt.ok,true);
 assert(validExecutionView(op.receipt.execution));const resumed=op.receipt.execution.selected;assert(resumed);
 assert.equal(resumed.id,selected.id);assert.deepEqual(resumed.call,selected.call);assert.equal(resumed.phase,'interrupted');assert.equal(resumed.output,undefined);
 assert.deepEqual(resumed.resources,selected.resources);assert.equal(op.receipt.execution.running.length,0);
 assert.deepEqual(result.dispatches,[{commands:op.commands,receipt:op.receipt}]);
 assert.equal(result.requests.length,5);assert.deepEqual(result.requests.map((x:any)=>x.kind),['chat','verifier','planner','planner','chat']);
 assert.equal(result.restoredSettings.selectedLanguagePairId,s.original.handoff.conversationId);
 assert.deepEqual(result.finalOriginal,result.restored);
 return {runId:selected.id,sourceTask:s.original.id,nativePhase:resumed.phase};
}
