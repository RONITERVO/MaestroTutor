// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium,type Browser} from 'playwright-core';
import {createServer,type ViteDevServer} from 'vite';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import assert from 'node:assert/strict';
import {createHeadlessClient} from '../src/headless/client';
import {parseStrictTutorResponseText} from '../src/core-sdk/chat/tutorResponse';
import {roomReplyHasToolRequest} from '../src/core-sdk/room/roomTaskProvider';
import {RoomProbeChannel} from '../src/headless/roomTransport';
import {captureManagedJourneyBilling,evaluateManagedJourneyBilling,waitForManagedJourneyBillingSettlement} from '../src/headless/managedJourneyBilling';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {assertCreatedParityBall,assertPaintedParityBall,assertSameRoomObjects} from './agent-provider-contract';
import {bookProviderStage,readBookProviderResponse,assertBookProviderPipeline,type BookProviderResponse} from './book-provider-evidence';
const directory=process.argv[2];if(!directory)throw new Error('Supply the fresh native probe directory.');
if(process.env.MAESTRO_ROOM_PROBE_SCENARIO!=='ContextCreateEdit')throw new Error('Real book currently requires ContextCreateEdit.');
const client=await createHeadlessClient(),managed=client.accessMode==='managed'; // Credentials/accounting only; UI performs all chat work.
const backend=process.env.MAESTRO_BACKEND_BASE_URL||'',apiKey=(process.env.MAESTRO_GEMINI_API_KEY||'').trim();
if(managed&&!backend)throw new Error('Managed book requires the explicit backend URL.');
if(!managed&&!apiKey)throw new Error('BYOK book requires MAESTRO_GEMINI_API_KEY.');
const channel=await RoomProbeChannel.connect(directory,120000);
let browser:Browser|undefined,server:ViteDevServer|undefined,page:Awaited<ReturnType<Browser['newPage']>>|undefined;
const secrets=new Set<string>(apiKey?[apiKey]:[]);
const safe=(text:string)=>{for(const secret of secrets)text=text.split(secret).join('[redacted]');return text;};
const errors:string[]=[],observations:RoomAgentState[]=[],commands=new Map<string,unknown>();
const calls:Array<{stage:BookProviderResponse['stage'];request:unknown;response?:BookProviderResponse;error?:string}>=[],indexes=new Map<string,number>();
const evidence:Record<string,unknown>={phase:'starting',providerUsed:true,accessMode:client.accessMode,
 boundary:'Real Chrome QuestBookSurface/chat/verifier/task/IndexedDB/provider and Unity room. Test credentials are supplied to an isolated fixture. No interactive sign-in, Quest attestation, Android texture, physical input or headset acceptance.'};
