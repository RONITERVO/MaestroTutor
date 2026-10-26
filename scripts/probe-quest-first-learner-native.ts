// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Production App onboarding + scripted provider boundary + actual Unity room wire.
// No App store, conversation component or successful native receipt is injected.
import assert from 'node:assert/strict';
import {createHash} from 'node:crypto';
import {readFile,writeFile} from 'node:fs/promises';
import {resolve,join} from 'node:path';
import {fileURLToPath} from 'node:url';
import type {Page} from 'playwright-core';
import {runQuestFirstLearner,type NativeFirstLearnerAdapter,type ScriptedRequest} from './probe-quest-first-learner.mjs';
import {RoomProbeChannel} from '../src/headless/roomTransport';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {RoomAgentClient} from '../src/core-sdk/room/roomAgentClient';
import type {} from '../src/platform/quest/bookBridge';
import {readObjectReference} from '../shared/objectReference';
import {readRoomObjectObservation} from '../src/core-sdk/room/roomObjectContext';
import {behaviourFact} from '../shared/behaviourCatalog';
import {validFactValue} from '../shared/behaviourFacts';
import {validCatalogView} from '../shared/roomCatalog';
import {objectSearchRequest,readObjectSearchPage} from '../shared/objectSearch';
import {findLanguageByExactCode} from '../src/shared/utils/languageUtils';
import {parseStrictTutorResponseText} from '../src/core-sdk/chat/tutorResponse';

const requestText='Could Maestro make a small green ball for our Spanish lesson?';
const objectName='Spanish lesson green ball';
const finalEnglish='Your green ball is ready. In Spanish, it is una pelota verde.';
const handoffReply='Voy a pedir una pelota verde para nuestra lección.\n[en] I will ask Maestro to make a green ball for our lesson.\n```maestro-tool\n{"tool":"agent"}\n```';
const finalReply='Tu pelota verde está lista. En español, es una pelota verde.\n[en] '+finalEnglish;
const finalSuggestion='\u00bfJugamos con la pelota?';
const sleep=(ms:number)=>new Promise<void>(resolve=>setTimeout(resolve,ms));
const record=(value:unknown):value is Record<string,unknown>=>value!==null&&typeof value==='object'&&!Array.isArray(value);
type Snapshot=ReturnType<RoomAgentClient['snapshot']>;
type WireRequest=NonNullable<Snapshot['request']>;

type PlannerInput={request:string;scene:RoomAgentState;receipts:unknown[];tutorContext:{nativeLanguageCode:unknown;systemInstruction:string}};
/** The current user turn also carries image-origin labels and file/inline parts.
 * Select its unique structured plan input without changing any source context. */
export function readFirstLearnerPlannerInput(body:unknown):PlannerInput {
 assert(record(body)&&Array.isArray(body.contents),'Planner request has no contents');
 const current=body.contents.at(-1);
 assert(record(current)&&current.role==='user'&&Array.isArray(current.parts),'Planner request has no current user turn');
 const candidates:PlannerInput[]=[];
 for(const part of current.parts) {
  if(!record(part)||typeof part.text!=='string')continue;
  let value:unknown;
  try {value=JSON.parse(part.text);}catch {continue;}
  if(!record(value)||value.request!==requestText||!record(value.scene)||!Array.isArray(value.receipts)||!record(value.tutorContext)||typeof value.tutorContext.systemInstruction!=='string')continue;
  candidates.push(value as PlannerInput);
 }
 assert.equal(candidates.length,1,'Expected one unambiguous structured planner input in the current user turn');
 return candidates[0];
}

