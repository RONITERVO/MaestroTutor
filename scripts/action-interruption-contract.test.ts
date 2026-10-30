// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {it,expect} from 'vitest';
import {createHash} from 'node:crypto';
import {join,resolve} from 'node:path';
import {assertActionInterruptionResult} from './action-interruption-contract';
import {WAVE_CALL,WAVE_REQUEST} from './probe-native-action-loss';
const id='1'.repeat(32),child='2'.repeat(32),session='3'.repeat(32),nextSession='4'.repeat(32),runId='5'.repeat(32);
function fixture(){
 const root=resolve('test-action-interruption'),profileDirectory=join(root,'profiles','action-interruption'),workspace=join(root,'workspace');
 const clean={phase:'idle',pending:false,dirty:false,dispatchHeld:false,error:''},checkpoint={room:clean,behaviours:clean};
 const request={version:2,id,operation:'restart',clientId:'6'.repeat(32),nativeSession:session,runId,taskId:'room-task:assistant',checkpoint};
 const bytes=JSON.stringify(request);
 const detail={id:runId,capability:'animation.play',version:1,resources:['maestro'],phase:'running',status:'Running',call:structuredClone(WAVE_CALL)};
 const {call,...summary}=detail;
 const state={session,ok:true,execution:{selected:detail,running:[summary],outcomes:[]}};
 const ops=['animation.play','object.definition','room.persistence'].map(capability=>({commands:[{action:'catalog',catalog:{operation:'inspect',capability}}],receipt:{session,ok:true,catalog:{value:checkpoint}}}));
 const original:any={version:1,id:request.taskId,phase:'working',startedAt:1,updatedAt:2,handoff:{id:request.taskId,sourceAssistantId:'assistant',sourceUserId:'user',conversationId:'es-en',nativeSession:session,input:{prompt:WAVE_REQUEST}},
 operations:[...ops,{commands:[{action:'execution',execution:{operation:'start',runId,call:WAVE_CALL}}],receipt:state}]};
 const identity={version:1,source:'explicit-native-object-directory',session,worldId:'7'.repeat(32),sceneRevision:1,ids:['book','maestro']};
 const ready={version:1,id,processId:10,processStartedAt:1000,workspaceDirectory:workspace,restartOf:'',syntheticPhysics:true,syntheticScan:false};
 const source={version:1,phase:'awaiting-process-termination',providerUsed:false,networkAttempts:0,nodeProcessId:20,profileDirectory,profileSha256:'a'.repeat(64),journalSha256:'b'.repeat(64),identity,ready,original,assistantId:'assistant',userId:'user',request,
 requests:['chat','verifier','planner','planner','planner','planner','planner'].map(kind=>({kind})),dispatches:original.operations.map((op:any)=>({commands:op.commands,receipt:op.receipt}))};
 const restored={...structuredClone(original),phase:'interrupted',readOnly:true,note:'This process cannot resume the unfinished task.'};
 const interrupted={...detail,phase:'interrupted',status:'Interrupted'};
 const {call:ignored,...endSummary}=interrupted;
 const inspectionOp={commands:[{action:'execution',execution:{operation:'inspect',runId}}],receipt:{session:nextSession,ok:true,execution:{selected:interrupted,running:[],outcomes:[endSummary]}}};
 const r:any={version:1,status:'passed',id,providerUsed:false,physicalDevice:false,networkAttempts:0,source,profileDirectory,beforeProfileSha256:source.profileSha256,beforeJournalSha256:source.journalSha256,afterJournalSha256:source.journalSha256,
 restored,chatProjection:{id:original.id,phase:'interrupted'},duplicateClaimed:false,oldHandoffRejected:true,automaticProviderCalls:0,automaticDispatches:0,
 identity:{...identity,session:nextSession},ready:{...ready,id:child,processId:11,processStartedAt:3000,restartOf:id},nodeProcessId:21,clientId:'8'.repeat(32),
 requests:['chat','verifier','planner','planner','chat'].map(kind=>({kind})),dispatches:[inspectionOp],inspectionTask:{phase:'completed',operations:[inspectionOp]},restoredSettings:{selectedLanguagePairId:'es-en'},finalOriginal:structuredClone(restored),
 nativeProcess:{version:1,id,method:'terminate-after-checkpoint',workspace,requestSha256:createHash('sha256').update(bytes).digest('hex'),source:{id,processId:10,startedAt:1000,exitedAt:2000,exitCode:-1,terminalExisted:false},resumed:{id:child,directory:join(root,'restart'),processId:11,startedAt:3000}},
 clientProcess:{version:1,id,profileDirectory,source:{processId:20,startedAt:1100,exitedAt:1900,exitCode:-1,normalExit:false},resumed:{processId:21,startedAt:3100}}};
 return {r,root,bytes};
}
it('requires both observed process deaths, unchanged saved intent and explicit interrupted native inspection',()=>{
 const {r,root,bytes}=fixture();expect(()=>assertActionInterruptionResult(r,root,bytes)).not.toThrow();
});
const changes:Record<string,(r:any)=>void>={
 completedBeforeDeath:r=>r.source.original.phase='completed',
 lostAcknowledgementInstead:r=>delete r.source.original.operations[3].receipt,
 repeatedStart:r=>r.source.original.operations.push(r.source.original.operations[3]),
 savedDifferentRequest:r=>r.source.original.handoff.input.prompt='different',
 dirtyCheckpoint:r=>r.source.request.checkpoint.room.dirty=true,
 syntheticFinish:r=>r.source.requests[6].response='done',
 automaticSpend:r=>r.automaticProviderCalls=1,
 automaticReplay:r=>r.automaticDispatches=1,
 duplicateClaim:r=>r.duplicateClaimed=true,
 oldHandoffAccepted:r=>r.oldHandoffRejected=false,
 workingChat:r=>r.chatProjection.phase='working',
 rewrittenIntent:r=>r.restored.operations.pop(),
 writableRestore:r=>r.restored.readOnly=false,
 falseCompletion:r=>r.restored.reply={text:'done'},
 replacedProfile:r=>r.beforeProfileSha256='c'.repeat(64),
 replacedJournal:r=>r.afterJournalSha256='c'.repeat(64),
 copiedWorkspace:r=>r.nativeProcess.workspace=join(r.nativeProcess.workspace,'copy'),
 copiedProfile:r=>r.profileDirectory=join(r.profileDirectory,'copy'),
 liveOldClient:r=>r.clientProcess.source.normalExit=true,
 wrongClient:r=>r.clientProcess.source.processId=999,
 reusedClient:r=>r.nodeProcessId=r.clientProcess.resumed.processId=r.source.nodeProcessId,
 overlappingClient:r=>r.clientProcess.resumed.startedAt=1800,
 overlappingNative:r=>r.nativeProcess.resumed.startedAt=1800,
 noNativeDeath:r=>r.nativeProcess.source.terminalExisted=true,
 reusedSession:r=>r.identity.session=session,
 replacedObjects:r=>r.identity.ids=['book','maestro','wrong'],
 restartedAction:r=>r.inspectionTask.operations[0].receipt.execution.selected.phase='running',
 differentAction:r=>r.inspectionTask.operations[0].commands[0].execution.runId='9'.repeat(32),
 changedCall:r=>r.inspectionTask.operations[0].receipt.execution.selected.call.arguments.seconds=2,
 extraResumeAction:r=>r.dispatches.push(r.dispatches[0]),
 providerUsed:r=>r.networkAttempts=1,
 historicalRepair:r=>r.finalOriginal.note='completed after all',
 wrongLanguage:r=>r.restoredSettings.selectedLanguagePairId='fr'
};
it.each(Object.entries(changes))('refuses misleading interruption evidence: %s',(_,change)=>{
 const {r,root,bytes}=fixture();change(r);expect(()=>assertActionInterruptionResult(r,root,bytes)).toThrow();
});
