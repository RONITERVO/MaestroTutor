// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Explicit offline fixture. The parent kills this source process and Unity.
// No runtime save, acknowledgement, cancellation or cleanup is injected at death.
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
import {join,resolve} from 'node:path';
import {createHeadlessClient,type HeadlessClient} from '../src/headless/client';
import {HeadlessRoomAgent} from '../src/headless/roomJourney';
import {HeadlessRoomTransport,publishRoomProbeFile} from '../src/headless/roomTransport';
import {selectHeadlessLanguage,runHeadlessChatTurn} from '../src/headless/chatJourney';
import {runHeadlessSuggestionAftersteps} from '../src/headless/suggestionJourney';
import type {RoomCommand} from '../src/core-sdk/room/roomAgent';
import {readNativeObjectIdentity} from './native-probe-identity';
import {restartCheckpoint} from './first-learner-native-restart';
import {bookProviderStage} from './book-provider-evidence';
import {WAVE_CALL,WAVE_REQUEST} from './probe-native-action-loss';
import {assertRunningSource,assertInterruptedTask,assertActionInterruptionResult} from './action-interruption-contract';

const root=resolve(process.argv[2]||''),mode=process.argv[3];assert(['source','resume'].includes(mode));
const sourceMode=mode==='source',wire=sourceMode?root:join(root,'restart');
const read=async(path:string)=>JSON.parse(await readFile(path,'utf8'));
const hash=(bytes:Uint8Array|string)=>createHash('sha256').update(bytes).digest('hex');
const profileDirectory=join(root,'profiles','action-interruption');
const owner=await read(join(root,'owner.json'));assert.equal(owner.actionInterruption,true);assert.equal(owner.nativeRestart,true);
const requests:any[]=[],dispatches:any[]=[];let networkAttempts=0,step=0;
globalThis.fetch=async()=>{networkAttempts++;throw new Error('Offline interruption fixture cannot use the network');};
const transport=await HeadlessRoomTransport.connect(wire,120000),ready=await read(join(wire,'ready.json'));
const execute=async(commands:RoomCommand[])=>{
 const native=transport.lease(),prepared=await transport.client.prepare(commands,native.state());
 return native.execute(commands,prepared.state.sceneRevision,prepared.objects);
};
let client:HeadlessClient|undefined;
let source:any,original:any;
const journalPath=(id:string)=>join(profileDirectory,'room-tasks',hash(id)+'.json');
const save=(name:string,value:any)=>publishRoomProbeFile(join(root,name),JSON.stringify(value,null,2));
try{
 const identity=await readNativeObjectIdentity(execute);
 source=sourceMode?undefined:await read(join(root,'action-interruption-source.json'));
 const beforeProfileSha256=sourceMode?undefined:hash(await readFile(join(profileDirectory,'profile.json')));
 const beforeJournalSha256=sourceMode?undefined:hash(await readFile(journalPath(source.original.id)));
 client=await createHeadlessClient({accessMode:'managed',profileName:'action-interruption',dataRoot:join(root,'profiles')});
 assert.equal(client.profile.directory,profileDirectory);client.accessMode='byok';
 const agent=new HeadlessRoomAgent(client,()=>{
  const native=transport.lease();return {...native,execute:async(...args)=>{
   const receipt=await native.execute(...args);dispatches.push({commands:structuredClone(args[0]),receipt:structuredClone(receipt)});return receipt;
  }};
 });client.roomAgent=agent;
 client.ai={models:{generateContentStream:async(request:Parameters<HeadlessClient['ai']['models']['generateContentStream']>[0])=>{
  const kind=bookProviderStage(request);step++;let text:string;
  const item:any={step,kind,request};requests.push(item);
  if(step===1){assert.equal(kind,'chat');text='Voy a ayudarte.\n[EN]I will help with that.\n\x60\x60\x60maestro-tool\n{"tool":"agent"}\n\x60\x60\x60';}
  else if(step===2){assert.equal(kind,'verifier');text=JSON.stringify({suggestions:[{target:'Hola.',native:'Hello.'}],toolRequest:{tool:'agent'}});}
  else if(sourceMode){
   assert.equal(kind,'planner');let command:RoomCommand;
   if(step===3)command={action:'catalog',catalog:{operation:'inspect',capability:'animation.play',version:1}};
   else if(step===4)command={action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:'maestro'}}};
   else if(step===5)command={action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'room.persistence',version:1,arguments:{}}};
   else if(step===6)command={action:'execution',execution:{operation:'start',call:WAVE_CALL}};
   else {
    assert.equal(step,7);const records=await agent.store.list();assert.equal(records.length,1);original=records[0];
    const disk=await readFile(journalPath(original.id));assert.deepEqual(JSON.parse(disk.toString()),original);
    const profile=await client!.profile.load();
    assert(profile.chats[original.handoff.conversationId].some(m=>m.id===original.handoff.sourceUserId&&m.text===WAVE_REQUEST));
    assert(profile.chats[original.handoff.conversationId].some(m=>m.id===original.handoff.sourceAssistantId));
    const native=transport.lease().state(),checkpoint=restartCheckpoint(original.operations[2].receipt.catalog.value);
    const restartRequest={version:2,id:owner.id,operation:'restart',clientId:transport.client.snapshot().clientId,nativeSession:native.session,runId:original.operations[3].receipt.execution.selected.id,taskId:original.id,checkpoint};
    source={version:1,phase:'awaiting-process-termination',providerUsed:false,networkAttempts,nodeProcessId:process.pid,profileDirectory,
     profileSha256:hash(await readFile(join(profileDirectory,'profile.json'))),journalSha256:hash(disk),identity,ready,original,
     assistantId:original.handoff.sourceAssistantId,userId:original.handoff.sourceUserId,request:restartRequest,requests,dispatches};
    assertRunningSource(source);await save('action-interruption-source.json',source);
    await publishRoomProbeFile(join(root,'restart-request.json'),JSON.stringify(restartRequest));
    // Await OS termination without returning a planner response or cleaning up.
    await new Promise<never>((_,reject)=>setTimeout(()=>reject(new Error('Parent did not terminate the source process')),60000));
    throw new Error('Unreachable');
   }
   text=JSON.stringify({commands:[command]});
  }else if(step===3){assert.equal(kind,'planner');assert(JSON.stringify(request).includes(source.request.runId));text=JSON.stringify({commands:[{action:'execution',execution:{operation:'inspect',runId:source.request.runId}}]});}
  else if(step===4){assert.equal(kind,'planner');text=JSON.stringify({commands:[]});}
  else {assert.equal(step,5);assert.equal(kind,'chat');text='El saludo se interrumpió.\n[EN]The earlier wave was interrupted when the app stopped. It has not been repeated.';}
  item.response=text;
  return(async function*(){yield {text,modelVersion:'offline-process-interruption-fixture',usageMetadata:{totalTokenCount:20},candidates:[{content:{role:'model',parts:[{text}]}}]};})();
 }}} as unknown as HeadlessClient['ai'];
 if(sourceMode){
  await selectHeadlessLanguage(client,{targetLanguageCode:'es-ES',nativeLanguageCode:'en-US'});
  const turn=await runHeadlessChatTurn(client,{text:WAVE_REQUEST,useGoogleSearch:false});
  await runHeadlessSuggestionAftersteps(client,{assistantMessageId:turn.assistantMessage.id});
  throw new Error('The source must be terminated during its running task');
 }
 assertRunningSource(source);assert.equal(beforeProfileSha256,source.profileSha256);assert.equal(beforeJournalSha256,source.journalSha256);
 await agent.restore();const restored=await agent.store.get(source.original.id);assert(restored);assertInterruptedTask(source.original,restored);
 const projection=client.state.chats[restored.handoff.conversationId].find(m=>m.id===restored.id);assert(projection?.agentTask);
 const duplicate=await agent.store.claim(source.original);assert.equal(duplicate.claimed,false);
 await assert.rejects(agent.start(source.assistantId),/no longer available/);
 const automaticProviderCalls=requests.length,automaticDispatches=dispatches.length;
 assert.equal(automaticProviderCalls,0);assert.equal(automaticDispatches,0);
 const turn=await runHeadlessChatTurn(client,{text:'What happened to that wave when the app stopped? Please only check. Do not repeat it.',useGoogleSearch:false});
 await runHeadlessSuggestionAftersteps(client,{assistantMessageId:turn.assistantMessage.id});
 const inspectionTask=await agent.store.get('room-task:'+turn.assistantMessage.id);
 const result={version:1,status:'passed',id:owner.id,providerUsed:false,physicalDevice:false,networkAttempts,source,
  profileDirectory,beforeProfileSha256,beforeJournalSha256,afterJournalSha256:hash(await readFile(journalPath(source.original.id))),
  restored,chatProjection:projection.agentTask,duplicateClaimed:duplicate.claimed,oldHandoffRejected:true,automaticProviderCalls,automaticDispatches,
  identity,ready,nodeProcessId:process.pid,clientId:transport.client.snapshot().clientId,requests,dispatches,inspectionTask,
  restoredSettings:client.state.settings,finalOriginal:await agent.store.get(source.original.id),
  nativeProcess:await read(join(root,'restart.json')),clientProcess:await read(join(root,'client-restart.json')),
  boundary:'Actual owned Node and Unity process termination while a native action is running. Same saved profile and workspace; offline scripted provider only. Does not qualify Android, headset, file-publication interruption or a rendered browser.'};
 const summary=assertActionInterruptionResult(result,root,await readFile(join(root,'restart-request.json'),'utf8'));
 await save('action-interruption-result.json',result);console.log(JSON.stringify({status:'passed',...summary}));
 await agent.disconnect();client.roomAgent=undefined;await transport.close();
}catch(error){
 await writeFile(join(wire,'action-interruption-error.json'),JSON.stringify({error:String(error),stack:(error as Error).stack,step,networkAttempts,requests,dispatches},null,2));
 try{await client?.roomAgent?.disconnect();await transport.close();}catch{}
 throw error;
}