/** Verify the latest-message field, not a matching earlier turn in its history. */
export function readFirstLearnerSuggestionMessage(body:unknown):string {
 assert(record(body)&&Array.isArray(body.contents),'Suggestions request has no contents');
 const current=body.contents.at(-1);
 assert(record(current)&&current.role==='user'&&Array.isArray(current.parts),'Suggestions request has no current user turn');
 const target=findLanguageByExactCode('es-ES');assert(target,'The selected Spanish language definition is missing');
 const prefix=`**${target.displayName} Tutor's Latest Message in ${target.displayName} chat:**\n"`,suffix='"\n\n**Your Task:**';
 const candidates:string[]=[];
 for(const part of current.parts) {
  if(!record(part)||typeof part.text!=='string')continue;
  const start=part.text.indexOf(prefix);if(start<0)continue;
  assert.equal(part.text.indexOf(prefix,start+prefix.length),-1,'Ambiguous latest-message section');
  const end=part.text.indexOf(suffix,start+prefix.length);assert(end>=0,'Suggestions latest-message section is incomplete');
  candidates.push(part.text.slice(start+prefix.length,end));
 }
 assert.equal(candidates.length,1,'Expected one suggestions latest-message section');
 return candidates[0];
}

class NativeLearner implements NativeFirstLearnerAdapter {
 private page!:Page;
 private url='';
 private pump?:Promise<void>;
 private stopPump=false;
 private failure:Error|null=null;
 private latest:RoomAgentState|null=null;
 private clientId='';
 private firstClient='';
 private clients:string[]=[];
 private observations:unknown[]=[];
 private requests=new Map<string,{clientId:string;request:WireRequest}>();
 private phase:'lesson'|'room'|'restored'='lesson';
 private providerCalls:unknown[]=[];
 private tutorCalls=0;
 private verifierCalls=0;
 private resultVerifierCalls=0;
 private workspaceCloseAcks:unknown[]=[];
 private plannerCalls=0;
 private resultCalls=0;
 private roomImages=0;
 private objectId='';
 private creationRunId='';
 private creationReceipt:unknown;
 private definition:unknown;
 private observation:unknown;
 private persistence:unknown;
 private restoredDefinition:unknown;
 private restoredObservation:unknown;
 private beforeReloadRequests=0;
 private qualified=false;
 constructor(private channel:RoomProbeChannel,private ready:Record<string,unknown>,private out:string) {}
 async attach(page:Page,origin:string) {this.page=page;this.url=origin+'/?surface=quest-book';}
 private check() {if(this.failure)throw this.failure;}
 private async until(test:()=>boolean,message:string,timeout=35000) {
  const deadline=Date.now()+timeout;
  while(Date.now()<deadline) {this.check();if(test())return;await sleep(100);}
  this.check();throw new Error(message);
 }
 private start() {
  assert(!this.pump,'Native transport must be drained before restarting');
  this.stopPump=false;
  this.pump=(async()=>{
   try {
    while(!this.stopPump) {
     assert.equal(this.page.url(),this.url,'Native transport is restricted to the exact owned top-level app URL');
     const snapshot=await this.page.evaluate(()=>window.maestroBook?.roomSnapshot());
     // The audience gate exposes only a lifecycle bridge. It is not a room client.
     if(snapshot&&/^[a-f0-9]{32}$/.test(snapshot.clientId)) {
      if(!this.clientId) {this.clientId=snapshot.clientId;assert(!this.clients.includes(this.clientId),'A reloaded document reused the old native client');this.clients.push(this.clientId);}
      assert.equal(snapshot.clientId,this.clientId,'Unexpected room client replacement within one document');
      if(snapshot.request) {
       const key=snapshot.clientId+':'+snapshot.request.sequence,previous=this.requests.get(key);
       if(previous)assert.deepEqual(previous.request,snapshot.request,'A native request changed before acknowledgement');
       else {assert(this.requests.size<128,'First-learner native command budget exceeded');this.requests.set(key,{clientId:snapshot.clientId,request:structuredClone(snapshot.request)});}
      }
      const response=await this.channel.exchange(snapshot);
      const applied=await this.page.evaluate(({response,clientId,url})=>{
       if(location.href!==url)throw new Error('Native reply crossed a document navigation');
       const bridge=window.maestroBook;
       if(!bridge||bridge.roomSnapshot().clientId!==clientId)throw new Error('Native reply crossed a room client replacement');
       const accepted=response.state?bridge.roomState(response.state):false;
       if(response.capture)bridge.roomCapture(response.capture);
       return accepted;
      },{response,clientId:snapshot.clientId,url:this.url});
      if(response.state) {
       const state=response.state as RoomAgentState;
       if(!applied)assert(this.latest&&this.latest.session===state.session&&state.revision<=this.latest.revision,'Production bridge rejected a fresh native observation');
       else {
        this.latest=structuredClone(state);
        assert(this.observations.length<2048,'First-learner observation budget exceeded');
        this.observations.push({clientId:this.clientId,session:state.session,revision:state.revision,sceneRevision:state.sceneRevision,ack:state.ack,ok:state.ok,status:state.status,
         execution:state.execution?.selected?{id:state.execution.selected.id,phase:state.execution.selected.phase,capability:state.execution.selected.capability}:null,
         catalog:state.catalog?.operation==='inspect'?{category:state.catalog.category,capability:state.catalog.capability}:null});
       }
      }
     }
     await sleep(100);
    }
   } catch(error) {this.failure=error instanceof Error?error:new Error(String(error));}
  })();
 }
 private async pause() {this.stopPump=true;await this.pump;this.pump=undefined;this.check();}
 async beforeFirstMessage() {
  this.start();await this.until(()=>Boolean(this.latest?.session),'Actual production book did not accept the native handshake');
  this.firstClient=this.clientId;
 }
 imagePurpose(bodyText:string) {
  if(this.phase==='lesson')return null;
  assert.equal(this.phase,'room','Reload must not generate another image');
  assert(bodyText.includes(requestText),'Room-turn default image must use the actual learner request');
  assert.equal(this.roomImages++,0,'Only one default user image is expected for the room turn');
  return 'native-user-default';
 }
 private createdCommands() {
  return [...this.requests.values()].flatMap(entry=>entry.request.commands).filter(command=>command.action==='execution'&&command.execution?.operation==='start');
 }
 private readOrPresentation(command:RoomCommand) {
  if(command.action==='catalog')return true;
  return command.action==='workspace'&&typeof command.visible==='boolean'&&Object.keys(command).every(key=>key==='action'||key==='visible');
 }
 private assertSingleCreation() {
  const commands=this.createdCommands();assert.equal(commands.length,1,'Exactly one app-originated native mutation is allowed');
  const call=commands[0].execution?.operation==='start'?commands[0].execution.call:undefined;
  assert.equal(call?.id,'object.create');assert.equal(call?.arguments.name,objectName);
  assert.equal(call?.arguments.shape,'ball');assert.equal(call?.arguments.red,0);assert.equal(call?.arguments.green,1);assert.equal(call?.arguments.blue,0);
  const all=[...this.requests.values()].flatMap(entry=>entry.request.commands);
  assert(all.every(command=>this.readOrPresentation(command)||command.action==='execution'&&['start','inspect'].includes(command.execution?.operation??'')),'Unplanned mutation bypassed the single-create scenario');
  assert(all.filter(command=>command.action==='workspace').length<=16,'Workspace presentation exceeded its bounded navigation budget');
 }
 async stream({body,schema,bodyText,systemText}:ScriptedRequest) {
  if(this.phase==='lesson') {assert(!schema?.properties?.commands,'Ordinary first lesson must not start native planning');return null;}
  assert.equal(this.phase,'room','Reload must not replay provider work');
  this.check();
  let kind:string,text:string;
  if(schema?.properties?.commands) {
   this.plannerCalls++;
   const input=readFirstLearnerPlannerInput(body);
   assert.equal(input.request,requestText,'Agent must receive the exact unmodified learner request');
   assert.equal(input.tutorContext.nativeLanguageCode,'en-US');
   assert(input.tutorContext.systemInstruction.includes('Spanish')&&input.tutorContext.systemInstruction.includes('English'));
   assert(bodyText.includes('This is my very first lesson.'),'Delegation lost the original conversation');
   const state=input.scene as RoomAgentState;
   assert.equal(state.session,this.latest?.session);assert.equal(input.receipts.length,this.plannerCalls-1);
   let commands:RoomCommand[];
   if(this.plannerCalls===1)commands=[{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}}];
   else if(this.plannerCalls===2) {
    const catalog=state.catalog;
    assert(validCatalogView(catalog),'Native action catalog failed the shared codec');
    // The wire omits category for its original/default actions category.
    assert(catalog.operation==='inspect'&&(catalog.category===undefined||catalog.category==='actions')&&catalog.capability==='object.create'&&catalog.definition);
    const example=catalog.definition.example;assert(example&&example.kind==='primitive'&&example.shape==='ball');
    commands=[{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...example,name:objectName,x:.4,y:1,z:.8,scale:.65,red:0,green:1,blue:0}}}}];
   } else if(this.plannerCalls===3) {
    const receipt=state.execution?.selected;assert(receipt?.id&&receipt.capability==='object.create');this.creationRunId=receipt.id;
    // Synchronize only this scripted provider response with actual native completion.
    // The next ordinary planner operation still reads the real execution receipt.
    await this.until(()=>this.latest?.execution?.selected?.id===receipt.id&&!['preparing','running'].includes(this.latest.execution.selected.phase),'Native creation never left its pending phase');
    commands=[{action:'execution',execution:{operation:'inspect',runId:receipt.id}}];
   } else if(this.plannerCalls===4) {
    const receipt=state.execution?.selected;assert.equal(receipt?.id,this.creationRunId);assert.equal(receipt?.phase,'completed');
    assert.equal(receipt?.call.arguments.name,objectName);assert.equal(typeof receipt?.output?.objectId,'string');
    this.objectId=receipt!.output!.objectId as string;assert(/^[a-f0-9]{32}$/.test(this.objectId));this.creationReceipt=structuredClone(receipt);
    commands=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:this.objectId}}}];
   } else if(this.plannerCalls===5) {
    const definition=readObjectReference(state.catalog,this.objectId);assert(definition);assert.equal(definition.name,objectName);assert.equal(definition.kind,'Ball');this.definition=structuredClone(state.catalog);
    commands=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.observation',version:1,arguments:{target:this.objectId}}}];
   } else if(this.plannerCalls===6) {
    const observed=readRoomObjectObservation(state.catalog,this.objectId);assert(observed);assert.equal(observed.name,objectName);assert.deepEqual(observed.color,{r:0,g:1,b:0,a:1});
    this.observation=structuredClone(state.catalog);commands=[];
   } else throw new Error('Unexpected additional room planner request');
   kind='native-planner';text=JSON.stringify({commands});
   this.providerCalls.push({kind,number:this.plannerCalls,request:input.request,tutorContext:input.tutorContext,scene:state,receipts:input.receipts,commands});
  } else if(schema?.properties?.suggestions) {
   assert.equal(this.tutorCalls,1);
   const latestMessage=readFirstLearnerSuggestionMessage(body);
   if(this.plannerCalls===0) {
    assert.equal(this.verifierCalls,0);assert.equal(latestMessage,handoffReply);
    assert(bodyText.includes(requestText));assert(bodyText.includes('originalUserRequest'));
    this.verifierCalls++;
    kind='native-verifier';text=JSON.stringify({suggestions:[{target:'Una pelota verde.',native:'A green ball.'}],reengagementSeconds:0,chatSummary:'A first Spanish lesson with a green ball.',globalProfile:'',artifact:null,toolRequest:{tool:'agent'}});
   } else {
    // Speaking the ordinary completed-task reply uses the same suggestions path.
    // It is not another delegation and must not replay the creation.
    assert.equal(this.plannerCalls,6);assert.equal(this.resultCalls,1);assert.equal(this.resultVerifierCalls,0);
    // roomTaskProvider publishes parsed visibleText, unlike the original handoff raw reply.
    assert.equal(latestMessage,parseStrictTutorResponseText(finalReply,'en-US').visibleText);assert(this.creationReceipt&&this.definition&&this.observation);
    this.resultVerifierCalls++;
    kind='native-result-verifier';text=JSON.stringify({suggestions:[{target:finalSuggestion,native:'Shall we play with the ball?'}],reengagementSeconds:0,chatSummary:'The Spanish lesson now has a saved green ball: una pelota verde.',globalProfile:'',artifact:null,toolRequest:null});
   }
  } else if(this.tutorCalls===0) {
   this.tutorCalls++;assert.equal(this.plannerCalls,0);assert(bodyText.includes(requestText));assert(bodyText.includes('This is my very first lesson.'));
   assert(systemText.includes('A connected room agent is available'));
   kind='native-tutor';text=handoffReply;
  } else {
   assert.equal(this.resultCalls++,0);assert.equal(this.plannerCalls,6);assert(this.creationReceipt&&this.definition&&this.observation);
   assert(bodyText.includes(requestText));assert(bodyText.includes('Write only the result reply for the room task that has already run.'));
   assert(systemText.includes(this.objectId)&&systemText.includes('completed'));
   kind='native-result';text=finalReply;
  }
  this.providerCalls.push({kind,bodySha256:createHash('sha256').update(bodyText).digest('hex')});
  return {kind,text};
 }
 private async readFact(id:string,target?:string) {
  const page=this.page,definition=behaviourFact(id);assert(definition);
  const back=page.getByRole('button',{name:'Back to workshop',exact:true}).filter({visible:true});
  if(await back.count())await back.click();
  else if(!await page.getByRole('button',{name:'Action catalog',exact:true}).isVisible())assert(await page.evaluate(()=>window.maestroBook!.command({version:1,type:'workspace.open'})));
  await page.getByRole('button',{name:'Action catalog',exact:true}).click();
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByLabel('Search facts',{exact:true}).fill(id);
  await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:definition.label+' '+id+' · v'+definition.version,exact:true}).click();
  await page.getByRole('heading',{name:definition.label,exact:true}).waitFor();
  if(target) {
   const choice=page.getByRole('region',{name:'Fact inputs target object choice',exact:true});await choice.waitFor();
   await choice.getByRole('button',{name:'Find Fact inputs target',exact:true}).click();
   await choice.getByLabel('Find Fact inputs target objects',{exact:true}).fill(target);
   await choice.getByRole('button',{name:'Search objects',exact:true}).click();
   await this.until(()=>Boolean(readObjectSearchPage(this.latest?.catalog,objectSearchRequest(target,'all',''))),'Exact saved-object search did not return');
   const found=readObjectSearchPage(this.latest?.catalog,objectSearchRequest(target,'all',''));assert(found);assert.equal(found.total,1);assert.equal(found.entries[0].id,target);
   await choice.getByRole('status').filter({hasText:'1–1 of 1'}).waitFor();
   await choice.locator('.room-object-list').getByRole('button').click();
  }
  const previousRevision=this.latest!.revision;
  await page.getByRole('button',{name:'Read fact',exact:true}).click();
  await this.until(()=>{
   const state=this.latest,c=state?.catalog;
   return !!state&&state.revision>previousRevision&&c?.operation==='inspect'&&c.category==='facts'&&c.capability===id&&c.available===true&&(!target||c.arguments?.target===target);
  },'Fresh native fact did not arrive: '+id);
  const view=this.latest!.catalog;assert(view?.operation==='inspect'&&view.category==='facts');assert(validFactValue(id,view.value));
  await page.getByLabel('Current fact value',{exact:true}).getByText('Current value',{exact:true}).waitFor();
  return structuredClone(view);
 }
 private async checkpoint() {
  const deadline=Date.now()+20000;
  do {
   const view=await this.readFact('room.persistence');this.persistence=view;
   const stores=Object.values(view.value as Record<string,{phase:string;pending:boolean;dirty:boolean;dispatchHeld:boolean;error:string}>);
   assert(stores.every(store=>!['unconfirmed','unavailable','temporary'].includes(store.phase)),'Native persistence cannot establish a saved result');
   if(stores.every(store=>store.phase==='idle'&&!store.pending&&!store.dirty&&!store.dispatchHeld&&store.error===''))return;
   await sleep(100);
  } while(Date.now()<deadline);
  throw new Error('Native journal checkpoint remained pending');
 }
 private async backToChat() {
  const back=this.page.getByRole('button',{name:'Back to workshop',exact:true}).filter({visible:true});if(await back.count())await back.click();
  const clientId=this.clientId,previous=this.latest;assert(previous?.visible===true);
  await this.page.getByRole('button',{name:'Back to chat',exact:true}).filter({visible:true}).first().click();
  // The visible button dispatches through the same serial client. Keep the
  // wire alive until Unity acknowledges that exact close request.
  let closeSequence=0;
  await this.until(()=>{
   const closes=[...this.requests.values()].filter(entry=>entry.clientId===clientId&&entry.request.sequence>previous.ack&&entry.request.commands.length===1&&entry.request.commands[0].action==='workspace'&&entry.request.commands[0].visible===false);
   assert(closes.length<=1,'Closing the workshop retried a presentation request');
   closeSequence=closes[0]?.request.sequence??0;
   return closeSequence>0&&this.latest?.session===previous.session&&this.latest.revision>previous.revision&&this.latest.ack>=closeSequence&&this.latest.visible===false&&this.latest.ok;
  },'Unity did not acknowledge the actual Back to chat request');
  await this.page.waitForFunction(clientId=>{
   const snapshot=window.maestroBook?.roomSnapshot();return snapshot?.clientId===clientId&&snapshot.request===null;
  },clientId);
  this.workspaceCloseAcks.push({clientId,sequence:closeSequence,revision:this.latest!.revision,ack:this.latest!.ack,visible:this.latest!.visible});
 }
 async afterLesson() {
  this.check();assert(this.firstClient);this.phase='room';
  const composer=this.page.locator('textarea').filter({visible:true});await composer.fill(requestText);
  await this.page.getByRole('button',{name:'Send message',exact:true}).click();
  await this.page.getByText(finalEnglish,{exact:false}).first().waitFor({timeout:60000});
  const task=this.page.getByRole('region',{name:'Agent task',exact:true});
  // AgentTaskStatus uses section+aria-label, whose accessible role is region.
  await task.getByText('Finished checking this request.',{exact:true}).waitFor();
  await task.getByText('Task details',{exact:true}).click();
  await task.getByText('Recorded action batches: 5.',{exact:true}).waitFor();
  await this.until(()=>this.resultVerifierCalls===1,'Completed-task reply did not reach the ordinary suggestions verifier');
  await this.page.getByText(finalSuggestion,{exact:true}).waitFor();
  await this.page.waitForFunction(()=>window.maestroBook?.snapshot().activity==='idle');
  assert.equal(this.tutorCalls,1);assert.equal(this.verifierCalls,1);assert.equal(this.resultVerifierCalls,1);assert.equal(this.plannerCalls,6);assert.equal(this.resultCalls,1);assert.equal(this.roomImages,1);
  this.assertSingleCreation();
  await this.page.waitForFunction(()=>Array.from(document.querySelectorAll<HTMLImageElement>('img[alt="Preview"]')).filter(img=>img.complete&&img.naturalWidth>0).length===3);
  await this.page.screenshot({path:join(this.out,'native-room-result.png')});
  await this.checkpoint();await this.backToChat();this.check();
 }
 async beforeReload() {
  await this.pause();assert.equal(this.latest?.visible,false,'Reload requires an acknowledged native chat view');
  const snapshot=await this.page.evaluate(()=>window.maestroBook!.roomSnapshot());
  assert.equal(snapshot.clientId,this.clientId);assert.equal(snapshot.request,null,'Reload cannot discard an unacknowledged room request');
  this.beforeReloadRequests=this.requests.size;this.latest=null;this.clientId='';this.phase='restored';
 }
 async afterReload() {
  this.start();await this.until(()=>Boolean(this.latest?.session),'Reloaded production app did not accept a fresh native handshake');
  assert.notEqual(this.clientId,this.firstClient);assert.equal(this.clients.length,2);
  assert.equal(this.latest?.visible,false,'Fresh native handshake must preserve the acknowledged chat view');
  assert.equal(this.requests.size,this.beforeReloadRequests,'Reload replayed a native room command');
  await this.page.getByText(requestText,{exact:true}).first().waitFor();await this.page.getByText(finalEnglish,{exact:false}).first().waitFor();
  const task=this.page.getByRole('region',{name:'Agent task',exact:true});
  await task.getByText('Finished checking this request.',{exact:true}).waitFor();await task.getByText('Task details',{exact:true}).click();
  await task.getByText('Recorded action batches: 5.',{exact:true}).waitFor();
  const definition=await this.readFact('object.definition',this.objectId);assert.deepEqual(definition.value,(this.definition as {value:unknown}).value);this.restoredDefinition=definition;
  const observation=await this.readFact('object.observation',this.objectId),observed=readRoomObjectObservation(observation,this.objectId);assert(observed);assert.equal(observed.name,objectName);assert.deepEqual(observed.color,{r:0,g:1,b:0,a:1});this.restoredObservation=observation;
  await this.checkpoint();this.assertSingleCreation();
  assert([...this.requests.values()].filter(entry=>entry.clientId===this.clientId).every(entry=>entry.request.commands.every(command=>this.readOrPresentation(command))),'Restored document dispatched a mutation');
  await this.page.screenshot({path:join(this.out,'native-restored-fact.png')});await this.backToChat();this.check();this.qualified=true;
 }
 evidence() {return {nativeProbeId:this.channel.id,qualified:this.qualified&&!this.failure,realProvider:false,physicalDevice:false,
  boundary:'Actual production App and Unity Editor room transport; scripted model replies. Reload is the browser document, not a native process restart. Completed creation plus journal checkpoint and fresh saved-object facts establish native acceptance; no headset/Android/real scan/Live/audio claim.',
  syntheticPhysics:this.ready.syntheticPhysics,syntheticScan:this.ready.syntheticScan,clients:this.clients,requestText,objectId:this.objectId,creationRunId:this.creationRunId,
  creationReceipt:this.creationReceipt,definition:this.definition,observation:this.observation,persistence:this.persistence,restoredDefinition:this.restoredDefinition,restoredObservation:this.restoredObservation,
  counts:{tutor:this.tutorCalls,verifier:this.verifierCalls,resultVerifier:this.resultVerifierCalls,planner:this.plannerCalls,result:this.resultCalls,roomImage:this.roomImages},workspaceCloseAcks:this.workspaceCloseAcks,requests:[...this.requests.values()],observations:this.observations,providerCalls:this.providerCalls,pumpError:this.failure?.message??null};}
 async close() {
  let error:unknown;
  try {await this.pause();}catch(caught){error=caught;this.qualified=false;}
  try {await this.channel.stop();}catch(caught){error??=caught;this.qualified=false;}
  await writeFile(join(this.out,'native-evidence.json'),JSON.stringify(this.evidence(),null,2)+'\n');
  if(error)throw error;
 }
}

export async function runNativeFirstLearner(directory:string) {
 const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8'));assert.equal(owner.firstLearner,true,'Use the explicit -Journey Book -FirstLearner wrapper mode');
 const channel=await RoomProbeChannel.connect(directory,120000);
 const ready:unknown=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert(record(ready)&&ready.id===channel.id&&ready.version===1);assert.equal(ready.syntheticPhysics,true);assert.equal(ready.syntheticScan,false);
 const native=new NativeLearner(channel,ready,join(directory,'first-learner'));
 await runQuestFirstLearner({out:join(directory,'first-learner'),native});
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
 const directory=process.argv[2];if(!directory)throw new Error('Supply the fresh explicitly started native probe directory.');
 await runNativeFirstLearner(resolve(directory));
}
