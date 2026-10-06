// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Interactive evidence collector: it records outcomes, never declares the lesson passed.
import assert from 'node:assert/strict';
import {readFile,writeFile,stat,mkdir} from 'node:fs/promises';
import {join,basename} from 'node:path';
import {createHeadlessClient} from '../src/headless/client';
import {HeadlessRoomAgent} from '../src/headless/roomJourney';
import {HeadlessRoomTransport,publishRoomProbeFile} from '../src/headless/roomTransport';
import {selectHeadlessLanguage,runHeadlessChatTurn} from '../src/headless/chatJourney';
import {runHeadlessSuggestionAftersteps} from '../src/headless/suggestionJourney';
import {runHeadlessLiveTurn} from '../src/headless/liveJourney';
import {decodePcm16LeBase64} from '../src/core-sdk/media/pcmInput';
import {captureManagedJourneyBilling,evaluateManagedJourneyBilling,waitForManagedJourneyBillingSettlement} from '../src/headless/managedJourneyBilling';
import type {ChatMessage} from '../src/core/types';

const directory=process.argv[2];
if(!directory||process.env.MAESTRO_ROOM_PROBE_SCENARIO!=='LearnerConversation')throw new Error('Explicit fresh learner probe required.');
const transport=await HeadlessRoomTransport.connect(directory,120000);
const sleep=(ms:number)=>new Promise(resolve=>setTimeout(resolve,ms));
const records:Record<string,unknown>[]=[];
let sequence=0,finished=false,active=false;
let resumedFrom:string|undefined,resumeObjectIds:string[]|undefined;
try{const resume=JSON.parse(await readFile(join(directory,'learner-resume.json'),'utf8'));assert.equal(resume.version,1);assert.match(resume.sourceRun,/^[a-f0-9]{32}$/);resumedFrom=resume.sourceRun;assert.ok(Array.isArray(resume.nativeObjectIds));resumeObjectIds=resume.nativeObjectIds;}
catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
const client=await createHeadlessClient({profileName:'learner',dataRoot:join(directory,'profiles')});
if(!resumedFrom){assert.equal(Object.keys(client.state.chats).length,0,'Learner history must start empty.');assert.equal(client.state.globalProfile,'');}
else{assert.equal(client.state.settings.selectedLanguagePairId,'es-ES-en-US');assert.ok(client.state.chats['es-ES-en-US']?.length,'Resumed learner history is missing.');}
const pair=await selectHeadlessLanguage(client,{targetLanguageCode:'es-ES',nativeLanguageCode:'en-US'});
const initialNative=structuredClone(transport.client.getSnapshot().state);
if(resumeObjectIds)assert.deepEqual(initialNative!.objects.map(object=>object.id).sort(),resumeObjectIds.slice().sort(),'Restored room must preserve the exact original object identities.');
const agent=new HeadlessRoomAgent(client,()=>transport.lease());client.roomAgent=agent;
const boundary='Adaptive novice dialogue chosen by the developer, without catalog coaching. Original chat/verifier/tools/provider and actual Unity. Fresh local learning history unless resumedFrom identifies an explicitly restored closed test run; existing test payer. Synthetic Editor floor; no real scan, onboarding UI, physical input or headset proof. Outcomes require review.';
const save=()=>writeFile(join(directory,'learner-session.json'),JSON.stringify({boundary,accessMode:client.accessMode,profile:client.profile.directory,sequence,finished,resumedFrom,initialNative,records},null,2));
const file=async(name:string,max:number)=>{
 const path=join(directory,name);if((await stat(path)).size>max)throw new Error('Oversized learner input');
 return JSON.parse(await readFile(path,'utf8'));
};
const compact=(messages:ChatMessage[])=>messages.map(message=>({id:message.id,role:message.role,text:message.text,
 translations:message.translations,agentTask:message.agentTask,suggestions:message.replySuggestions,
 attachment:message.imageUrl?{mimeType:message.imageMimeType,name:message.attachmentName,length:message.imageUrl.length}:undefined}));
