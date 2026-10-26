// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Offline production text-to-Live journey through the actual native room executor.
import assert from 'node:assert/strict';
import {readFile} from 'node:fs/promises';
import {dirname,join,resolve} from 'node:path';
import {installFirstLearnerSpeechObserver,loadFirstLearnerSpeechAssets} from './first-learner-speech-assets.mjs';
import type {Page} from 'playwright-core';
import {fileURLToPath} from 'node:url';
import {NativeLearner,readFirstLearnerPlannerInput,readFirstLearnerSuggestionMessage} from './probe-quest-first-learner-native';
import {runQuestFirstLearner,type ScriptedRequest} from './probe-quest-first-learner.mjs';
import {firstLearnerLive,LIVE_REQUEST,LIVE_REPLY} from './first-learner-live.mjs';
import {RoomProbeChannel} from '../src/headless/roomTransport';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {readObjectReference} from '../shared/objectReference';
import {readRoomObjectObservation} from '../src/core-sdk/room/roomObjectContext';
import {readObjectSearchPage,objectSearchRequest} from '../shared/objectSearch';
import {validCatalogView} from '../shared/roomCatalog';
import {parseStrictTutorResponseText} from '../src/core-sdk/chat/tutorResponse';

const CREATE_REQUEST='Could Maestro make an apple tree and a separate red apple for our Spanish lesson? Keep the apple independent so we can use it later.';
const TREE_NAME='Spanish lesson apple tree',APPLE_NAME='Spanish lesson independent apple';
const CREATE_REPLY='Voy a pedir un árbol y una manzana independiente para practicar.\n[en] I will ask Maestro to make an apple tree and a separate apple.\n```maestro-tool\n{"tool":"agent"}\n```';
const CREATE_RESULT='El árbol y la manzana están listos.\n[en] Our tree and our separate apple are ready.';
export const DELETE_RESULT='El árbol ya no está y la misma manzana sigue aquí.\n[en] The tree is gone and our same apple is still here.';
const suggestions=(tool:'agent'|null)=>JSON.stringify({suggestions:[{target:'La manzana.',native:'The apple.'}],reengagementSeconds:0,chatSummary:'An English speaker is learning Spanish with an apple tree and an independent apple.',globalProfile:'',artifact:null,toolRequest:tool?{tool}:null});
const definition=(state:RoomAgentState,id:string)=>{
 const d=readObjectReference(state.catalog,id);assert(d,'Expected a real available native object definition');return d;
};
const fact=(id:string,args:Record<string,unknown>):RoomCommand=>({action:'catalog',catalog:{operation:'inspect',category:'facts',capability:id,version:1,arguments:args}});
const part=(id:string,shape:string,y:number,size:{x:number;y:number;z:number},color:{r:number;g:number;b:number})=>({id,parent:'',shape,position:{x:0,y,z:0},size,rotation:{x:0,y:0,z:0,w:1},color:{...color,a:1}});