const save=()=>writeFile(join(directory,'book-provider.json'),JSON.stringify({...evidence,calls,commands:[...commands.values()],observations,errors},null,2));
const pause=(ms:number)=>new Promise(resolve=>setTimeout(resolve,ms));
const billingId=client.runtime.ids.create('book-provider');let beforeBilling:Awaited<ReturnType<typeof captureManagedJourneyBilling>>|undefined;
try{
 if(managed)beforeBilling=await captureManagedJourneyBilling(client,billingId);
 server=await createServer({define:{'import.meta.env.VITE_BACKEND_BASE_URL':JSON.stringify(backend)},cacheDir:join(directory,'vite-cache'),
  optimizeDeps:{entries:['test-fixtures/browser/quest-native-book.html']},server:{host:managed?'localhost':'127.0.0.1',port:managed?80:0,strictPort:true,watch:null},logLevel:'warn',clearScreen:false});
 await server.listen();const address=server.httpServer!.address();assert.ok(address&&typeof address==='object');
 // Managed staging explicitly allows localhost with its default port. Keep CORS intact.
 const base=managed?'http://localhost':'http://127.0.0.1:'+address.port,url=base+'/test-fixtures/browser/quest-native-book.html';
 browser=await chromium.launch({channel:'chrome',headless:true});const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.addInitScript(()=>{let workers:ServiceWorkerContainer|undefined;try{workers=navigator.serviceWorker;}catch(error){if(error instanceof DOMException&&error.name==='SecurityError')return;throw error;}
  if(workers)workers.register=async()=>{throw new DOMException('Workers disabled in isolated book probe.','NotSupportedError');};});
 page=await context.newPage();page.setDefaultTimeout(20000);page.on('pageerror',error=>errors.push(safe(error.message)));
 const ownFrame=(source:{frame:import('playwright-core').Frame})=>{if(source.frame!==page!.mainFrame()||source.frame.url()!==url)throw new Error('Only the owned top-level fixture may use this binding.');};
 await page.exposeBinding('nativeBookCredentials',async source=>{
  ownFrame(source);if(!managed)return {mode:'byok',apiKey};
  const [result,firebaseIdToken,appCheckToken]=await Promise.all([client.backend.getManagedSession(),client.credentials.getFirebaseIdToken(),client.credentials.getAppCheckToken()]);
  assert.ok(firebaseIdToken&&appCheckToken);secrets.add(firebaseIdToken);secrets.add(appCheckToken);
  return {mode:'managed',session:{...result.session,firebaseIdToken,refreshToken:null,expiresAt:null,lastSyncedAt:Date.now()},appCheckToken};
 });
 await page.exposeBinding('maestroNativeExchange',async(source,snapshot)=>{
  ownFrame(source);if(snapshot.request)commands.set(snapshot.clientId+':'+snapshot.request.sequence,structuredClone(snapshot.request));
  const result=await channel.exchange(snapshot);
  if(result.state){const state=result.state as RoomAgentState,old=observations.at(-1);if(old?.session!==state.session||old.revision!==state.revision)observations.push(structuredClone(state));}
  return result;
 });
 const providerUrl=(value:string)=>{const target=new URL(value);return managed?target.origin===new URL(backend).origin&&target.pathname===new URL(backend).pathname.replace(/\/$/,'')+'/gemini/generate-content-stream'
  :target.hostname==='generativelanguage.googleapis.com'&&target.pathname.includes(':streamGenerateContent');};
 // Network allowlist only; all provider request and response bytes pass unchanged.
 await context.route('**/*',route=>{const target=new URL(route.request().url());return target.origin===base||providerUrl(target.href)?route.continue():route.abort();});
 // CDP's response body can guess a legacy charset for SSE without charset.
 // Fetch text() always decodes UTF-8, exactly like the provider client's reader.
 // Observe a clone; the original Response and provider bytes pass untouched.
 await page.exposeBinding('nativeBookProviderResponse',async(source,observation:{id:string;request?:unknown;body?:string;status?:number;error?:string})=>{
  ownFrame(source);
  if(observation.request!==undefined){assert.ok(!indexes.has(observation.id));indexes.set(observation.id,calls.length);calls.push({stage:bookProviderStage(observation.request),request:observation.request});return;}
  const index=indexes.get(observation.id);assert.notEqual(index,undefined);const call=calls[index!];
  try{if(observation.error)throw new Error(observation.error);call.response=readBookProviderResponse(observation.body!,managed,call.stage,observation.status!);}
  catch(error){call.error=safe(String(error));}await save();
 });
 await context.addInitScript({content:`{
  const {managed,backend}=${JSON.stringify({managed,backend})};
  const original=window.fetch.bind(window);let count=0;
  const report=value=>window.nativeBookProviderResponse(value);
  window.fetch=async(...args)=>{
   const target=new URL(typeof args[0]==='string'?args[0]:args[0] instanceof URL?args[0].href:args[0].url,location.href);
   const matched=managed?target.origin===new URL(backend).origin&&target.pathname===new URL(backend).pathname.replace(/\\/$/,'')+'/gemini/generate-content-stream'
    :target.hostname==='generativelanguage.googleapis.com'&&target.pathname.includes(':streamGenerateContent');
   if(!matched)return original(...args);
   const id=String(performance.timeOrigin)+':'+(++count),request=JSON.parse(String(args[1]?.body||'{}'));
   await report({id,request});
   try{const response=await original(...args);void response.clone().text().then(body=>report({id,body,status:response.status}),error=>report({id,error:String(error)}));return response;}
   catch(error){await report({id,error:String(error)});throw error;}
  };
 }`});
 const flush=async()=>{const deadline=Date.now()+15000;while(calls.some(call=>!call.response&&!call.error)&&Date.now()<deadline)await pause(100);};
 const read=()=>page!.evaluate(()=>({ ...window.nativeBookEvidence!(),book:window.maestroBook!.snapshot() }));
 type State=Awaited<ReturnType<typeof read>>;
 const wait=async(label:string,predicate:(state:State)=>boolean,timeout=180000)=>{
  const deadline=Date.now()+timeout;do{const state=await read();if(state.errors.length||errors.length)throw new Error('Book reported an error: '+[...state.errors,...errors].join('; '));
   if(predicate(state))return state;await pause(150);}while(Date.now()<deadline);throw new Error('Timed out waiting for '+label);};
 await page.goto(url,{waitUntil:'domcontentloaded',timeout:60000});await page.waitForFunction(()=>!!window.nativeBookEvidence?.().state,{},{timeout:30000});
 const initial=await read();evidence.initial=initial;
 const contextText='For our room test, remember that my test object is named ParityBall. It is a blue ball with half the standard diameter. Do not create or change anything yet; just acknowledge this definition.';
 const submit=async(text:string)=>{const input=page!.getByRole('textbox').filter({visible:true}).last();await input.fill(text);await input.press('Enter');};
 await submit(contextText);
 await wait('context chat',state=>state.messages.some(m=>m.role==='assistant'&&!m.thinking&&!!m.llmRawResponse)&&!state.inputBlocked);
 // Suggestions are independently asynchronous. Wait for the real verifier stream too.
 const contextDeadline=Date.now()+120000;
 while(!calls.some(call=>call.stage==='verifier'&&call.response)&&Date.now()<contextDeadline)await pause(100);
 assert.ok(calls.some(call=>call.stage==='verifier'&&call.response),'Context verifier did not finish');await flush();
 const contextDone=await read();assertSameRoomObjects(initial.state!,contextDone.state!);assert.equal(contextDone.messages.some(m=>m.agentTask),false);
 evidence.context=contextDone;await save();
 const turn=async(text:string,label:string)=>{
  const callStart=calls.length,before=await read();const oldIds=before.messages.map(m=>m.id);await submit(text);
  const working=await wait(label+' active agent',state=>state.agentWorking&&state.messages.some(m=>m.agentTask&&!oldIds.includes(m.id)));
  assert.equal(working.inputBlocked,false,'The agent blocked normal chat input');assert.equal(working.book.activity,'thinking');
  await page!.screenshot({path:join(directory,'book-provider-'+label+'-working.png')});
  const done=await wait(label+' final chat',state=>!state.agentWorking&&state.messages.some(m=>m.agentTask&&!oldIds.includes(m.id)&&!['working','replying'].includes(m.agentTask.phase)),600000);
  const message=done.messages.find(m=>m.agentTask&&!oldIds.includes(m.id))!;
  const task=await page!.evaluate(id=>window.nativeBookTask!(id),message.agentTask!.id);assert.ok(task);
  evidence[label]={working,done,task};await save();
  assert.equal(task.phase,'completed');assert.equal(task.handoff.input.prompt,text);assert.ok(task.handoff.input.history.length>0);
  assert.equal(task.handoff.sourceUserId,done.messages.find(m=>m.role==='user'&&m.text===text)?.id);
  assert.ok(task.operations.length&&task.operations.every(op=>op.receipt?.ok===true),'Native task receipts failed');assert.ok(task.reply?.rawResponse);
  assert.equal(message.role,'assistant');assert.equal(message.agentTask!.phase,'completed');
  await flush();const responses=calls.slice(callStart).map(call=>{assert.ok(call.response&&!call.error,'Missing real network response');return call.response;});assertBookProviderPipeline(responses);
  const finalResponse=responses.at(-1)!;assert.equal(finalResponse.stage,'chat');assert.equal(roomReplyHasToolRequest(finalResponse.text),false,'Result delegated again');
  assert.equal(parseStrictTutorResponseText(finalResponse.text,task.handoff.input.nativeLanguageCode).visibleText,task.reply!.rawResponse,'Task reply differs from actual provider output');
  const section=page!.getByRole('region',{name:'Agent task',exact:true}).last();await section.getByText('Task details',{exact:true}).click();
  await section.getByText('Recorded action batches: '+task.operations.length+'.',{exact:true}).waitFor();
  const replyText=message.translations?.[0]?.target||message.text;assert.ok(replyText&&replyText.trim());await page!.getByText(replyText,{exact:true}).last().waitFor();
  await page!.screenshot({path:join(directory,'book-provider-'+label+'-completed.png')});
  return {done,task};
 };
 evidence.phase='creation';await save();
 const created=await turn('Please ask the room agent to create my test object now using the definition I gave earlier.','create');
 const ball=assertCreatedParityBall(initial.state!,created.done.state!,'blue',.5);
 evidence.phase='editing';await save();
 const edited=await turn('Ask the room agent to paint that same ParityBall red, keeping its size and placement and leaving all other objects unchanged.','edit');
 assertPaintedParityBall(created.done.state!,edited.done.state!,ball.id);
 // Ordinary generated workshop controls call the same native Undo/Redo handlers.
 await page.evaluate(()=>window.maestroBook!.command({version:1,type:'workspace.open'}));
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();
 const undo=await wait('native Undo',state=>state.state!.sceneRevision>edited.done.state!.sceneRevision);assertSameRoomObjects(created.done.state!,undo.state!);
 await page.getByRole('button',{name:'Redo',exact:true}).first().click();
 const redo=await wait('native Redo',state=>state.state!.sceneRevision>undo.state!.sceneRevision);assertSameRoomObjects(edited.done.state!,redo.state!);
 await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();await wait('workshop closed',state=>!state.state!.visible);
 evidence.undo=undo;evidence.redo=redo;
 const commandCount=commands.size,callCount=calls.length;
 await page.reload();await page.waitForFunction(()=>!!window.nativeBookEvidence?.().state);
 const restored=await wait('saved task reload',state=>state.messages.some(m=>m.id===edited.task.id&&m.agentTask?.phase==='completed'));
 await pause(1000);assert.equal(commands.size,commandCount,'Reload repeated a native command');assert.equal(calls.length,callCount,'Reload repeated a provider request');
 assertSameRoomObjects(edited.done.state!,restored.state!);assert.equal(restored.agentWorking,false);
 const stored=await page.evaluate(id=>window.nativeBookTask!(id),edited.task.id);assert.deepEqual(stored,edited.task);
 await page.screenshot({path:join(directory,'book-provider-reloaded.png')});
 evidence.restored=restored;evidence.phase='passed';evidence.coverage={realTutorVerifierAgentReply:true,earlierContext:true,originalRequest:true,
  nativeCreateAndEdit:true,unrelatedObjectsPreserved:true,visibleTaskStateAndResult:true,chatUsableDuringAgent:true,nativeUndoRedo:true,persistedJournal:true,reloadWithoutProviderOrNativeReplay:true};
}catch(error){evidence.phase='failed';evidence.error=safe(String(error));if(page&&!page.isClosed())await page.screenshot({path:join(directory,'book-provider-failure.png')}).catch(()=>{});throw new Error(safe(String(error)));
}finally{
 // Stop before billing reconciliation, including failed/timeout paths. Never retry the task.
 await page?.evaluate(()=>window.nativeBookStop?.()).catch(()=>{});
 const observationDeadline=Date.now()+15000;while(calls.some(call=>!call.response&&!call.error)&&Date.now()<observationDeadline)await pause(100);
 await browser?.close();
 try{if(beforeBilling){const after=await waitForManagedJourneyBillingSettlement(client,billingId);evidence.billing=evaluateManagedJourneyBilling(beforeBilling,after,{requirePaidUsage:evidence.phase==='passed'});}
  else evidence.billing={applicable:false,passed:true,payer:'byok-api-key-owner'};
 }catch(error){evidence.billing={applicable:true,passed:false,error:safe(String(error))};}
 if(!(evidence.billing as {passed:boolean})?.passed)evidence.phase='failed';
 await save();try{await server?.close();}finally{await channel.stop();}
}
assert.equal((evidence.billing as {passed:boolean})?.passed,true,'Book billing did not reconcile');
console.log('Real-provider rendered book and native room journey passed.');