const exportAttachments=async(messages:ChatMessage[],n:number)=>{
 const paths:string[]=[];await mkdir(join(directory,'artifacts'),{recursive:true});
 for(const [i,message] of messages.entries()){
  const match=message.imageUrl?.match(/^data:([^;,]+)([^,]*),([\s\S]*)$/);if(!match)continue;
  const extension=({'text/html':'html','image/png':'png','image/jpeg':'jpg','image/webp':'webp','image/svg+xml':'svg'} as Record<string,string>)[match[1]]||'bin';
  const path=join(directory,'artifacts',n+'-'+i+'.'+extension);
  await writeFile(path,match[2].split(';').includes('base64')?Buffer.from(match[3],'base64'):Buffer.from(decodeURIComponent(match[3])));paths.push(path);
 }return paths;
};
// Only Stop can interrupt a paid turn. Ordinary turn requests stay sequential.
const control=setInterval(()=>{void file('learner-control.json',1000).then(value=>{
 if(active&&value.stopSequence===sequence)agent.tasks.stopAll();
}).catch(()=>{});},250);
try{
 await publishRoomProbeFile(join(directory,'learner-response.json'),JSON.stringify({phase:'ready',boundary,accessMode:client.accessMode,sequence:0}));
 const deadline=Date.now()+3450000;
 while(!finished&&Date.now()<deadline){
  let request;try{request=await file('learner-request.json',32000);}catch(error){if((error as NodeJS.ErrnoException).code==='ENOENT'){await sleep(200);continue;}throw error;}
  if(request.sequence===sequence){await sleep(200);continue;}
  assert.equal(request.sequence,sequence+1,'Requests must be consecutive and must not replay.');
  assert.ok(['text','live','finish'].includes(request.type));
  sequence=request.sequence;
  if(request.type==='finish'){finished=true;break;}
  assert.ok(typeof request.text==='string'&&request.text.trim()&&request.text.length<=12000);
  const record:Record<string,unknown>={sequence,type:request.type,text:request.text,startedAt:new Date().toISOString()};
  records.push(record);await save();active=true;
  const beforeIds=new Set((client.state.chats[pair.id]||[]).map(message=>message.id));
  const operationId=client.runtime.ids.create('learner-turn'),usageStart=agent.usage.length;
  let beforeBilling:Awaited<ReturnType<typeof captureManagedJourneyBilling>>|undefined;
  const samples:unknown[]=[];
  const sample=setInterval(()=>{if(samples.length<1800)samples.push({at:Date.now(),state:structuredClone(transport.client.getSnapshot().state)});},250);
  await publishRoomProbeFile(join(directory,'learner-response.json'),JSON.stringify({phase:'working',sequence,text:request.text}));
  try{
   if(client.accessMode==='managed')beforeBilling=await captureManagedJourneyBilling(client,operationId);
   if(request.type==='text'){
    const turn=await runHeadlessChatTurn(client,{text:request.text});record.turn=turn;
    await save();
    record.aftersteps=await runHeadlessSuggestionAftersteps(client,{assistantMessageId:turn.assistantMessage.id});
   }else{
    assert.ok(typeof request.speechFixture==='string'&&basename(request.speechFixture)===request.speechFixture);
    const speech=await file(request.speechFixture,16000000);
    assert.equal(speech.expectedTranscript,request.text);
    record.live=await runHeadlessLiveTurn(client,{mode:'conversation',pcm:decodePcm16LeBase64(speech.pcmBase64),expectedTranscript:request.text,
     pace:true,timeoutMs:180000,runSuggestionAftersteps:true});
   }
  }catch(error){record.error=String(error);}
  finally{
   active=false;clearInterval(sample);record.endedAt=new Date().toISOString();
   record.usage=agent.usage.slice(usageStart);record.tasks=await agent.store.list();
   record.native=structuredClone(transport.client.getSnapshot().state);
   const messages=(client.state.chats[pair.id]||[]).filter(message=>!beforeIds.has(message.id));
   record.messages=messages;record.attachments=await exportAttachments(messages,sequence);
   await writeFile(join(directory,'learner-samples-'+sequence+'.json'),JSON.stringify(samples));
   try{
    record.billing=beforeBilling?evaluateManagedJourneyBilling(beforeBilling,await waitForManagedJourneyBillingSettlement(client,operationId),{requirePaidUsage:!record.error})
     :{applicable:false,passed:client.accessMode==='byok',payer:'byok-api-key-owner'};
   }catch(error){record.billing={passed:false,error:String(error)};}
   await client.save();await save();
   await publishRoomProbeFile(join(directory,'learner-response.json'),JSON.stringify({phase:'review-required',sequence,error:record.error,billing:record.billing,
    messages:compact(messages),attachments:record.attachments,native:record.native},null,2));
   if(!(record.billing as {passed:boolean}).passed)throw new Error('Accounting failed; session must stop before another paid request.');
  }
 }
 if(!finished)throw new Error('Learner session exceeded its bounded duration.');
}finally{
 clearInterval(control);agent.tasks.stopAll();await agent.disconnect();await save();await transport.close();
}
console.log('Learner session collected; semantic and UI outcomes require review.');