class LiveContinuityLearner extends NativeLearner {
 private mode:'lesson'|'create'|'live'|'restored'='lesson';
 private live!:Awaited<ReturnType<typeof firstLearnerLive>>;
 private createPlans=0;private deletePlans=0;private creates=0;private verifiers=0;private createResults=0;private deleteResults=0;private images=0;
 private creationAction:any;private treeId='';private appleId='';private pendingId='';
 private treeDefinition:unknown;private appleDefinition:unknown;private appleObservation:unknown;
 private receipts:unknown[]=[];private plans:unknown[]=[];private mediaChecks:unknown[]=[];private releasedCapture:unknown;
 private beforeReloadLive=0;private restored:unknown;private afterDeletion:unknown;
 private speechWorkers:unknown;private restoredSpeechWorkers:unknown;
 private beforeReloadCapture:unknown;private restoredCapture:unknown;
 async attach(page:Page,origin:string) {await page.context().addInitScript(installFirstLearnerSpeechObserver);await super.attach(page,origin);}
 private async readSpeechWorkers(requireReady:boolean) {
  const result=await this.page.evaluate(()=>(globalThis as any).__firstLearnerSpeechWorkers());
  assert.equal(result.version,1);assert.equal(result.overflow,false);
  assert(result.workers.every((w:any)=>w.errors.length===0),'Actual speech worker reported an initialization/inference error');
  if(requireReady)assert(result.workers.some((w:any)=>w.ready.some((x:any)=>x.model==='onnx-community/whisper-tiny.en'&&x.profile==='q4')),'Actual q4 Whisper pipeline never became ready');
  return result;
 }
 async close() {
  if(!this.speechWorkers&&this.page)this.speechWorkers=await this.page.evaluate(()=>(globalThis as any).__firstLearnerSpeechWorkers()).catch(()=>null);
  await super.close();
 }
 async offlineSpeechAssets() {
  const root=resolve(dirname(fileURLToPath(import.meta.url)),'..'),manifest=join(root,'test-fixtures/models/whisper-tiny-en.json');
  const {revision}=JSON.parse(await readFile(manifest,'utf8'));
  assert(/^[a-f0-9]{40}$/.test(revision));
  return loadFirstLearnerSpeechAssets(join(root,'.quest-tools','whisper-tiny-en-'+revision),manifest);
 }
 async offlineLive(input:{fakeKey:string;errors:string[];out:string}) {
  const root=resolve(dirname(fileURLToPath(import.meta.url)),'..');
  const metadata=JSON.parse(await readFile(join(root,'test-fixtures/audio/first-learner-apple-request.json'),'utf8'));
  assert.equal(metadata.text,LIVE_REQUEST);assert.equal(metadata.humanRecording,false);assert.equal(metadata.providerUsed,false);
  const wav=await readFile(join(root,'test-fixtures/audio/first-learner-apple-request.wav'));
  this.live=await firstLearnerLive({...input,syntheticSpeech:{wav,sha256:metadata.sha256,description:'Locally synthesized English request using '+metadata.voice.name+'; no human recording or remote provider.'}});
  return this.live;
 }
 imagePurpose(bodyText:string) {
  if(this.mode==='lesson')return null;
  assert.equal(this.mode,'create','Microphone-only Live and reload must not generate an image');
  assert(bodyText.includes(CREATE_REQUEST));assert.equal(this.images++,0);return 'native-tree-apple-default';
 }
 private async inspectPending(state:RoomAgentState,capability:string):Promise<RoomCommand[]> {
  const receipt=state.execution?.selected;assert(receipt?.id&&receipt.capability===capability);this.pendingId=receipt.id;
  await this.until(()=>this.latest?.execution?.selected?.id===receipt.id&&!['preparing','running'].includes(this.latest.execution.selected.phase),'Native execution remained pending');
  return [{action:'execution',execution:{operation:'inspect',runId:receipt.id}}];
 }
 private accept(state:RoomAgentState,capability:string) {
  const receipt=state.execution?.selected;assert.equal(receipt?.id,this.pendingId);assert.equal(receipt?.capability,capability);assert.equal(receipt?.phase,'completed');
  assert(receipt);this.receipts.push(structuredClone(receipt));return receipt;
 }
 private checkMutations() {
  const all=[...this.requests.values()].flatMap(x=>x.request.commands),starts=all.filter(c=>c.action==='execution'&&c.execution?.operation==='start');
  assert.equal(starts.length,this.mode==='create'?2:3,'Unexpected or replayed room mutation');
  const calls=starts.map(c=>c.execution?.operation==='start'?c.execution.call:null);assert(calls.every(Boolean));
  assert.equal(calls[0]!.id,'object.create');assert.equal(calls[0]!.arguments.name,TREE_NAME);assert.equal(calls[0]!.arguments.kind,'recipe');
  assert.equal(calls[1]!.id,'object.create');assert.equal(calls[1]!.arguments.name,APPLE_NAME);assert.equal(calls[1]!.arguments.kind,'primitive');assert.equal(calls[1]!.arguments.shape,'ball');
  if(calls.length===3){assert.equal(calls[2]!.id,'object.delete');assert.deepEqual(calls[2]!.arguments,{target:this.treeId});}
  assert.notEqual(this.appleId,this.treeId);
  assert(all.every(c=>c.action==='catalog'||c.action==='workspace'&&typeof c.visible==='boolean'&&Object.keys(c).every(k=>k==='action'||k==='visible')||c.action==='execution'&&['start','inspect'].includes(c.execution?.operation??'')));
  assert(all.filter(c=>c.action==='workspace').length<=24);
 }
 async stream(request:ScriptedRequest) {
  const {body,schema,bodyText,systemText}=request;
  if(this.mode==='lesson')return super.stream(request);
  assert.notEqual(this.mode,'restored','Reload must not replay provider operations');this.check();
  if(schema?.properties?.commands) {
   const live=this.mode==='live',count=live?++this.deletePlans:++this.createPlans;
   const input=readFirstLearnerPlannerInput(body,live?LIVE_REQUEST:CREATE_REQUEST),state=input.scene;
   assert.equal(input.tutorContext.nativeLanguageCode,'en-US');assert(input.tutorContext.systemInstruction.includes('English')&&input.tutorContext.systemInstruction.includes('Spanish'));
   assert.equal(state.session,this.latest?.session);assert.equal(input.receipts.length,count-1);
   let commands:RoomCommand[]=[];
   if(!live) {
    assert(bodyText.includes('This is my very first lesson.'));
    if(count===1)commands=[{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}}];
    else if(count===2) {
     const catalog=state.catalog;assert(validCatalogView(catalog));assert(catalog?.operation==='inspect'&&catalog.capability==='object.create'&&catalog.definition);this.creationAction=structuredClone(catalog.definition);
     const variants=(this.creationAction.input as any)?.oneOf;assert(Array.isArray(variants)&&variants.some(v=>v.properties?.kind?.enum?.includes('recipe')));
     const recipe={version:1,duration:1,loop:false,playing:false,parts:[part('Trunk','cylinder',.5,{x:.18,y:1,z:.18},{r:.4,g:.2,b:0}),part('Crown','sphere',1.2,{x:1,y:.85,z:1},{r:0,g:.7,b:0})],tracks:[]};
     commands=[{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{kind:'recipe',name:TREE_NAME,x:-.6,y:0,z:1.3,scale:1,recipe}}}}];
    } else if(count===3)commands=await this.inspectPending(state,'object.create');
    else if(count===4) {
     const receipt=this.accept(state,'object.create');assert.equal(receipt.call.arguments.name,TREE_NAME);this.treeId=String(receipt.output?.objectId);assert(/^[a-f0-9]{32}$/.test(this.treeId));
     const example=this.creationAction.example;assert(example.kind==='primitive'&&example.shape==='ball');
     commands=[{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...example,name:APPLE_NAME,x:.5,y:.7,z:.8,scale:.6,red:1,green:0,blue:0}}}}];
    } else if(count===5)commands=await this.inspectPending(state,'object.create');
    else if(count===6) {
     const receipt=this.accept(state,'object.create');assert.equal(receipt.call.arguments.name,APPLE_NAME);this.appleId=String(receipt.output?.objectId);assert(/^[a-f0-9]{32}$/.test(this.appleId));assert.notEqual(this.appleId,this.treeId);
     commands=[fact('object.definition',{target:this.treeId})];
    } else if(count===7) {const d=definition(state,this.treeId);assert.equal(d.name,TREE_NAME);assert.equal(d.kind,'Assembly');assert.equal((state.catalog as any).value.content.parts,2);assert.equal((state.catalog as any).value.content.recipePlaying,false);this.treeDefinition=structuredClone(state.catalog);commands=[fact('object.definition',{target:this.appleId})];}
    else if(count===8) {const d=definition(state,this.appleId);assert.equal(d.name,APPLE_NAME);assert.equal(d.kind,'Ball');this.appleDefinition=structuredClone(state.catalog);commands=[fact('object.observation',{target:this.appleId})];}
    else if(count===9) {const d=readRoomObjectObservation(state.catalog,this.appleId);assert(d&&d.name===APPLE_NAME);assert.deepEqual(d.color,{r:1,g:0,b:0,a:1});this.appleObservation=structuredClone(state.catalog);}
    else throw new Error('Unexpected creation planner call');
   } else {
    // Every continuation carries the original captured turn, never a new microphone read.
    this.mediaChecks.push(this.live.assertPlannerMedia(body));
    assert(input.tutorContext.systemInstruction.includes(CREATE_REQUEST),'Live delegated context lost the prior normal-chat creation request');
    if(count===1)commands=[{action:'catalog',catalog:{operation:'inspect',capability:'object.delete',version:1}}];
    else if(count===2) {assert(validCatalogView(state.catalog));assert(state.catalog?.operation==='inspect'&&state.catalog.capability==='object.delete'&&state.catalog.definition);commands=[fact('object.definition',{target:this.treeId})];}
    else if(count===3) {const d=definition(state,this.treeId);assert.equal(d.kind,'Assembly');assert.equal(d.name,TREE_NAME);commands=[{action:'execution',execution:{operation:'start',call:{id:'object.delete',version:1,arguments:{target:this.treeId}}}}];}
    else if(count===4)commands=await this.inspectPending(state,'object.delete');
    else if(count===5) {const receipt=this.accept(state,'object.delete');assert.deepEqual(receipt.call.arguments,{target:this.treeId});commands=[fact('object.definition',{target:this.treeId})];}
    else if(count===6) {const c=state.catalog;assert(c?.operation==='inspect'&&c.category==='facts'&&c.capability==='object.definition'&&c.arguments?.target===this.treeId);assert.equal(c.available,false);assert.equal(c.value,null);commands=[fact('object.definition',{target:this.appleId})];}
    else if(count===7) {definition(state,this.appleId);assert.deepEqual((state.catalog as any).value,(this.appleDefinition as any).value);commands=[fact('object.observation',{target:this.appleId})];}
    else if(count===8) {const d=readRoomObjectObservation(state.catalog,this.appleId);assert(d&&d.name===APPLE_NAME);assert.deepEqual(d.color,{r:1,g:0,b:0,a:1});commands=[{action:'catalog',catalog:objectSearchRequest(this.treeId,'all','')}];}
    else if(count===9) {const page=readObjectSearchPage(state.catalog,objectSearchRequest(this.treeId,'all',''));assert(page);assert.equal(page.total,0);this.afterDeletion=structuredClone(state.catalog);}
    else throw new Error('Unexpected deletion planner call');
   }
   this.plans.push({phase:this.mode,count,scene:state,receipts:input.receipts,commands});return {kind:'native-planner',text:JSON.stringify({commands})};
  }
  if(schema?.properties?.suggestions) {
   const message=readFirstLearnerSuggestionMessage(body);this.verifiers++;
   if(this.mode==='create'&&this.createPlans===0) {assert.equal(this.verifiers,1);assert.equal(message,CREATE_REPLY);assert(bodyText.includes('originalUserRequest')&&bodyText.includes(CREATE_REQUEST));return {kind:'native-verifier',text:suggestions('agent')};}
   if(this.mode==='create') {assert.equal(this.verifiers,2);assert.equal(this.createPlans,9);assert.equal(this.createResults,1);assert.equal(message,parseStrictTutorResponseText(CREATE_RESULT,'en-US').visibleText);return {kind:'native-result-verifier',text:suggestions(null)};}
   if(this.deletePlans===0) {assert.equal(this.verifiers,3);assert.equal(message,LIVE_REPLY);assert(bodyText.includes('originalUserTranscript')&&bodyText.includes(LIVE_REQUEST));return {kind:'native-live-verifier',text:suggestions('agent')};}
   assert.equal(this.verifiers,4);assert.equal(this.deletePlans,9);assert.equal(this.deleteResults,1);assert.equal(message,parseStrictTutorResponseText(DELETE_RESULT,'en-US').visibleText);return {kind:'native-result-verifier',text:suggestions(null)};
  }
  if(this.mode==='create'&&this.creates===0) {assert.equal(this.creates++,0);assert(bodyText.includes(CREATE_REQUEST));assert(systemText.includes('A connected room agent is available'));return {kind:'native-tutor',text:CREATE_REPLY};}
  assert(bodyText.includes('Write only the result reply for the room task that has already run.'));assert(systemText.includes('completed'));
  if(this.mode==='create') {assert.equal(this.createResults++,0);assert.equal(this.createPlans,9);assert(systemText.includes(this.treeId)&&systemText.includes(this.appleId));return {kind:'native-result',text:CREATE_RESULT};}
  assert.equal(this.deleteResults++,0);assert.equal(this.deletePlans,9);assert(systemText.includes(this.treeId));return {kind:'native-result',text:DELETE_RESULT};
 }
 async afterLesson() {
  this.mode='create';const composer=this.page.locator('textarea').filter({visible:true});await composer.fill(CREATE_REQUEST);await this.page.getByRole('button',{name:'Send message',exact:true}).click();
  await this.page.getByText('Our tree and our separate apple are ready.',{exact:false}).first().waitFor({timeout:60000});await this.until(()=>this.verifiers===2,'Creation result verifier did not complete');
  await this.page.waitForFunction(()=>window.maestroBook?.snapshot().activity==='idle');this.checkMutations();await this.checkpoint();await this.backToChat();
  await this.page.waitForFunction(()=>{const x=(globalThis as any).__firstLearnerSpeechWorkers();return x.overflow||x.workers.some((w:any)=>w.ready.length||w.errors.length);},null,{timeout:30000});
  await this.readSpeechWorkers(true);
  this.mode='live';await this.live.arm(this.page);await this.page.getByRole('button',{name:'Start Live',exact:true}).click();
  // This is a real finalized Live turn: verifier runs only after captured context is sealed.
  await this.until(()=>this.verifiers>=3,'Live turn never reached the shared suggestions handoff',45000);
  // End explicit Live promptly. Let the normal completed-task verifier finish
  // before pausing background observation; Pause also defers automatic replies.
  await this.page.getByRole('button',{name:'Stop Live',exact:true}).click();await this.page.getByRole('button',{name:'Start Live',exact:true}).waitFor();
  await this.page.getByText(LIVE_REQUEST,{exact:true}).first().waitFor();
  await this.page.getByText('The tree is gone and our same apple is still here.',{exact:false}).first().waitFor({timeout:60000});await this.until(()=>this.verifiers===4,'Live task result verifier did not complete');
  const flag=this.page.getByTitle('Click to view status',{exact:true});await flag.waitFor({state:'visible'});
  const box=await flag.boundingBox();assert(box);const point={x:box.x+box.width/2,y:box.y+box.height/2};
  // The waving flag never satisfies a two-frame stability wait. Hit-test its
  // visible centre, then send a real pointer click; do not invoke its handler.
  assert(await flag.evaluate((element,p)=>element.contains(document.elementFromPoint(p.x,p.y)),point));
  await this.page.mouse.click(point.x,point.y);
  await this.page.getByRole('button',{name:'Pause Maestro',exact:true}).click();
  await this.page.getByRole('button',{name:'Resume Maestro',exact:true}).waitFor();
  this.releasedCapture=await this.live.assertReleased(this.page);
  this.speechWorkers=await this.readSpeechWorkers(true);
  await this.page.waitForFunction(()=>window.maestroBook?.snapshot().activity==='idle');await this.live.settled();this.checkMutations();
  const directory=await this.readFact('world.objects',undefined,{query:this.treeId});assert.equal(readObjectSearchPage(directory,objectSearchRequest(this.treeId,'all',''))?.total,0);
  const apple=await this.readFact('object.definition',this.appleId);assert.deepEqual(apple.value,(this.appleDefinition as any).value);
  await this.checkpoint();await this.page.screenshot({path:join(this.out,'native-live-continuity.png')});await this.backToChat();
  assert.equal(this.images,1);assert.equal(this.creates,1);assert.equal(this.createPlans,9);assert.equal(this.deletePlans,9);
 }
 async beforeReload() {
  this.beforeReloadLive=this.live.evidence().sessions.length;await super.beforeReload();
  // This finite fixture supplies exactly one utterance, then verified silence.
  // Withdraw its isolated microphone grant at the reload boundary; retained-
  // permission observer startup is a separate qualification scenario.
  // Recheck immediately before withdrawing permission: reload cannot hide a
  // late reacquisition after the learner paused observation.
  this.beforeReloadCapture=await this.live.assertReleased(this.page);
  assert.deepEqual(this.beforeReloadCapture,this.releasedCapture,'Paused observation reacquired capture before reload');
  await this.page.context().clearPermissions();this.mode='restored';
 }
 async afterReload() {
  this.start();await this.until(()=>Boolean(this.latest?.session),'Reload did not accept the fresh native handshake');assert.notEqual(this.clientId,this.firstClient);assert.equal(this.clients.length,2);assert.equal(this.latest?.visible,false);assert.equal(this.requests.size,this.beforeReloadRequests);
  await this.page.getByText(CREATE_REQUEST,{exact:true}).first().waitFor();await this.page.getByText(LIVE_REQUEST,{exact:true}).first().waitFor();await this.page.getByText('The tree is gone and our same apple is still here.',{exact:false}).first().waitFor();
  const apple=await this.readFact('object.definition',this.appleId);assert.deepEqual(apple.value,(this.appleDefinition as any).value);
  const directory=await this.readFact('world.objects',undefined,{query:this.treeId});assert.equal(readObjectSearchPage(directory,objectSearchRequest(this.treeId,'all',''))?.total,0);this.restored={apple,directory};
  await this.checkpoint();this.checkMutations();assert([...this.requests.values()].filter(x=>x.clientId===this.clientId).every(x=>x.request.commands.every(c=>c.action==='catalog'||c.action==='workspace')));
  assert.equal(this.live.evidence().sessions.length,this.beforeReloadLive);
  await this.backToChat();await this.page.getByRole('button',{name:'Start Live',exact:true}).waitFor();
  await this.page.waitForFunction(()=>{const x=(globalThis as any).__firstLearnerCapture();return x.tracks.length===0&&x.attempts.every((a:any)=>a.state==='rejected');});
  this.restoredCapture=await this.page.evaluate(()=>(globalThis as any).__firstLearnerCapture());
  const capture=this.restoredCapture as any;assert.deepEqual(capture.tracks,[]);assert(capture.attempts.every((a:any)=>a.state==='rejected'&&a.audio===true&&a.video===false),'Reload retained an active or non-audio capture acquisition');
  this.restoredSpeechWorkers=await this.readSpeechWorkers(false);this.check();this.qualified=true;
 }
 evidence() {return {nativeProbeId:this.channel.id,qualified:this.qualified,realProvider:false,physicalDevice:false,syntheticPhysics:this.ready.syntheticPhysics,syntheticScan:this.ready.syntheticScan,
  boundary:'Production App onboarding/text/Live capture/shared handoff plus actual Unity Editor receipts; scripted provider and synthetic browser microphone. Synthetic microphone permission withdrawn before browser reload; no native restart, real provider, physical headset/mic/camera, recognition, audible speech or AEC quality claim.',
  speechWorkers:this.speechWorkers,restoredSpeechWorkers:this.restoredSpeechWorkers,treeId:this.treeId,appleId:this.appleId,treeDefinition:this.treeDefinition,appleDefinition:this.appleDefinition,appleObservation:this.appleObservation,receipts:this.receipts,afterDeletion:this.afterDeletion,restored:this.restored,mediaChecks:this.mediaChecks,releasedCapture:this.releasedCapture,beforeReloadCapture:this.beforeReloadCapture,restoredCapture:this.restoredCapture,
  counts:{createPlanner:this.createPlans,deletePlanner:this.deletePlans,verifiers:this.verifiers,creationTutor:this.creates,creationResults:this.createResults,deletionResults:this.deleteResults,images:this.images},live:this.live?.evidence(),clients:this.clients,workspaceCloseAcks:this.workspaceCloseAcks,requests:[...this.requests.values()],observations:this.observations,plans:this.plans};}
}
export async function runNativeLiveContinuity(directory:string) {
 const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8'));assert.equal(owner.firstLearner,true);assert.equal(owner.liveContinuity,true,'Root wrapper must explicitly identify this scenario');
 const channel=await RoomProbeChannel.connect(directory,120000),ready=JSON.parse(await readFile(join(directory,'ready.json'),'utf8'));
 assert.equal(ready.id,channel.id);assert.equal(ready.version,1);assert.equal(ready.syntheticPhysics,true);assert.equal(ready.syntheticScan,false);
 const native=new LiveContinuityLearner(channel,ready,join(directory,'first-learner'));await runQuestFirstLearner({out:join(directory,'first-learner'),native});
}
if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)){assert(process.argv[2]);await runNativeLiveContinuity(resolve(process.argv[2]));}
