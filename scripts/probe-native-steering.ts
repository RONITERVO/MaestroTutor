// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {createHeadlessClient,type HeadlessClient} from '../src/headless/client';
import {HeadlessRoomAgent} from '../src/headless/roomJourney';
import {selectHeadlessLanguage} from '../src/headless/chatJourney';
import type {RoomAgentState,RoomCommand,RoomAgentLease} from '../src/core-sdk/room/roomAgent';
import {runAgentSteeringProof,type ReceiptObserver} from './probe-agent-steering';
import {readNativeObjects} from './native-probe-read-set';
import {assertSameRoomObjects} from './agent-provider-contract';
import {bookProviderStage} from './book-provider-evidence';
/** Script only the provider transport; tutor/verifier/handoff/task/control/store
 * logic and native actions use the actual application. All network is refused. */
export async function probeNativeSteering(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>,lease:()=>RoomAgentLease,directory:string,sparseObjectView=false){
 const baseline=await readNativeObjects(execute),requests:unknown[]=[];let networkAttempts=0,client:HeadlessClient|undefined,target='',observer:ReceiptObserver|undefined;
 const originalFetch=globalThis.fetch;
 globalThis.fetch=async()=>{networkAttempts++;throw new Error('Offline steering fixture cannot use the network');};
 const evidence:Record<string,unknown>={phase:'setup',sparseObjectView,boundary:'Fixture-authored provider streams through the actual tutor, verifier, task runner, public Stop, durable journal and native Unity. Synthetic token counts are parser fixtures, not real provider usage or billing. No headset or OS-crash proof.',baseline};
 try{
  client=await createHeadlessClient({accessMode:'managed',profileName:'sparse-steering',dataRoot:join(directory,'profiles')});
  // Explicit managed construction avoids loading a local BYOK key. This selects
  // the non-billing test path only; the real transport is replaced below.
  client.accessMode='byok';
  const created=await execute([{action:'create',reference:'steering_ball',kind:'ball',name:'ParityBall',atPosition:true,position:{x:.2,y:1,z:2.5},scale:.5,color:{r:0,g:0,b:1,a:1}}]);
  target=created.created[0];assert.ok(target);await execute([{action:'physicsSettings',target,physics:{mode:'bouncy',shape:'sphere',mass:2}}]);
  const records:unknown[]=[],selectedParents:string[]=[];
  type Request=Parameters<HeadlessClient['ai']['models']['generateContentStream']>[0];
  const proposal='Lo prepararé.\n[EN]I will prepare it.\n```maestro-tool\n{"tool":"agent"}\n```';
  const parentId=(request:Request)=>{
   const found:string[][]=[];
   const walk=(v:unknown)=>{if(!v||typeof v!=='object')return;const item=v as Record<string,unknown>;
    if(item.taskId&&typeof item.taskId==='object'){const ids=(item.taskId as {enum?:unknown}).enum;if(Array.isArray(ids)&&ids.every(id=>typeof id==='string'))found.push(ids);}
    for(const value of Object.values(item))walk(value);
   };walk(request.config?.responseJsonSchema??request.config?.responseSchema);assert.equal(found.length,1);assert.ok(found[0].length);
   const id=found[0].find(id=>!selectedParents.includes(id));assert.ok(id,'Missing newly captured parent task');selectedParents.push(id);return id;
  };
  const verifier=(action?:'revise'|'continue')=>(request:Request)=>JSON.stringify({suggestions:[{target:'Gracias.',native:'Thanks.'}],toolRequest:{tool:'agent',...(action?{task:{action,taskId:parentId(request)}}:{})}});
  const inspect=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.observation',version:1,arguments:{target}}}]}),paint=(red:boolean)=>JSON.stringify({commands:[{action:'paint',target,color:{r:red?1:0,g:0,b:red?0:1,a:1}}]});
  const steps:Array<{stage:'chat'|'verifier'|'planner';reply:string|((request:Request)=>string)}>=[
   {stage:'chat',reply:proposal},{stage:'verifier',reply:verifier()},{stage:'planner',reply:inspect},{stage:'planner',reply:paint(true)},
   {stage:'chat',reply:proposal},{stage:'verifier',reply:verifier('revise')},{stage:'planner',reply:inspect},{stage:'planner',reply:paint(false)},{stage:'planner',reply:'{"commands":[]}'},{stage:'chat',reply:'Listo.\n[EN]The ball is blue at your chosen position.'},
   {stage:'chat',reply:proposal},{stage:'verifier',reply:verifier('continue')},{stage:'planner',reply:inspect},{stage:'planner',reply:'{"commands":[]}'},{stage:'chat',reply:'Sigue azul.\n[EN]The ball is still blue at your chosen position.'}
  ];
  client.ai={models:{generateContentStream:async(request:Request)=>{
   const step=steps.shift();assert.ok(step,'Unexpected fixture provider call');assert.equal(bookProviderStage(request),step.stage);
   const text=typeof step.reply==='string'?step.reply:step.reply(request);requests.push({stage:step.stage,request,response:text,syntheticTokens:20});
   return (async function*(){yield {text,modelVersion:'offline-steering-fixture',usageMetadata:{totalTokenCount:20},candidates:[{content:{role:'model',parts:[{text}]}}]};})();
  }}} as unknown as HeadlessClient['ai'];
  await selectHeadlessLanguage(client,{targetLanguageCode:'es-ES',nativeLanguageCode:'en-US'});
  const pair=client.state.settings.selectedLanguagePairId!;client.state.chats[pair].push({id:'fixture-context',role:'user',text:'My test object is the blue ParityBall, at half the standard diameter.',timestamp:1});
  const agent=new HeadlessRoomAgent(client,()=>{const native=lease();return !observer&&!sparseObjectView?native:{...native,execute:async(...args)=>{const receipt=await native.execute(...args);if(sparseObjectView)assert.equal(receipt.objects.length,0,'Sparse agent receipt leaked an inventory');await observer?.(args[0],receipt);return receipt;}};});
  client.roomAgent=agent;
  evidence.proof=await runAgentSteeringProof({client,before:structuredClone(lease().state()),target,directory,execute,lease,observe:value=>{observer=value;},providerMode:'scripted'});
  assert.equal(steps.length,0);assert.equal(networkAttempts,0);assert.equal(selectedParents.length,2);assert.notEqual(selectedParents[0],selectedParents[1]);
  records.push(...await agent.store.list());evidence.records=records;evidence.selectedParents=selectedParents;evidence.phase='passed';
 }catch(error){evidence.phase='failed';evidence.error=String(error);throw error;}
 finally{
  try{observer=undefined;await client?.roomAgent?.disconnect();if(target)await execute([{action:'delete',target}]);assertSameRoomObjects(baseline,await readNativeObjects(execute));}
  catch(error){evidence.phase='failed';evidence.cleanupError=String(error);throw error;}
  finally{globalThis.fetch=originalFetch;await writeFile(join(directory,'native-steering.json'),JSON.stringify({...evidence,requests,networkAttempts},null,2));}
 }
}
