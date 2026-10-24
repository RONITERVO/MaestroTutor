import {validFactValue} from '../shared/behaviourFacts';
import {nativeBookObjects,readNativeBookObjects} from './native-book-objects';
import {probeBookEnvironmentRecovery} from './probe-book-environment-recovery';
import {readObjectReference} from '../shared/objectReference';
import {chatSoundFixture} from './probe-chat-audio';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {assertControllerSettings,type ControllerSettings} from './probe-controller-completion';
import {chromium,type Browser} from 'playwright-core';
import {createServer,type ViteDevServer} from 'vite';
import {readFile,writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import assert from 'node:assert/strict';
import {assertWorldLocation,type WorldLocation} from './probe-world-location';
import {gaitFixture} from './probe-object-walk';
import {RoomProbeChannel} from '../src/headless/roomTransport';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {roomGuide} from '../shared/prompts/roomguides';
import {ROOM_TASK_LIMITS} from '../shared/roomTaskBudget';
const directory=process.argv[2];if(!directory)throw new Error('Supply the fresh, explicitly started native probe directory.');
const owner=JSON.parse(await readFile(join(directory,'owner.json'),'utf8'));
const checkSparse=(state:RoomAgentState)=>{if(owner.sparseObjectView&&state.objects.length)throw new Error('Sparse book fixture leaked a complete object inventory');};
const channel=await RoomProbeChannel.connect(directory,120000);
let browser:Browser|undefined,server:ViteDevServer|undefined;
const requests=new Map<string,unknown>(),observations:RoomAgentState[]=[],providerRequests:unknown[]=[],errors:string[]=[];
let page:Awaited<ReturnType<Browser['newPage']>>|undefined;
let releasePlan:()=>void=()=>{};const blockedPlan=new Promise<void>(resolve=>{releasePlan=resolve;});
let plannerCalls=0,normalReplies=0,verificationCalls=0,planWaiting=false,plannedBallId='',plannedCreationRunId='';
const discoveryReads=ROOM_TASK_LIMITS.queryBatches,createPlan=discoveryReads-1,expectedPlans=discoveryReads+3;
try{
 // This immutable verification page needs no watcher or shared optimizer cache.
 server=await createServer({cacheDir:join(directory,'vite-cache'),optimizeDeps:{entries:['test-fixtures/browser/quest-native-book.html']},server:{host:'127.0.0.1',port:0,strictPort:true,watch:null},logLevel:'warn',clearScreen:false});await server.listen();
 const address=server.httpServer!.address();assert.ok(address&&typeof address==='object');const base='http://127.0.0.1:'+address.port;
 browser=await chromium.launch({channel:'chrome',headless:true});
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 // Playwright's built-in blocker reads navigator.serviceWorker without guarding
 // opaque sandbox frames. Keep worker registration blocked without that exception.
 await context.addInitScript(()=>{
  let workers:ServiceWorkerContainer|undefined;
  try{workers=navigator.serviceWorker;}catch(error){if(error instanceof DOMException&&error.name==='SecurityError')return;throw error;}
  if(workers)workers.register=async()=>{throw new DOMException('Service workers are disabled in the native-book probe.','NotSupportedError');};
 });
 page=await context.newPage();page.setDefaultTimeout(20000);page.on('pageerror',error=>errors.push(error.stack||error.message));
 await page.exposeBinding('maestroNativeExchange',async(source,snapshot)=>{
  if(source.frame!==page!.mainFrame()||source.frame.url()!==base+'/test-fixtures/browser/quest-native-book.html')throw new Error('Only the owned top-level book can use this native channel.');
  if(snapshot.request)requests.set(snapshot.clientId+':'+snapshot.request.sequence,structuredClone(snapshot.request));
  const result=await channel.exchange(snapshot);
  if(result.state){const state=result.state as RoomAgentState,previous=observations.at(-1);checkSparse(state);if(previous?.session!==state.session||previous.revision!==state.revision)observations.push(structuredClone(state));}
  return result;
 });
 await context.route('**/*',async route=>{
  const request=route.request(),url=new URL(request.url());
  if(['127.0.0.1','localhost'].includes(url.hostname))return route.continue();
  if(url.hostname!=='generativelanguage.googleapis.com'||!url.pathname.includes(':streamGenerateContent'))return route.abort();
  const body=request.postDataJSON();providerRequests.push(body);
  const schema=body.generationConfig?.responseJsonSchema??body.generationConfig?.responseSchema;
  let text='';
  if(schema?.properties?.commands){
   plannerCalls++;
   const texts=body.contents.flatMap((c:{parts?:{text?:string}[]})=>(c.parts??[]).flatMap(p=>p.text?[p.text]:[]));
   const input=JSON.parse(texts.at(-1));assert.equal(input.request,'Create a ball, then make it green.');
   const state=input.scene as RoomAgentState;assert.ok(state.session&&state.sceneRevision);
   assert.equal(input.receipts.length,plannerCalls-1);
   // Exercise the exhausted-discovery boundary with actual native reads.
   assert.deepEqual(input.budget,{planningCalls:ROOM_TASK_LIMITS.planningCalls+1-plannerCalls,queryBatches:discoveryReads-(plannerCalls<=createPlan?plannerCalls-1:plannerCalls<=discoveryReads+2?plannerCalls-2:discoveryReads),actionBatches:ROOM_TASK_LIMITS.actionBatches-(plannerCalls<=createPlan?0:plannerCalls<=discoveryReads+2?1:2)});
   if(plannerCalls===1||plannerCalls===4)text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'search',query:plannerCalls===1?'object.create':'object.color',offset:0}}]});
   else if(plannerCalls===5)text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'inspect',capability:'object.color.set',version:1}}]});
   else if(plannerCalls===3){
    const definition=state.catalog?.operation==='inspect'?state.catalog.definition:null;assert.ok(definition&&'example' in definition&&definition.example);
    text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'check',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Cooperative ball',x:.5,y:1,z:.8}}}}]});
   }else if(plannerCalls<createPlan)text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}}]});
   else if(plannerCalls===createPlan){
    const definition=state.catalog?.operation==='inspect'?state.catalog.definition:null;
    assert.ok(definition&&'example' in definition&&definition.example);
    text=JSON.stringify({commands:[{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Cooperative ball',x:.5,y:1,z:.8}}}}]});
   }else if(plannerCalls===discoveryReads){
    const created=state.execution?.selected;assert.equal(created?.capability,'object.create');assert.ok(created?.id);
    assert.equal(created.call.arguments.name,'Cooperative ball');plannedCreationRunId=created.id;
    assert.ok(['preparing','running','completed'].includes(created.phase));
    if(created.phase!=='completed')assert.equal(created.output?.objectId,undefined,'Pending creation cannot supply its result');
    // Synchronize this fast scripted response with the native worker. No native
    // command or result is injected: the provider still proposes one real read,
    // using only the run ID in its input. That read consumes discovery allowance.
    await page!.waitForFunction(id=>{const receipt=window.nativeBookEvidence!().state?.execution?.selected;return receipt?.id===id&&receipt.phase!=='preparing'&&receipt.phase!=='running';},created.id,{timeout:35000});
    text=JSON.stringify({commands:[{action:'execution',execution:{operation:'inspect',runId:created.id}}]});
   }else if(plannerCalls===discoveryReads+1){
    const created=state.execution?.selected;assert.equal(created?.capability,'object.create');assert.equal(created?.phase,'completed');assert.equal(created?.call.arguments.name,'Cooperative ball');assert.equal(typeof created?.output?.objectId,'string');plannedBallId=created!.output!.objectId as string;
    text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.definition',version:1,arguments:{target:plannedBallId}}}]});
   }else if(plannerCalls===discoveryReads+2){
    const ball=readObjectReference(state.catalog,plannedBallId);assert.ok(ball);assert.equal(ball.name,'Cooperative ball');planWaiting=true;await blockedPlan;
    text=JSON.stringify({commands:[{action:'paint',target:ball.id,color:{r:0,g:1,b:0,a:1}}]});
   }else if(plannerCalls===expectedPlans){assert.equal(input.receipts.at(-1).ok,false);assert.match(input.receipts.at(-1).status,/target changed/);text=JSON.stringify({commands:[]});}
   else throw new Error('Unexpected additional planner request');
  }else if(schema?.properties?.suggestions){
   verificationCalls++;
   text=JSON.stringify({suggestions:[{target:'Gracias.',native:'Thank you.'}],reengagementSeconds:90,chatSummary:'Offline native book integration.',globalProfile:'',artifact:null,toolRequest:verificationCalls===1?{tool:'agent'}:null});
  }else{
   normalReplies++;text=normalReplies===1?'Voy a crear la pelota.\n[en] I will create the ball.\n```maestro-tool\n{"tool":"agent"}\n```':'La pelota conserva tu color.\n[en] The ball keeps your colour.';
  }
  await route.fulfill({contentType:'text/event-stream',body:'data: '+JSON.stringify({candidates:[{content:{role:'model',parts:[{text}]},finishReason:'STOP'}],usageMetadata:{promptTokenCount:1,candidatesTokenCount:1,totalTokenCount:2},modelVersion:'offline-native-book-fixture'})+'\n\n'});
 });
 await page.goto(base+'/test-fixtures/browser/quest-native-book.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.waitForFunction(()=>!!window.nativeBookEvidence?.().state,{},{timeout:30000});
 console.log('Actual native book handshake received.');
 const initial=await page.evaluate(()=>window.nativeBookEvidence!().state!);checkSparse(initial);
 const book=nativeBookObjects(page),chooseObject=book.choose;
 const readResourceFact=async(id:string,label:string,args:Record<string,string>={})=>(await book.fact(id,label,args)).value as Record<string,string|number>;
 const undoRemoved=async(target:string)=>{
  const revision=await page!.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
  await page!.getByRole('button',{name:'Undo',exact:true}).filter({visible:true}).first().click();
  await page!.waitForFunction(revision=>window.nativeBookEvidence!().state!.sceneRevision>revision,revision);
  assert.equal(await book.exists(target),false);await book.workshop();
 };
 const checkpointSamples:unknown[]=[];const checkpointDeadline=Date.now()+20000;
 do{
  const value=(await book.fact('room.persistence','Room and behaviour save status')).value;
  assert.ok(validFactValue('room.persistence',value));checkpointSamples.push(value);
  const stores=Object.values(value as Record<string,{phase:string;dirty:boolean;pending:boolean;dispatchHeld:boolean;error:string}>);
  assert.ok(stores.every(store=>!['unconfirmed','unavailable','temporary'].includes(store.phase)),JSON.stringify(value));
  if(stores.every(store=>store.phase==='idle'&&!store.dirty&&!store.pending&&!store.dispatchHeld&&store.error===''))break;
  await page.waitForTimeout(100);
 }while(Date.now()<checkpointDeadline);
 const checkpointLast=checkpointSamples.at(-1) as Record<string,{phase:string;dirty:boolean;pending:boolean;dispatchHeld:boolean;error:string}>;
 assert.ok(Object.values(checkpointLast).every(store=>store.phase==='idle'&&!store.dirty&&!store.pending&&!store.dispatchHeld&&store.error===''),'Book did not observe confirmed journal checkpoints');
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:join(directory,'book-room-persistence.png')});
 await writeFile(join(directory,'book-room-persistence.json'),JSON.stringify({samples:checkpointSamples,boundary:'Visible original-book catalog controls reading real native checkpoint state; desktop only, no headset or provider claim.'},null,2));
 await book.workshop();
 const initialObjects=await readNativeBookObjects(page);
 assert.ok(initialObjects.objects.some(o=>o.id==='book')&&initialObjects.objects.some(o=>o.id==='maestro'));
 await writeFile(join(directory,'book-explicit-read-set.json'),JSON.stringify({boundary:'Real visible generated facts and object inspector; separate exact object/physics read set with no hydration or hidden native requests.',initialObjects},null,2));
 const initialSavedCount=await book.count();assert.equal(initialObjects.objects.length,initialSavedCount);await book.workshop();
 await page.evaluate(()=>window.maestroBook!.command({version:1,type:'workspace.open'}));
 // Optional physical tools use the actual generated form and native receipt path.
 await page.getByRole('button',{name:'Physical tools',exact:true}).click();
 await page.getByText('Edit action fields',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action arguments',{exact:true}).isVisible(),false,'Source must be optional');
 assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).isVisible(),false,'Read-only state references must be optional');
 assert.equal(await page.getByText('Argument reference',{exact:true}).evaluate(e=>(e.parentElement as HTMLDetailsElement).open),false);
 const formRequests=requests.size,manualForm:unknown[]=[];
 for(const size of [{width:1024,height:768},{width:819,height:614}]){
  await page.setViewportSize(size);
  const tray=page.getByRole('combobox',{name:'Action inputs tray',exact:true});await tray.scrollIntoViewIfNeeded();
  assert.equal(await tray.isVisible(),true);const trayBox=await tray.boundingBox();assert.ok(trayBox&&trayBox.height>=44&&trayBox.x>=size.width/2&&trayBox.x+trayBox.width<=size.width);
  const run=page.getByRole('button',{name:'Run action now',exact:true});assert.equal(await run.isDisabled(),true,'Opening fields must not bypass current-value guards');await run.scrollIntoViewIfNeeded();
  const runBox=await run.boundingBox();assert.ok(runBox&&runBox.height>=44&&runBox.y>=0&&runBox.y+runBox.height<=size.height);
  const widths=await page.getByLabel('Action details',{exact:true}).evaluate(e=>({visible:e.clientWidth,content:e.scrollWidth}));assert.ok(widths.content<=widths.visible+1,'Manual controls overflow the book page');
  await page.screenshot({path:join(directory,`book-action-form-${size.width}x${size.height}.png`)});manualForm.push({size,trayBox,runBox,widths});
 }
 await page.getByText('Argument reference',{exact:true}).click();
 assert.equal(await page.getByText('Argument reference',{exact:true}).evaluate(e=>(e.parentElement as HTMLDetailsElement).open),true);
 await page.getByText('Argument reference',{exact:true}).click();
 await page.getByText('Current room references',{exact:true}).click();
 assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).isVisible(),true);
 assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).getAttribute('readonly'),'');
 await page.getByText('Current room references',{exact:true}).click();
 assert.equal(requests.size,formRequests,'Reading the manual fields/reference dispatched a room command');
 await page.setViewportSize({width:1440,height:1080});
 await page.getByLabel('Action details',{exact:true}).evaluate(e=>{e.scrollTop=0;});
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.waitForFunction(()=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability==='room.tools'&&c.available===true;});
 const toolsBefore=await page.evaluate(()=>{const c=window.nativeBookEvidence!().state!.catalog;if(c?.operation!=='inspect'||c.category!=='facts')throw new Error('Tool fact missing');return c.value as {stateId:string;visible:Record<string,boolean>};});
 assert.equal(Object.values(toolsBefore.visible).some(Boolean),false,'Fresh workspace exposed physical trays');
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='room.tools.set'&&r.phase==='completed'&&r.output?.visible&&typeof r.output.visible==='object'&&!Array.isArray(r.output.visible)&&(r.output.visible as Record<string,unknown>).creation===true;});
 const toolsShown=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 await page.screenshot({path:join(directory,'book-tools-shown.png')});
 assert.equal(await page.getByText('Edit action fields',{exact:true}).evaluate(e=>(e.parentElement as HTMLDetailsElement).open),true,'Normal fields must start open');
 await page.getByLabel('Action inputs tray',{exact:true}).selectOption('all');
 await page.getByLabel('Action inputs visible',{exact:true}).selectOption('false');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(previous=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.id!==previous&&r?.capability==='room.tools.set'&&r.phase==='completed';},toolsShown.id);
 const toolsHidden=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 assert.equal(Object.values(toolsHidden.output!.visible as Record<string,boolean>).some(Boolean),false);
 assert.equal((await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision)),initial.sceneRevision,'Tool visibility changed saved scene data');
 await writeFile(join(directory,'book-physical-tools.json'),JSON.stringify({before:toolsBefore,shown:toolsShown,hidden:toolsHidden,sharedFormAndNativeReceipts:true},null,2));
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 await page.getByRole('button',{name:'+ Box robot',exact:true}).click();
 await page.getByRole('heading',{name:'Practice robot',exact:true}).waitFor();
 const robot=await page.evaluate(()=>window.nativeBookEvidence!().state!.inspection!);
 assert.ok(robot.recipe&&robot.recipe.parts.length>1);
 await page.getByRole('button',{name:'Purple',exact:true}).click();
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(id=>{const s=window.nativeBookEvidence!().state;return !!s?.inspection&&s.inspection.id===id&&s.execution?.selected?.capability==='object.recipe.edit'&&s.execution.selected.phase==='completed';},robot.id);
 const edited=await page.evaluate(()=>window.nativeBookEvidence!().state!);
 assert.notDeepEqual(edited.inspection!.recipe,robot.recipe);assert.equal(edited.inspection!.id,robot.id);
 await page.getByRole('status').filter({hasText:'Recipe changes saved.'}).waitFor();
 await page.screenshot({path:join(directory,'book-native-edit.png')});
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();
 await page.waitForFunction(revision=>window.nativeBookEvidence!().state!.sceneRevision>revision,edited.sceneRevision);
 const restored=await page.evaluate(()=>window.nativeBookEvidence!().state!);assert.deepEqual(restored.inspection!.recipe,robot.recipe);
 await undoRemoved(robot.id);
 await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();
 console.log('Actual book create/edit/Undo passed.');
 await writeFile(join(directory,'manual-book.json'),JSON.stringify({boundary:'Real book UI and Unity room; no native replay, headset or live provider.',initial,robot,edited,restored},null,2));
 // The ordinary chat hook, verifier, task coordinator and browser provider client all run.
 const input=page.getByRole('textbox').filter({visible:true}).last();await input.fill('Create a ball, then make it green.');
 await input.press('Enter');
 await page.waitForFunction(()=>window.nativeBookEvidence!().agentWorking,{},{timeout:30000});
 const waitingDeadline=Date.now()+30000;while(!planWaiting&&Date.now()<waitingDeadline)await new Promise(resolve=>setTimeout(resolve,100));assert.ok(planWaiting,'Planner did not reach the live edit boundary');
 const working=await page.evaluate(()=>window.nativeBookEvidence!());assert.equal(working.inputBlocked,false);
 const ball={id:plannedBallId};assert.ok(ball.id);assert.equal(readObjectReference(working.state!.catalog,ball.id)?.name,'Cooperative ball');
 await page.evaluate(()=>window.maestroBook!.command({version:1,type:'workspace.open'}));
 await book.inspect(ball.id);
 await page.getByRole('button',{name:'Purple',exact:true}).click();
 await page.waitForFunction(id=>{const item=window.nativeBookEvidence!().state!.inspection?.item;return item?.id===id&&Math.abs(item.color.r-.47)<.0001;},ball.id);
 const human=await page.evaluate(()=>window.nativeBookEvidence!().state!);
 await page.screenshot({path:join(directory,'book-human-takeover.png')});
 releasePlan();
 await page.waitForFunction(()=>window.nativeBookEvidence!().messages.some(m=>m.agentTask?.phase==='completed'),{},{timeout:30000});
 const completed=await page.evaluate(()=>window.nativeBookEvidence!());assert.equal(completed.agentWorking,false);
 const taskMessage=completed.messages.find(m=>m.agentTask?.phase==='completed')!;
 const task=await page.evaluate(id=>window.nativeBookTask!(id),taskMessage.agentTask!.id);assert.ok(task);
 assert.equal(task.handoff.input.prompt,'Create a ball, then make it green.');assert.equal(task.phase,'completed');
 const creationStart=task.operations.filter(o=>o.commands[0].action==='execution'&&o.commands[0].execution?.operation==='start'&&o.commands[0].execution.call?.id==='object.create');
 const creationRead=task.operations.filter(o=>o.commands[0].action==='execution'&&o.commands[0].execution?.operation==='inspect'&&o.commands[0].execution.runId===plannedCreationRunId);
 assert.equal(creationStart.length,1);assert.equal(creationRead.length,1);
 assert.equal(creationRead[0].receipt?.execution?.selected?.phase,'completed');
 assert.equal(creationRead[0].receipt?.execution?.selected?.output?.objectId,plannedBallId);
 const failedPaint=task.operations.find(o=>o.commands[0].action==='paint');assert.ok(failedPaint?.receipt&&!failedPaint.receipt.ok,'Stale planned paint must be refused after the human edit');
 const keptBall=await book.inspect(ball.id);assert.deepEqual(keptBall.item!.color,human.inspection!.item!.color);
 assert.equal(await book.count(),initialSavedCount+1);await book.workshop();assert.equal(plannerCalls,expectedPlans);assert.equal(verificationCalls,1);assert.equal(normalReplies,2);
 await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();
 assert.equal(task.operations.length,discoveryReads+2);
 await page.getByText('Task details',{exact:true}).click();await page.getByText(`Recorded action batches: ${discoveryReads+2}.`,{exact:true}).waitFor();
 await page.screenshot({path:join(directory,'book-agent-result.png')});
 // Capture through the real generated book form and native image channel.
 await page.evaluate(()=>window.maestroBook!.command({version:1,type:'workspace.open'}));
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Capture virtual room');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Capture virtual room.*room.view.capture/}).click();
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.getByRole('img',{name:'Virtual room snapshot',exact:true}).waitFor();
 const image=await page.getByRole('img',{name:'Virtual room snapshot',exact:true}).getAttribute('src');assert.ok(image&&image.startsWith('data:image/jpeg;base64,'));
 const capture=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);assert.equal(capture.capability,'room.view.capture');assert.equal(capture.phase,'completed');
 const pixels=Buffer.from(image.split(',')[1],'base64');const {createHash}=await import('node:crypto');assert.equal(createHash('sha256').update(pixels).digest('hex'),capture.output!.sha256);
 await writeFile(join(directory,'book-native-view.jpg'),pixels);await page.screenshot({path:join(directory,'book-native-snapshot.png')});
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Save shared appearance');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Save shared appearance.*appearance.save/}).click();
 await page.getByLabel('Action inputs name',{exact:true}).fill('Book-created glass');
 await page.getByLabel('Action inputs style opacity',{exact:true}).fill('0.35');
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='appearance.save'&&r.phase==='completed';});
 const appearance=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 assert.equal(appearance.call.arguments.name,'Book-created glass');assert.equal((appearance.call.arguments.style as {opacity:number}).opacity,.35);assert.ok(appearance.output?.id);
 await writeFile(join(directory,'book-native-appearance.json'),JSON.stringify({boundary:'Actual generated book form and native saved receipt, scripted provider elsewhere; no headset/provider proof for appearances.',appearance},null,2));
 await page.screenshot({path:join(directory,'book-native-appearance.png')});
 // Saved layers use ordinary generated forms and their current-value guard.
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Save visual layer');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Save visual layer.*visibility.layer.save/}).click();
 await page.getByLabel('Action inputs name',{exact:true}).fill('Book-created layer');await page.getByLabel('Action inputs opacity',{exact:true}).fill('0.6');
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='visibility.layer.save'&&r.phase==='completed';});
 const visualLayer=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Choose object visual layer');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Choose object visual layer.*object.visibility.assign/}).click();
 await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved visual layer',exact:true}).click();
 await page.getByLabel('Choose visual layer',{exact:true}).selectOption(JSON.stringify([visualLayer.output!.id,visualLayer.output!.revision]));
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='object.visibility.assign'&&r.phase==='completed';});
 const visualBinding=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 await writeFile(join(directory,'book-native-visibility.json'),JSON.stringify({boundary:'Generated book forms and native saved-layer receipts; no headset or provider acceptance.',visualLayer,visualBinding},null,2));
 await page.screenshot({path:join(directory,'book-native-visibility.png')});
 // Use the ordinary generated forms to bind a style and capture the styled object.
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Choose object appearance');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Choose object appearance.*object.appearance.bind/}).click();
 await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved appearance',exact:true}).click();
 await page.getByLabel('Choose appearance',{exact:true}).selectOption(JSON.stringify([appearance.output!.id,appearance.output!.revision]));
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='object.appearance.bind'&&r.phase==='completed';});
 const boundAppearance=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Save construction');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Save construction.*program.module.captureConstruction/}).click();
 const memberFields=page.getByText('Action inputs members · 1 entries',{exact:true}),memberTarget=page.getByLabel('Action inputs members 1 target',{exact:true});
 await memberFields.waitFor({state:'visible'}); // Inspection is asynchronous; the keyed form opens after its native reply.
 if(!await memberTarget.isVisible())await memberFields.click();
 await page.getByLabel('Action inputs name',{exact:true}).fill('My styled ball');
 await chooseObject('Action inputs members 1 target',ball.id);
 await page.getByLabel('Action inputs members 1 slot',{exact:true}).fill('ball');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='program.module.captureConstruction'&&r.phase==='completed';});
 const portableCapture=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);assert.equal(typeof portableCapture.output?.hash,'string');
 await writeFile(join(directory,'book-native-construction-resources.json'),JSON.stringify({boundary:'Real generated book forms and native receipts for style assignment and library capture; no provider or headset proof.',appearance,boundAppearance,portableCapture},null,2));
 await page.screenshot({path:join(directory,'book-native-construction-resources.png')});
 // Saved collision and sound choices use the same catalog-driven selector, with no implicit execution.
 const openNamedAction=async(label:string,id:string)=>{
  await page!.getByRole('button',{name:'Back to workshop',exact:true}).click();await page!.getByRole('button',{name:'Action catalog',exact:true}).click();
  await page!.getByLabel('Search actions',{exact:true}).fill(label);await page!.getByRole('button',{name:'Search',exact:true}).click();
  await page!.getByRole('button',{name:new RegExp(label+'.*'+id.replaceAll('.','\\.'))}).click();
 };
 const runNamedAction=async(id:string,timeout=30000)=>{
  const previous=await page!.evaluate(()=>{const s=window.nativeBookEvidence!().state!;return {id:s.execution?.selected?.id,ack:s.ack};});
  await page!.getByRole('button',{name:'Run action now',exact:true}).click();
  await page!.waitForFunction(({id,previous})=>{const s=window.nativeBookEvidence!().state!,r=s.execution?.selected;return s.ack!==previous.ack&&s.ok===false||r?.id!==previous.id&&r?.capability===id&&['completed','failed','cancelled'].includes(r.phase);},{id,previous},{timeout});
  const state=await page!.evaluate(()=>window.nativeBookEvidence!().state!);
  assert.equal(state.ok,true,state.status);assert.notEqual(state.execution?.selected?.id,previous.id);
  assert.equal(state.execution?.selected?.capability,id);assert.equal(state.execution?.selected?.phase,'completed',state.execution?.selected?.status);
  return state.execution!.selected!;
 };
 await probeBookEnvironmentRecovery(page,directory);
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 // Navigate through visible generated forms, then return to the starting location.
 const locationBefore=await readResourceFact('world.viewpoint','Your world location') as unknown as WorldLocation;
 assert.equal(locationBefore.located,true);
 const locationRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 const chooseLocation=async(location:Pick<WorldLocation,'position'|'yaw'>)=>{
  await openNamedAction('Go to a world location','world.viewpoint.set');
  await page!.getByRole('button',{name:'Load current values',exact:true}).click();await page!.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  for(const axis of ['x','y','z'] as const)await page!.getByLabel('Action inputs position '+axis,{exact:true}).fill(String(location.position[axis]));
  await page!.getByLabel('Action inputs yaw',{exact:true}).fill(String(location.yaw));
  return runNamedAction('world.viewpoint.set');
 };
 const locationTarget={position:locationBefore.position,yaw:(locationBefore.yaw+25+540)%360-180};
 const locationMoved=await chooseLocation(locationTarget);assertWorldLocation(locationMoved.output as WorldLocation,locationTarget);
 const locationAfter=await readResourceFact('world.viewpoint','Your world location') as unknown as WorldLocation;
 assertWorldLocation(locationAfter,locationTarget);assert.notEqual(locationAfter.stateId,locationBefore.stateId);
 const locationRestored=await chooseLocation(locationBefore);assertWorldLocation(locationRestored.output as WorldLocation,locationBefore);
 assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),locationRevision);
 await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-world-location-completion.png')});
 await writeFile(join(directory,'book-world-location-completion.json'),JSON.stringify({before:locationBefore,target:locationTarget,moved:locationMoved,after:locationAfter,restored:locationRestored,boundary:'Visible generated book controls with actual desktop Unity completions, fresh guards, current location and unchanged scene revision. No provider or physical headset claim.'},null,2));
 // Personal settings use the same completion operation through visible book fields.
 const controlsBefore=await readResourceFact('controller.settings','Controller settings and bindings') as unknown as ControllerSettings;
 const controlsRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 const editControls=async(value:ControllerSettings)=>{
  await openNamedAction('Configure controller bindings','controller.configure');
  await page!.getByLabel('Controller settings',{exact:true}).selectOption(String(['right','left','none'].indexOf(value.avatarStick)));
  await page!.getByRole('button',{name:'Load current values',exact:true}).click();await page!.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  await page!.getByLabel('Action inputs userStick',{exact:true}).selectOption(value.userStick);
  for(const key of ['deadZone','userSpeed'] as const)await page!.getByLabel('Action inputs '+key,{exact:true}).fill(String(value[key]));
  return runNamedAction('controller.configure');
 };
 const controlsTarget={...controlsBefore,deadZone:controlsBefore.deadZone===.3?.25:.3};
 const controlsChanged=await editControls(controlsTarget);
 const controlsAfter=await readResourceFact('controller.settings','Controller settings and bindings') as unknown as ControllerSettings;
 assertControllerSettings(controlsAfter,controlsTarget);assert.notEqual(controlsAfter.configurationId,controlsBefore.configurationId);
 assert.deepEqual(controlsChanged.output,{configurationId:controlsAfter.configurationId,saved:true});
 const controlsRestored=await editControls(controlsBefore);
 await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-controller-completion.png')});
 const controlsFinal=await readResourceFact('controller.settings','Controller settings and bindings') as unknown as ControllerSettings;
 assertControllerSettings(controlsFinal,controlsBefore);assert.notEqual(controlsFinal.configurationId,controlsAfter.configurationId);
 assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),controlsRevision);
 await writeFile(join(directory,'book-controller-completion.json'),JSON.stringify({before:controlsBefore,target:controlsTarget,changed:controlsChanged,after:controlsAfter,restored:controlsRestored,final:controlsFinal,boundary:'Visible generated book forms with real desktop Unity preference save completions. Input neutral/cancellation/lifetime tested separately in native suites. No headset or provider proof.'},null,2));
 await openNamedAction('Set world lighting','world.lighting.set');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs settings enabled',{exact:true}).selectOption('true');
 await page.getByLabel('Action inputs settings ambientIntensity',{exact:true}).fill('0.2');
 await page.getByLabel('Action inputs settings sunIntensity',{exact:true}).fill('0.7');
 await page.getByLabel('Action inputs settings ambientColor',{exact:true}).fill('#223344');
 const lighting=await runNamedAction('world.lighting.set');
 assert.equal((lighting.output!.settings as {enabled:boolean}).enabled,true);assert.ok(Math.abs((lighting.output!.settings as {ambientIntensity:number}).ambientIntensity-.2)<1e-6);
 await writeFile(join(directory,'book-native-lighting.json'),JSON.stringify({boundary:'Real generated book form, current-value guard and native saved lighting receipt; no headset/provider proof.',lighting},null,2));
 await page.screenshot({path:join(directory,'book-native-lighting.png')});
 await openNamedAction('Edit an object lamp','object.light.edit');
 await chooseObject('Action inputs target',ball.id);
 await page.getByLabel('Action inputs settings kind',{exact:true}).selectOption('spot');
 await page.getByLabel('Action inputs settings shadows',{exact:true}).selectOption('hard');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const lampSaved=await runNamedAction('object.light.edit');assert.equal(lampSaved.output!.light,'Lamp');
 await openNamedAction('Control a lamp','object.light.control');await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs enabled',{exact:true}).selectOption('false');
 const lampOff=await runNamedAction('object.light.control');assert.equal(lampOff.output!.active,false);assert.equal(lampOff.output!.revision,lampSaved.output!.revision);
 await page.getByRole('heading',{name:'Control a lamp',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-lamp.png')});
 await page.getByLabel('Variant',{exact:true}).selectOption('1');await chooseObject('Action inputs target',ball.id);await page.getByLabel('Action inputs light',{exact:true}).fill('Lamp');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const lampReset=await runNamedAction('object.light.control');assert.equal(lampReset.output!.active,true);
 await openNamedAction('Edit an object lamp','object.light.edit');await page.getByLabel('Variant',{exact:true}).selectOption('1');await chooseObject('Action inputs target',ball.id);await page.getByLabel('Action inputs light',{exact:true}).fill('Lamp');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const lampRemoved=await runNamedAction('object.light.edit');
 await writeFile(join(directory,'book-native-lamp.json'),JSON.stringify({boundary:'Original generated book forms, native lamp editing, live control/reset and removal. No provider/headset or visual illumination claim.',lampSaved,lampOff,lampReset,lampRemoved},null,2));
 await openNamedAction('Set world time','world.time.seek');
 await page.getByLabel('Action inputs day',{exact:true}).fill('2');await page.getByLabel('Action inputs second',{exact:true}).fill('64800');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs day',{exact:true}).inputValue(),'2');assert.equal(await page.getByLabel('Action inputs second',{exact:true}).inputValue(),'64800');
 const worldTime=await runNamedAction('world.time.seek');assert.equal(worldTime.output!.day,2);assert.equal(worldTime.output!.second,64800);
 await writeFile(join(directory,'book-native-world-time.json'),JSON.stringify({boundary:'Actual generated book seek form, guard-only loading and native saved time receipt; no headset or real-provider proof.',worldTime},null,2));
 await page.screenshot({path:join(directory,'book-native-world-time.png')});
 await openNamedAction('Configure world time and day lighting','world.time.configure');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs settings running',{exact:true}).selectOption('false');
 await page.getByLabel('Action inputs settings rate',{exact:true}).fill('60');
 await page.getByLabel('Action inputs settings cycleEnabled',{exact:true}).selectOption('true');
 await page.getByText('Action inputs settings frames · 0 entries',{exact:true}).click();
 const frames=[{second:0,ambientColor:'#0000FF',sunColor:'#FFFFFF',ambientIntensity:.2,sunIntensity:0,azimuth:0,elevation:-60},{second:43200,ambientColor:'#FFFFFF',sunColor:'#FFF0DD',ambientIntensity:.6,sunIntensity:1,azimuth:0,elevation:60}];
 for(const [index,frame] of frames.entries()){
  await page.getByRole('button',{name:'Add Action inputs settings frames entry',exact:true}).click();
  for(const [key,value] of Object.entries(frame))await page.getByLabel(`Action inputs settings frames ${index+1} ${key}`,{exact:true}).fill(String(value));
 }
 const dayCycle=await runNamedAction('world.time.configure');
 const cycleSettings=dayCycle.output!.settings as {running:boolean;rate:number;cycleEnabled:boolean;frames:typeof frames};
 assert.equal(cycleSettings.cycleEnabled,true);assert.equal(cycleSettings.running,false);assert.equal(cycleSettings.rate,60);assert.equal(cycleSettings.frames.length,2);assert.equal(cycleSettings.frames[0].ambientColor,'#0000FF');assert.equal(cycleSettings.frames[1].second,43200);assert.equal(dayCycle.output!.day,2);assert.equal(dayCycle.output!.second,64800);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs settings cycleEnabled',{exact:true}).selectOption('false');const retainedCycle=await runNamedAction('world.time.configure');
 assert.equal((retainedCycle.output!.settings as typeof cycleSettings).cycleEnabled,false);assert.deepEqual((retainedCycle.output!.settings as typeof cycleSettings).frames,cycleSettings.frames);
 await writeFile(join(directory,'book-native-day-cycle.json'),JSON.stringify({boundary:'Actual generated book controls add two daily frames, save through native receipts and disable the cycle without losing its frames; no headset/provider claim.',dayCycle,retainedCycle},null,2));
 await page.getByRole('heading',{name:'Configure world time and day lighting',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-day-cycle.png')});
 await openNamedAction('Set world weather','world.weather.set');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 for(const [key,value] of Object.entries({rainMmPerHour:12,windX:2,windZ:-1,cloudCover:.6,fogDensity:.04,fogColor:'#AABBCC'}))await page.getByLabel('Action inputs settings '+key,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs seed',{exact:true}).fill('31');await page.getByLabel('Action inputs transitionSeconds',{exact:true}).fill('0');
 const weather=await runNamedAction('world.weather.set');
 assert.equal((weather.output!.settings as {rainMmPerHour:number}).rainMmPerHour,12);assert.equal(weather.output!.seed,31);assert.equal((weather.output!.transition as {progress:number}).progress,1);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs settings fogColor',{exact:true}).inputValue(),'#AABBCC');assert.equal(await page.getByLabel('Action inputs seed',{exact:true}).inputValue(),'31');
 await writeFile(join(directory,'book-native-weather.json'),JSON.stringify({boundary:'Actual generated original-book controls and native weather receipt; no headset appearance or real-provider proof.',weather},null,2));
 await page.getByRole('heading',{name:'Set world weather',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-weather.png')});
 await openNamedAction('Configure or fill a liquid container','object.container.edit');
 await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs definition amountMl',{exact:true}).fill('100');
 await page.getByLabel('Action inputs definition fluid densityKgM3',{exact:true}).fill('850');
 await page.getByLabel('Action inputs definition fluid linearDrag',{exact:true}).fill('3');
 const medium=await runNamedAction('object.container.edit');
 assert.equal((medium.call.arguments.definition as {fluid:{densityKgM3:number}}).fluid.densityKgM3,850);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs definition fluid densityKgM3',{exact:true}).inputValue(),'850');
 assert.equal(await page.getByLabel('Action inputs definition amountMl',{exact:true}).inputValue(),'100');
 await page.getByLabel('Include Action inputs definition fluid',{exact:true}).uncheck();
 await page.getByLabel('Include Action inputs definition fluid',{exact:true}).check();
 assert.equal(await page.getByLabel('Action inputs definition fluid densityKgM3',{exact:true}).inputValue(),'1000');
 assert.equal(await page.getByLabel('Action inputs definition fluid linearDrag',{exact:true}).inputValue(),'2');
 assert.equal(await page.getByLabel('Action inputs definition fluid angularDrag',{exact:true}).inputValue(),'1');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs definition fluid densityKgM3',{exact:true}).inputValue(),'850');
 await page.getByLabel('Action inputs definition fluid densityKgM3',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-medium.png')});
 await writeFile(join(directory,'book-native-medium.json'),JSON.stringify({boundary:'Actual original-book fluid authoring, native receipt and current-value reload; no physical buoyancy or provider proof.',medium},null,2));
 // The same measured transfer is authored through visible nested object inputs.
 await openNamedAction('Create object','object.create');
 await page.getByLabel('Creation kind',{exact:true}).selectOption({label:'Create starter object'});
 await page.getByLabel('Action inputs templateHash',{exact:true}).selectOption({label:'Cup'});
 await page.getByLabel('Action inputs name',{exact:true}).fill('Book transfer cup');
 for(const [key,value] of Object.entries({x:4,y:2,z:3,scale:1}))await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(value));
 const transferCup=await runNamedAction('object.create'),transferCupId=transferCup.output!.objectId as string;
 await openNamedAction('Transfer a measured amount of liquid','object.container.transfer');
 await chooseObject('Action inputs source target',ball.id);await chooseObject('Action inputs destination target',transferCupId);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs amountMl',{exact:true}).fill('25');
 const measuredTransfer=await runNamedAction('object.container.transfer');assert.equal(measuredTransfer.output!.transferredMl,25);
 await page.getByRole('heading',{name:'Transfer a measured amount of liquid',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-measured-transfer.png')});
 type BookContainer={definition:{amountMl:number;capacityMl:number;fluid:{densityKgM3:number;linearDrag:number}}};
 const transferredFrom=await readResourceFact('object.container','Liquid container and contents',{target:ball.id}) as unknown as BookContainer;
 const transferredTo=await readResourceFact('object.container','Liquid container and contents',{target:transferCupId}) as unknown as BookContainer;
 assert.equal(transferredFrom.definition.amountMl,75);assert.equal(transferredTo.definition.amountMl,25);
 assert.equal(transferredTo.definition.fluid.densityKgM3,850);assert.equal(transferredTo.definition.fluid.linearDrag,3);
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 const transferRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();await page.waitForFunction(revision=>window.nativeBookEvidence!().state!.sceneRevision>revision,transferRevision);
 const transferRestored=await readResourceFact('object.container','Liquid container and contents',{target:ball.id}) as unknown as BookContainer;assert.equal(transferRestored.definition.amountMl,100);
 await writeFile(join(directory,'book-native-measured-transfer.json'),JSON.stringify({boundary:'Visible original-book source/destination choices, current revisions, measured input, confirmed native receipt, paired amounts and adopted properties, then ordinary Undo. No provider or headset acceptance.',transferCup,measuredTransfer,transferredFrom,transferredTo,transferRestored},null,2));
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await undoRemoved(transferCupId);
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 // A user authors the same terrain reservoir through the original book forms.
 await openNamedAction('Create object','object.create');
 await page.getByLabel('Creation kind',{exact:true}).selectOption({label:'Create starter object'});
 await page.getByLabel('Action inputs templateHash',{exact:true}).selectOption({label:'Snow patch'});
 await page.getByLabel('Action inputs name',{exact:true}).fill('Book terrain reservoir');
 for(const [key,value] of Object.entries({x:4,y:2,z:4,scale:1}))await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(value));
 const terrainCreated=await runNamedAction('object.create'),terrainTarget=terrainCreated.output!.objectId as string;
 await openNamedAction('Configure or fill a liquid container','object.container.edit');
 await chooseObject('Action inputs target',terrainTarget);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Include Action inputs definition terrainFloor',{exact:true}).check();
 await page.getByLabel('Action inputs definition terrainFloor',{exact:true}).selectOption('true');
 await page.getByLabel('Action inputs definition height',{exact:true}).fill('.3');
 await page.getByLabel('Action inputs definition amountMl',{exact:true}).fill('50000');
 const terrainConfigured=await runNamedAction('object.container.edit');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs definition terrainFloor',{exact:true}).inputValue(),'true');
 const terrainCapacity=Number(await page.getByLabel('Action inputs definition capacityMl',{exact:true}).inputValue());assert.ok(terrainCapacity>50000);
 await page.getByRole('heading',{name:'Configure or fill a liquid container',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-terrain-water.png')});
 await writeFile(join(directory,'book-native-terrain-water.json'),JSON.stringify({boundary:'Original book generated controls create a terrain owner and configure its reservoir; native readback derives capacity and keeps the requested amount. No hidden fixture mutation, provider or headset acceptance.',terrainCreated,terrainConfigured,capacityMl:terrainCapacity},null,2));
 // The user selects both exact objects in the generated habitat fact form.
 const habitatRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 const habitatView=await book.fact('object.water.habitat.path','Inspect a water habitat path',{target:ball.id,habitat:terrainTarget});
 const habitatValue=habitatView.value as {bodyId:string;contained:boolean;availability:string;reason:string};
 assert.equal(habitatValue.bodyId,terrainTarget);assert.equal(habitatValue.contained,false);
 assert.equal(habitatValue.availability,'ready');assert.ok(habitatValue.reason.includes('cavity'));
 assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),habitatRevision);
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:join(directory,'book-native-water-habitat.png')});
 await writeFile(join(directory,'book-native-water-habitat.json'),JSON.stringify({boundary:'Visible original-book object choices and shared native whole-body habitat fact. The actor starts outside this exact terrain reservoir, so containment correctly refuses without editing or moving it. No swimming/provider/headset acceptance.',view:habitatView,sceneRevision:habitatRevision},null,2));
 // Sculpt through the same visible controls, preserving the reservoir's water.
 await openNamedAction('Sculpt a surface path','object.field.sculpt');
 await chooseObject('Action inputs target',terrainTarget);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs mode',{exact:true}).selectOption('lower');
 await page.getByLabel('Action inputs radius',{exact:true}).fill('.14');
 await page.getByLabel('Action inputs height',{exact:true}).fill('.04');
 const terrainSculpted=await runNamedAction('object.field.sculpt');assert.ok(Number(terrainSculpted.output!.changedVertices)>0);
 await page.getByRole('heading',{name:'Sculpt a surface path',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-terrain-sculpt.png')});
 const sculptedField=await readResourceFact('object.field','Sculptable surface',{target:terrainTarget});
 assert.ok(Math.abs(Number(sculptedField.minimum)-.04)<.000001);
 const sculptedWater=await readResourceFact('object.container','Liquid container and contents',{target:terrainTarget}) as unknown as BookContainer;
 assert.equal(sculptedWater.definition.amountMl,50000);assert.ok(Number(sculptedWater.definition.capacityMl)>terrainCapacity);
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 const sculptRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();await page.waitForFunction(revision=>window.nativeBookEvidence!().state!.sceneRevision>revision,sculptRevision);
 const restoredField=await readResourceFact('object.field','Sculptable surface',{target:terrainTarget});assert.ok(Math.abs(Number(restoredField.minimum)-.08)<.000001);
 const restoredWater=await readResourceFact('object.container','Liquid container and contents',{target:terrainTarget}) as unknown as BookContainer;
 assert.equal(restoredWater.definition.amountMl,50000);assert.equal(restoredWater.definition.capacityMl,terrainCapacity);
 await writeFile(join(directory,'book-native-terrain-sculpt.json'),JSON.stringify({boundary:'Visible original-book terrain selection, current revision, sculpt inputs and confirmed result; native shape/quantity reads and ordinary Undo. No provider or headset acceptance.',terrainSculpted,sculptedField,sculptedWater,restoredField,restoredWater},null,2));
 // Pack, edit and return material through the same visible shared forms.
 await openNamedAction('Pack surface material into a ball','object.material.pack');
 await chooseObject('Action inputs source target',terrainTarget);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 for(const axis of ['x','z'])await page.getByLabel('Action inputs source centre '+axis,{exact:true}).fill('0');
 await page.getByLabel('Action inputs source radius',{exact:true}).fill('.2');
 await page.getByLabel('Action inputs amountLitres',{exact:true}).fill('.5');await page.getByLabel('Action inputs name',{exact:true}).fill('Book packed snowball');
 await page.getByLabel('Action inputs mass',{exact:true}).fill('.15');
 for(const [key,value] of Object.entries({x:4,y:3,z:4}))await page.getByLabel('Action inputs position '+key,{exact:true}).fill(String(value));
 const materialPacked=await runNamedAction('object.material.pack'),packedId=materialPacked.output!.objectId as string,packedAmount=Number(materialPacked.output!.amountLitres);
 assert.equal(typeof packedId,'string');assert.ok(packedAmount>0&&packedAmount<=.5);
 type BookMaterial={configured:boolean;definition:{amountLitres:number;capacityLitres:number}};
 const packedField=await readResourceFact('object.field','Sculptable surface',{target:terrainTarget});
 const packedMaterial=await readResourceFact('object.material','Measured carried material',{target:packedId}) as unknown as BookMaterial;
 assert.ok(Math.abs(Number(restoredField.volumeLitres)-Number(packedField.volumeLitres)-packedAmount)<1e-8);
 assert.equal(packedMaterial.definition.amountLitres,packedAmount);assert.equal(packedMaterial.definition.capacityLitres,packedAmount);
 await openNamedAction('Configure a measured material store','object.material.edit');await chooseObject('Action inputs target',packedId);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs definition capacityLitres',{exact:true}).fill('1');
 const materialConfigured=await runNamedAction('object.material.edit');
 const configuredMaterial=await readResourceFact('object.material','Measured carried material',{target:packedId}) as unknown as BookMaterial;
 assert.equal(configuredMaterial.definition.capacityLitres,1);assert.equal(configuredMaterial.definition.amountLitres,packedAmount);
 await openNamedAction('Transfer carried or surface material','object.material.transfer');
 await page.getByLabel('Action inputs source variant',{exact:true}).selectOption({label:'Carried material store'});
 await page.getByLabel('Action inputs destination variant',{exact:true}).selectOption({label:'Surface footprint'});
 await chooseObject('Action inputs source target',packedId);await chooseObject('Action inputs destination target',terrainTarget);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 for(const axis of ['x','z'])await page.getByLabel('Action inputs destination centre '+axis,{exact:true}).fill('0');
 await page.getByLabel('Action inputs destination radius',{exact:true}).fill('.2');await page.getByLabel('Action inputs amountLitres',{exact:true}).fill('.1');
 const materialReturned=await runNamedAction('object.material.transfer');
 await page.getByRole('heading',{name:'Transfer carried or surface material',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-material-completion.png')});
 const returnedMaterial=await readResourceFact('object.material','Measured carried material',{target:packedId}) as unknown as BookMaterial;
 const returnedField=await readResourceFact('object.field','Sculptable surface',{target:terrainTarget});
 const materialWater=await readResourceFact('object.container','Liquid container and contents',{target:terrainTarget}) as unknown as BookContainer;
 assert.ok(Math.abs(packedAmount-returnedMaterial.definition.amountLitres-Number(materialReturned.output!.removedLitres))<1e-8);
 assert.ok(Math.abs(Number(returnedField.volumeLitres)-Number(packedField.volumeLitres)-Number(materialReturned.output!.addedLitres))<1e-8);
 assert.equal(materialWater.definition.amountMl,50000);
 const undoMaterialStep=async()=>{await book.workshop();const revision=await page!.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
  await page!.getByRole('button',{name:'Undo',exact:true}).first().click();await page!.waitForFunction(previous=>window.nativeBookEvidence!().state!.sceneRevision>previous,revision);};
 await undoMaterialStep();
 const materialUndoTransfer=await readResourceFact('object.material','Measured carried material',{target:packedId}) as unknown as BookMaterial;
 const fieldUndoTransfer=await readResourceFact('object.field','Sculptable surface',{target:terrainTarget});
 assert.equal(materialUndoTransfer.definition.amountLitres,packedAmount);assert.equal(fieldUndoTransfer.volumeLitres,packedField.volumeLitres);
 await undoMaterialStep();
 const materialUndoConfigure=await readResourceFact('object.material','Measured carried material',{target:packedId}) as unknown as BookMaterial;assert.equal(materialUndoConfigure.definition.capacityLitres,packedAmount);
 await book.workshop();await undoRemoved(packedId);
 const fieldUndoPack=await readResourceFact('object.field','Sculptable surface',{target:terrainTarget});
 const waterUndoPack=await readResourceFact('object.container','Liquid container and contents',{target:terrainTarget}) as unknown as BookContainer;
 assert.equal(fieldUndoPack.volumeLitres,restoredField.volumeLitres);assert.equal(waterUndoPack.definition.amountMl,50000);assert.equal(waterUndoPack.definition.capacityMl,terrainCapacity);
 await writeFile(join(directory,'book-native-material-completion.json'),JSON.stringify({boundary:'Visible book packing, store configuration and return transfer await real native completion; exact identity, geometric and carried balances, unchanged water and three ordinary Undo steps. No provider or headset acceptance.',materialPacked,packedField,packedMaterial,materialConfigured,configuredMaterial,materialReturned,returnedMaterial,returnedField,materialWater,materialUndoTransfer,fieldUndoTransfer,materialUndoConfigure,fieldUndoPack,waterUndoPack},null,2));
 // Cleanup uses the user's normal Undo path, leaving later scenarios unchanged.
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 const terrainRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();
 await page.waitForFunction(revision=>window.nativeBookEvidence!().state!.sceneRevision>revision,terrainRevision);
 await undoRemoved(terrainTarget);
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await openNamedAction('Blend a visual layer','visibility.layer.present');
 await page.getByRole('button',{name:'Load saved visual layer',exact:true}).click();
 await page.getByLabel('Choose visual layer',{exact:true}).selectOption(JSON.stringify([visualLayer.output!.id,null]));
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true,'A named selection cannot reuse an unrelated view guard');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs opacity',{exact:true}).fill('0.5');await page.getByLabel('Action inputs seconds',{exact:true}).fill('0.2');
 const layerPresentation=await runNamedAction('visibility.layer.present');
 assert.equal(layerPresentation.call.arguments.id,visualLayer.output!.id);assert.equal(layerPresentation.output!.opacity,.5);
 await writeFile(join(directory,'book-native-layer-presentation.json'),JSON.stringify({boundary:'Named layer lookup, current guard read and transient view request through the original book and native Unity. No headset or real-provider proof.',layerPresentation},null,2));
 await page.getByRole('region',{name:'Visual layer choice',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-layer-presentation.png')});
 await openNamedAction('Configure water traversal','object.water.traversal.configure');
 await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const retainedDepth=await page.getByLabel('Action inputs maxDepthMetres',{exact:true}).inputValue();
 await page.getByLabel('Action inputs mode',{exact:true}).selectOption('avoid');
 const waterTraversal=await runNamedAction('object.water.traversal.configure');
 assert.equal(waterTraversal.call.arguments.mode,'avoid');assert.equal(waterTraversal.call.arguments.maxDepthMetres,Number(retainedDepth));
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs mode',{exact:true}).inputValue(),'avoid');
 await page.getByRole('heading',{name:'Configure water traversal',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-water-traversal.png')});
 await writeFile(join(directory,'book-native-water-traversal.json'),JSON.stringify({boundary:'Actual original-book generated form, native save receipt and current-value reload. No provider, physical movement or headset claim.',waterTraversal},null,2));
 // The original book reads shared contact state and event documentation; inspection never enables physics.
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByLabel('Search facts',{exact:true}).fill('input.medium.contactState');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Tracked hand or controller water contact.*input.medium.contactState/}).click();
 await page.getByLabel('Fact inputs side',{exact:true}).selectOption('left');await page.getByRole('button',{name:'Read fact',exact:true}).click();
 await page.waitForFunction(()=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability==='input.medium.contactState'&&c.available===true;});
 const contactState=await page.evaluate(()=>{const c=window.nativeBookEvidence!().state!.catalog;if(c?.operation!=='inspect'||c.category!=='facts')throw new Error('Contact fact missing');return c.value as {known:boolean;immersed:boolean};});assert.equal(contactState.known,false);assert.equal(contactState.immersed,false);
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-liquid-contact.png')});
 await writeFile(join(directory,'book-native-liquid-contact.json'),JSON.stringify({boundary:'Original book fact form through native transport. Untracked desktop input stays unknown. No contact, provider or headset claim.',contactState},null,2));
 const liquidPersistence=await readResourceFact('world.liquidPersistence','Liquid save status');
 assert.equal(liquidPersistence.phase,'idle');assert.equal(liquidPersistence.pending,0);assert.equal(liquidPersistence.capacity,2);
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-liquid-persistence.png')});
 await writeFile(join(directory,'book-native-liquid-persistence.json'),JSON.stringify({boundary:'Original book generated fact controls read the shared native durability state. No provider or headset claim.',liquidPersistence},null,2));
 // Shared area authoring uses the same generated controls as every other catalog action.
 await openNamedAction('Save world area','world.region.save');
 await page.getByLabel('Action inputs name',{exact:true}).fill('Café garden');
 assert.equal(await page.getByLabel('Action inputs loading',{exact:true}).inputValue(),'independent');
 await page.getByLabel('Action inputs loading',{exact:true}).selectOption('together');
 const region=await runNamedAction('world.region.save');assert.equal(region.call.arguments.loading,'together');
 await page.getByRole('heading',{name:'Save world area',exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:join(directory,'book-native-area-loading.png')});
 const regionDefinition=await readResourceFact('world.region','World area',{id:String(region.output!.id)});
 assert.equal(regionDefinition.loading,'together');
 await openNamedAction('Choose creation area','object.region.assign');
 await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved area',exact:true}).click();
 await page.getByLabel('Choose area',{exact:true}).selectOption(JSON.stringify([region.output!.id,region.output!.revision]));
 const regionBinding=await runNamedAction('object.region.assign');assert.equal(regionBinding.call.arguments.regionId,region.output!.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs regionId',{exact:true}).inputValue(),region.output!.id);
 await page.getByRole('region',{name:'Area choice',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-region.png')});
 await writeFile(join(directory,'book-native-region.json'),JSON.stringify({boundary:'Actual original-book generated controls, named area choice and native saved membership readback. No regional streaming, provider or headset claim.',region,regionDefinition,regionBinding},null,2));
 await openNamedAction('Save environment profile','environment.profile.save');
 await page.getByLabel('Action inputs name',{exact:true}).fill('Virtual terrain only');
 await page.getByLabel('Action inputs realCollisions',{exact:true}).selectOption('false');
 const collisionProfile=await runNamedAction('environment.profile.save');
 await openNamedAction('Choose object environment','object.environment.assign');
 await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved collision profile',exact:true}).click();
 await page.getByLabel('Choose collision profile',{exact:true}).selectOption(JSON.stringify([collisionProfile.output!.id,collisionProfile.output!.revision]));
 const beforeCollisionChoice=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 assert.equal(beforeCollisionChoice.capability,'environment.profile.save');
 const collisionBinding=await runNamedAction('object.environment.assign');
 assert.equal(collisionBinding.call.arguments.profileId,collisionProfile.output!.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs profileId',{exact:true}).inputValue(),collisionProfile.output!.id);
 assert.equal(Number(await page.getByLabel('Action inputs profileRevision',{exact:true}).inputValue()),collisionProfile.output!.revision);
 await page.getByRole('region',{name:'Collision profile choice',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-environment.png')});
 const savedCollisionSettings=await readResourceFact('object.environment.settings','Saved object environment',{target:ball.id});
 assert.equal(savedCollisionSettings.profileId,collisionProfile.output!.id);assert.equal(savedCollisionSettings.profileRevision,collisionProfile.output!.revision);
 assert.equal(savedCollisionSettings.realCollisions,false);assert.equal(savedCollisionSettings.state,undefined);
 const savedCollisionMembers=await readResourceFact('resource.members','Saved resource members',{kind:'environment',id:String(collisionProfile.output!.id),cursor:''});
 assert.equal(savedCollisionMembers.id,collisionProfile.output!.id);assert.equal(savedCollisionMembers.revision,collisionProfile.output!.revision);
 assert.deepEqual(savedCollisionMembers.members,[ball.id]);assert.equal(savedCollisionMembers.total,1);assert.equal(savedCollisionMembers.offset,0);assert.equal(savedCollisionMembers.next,'');
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-resource-members.png')});
 await writeFile(join(directory,'book-native-environment.json'),JSON.stringify({boundary:'Original book generated profile and assignment forms, completed native receipts and exact saved choice reloaded. No headset, alignment, provider or physical collision claim.',collisionProfile,collisionBinding,savedCollisionSettings,savedCollisionMembers},null,2));
 await openNamedAction('Create or edit a reusable sound','audio.source.edit');
 await page.getByLabel('Action inputs definition name',{exact:true}).fill('Friendly ball beep');
 const sound=await runNamedAction('audio.source.edit');
 await openNamedAction('Attach a sound to an object','object.audioEmitter.edit');
 await chooseObject('Action inputs target',ball.id);
 await page.getByLabel('Action inputs definition joint',{exact:true}).selectOption('');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved sound',exact:true}).click();
 await page.getByLabel('Choose sound',{exact:true}).selectOption(JSON.stringify([sound.output!.id,null]));
 const soundBinding=await runNamedAction('object.audioEmitter.edit');assert.equal((soundBinding.call.arguments.definition as {source:string}).source,sound.output!.id);
 await writeFile(join(directory,'book-native-resource-choices.json'),JSON.stringify({boundary:'Named resource choices through real book forms and desktop Unity; sound assignment does not play audio. No provider or headset proof.',visualLayer,visualBinding,appearance,boundAppearance,collisionProfile,collisionBinding,sound,soundBinding},null,2));
 await page.getByRole('region',{name:'Sound choice',exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:join(directory,'book-native-resource-choices.png')});
 // Optional explicit synthetic WAV: ordinary book forms discover, bind and play
 // exact saved bytes. This does not impersonate the Android file chooser.

 const viewerPose=await readResourceFact('world.viewpoint','Your world location') as unknown as {located:boolean;position:{x:number;y:number;z:number};yaw:number};
 assert.equal(viewerPose.located,true);const viewYaw=viewerPose.yaw*Math.PI/180;
 const viewPosition={x:viewerPose.position.x+Math.sin(viewYaw)*3,y:viewerPose.position.y+.8,z:viewerPose.position.z+Math.cos(viewYaw)*3};
 const moveObservedBall=async(position:Record<string,string|number>)=>{
  await openNamedAction('Move object','object.position.set');await chooseObject('Action inputs target',ball.id);
  for(const axis of ['x','y','z'])await page!.getByLabel('Action inputs '+axis,{exact:true}).fill(String(position[axis]));
  return runNamedAction('object.position.set');
 };
 await moveObservedBall(viewPosition);
 const areaRetention=await readResourceFact('world.region.retention','Area runtime dependencies',{id:String(region.output!.id)});
 assert.equal(areaRetention.memberCount,1);assert.equal(areaRetention.residentCount,1);assert.equal(areaRetention.unloadingSupported,true);
 await page.screenshot({path:join(directory,'book-native-region-retention.png')});
 await writeFile(join(directory,'book-native-region-retention.json'),JSON.stringify({boundary:'Original book fact form and actual native area dependency report; no unloading or provider claim.',areaRetention},null,2));

 const originalPosition=await readResourceFact('object.position','Object room position',{target:ball.id});
 const viewerDemand=await readResourceFact('world.region.retention','Area runtime dependencies',{id:String(region.output!.id)});
 const demandReasons=viewerDemand.reasons as unknown as string[];
 assert.ok(demandReasons.includes('approachingView'),JSON.stringify(viewerDemand));assert.ok(demandReasons.includes('recentObservation'),JSON.stringify(viewerDemand));
 await page.screenshot({path:join(directory,'book-native-viewer-demand.png')});
 await writeFile(join(directory,'book-native-viewer-demand.json'),JSON.stringify({boundary:'Original book placement form and dependency inspector against actual Unity frame-owned viewer demand. No headset or automatic-streaming proof.',viewerPose,viewPosition,viewerDemand},null,2));
 const behindView={x:viewerPose.position.x-Math.sin(viewYaw)*6,y:viewerPose.position.y+.8,z:viewerPose.position.z-Math.cos(viewYaw)*6};
 await moveObservedBall(behindView);
 let retiredArea=await readResourceFact('world.region.retention','Area runtime dependencies',{id:String(region.output!.id)});const retirementDeadline=Date.now()+25000;
 while(retiredArea.retirementState!=='dormant'&&Date.now()<retirementDeadline){await new Promise(r=>setTimeout(r,250));retiredArea=await readResourceFact('world.region.retention','Area runtime dependencies',{id:String(region.output!.id)});}
 assert.equal(retiredArea.retirementState,'dormant',JSON.stringify(retiredArea));assert.equal(retiredArea.residentCount,0);
 await page.screenshot({path:join(directory,'book-native-area-retired.png')});
 const restoredAreaAction=await moveObservedBall(viewPosition);
 const restoredArea=await readResourceFact('world.region.retention','Area runtime dependencies',{id:String(region.output!.id)});
 assert.equal(restoredArea.residentCount,1);assert.ok((restoredArea.reasons as unknown as string[]).includes('observation'));
 await writeFile(join(directory,'book-native-area-retirement.json'),JSON.stringify({boundary:'Original book move and fact forms against real Unity automatically retire an idle offscreen area and reacquire its object through the ordinary shared action. No larger-world physics or headset acceptance.',behindView,retiredArea,restoredAreaAction,restoredArea},null,2));
 const lampPosition={x:viewerPose.position.x+Math.cos(viewYaw)*6-Math.sin(viewYaw)*3,y:viewerPose.position.y,z:viewerPose.position.z-Math.sin(viewYaw)*6-Math.cos(viewYaw)*3};
 await moveObservedBall(lampPosition);
 await openNamedAction('Edit an object lamp','object.light.edit');await chooseObject('Action inputs target',ball.id);
 await page.getByLabel('Action inputs settings range',{exact:true}).fill('8');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();await runNamedAction('object.light.edit');
 const lampDemand=await readResourceFact('world.region.retention','Area runtime dependencies',{id:String(region.output!.id)});
 assert.ok((lampDemand.reasons as unknown as string[]).includes('lighting'),JSON.stringify(lampDemand));
 assert.ok(!(lampDemand.reasons as unknown as string[]).includes('observation'),'Lamp object is behind the actual native camera');
 await page.screenshot({path:join(directory,'book-native-light-demand.png')});
 await openNamedAction('Control a lamp','object.light.control');await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs enabled',{exact:true}).selectOption('false');await runNamedAction('object.light.control');
 const lampReleased=await readResourceFact('world.region.retention','Area runtime dependencies',{id:String(region.output!.id)});
 assert.ok(!(lampReleased.reasons as unknown as string[]).includes('lighting'),JSON.stringify(lampReleased));
 await openNamedAction('Edit an object lamp','object.light.edit');await page.getByLabel('Variant',{exact:true}).selectOption('1');await chooseObject('Action inputs target',ball.id);await page.getByLabel('Action inputs light',{exact:true}).fill('Lamp');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();await runNamedAction('object.light.edit');
 await writeFile(join(directory,'book-native-light-demand.json'),JSON.stringify({boundary:'Original book forms and native off-camera illumination dependency; turning a lamp off releases its lighting reason. Conservative demand, no provider/headset or rendered-quality claim.',lampPosition,lampDemand,lampReleased},null,2));
 await moveObservedBall(originalPosition);

 const bookPresence=await readResourceFact('object.presence','Object saved and native presence',{target:'book'});
 assert.equal(bookPresence.target,'book');assert.equal(bookPresence.state,'active');assert.equal(bookPresence.saved,true);assert.equal(bookPresence.nativeInstance,true);assert.equal(bookPresence.active,true);
 await page.screenshot({path:join(directory,'book-native-object-presence.png')});
 await writeFile(join(directory,'book-native-object-presence.json'),JSON.stringify({boundary:'Original book fact form and actual native book presence; no regional loading, provider or headset claim.',bookPresence},null,2));
 const bookSpatial=await readResourceFact('object.spatialBounds','Object spatial bounds',{target:'book'});
 assert.equal(bookSpatial.target,'book');assert.equal(bookSpatial.source,'native');assert.equal(bookSpatial.coordinates,'room');
 const bookVisual=bookSpatial.visual as unknown as {known:boolean;hasBounds:boolean};assert.equal(bookVisual.known,true);assert.equal(bookVisual.hasBounds,true);
 await page.screenshot({path:join(directory,'book-native-spatial-bounds.png')});
 await writeFile(join(directory,'book-native-spatial-bounds.json'),JSON.stringify({boundary:'Original book fact form and native spatial envelopes; no streaming, provider or headset claim.',bookSpatial},null,2));
 let importedSound:unknown=null;
 let soundFixture:{hash:string;seconds:number;name:string}|null=null;
 try{soundFixture=JSON.parse(await readFile(join(directory,'sound-fixture.json'),'utf8'));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
 if(soundFixture){
  await openNamedAction('Import a sound together','audio.import');await page.getByLabel('Variant',{exact:true}).selectOption('3');
  const refreshed=await runNamedAction('audio.import');
  await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByLabel('Search facts',{exact:true}).fill('audio.library');await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:/Imported sound files.*audio\.library/}).click();await page.getByRole('button',{name:'Read fact',exact:true}).click();
  await page.waitForFunction(()=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability==='audio.library'&&c.available===true;});
  const library=await page.evaluate(()=>{const c=window.nativeBookEvidence!().state!.catalog;if(c?.operation!=='inspect'||c.category!=='facts')throw new Error('Sound library missing');return c.value as {ready:boolean;entries:{id:string;name:string;seconds:number}[]};});
  assert.equal(library.ready,true);const file=library.entries.find(e=>e.id===soundFixture!.hash);assert.ok(file);assert.equal(file.name,soundFixture.name);assert.equal(file.seconds,soundFixture.seconds);
  await openNamedAction('Create or edit a reusable sound','audio.source.edit');await page.getByLabel('Action inputs definition variant',{exact:true}).selectOption('1');
  await page.getByLabel('Action inputs definition name',{exact:true}).fill('Imported book bell');await page.getByLabel('Action inputs definition clip assetHash',{exact:true}).fill(file.id);await page.getByLabel('Action inputs definition clip seconds',{exact:true}).fill(String(file.seconds));
  assert.equal(await page.getByLabel('Action inputs definition tone wave',{exact:true}).count(),0);
  const clip=await runNamedAction('audio.source.edit');assert.equal((clip.call.arguments.definition as {kind:string}).kind,'clip');
  await openNamedAction('Attach a sound to an object','object.audioEmitter.edit');await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs emitter',{exact:true}).fill('bell');
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Load saved sound',exact:true}).click();await page.getByLabel('Choose sound',{exact:true}).selectOption(JSON.stringify([clip.output!.id,null]));
  await page.getByLabel('Action inputs definition joint',{exact:true}).selectOption('');await page.getByLabel('Action inputs definition gain',{exact:true}).fill('0');
  const binding=await runNamedAction('object.audioEmitter.edit');
  await page.getByRole('region',{name:'Sound choice',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-imported-sound.png')});
  await openNamedAction("Play an object's sound",'audio.play');await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs emitter',{exact:true}).fill('bell');
  const played=await runNamedAction('audio.play');assert.equal(played.output?.source,clip.output!.id);assert.ok(Number(played.output?.seconds)>=file.seconds-.0001);
  await openNamedAction('Start independent room sound','audio.start');await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs emitter',{exact:true}).fill('bell');
  await page.getByLabel('Action inputs loop',{exact:true}).selectOption('true');const started=await runNamedAction('audio.start');
  const instance=(started.output!.identity as {instance:string}).instance;assert.match(instance,/^[a-f0-9]{32}$/);
  const observationRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
  const reservation=await readResourceFact('runtime.audioReservation','Room sound reservation',{index:'0'});
  const budget=await readResourceFact('runtime.audioBudget','Shared room audio budget');
  const owner=await readResourceFact('runtime.audioOwner','Room sound owner',{reservationId:String(reservation.reservationId),index:'0'});
  assert.equal(reservation.sourceId,clip.output!.id);assert.equal(reservation.assetHash,file.id);assert.equal(reservation.kind,'clip');assert.equal(reservation.state,'ready');
  assert.equal(budget.sources,1);assert.equal(budget.owners,1);assert.equal(budget.readyPcmBytes,48000);assert.equal(reservation.readyPcmBytes,budget.readyPcmBytes);
  assert.equal(owner.instanceId,instance);assert.equal(owner.target,'book');assert.equal(owner.role,'audio');assert.equal(owner.sourceId,clip.output!.id);
  assert.match(String(owner.worldId),/^[a-f0-9]{32}$/);assert.match(String(owner.regionId),/^[a-f0-9]{32}$/);
  assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),observationRevision,'Audio observations must not change the room');
  await page.getByRole('heading',{name:'Room sound owner',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-audio-residency.png')});
  const mixBefore=await readResourceFact('audio.mix','World sound levels');
  await openNamedAction('Adjust world sound levels','audio.mix.control');
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).inputValue(),mixBefore.stateId);
  for(const [role,value] of Object.entries({master:'.6',effects:'.4',media:'.5',ambience:'.1'}))await page.getByLabel('Action inputs '+role,{exact:true}).fill(value);
  const mixSet=await runNamedAction('audio.mix.control');
  await page.screenshot({path:join(directory,'book-native-audio-mix.png')});
  const mixAfter=await readResourceFact('audio.mix','World sound levels');
  for(const [role,value] of Object.entries({master:.6,effects:.4,media:.5,ambience:.1}))assert.ok(Math.abs(Number(mixAfter[role])-value)<1e-6);
  assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),observationRevision,'Mix controls must not save the room');
  const voiceAfterMix=await readResourceFact('audio.instance','Sound playback instance',{target:'book',instance});
  assert.equal(voiceAfterMix.revision,started.output!.revision,'Mix controls leave the independent voice revision unchanged');
  await openNamedAction('Adjust world sound levels','audio.mix.control');await page.getByLabel('Variant',{exact:true}).selectOption('1');
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  const mixReset=await runNamedAction('audio.mix.control');
  for(const role of ['master','effects','media','ambience'])assert.equal(mixReset.output![role],1);
  await openNamedAction('Control a room sound','audio.control');await page.getByLabel('Variant',{exact:true}).selectOption('2');
  await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs instance',{exact:true}).fill(instance);
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  const stopped=await runNamedAction('audio.control');assert.equal((stopped.output!.playback as {phase:string}).phase,'stopped');
  const released=await readResourceFact('runtime.audioBudget','Shared room audio budget');assert.equal(released.sources,0);assert.equal(released.owners,0);assert.equal(released.readyPcmBytes,0);assert.equal(released.reservedPcmBytes,0);
  importedSound={boundary:'Synthetic WAV, original book forms, native decoder and PCM consumption, read-only resource ownership, exact-instance stop and final release; muted renderer, no Android picker or physical audibility claim.',refreshed,library,clip,binding,played,started,budget,reservation,owner,mixBefore,mixSet,mixAfter,voiceAfterMix,mixReset,stopped,released};
  await writeFile(join(directory,'book-native-imported-sound.json'),JSON.stringify(importedSound,null,2));
 }
 // An explicit offline transport fixture exercises live audio through the same user forms.
 let liveSoundFixture:{endpoint:string}|null=null;
 try{liveSoundFixture=JSON.parse(await readFile(join(directory,'live-sound-fixture.json'),'utf8'));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
 if(liveSoundFixture){
  await openNamedAction('Create or edit a reusable sound','audio.source.edit');await page.getByLabel('Action inputs definition variant',{exact:true}).selectOption('2');
  await page.getByLabel('Action inputs definition name',{exact:true}).fill('Book radio');await page.getByLabel('Action inputs definition stream endpoint',{exact:true}).fill(liveSoundFixture.endpoint);
  const saved=await runNamedAction('audio.source.edit');assert.equal((saved.call.arguments.definition as {kind:string}).kind,'stream');
  const source=await readResourceFact('audio.source.stream','Live sound endpoint',{id:String(saved.output!.id),offset:'0'});assert.equal(source.endpoint,liveSoundFixture.endpoint);assert.equal(source.next,-1);
  const silent=await readResourceFact('runtime.audioBudget','Shared room audio budget');assert.equal(silent.sources,0,'Saving/reading a source cannot connect');
  await openNamedAction('Attach a sound to an object','object.audioEmitter.edit');await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs emitter',{exact:true}).fill('radio');
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Load saved sound',exact:true}).click();await page.getByLabel('Choose sound',{exact:true}).selectOption(JSON.stringify([saved.output!.id,null]));
  await page.getByLabel('Action inputs definition joint',{exact:true}).selectOption('');await page.getByLabel('Action inputs definition gain',{exact:true}).fill('0');const binding=await runNamedAction('object.audioEmitter.edit');
  await openNamedAction('Start independent room sound','audio.start');await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs emitter',{exact:true}).fill('radio');await page.getByLabel('Action inputs loop',{exact:true}).selectOption('false');
  const started=await runNamedAction('audio.start');const instance=String((started.output!.identity as {instance:string}).instance);assert.ok(Number((started.output!.playback as {seconds:number}).seconds)>0);
  const control=async(operation:'pause'|'resume'|'stop',variant:string)=>{
   await openNamedAction('Control a room sound','audio.control');await page!.getByLabel('Variant',{exact:true}).selectOption(variant);await chooseObject('Action inputs target','book');await page!.getByLabel('Action inputs instance',{exact:true}).fill(instance);
   await page!.getByRole('button',{name:'Load current values',exact:true}).click();await page!.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
   const result=await runNamedAction('audio.control');assert.equal(result.call.arguments.operation,operation);return result;
  };
  const paused=await control('pause','0');assert.equal((paused.output!.playback as {phase:string}).phase,'paused');await page.waitForTimeout(2500);
  const resumed=await control('resume','1');await page.waitForTimeout(300);
  const live=await readResourceFact('audio.live','Live sound buffering',{target:'book',instance});assert.ok(Number(live.skippedSeconds)>1.5);assert.ok(Number(live.receivedSeconds)>Number(live.skippedSeconds));assert.ok(Number(live.bufferedSeconds)<=2);
  await page.getByRole('heading',{name:'Live sound buffering',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-live-sound.png')});
  const stopped=await control('stop','2');assert.equal((stopped.output!.playback as {phase:string}).phase,'stopped');
  const terminal=await readResourceFact('audio.live','Live sound buffering',{target:'book',instance});assert.equal(terminal.sourcePhase,'closed');
  const released=await readResourceFact('runtime.audioBudget','Shared room audio budget');assert.equal(released.sources,0);
  await writeFile(join(directory,'book-native-live-sound.json'),JSON.stringify({boundary:'Real book forms and native live MP3 decoder/output with explicit offline HTTP fixture. No sockets, paid provider or headset quality proof.',saved,source,silent,binding,started,paused,resumed,live,stopped,terminal,released},null,2));
 }
 // Optional explicit image fixture follows the original generated forms.
 let imageFixture:{hash:string;name:string;width:number;height:number}|null=null;
 try{imageFixture=JSON.parse(await readFile(join(directory,'image-fixture.json'),'utf8'));}catch(error){if((error as NodeJS.ErrnoException).code!=='ENOENT')throw error;}
 if(imageFixture){
  await openNamedAction('Import an image together','image.import');await page.getByLabel('Variant',{exact:true}).selectOption('3');const refreshed=await runNamedAction('image.import');
  const library=refreshed.output!.library as {ready:boolean;entries:{id:string;name:string}[]};assert.equal(library.ready,true);assert.equal(library.entries[0].id,imageFixture.hash);
  await openNamedAction('Save shared appearance','appearance.save');await page.getByLabel('Action inputs name',{exact:true}).fill('Imported blue tiles');await page.getByLabel('Action inputs style patternMode',{exact:true}).selectOption('image');await page.getByLabel('Action inputs style imageHash',{exact:true}).fill(imageFixture.hash);
  await page.getByLabel('Action inputs style tint',{exact:true}).fill('#FFFFFF');await page.getByLabel('Action inputs style renderMode',{exact:true}).selectOption('blend');await page.getByLabel('Action inputs style opacity',{exact:true}).fill('0.5');
  const appearance=await runNamedAction('appearance.save');
  await openNamedAction('Choose object appearance','object.appearance.bind');await chooseObject('Action inputs target','book');await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  await page.getByRole('button',{name:'Load saved appearance',exact:true}).click();await page.getByLabel('Choose appearance',{exact:true}).selectOption(JSON.stringify([appearance.output!.id,appearance.output!.revision]));
  const bound=await runNamedAction('object.appearance.bind');assert.equal(bound.call.arguments.binding && (bound.call.arguments.binding as {appearanceId:string}).appearanceId,appearance.output!.id);
  await page.getByRole('region',{name:'Appearance choice',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-imported-image.png')});
  await writeFile(join(directory,'book-native-imported-image.json'),JSON.stringify({boundary:'Synthetic PNG, original book controls and native shared appearance. Android picker and physical Quest visual acceptance are separate.',fixture:imageFixture,refreshed,appearance,bound},null,2));
 }
 // Chat bytes enter through the original store and the shared client; no native file is preseeded.
 const chatData=await page.evaluate(()=>window.nativeBookChatImage!());
 const chatHash=createHash('sha256').update(Buffer.from(chatData.split(',')[1],'base64')).digest('hex');
 await openNamedAction('Import an image together','image.import');
 await page.getByLabel('Variant',{exact:true}).selectOption('4');
 await page.getByRole('button',{name:'Load saved chat image',exact:true}).click();
 await page.getByLabel('Choose chat image',{exact:true}).selectOption(JSON.stringify([chatHash,null]));
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.waitForFunction(()=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability==='image.chat.item'&&c.available===true;});
 const selectedChat=await runNamedAction('image.import');
 assert.match(String(selectedChat.output?.requestId),/^[a-f0-9]{32}$/);
 // Transfer and native decode happen asynchronously; inspect the real preview before accepting.
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');
 await page.getByLabel('Search facts',{exact:true}).fill('image.import.selection');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Image import status.*image\.import\.selection/}).click();
 const previewDeadline=Date.now()+10000;let previewReady=false;
 while(Date.now()<previewDeadline){
  const fact=await page.evaluate(()=>window.nativeBookEvidence!().state!.catalog);
  if(fact?.operation==='inspect'&&fact.category==='facts'&&fact.capability==='image.import.selection'&&fact.available&&fact.value&&typeof fact.value==='object'&&'phase' in fact.value&&fact.value.phase==='preview'){previewReady=true;break;}
  await page.getByRole('button',{name:'Refresh fact',exact:true}).click();
  await page.waitForTimeout(100);
 }
 assert.equal(previewReady,true,'Native chat-image preview did not become ready');
 await openNamedAction('Import an image together','image.import');await page.getByLabel('Variant',{exact:true}).selectOption('1');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const acceptedChat=await runNamedAction('image.import');
 assert.equal(acceptedChat.output?.imageHash,chatHash);assert.match(String(acceptedChat.output?.appearanceId),/^[a-f0-9]{32}$/);
 await writeFile(join(directory,'book-chat-image.json'),JSON.stringify({boundary:'Synthetic local PNG in the original chat store, actual Chrome controls/transport and native preview/acceptance. No provider or headset capture.',chatHash,selectedChat,acceptedChat},null,2));
 await page.screenshot({path:join(directory,'book-chat-image.png')});
 // A finished chat audio attachment follows the same user-visible import owner.
 for(const soundFormat of ['wav','mp3','vorbis'] as const){
 const chatSound=chatSoundFixture(soundFormat);await page.evaluate(data=>window.nativeBookChatAudio!(data),chatSound.dataUrl);
 await openNamedAction('Import a sound together','audio.import');await page.getByLabel('Variant',{exact:true}).selectOption('4');
 await page.getByRole('button',{name:'Load saved chat sound',exact:true}).click();await page.getByLabel('Choose chat sound',{exact:true}).selectOption(JSON.stringify([chatSound.hash,null]));
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const selectedSound=await runNamedAction('audio.import');
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByLabel('Search facts',{exact:true}).fill('audio.import.selection');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Sound import status.*audio\.import\.selection/}).click();
 const soundDeadline=Date.now()+45000;let soundPreview:{assetHash:string;seconds:number}|undefined;
 while(Date.now()<soundDeadline){const fact=await page.evaluate(()=>window.nativeBookEvidence!().state!.catalog);if(fact?.operation==='inspect'&&fact.category==='facts'&&fact.capability==='audio.import.selection'&&fact.available&&fact.value&&typeof fact.value==='object'&&'phase' in fact.value&&fact.value.phase==='preview'){soundPreview=(fact.value as {preview:{assetHash:string;seconds:number}}).preview;break;}await page.getByRole('button',{name:'Refresh fact',exact:true}).click();await page.waitForTimeout(100);}
 assert.ok(soundPreview,'Native chat sound preview must finish before acceptance');assert.match(soundPreview.assetHash,/^[a-f0-9]{64}$/);if(soundFormat==='wav')assert.equal(soundPreview.assetHash,chatSound.hash);else assert.notEqual(soundPreview.assetHash,chatSound.hash);
 await openNamedAction('Import a sound together','audio.import');await page.getByLabel('Variant',{exact:true}).selectOption('1');await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const acceptedSound=await runNamedAction('audio.import');assert.equal(acceptedSound.output!.assetHash,soundPreview.assetHash);
 await openNamedAction('Attach a sound to an object','object.audioEmitter.edit');await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs emitter',{exact:true}).fill('bell');await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved sound',exact:true}).click();await page.getByLabel('Choose sound',{exact:true}).selectOption(JSON.stringify([acceptedSound.output!.sourceId,null]));await page.getByLabel('Action inputs definition joint',{exact:true}).selectOption('');await page.getByLabel('Action inputs definition gain',{exact:true}).fill('0');
 const soundAttached=await runNamedAction('object.audioEmitter.edit');
 await openNamedAction("Play an object's sound",'audio.play');await chooseObject('Action inputs target','book');await page.getByLabel('Action inputs emitter',{exact:true}).fill('bell');const soundPlayed=await runNamedAction('audio.play',chatSound.seconds*1000+15000);assert.equal(soundPlayed.output!.source,acceptedSound.output!.sourceId);assert.ok(Math.abs(Number(soundPlayed.output!.seconds)-soundPreview.seconds)<.001);
 await page.screenshot({path:join(directory,`book-chat-audio-${soundFormat}.png`)});await writeFile(join(directory,soundFormat==='wav'?'book-chat-audio.json':`book-chat-audio-${soundFormat}.json`),JSON.stringify({boundary:'Synthetic finished sound in original chat, real book selection/preview/acceptance, emitter attachment and native PCM consumption at gain zero. No provider or audible/headset acceptance.',soundFormat,inputHash:chatSound.hash,preview:soundPreview,seconds:chatSound.seconds,selectedSound,acceptedSound,soundAttached,soundPlayed},null,2));
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 for(let i=0;i<2;i++){const before=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);await page.getByRole('button',{name:'Undo',exact:true}).click();await page.waitForFunction(rev=>window.nativeBookEvidence!().state!.sceneRevision>rev,before);}
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 }
 // Bind the accepted chat image, then inspect actual retention through generated fact forms.
 await openNamedAction('Choose object appearance','object.appearance.bind');await chooseObject('Action inputs target','book');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved appearance',exact:true}).click();await page.getByLabel('Choose appearance',{exact:true}).selectOption(JSON.stringify([acceptedChat.output!.appearanceId,acceptedChat.output!.revision]));
 const chatBinding=await runNamedAction('object.appearance.bind');
 const imageObservationRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);

 let imageReservation=await readResourceFact('runtime.imageReservation','Shared appearance image reservation',{index:'0'});
 const imageReadyDeadline=Date.now()+10000;
 while(imageReservation.state==='loading'&&Date.now()<imageReadyDeadline){await page.waitForTimeout(100);imageReservation=await readResourceFact('runtime.imageReservation','Shared appearance image reservation',{index:'0'});}
 assert.equal(imageReservation.imageHash,chatHash);assert.equal(imageReservation.state,'ready');assert.equal(imageReservation.owners,1);
 const imageBudget=await readResourceFact('runtime.imageBudget','Current room appearance image budget');
 assert.equal(imageBudget.entries,1);assert.equal(imageBudget.owners,1);assert.equal(imageBudget.textureBytes,imageReservation.textureBytes);assert.ok(Number(imageBudget.textureBytes)>0);
 const imageOwner=await readResourceFact('runtime.imageOwner','Appearance image owner',{reservationId:String(imageReservation.reservationId),index:'0'});
 assert.equal(imageOwner.target,'book');assert.equal(imageOwner.role,'appearance');assert.equal(imageOwner.imageHash,chatHash);assert.match(String(imageOwner.worldId),/^[a-f0-9]{32}$/);assert.match(String(imageOwner.regionId),/^[a-f0-9]{32}$/);
 assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),imageObservationRevision,'Fact forms must not change the world');
 await writeFile(join(directory,'book-image-residency.json'),JSON.stringify({boundary:'Actual Chrome book forms and native shared image cache. Runtime ownership is not visibility or streaming permission; no provider or physical headset claim.',chatBinding,imageBudget,imageReservation,imageOwner},null,2));
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-image-residency.png')});
 // The user edits the same live presentation schema the agent uses.
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Blend the backdrop');
 await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Blend the backdrop.*world.presentation.set/}).click();
 const presentationRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 await page.getByLabel('Action inputs backdropOpacity',{exact:true}).fill('0.5');
 await page.getByLabel('Action inputs realDepth',{exact:true}).selectOption('false');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.waitForFunction(()=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability==='world.presentation'&&c.available===true;});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='world.presentation.set'&&r.phase==='completed';});
 const presentation=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 assert.equal(presentation.output?.backdropOpacity,.5);assert.equal(presentation.output?.realDepth,false);
 assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),presentationRevision,'View action changed the saved room');
 await writeFile(join(directory,'book-native-presentation.json'),JSON.stringify({boundary:'Actual generated book inputs, native execution and receipt; desktop only.',presentation},null,2));
 await page.screenshot({path:join(directory,'book-native-presentation.png')});
 // Enabling user locomotion uses the same generated action while MR remains visible.
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions',{exact:true}).fill('Change movement');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Change movement or room view.*controller.mode.set/}).click();
 await page.getByLabel('Action inputs operation',{exact:true}).selectOption('user.enable');await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.waitForFunction(()=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability==='controller.mode'&&c.available===true;});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.capability==='controller.mode.set'&&r.phase==='completed'&&r.output?.userEnabled===true;});
 const mixedMovement=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 assert.equal(mixedMovement.output?.virtualView,false);assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),presentationRevision);
 await writeFile(join(directory,'book-native-mixed-movement.json'),JSON.stringify({boundary:'Generated book opt-in and real native receipt in a partial backdrop. Physical walking is tested separately; no headset proof.',mixedMovement},null,2));
 await page.screenshot({path:join(directory,'book-native-mixed-movement.png')});


 // Passthrough masks are ordinary shared surface components in generated book forms.
 await openNamedAction('Edit a drawing surface','object.surface.edit');
 await chooseObject('Action inputs target',ball.id);
 await page.getByLabel('Action inputs surface',{exact:true}).fill('WindowPlane');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const windowPlane=await runNamedAction('object.surface.edit');
 await openNamedAction('Edit a passthrough opening','object.window.edit');
 await chooseObject('Action inputs target',ball.id);
 await page.getByLabel('Action inputs surface',{exact:true}).fill('WindowPlane');
 await page.getByLabel('Action inputs reveal',{exact:true}).fill('0.5');
 await page.getByLabel('Action inputs shape',{exact:true}).selectOption('ellipse');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const windowSaved=await runNamedAction('object.window.edit');assert.equal(windowSaved.call.arguments.reveal,.5);assert.equal(windowSaved.call.arguments.shape,'ellipse');
 await page.screenshot({path:join(directory,'book-native-window.png')});
 await page.getByLabel('Variant',{exact:true}).selectOption('1');
 await chooseObject('Action inputs target',ball.id);
 await page.getByLabel('Action inputs window',{exact:true}).fill('Window');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const windowRemoved=await runNamedAction('object.window.edit');assert.equal(windowRemoved.call.arguments.operation,'remove');
 await writeFile(join(directory,'book-native-window.json'),JSON.stringify({boundary:'Actual original book generated forms, native plane/mask save/removal and revision guards. Desktop only; mask rendering is disabled without headset passthrough.',windowPlane,windowSaved,windowRemoved},null,2));

 // Replace a real collision proxy through generated inputs, then undo the same native edit.
 const collisionBefore=await readResourceFact('object.collision','Collision shape summary',{target:ball.id});
 await openNamedAction('Edit collision shapes','object.collision.edit');await chooseObject('Action inputs target',ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByText('Action inputs collision shapes · 1 entries',{exact:true}).click();
 await page.getByLabel('Action inputs collision shapes 1 id',{exact:true}).fill('BookProxy');
 for(const [key,value] of Object.entries({x:.24,y:.18,z:.2}))await page.getByLabel('Action inputs collision shapes 1 size '+key,{exact:true}).fill(String(value));
 const collisionEdited=await runNamedAction('object.collision.edit');
 assert.equal(collisionEdited.output!.target,ball.id);assert.equal(collisionEdited.output!.shapes,1);assert.equal(collisionEdited.output!.pieces,1);assert.equal(collisionEdited.output!.customActive,true);
 await page.getByRole('heading',{name:'Edit collision shapes',exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:join(directory,'book-native-collision-completion.png')});
 const collisionAfter=await readResourceFact('object.collision','Collision shape summary',{target:ball.id});
 assert.equal(collisionAfter.revision,collisionEdited.output!.revision);assert.equal(collisionAfter.shapes,1);
 const collisionShape=await readResourceFact('object.collision.shape','Collision shape source',{target:ball.id,revision:String(collisionAfter.revision),index:'0'});
 const proxy=collisionShape.shape as unknown as {id:string;size:{x:number;y:number;z:number}};assert.equal(proxy.id,'BookProxy');
 for(const [axis,value] of Object.entries({x:.24,y:.18,z:.2}))assert.ok(Math.abs(proxy.size[axis as 'x'|'y'|'z']-value)<1e-6);
 await book.workshop();const collisionSceneRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();await page.waitForFunction(previous=>window.nativeBookEvidence!().state!.sceneRevision>previous,collisionSceneRevision);
 const collisionRestored=await readResourceFact('object.collision','Collision shape summary',{target:ball.id});
 for(const key of ['shapes','pieces','customActive'])assert.equal(collisionRestored[key],collisionBefore[key]);
 await writeFile(join(directory,'book-native-collision-completion.json'),JSON.stringify({boundary:'Actual original-book collision proxy inputs, completed native receipt, exact shape readback and ordinary Undo. Desktop only; no headset/provider acceptance.',collisionBefore,collisionEdited,collisionAfter,collisionShape,collisionRestored},null,2));

 // Model settings are edited through the same generated form and native guard used by agents.
 const modelFixture=await readFile(join(directory,'model-fixture.json'),'utf8').then(text=>JSON.parse(text) as {target:string;hash:string}).catch(error=>{if(error.code==='ENOENT')return null;throw error;});
 if(modelFixture){
  await openNamedAction('Set imported model geometry','object.model.geometry.set');
  await chooseObject('Action inputs target',modelFixture.target);
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  assert.equal(await page.getByLabel('Action inputs settings scaleMode',{exact:true}).inputValue(),'fitted');
  await page.getByLabel('Action inputs settings scaleMode',{exact:true}).selectOption('source');
  await page.getByLabel('Action inputs settings pivot',{exact:true}).selectOption('source');
  await page.getByLabel('Action inputs settings meshCollision',{exact:true}).selectOption('true');
  await page.getByLabel('Action inputs settings walkable',{exact:true}).selectOption('true');
  const modelGeometry=await runNamedAction('object.model.geometry.set');
  assert.equal(modelGeometry.output?.target,modelFixture.target);
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  const modelFact=await page.evaluate(()=>window.nativeBookEvidence!().state!.catalog);
  assert.ok(modelFact?.operation==='inspect'&&modelFact.category==='facts'&&modelFact.capability==='object.model.geometry');
  const geometry=modelFact.value as {ready:boolean;modelHash:string;settings:{meshCollision:boolean;walkable:boolean};size:{x:number}};
  assert.equal(geometry.ready,true);assert.equal(geometry.modelHash,modelFixture.hash);assert.equal(geometry.settings.meshCollision,true);assert.equal(geometry.settings.walkable,true);assert.ok(Math.abs(geometry.size.x-6)<.001);
  await page.getByRole('heading',{name:'Set imported model geometry',exact:true}).scrollIntoViewIfNeeded();
  await page.screenshot({path:join(directory,'book-native-model-geometry.png')});
  await writeFile(join(directory,'book-native-model-geometry.json'),JSON.stringify({boundary:'Original book generated form, current-value prefill, actual native GLB scale/rigid geometry save and readiness readback. Scripted provider elsewhere; no headset claim.',modelFixture,modelGeometry,modelFact},null,2));
  const modelAssetBounds=await readResourceFact('object.model.assetBounds','Imported asset bounds',{target:modelFixture.target});
  assert.equal(modelAssetBounds.modelHash,modelFixture.hash);assert.equal(modelAssetBounds.derivationVersion,1);assert.equal(modelAssetBounds.coordinates,'object');
  assert.equal(modelAssetBounds.inspected,true);assert.equal(modelAssetBounds.known,true);assert.equal(modelAssetBounds.hasBounds,true);
  const assetMin=modelAssetBounds.min as unknown as {x:number;y:number;z:number},assetMax=modelAssetBounds.max as unknown as {x:number;y:number;z:number};
  assert.ok(Math.abs(assetMax.x-assetMin.x-6)<.0002);assert.ok(Math.abs(assetMin.y+.1)<.0001);assert.ok(Math.abs(assetMax.y-3)<.0001);
  await page.getByRole('heading',{name:'Imported asset bounds',exact:true}).scrollIntoViewIfNeeded();
  await page.screenshot({path:join(directory,'book-native-model-asset-bounds.png')});
  await writeFile(join(directory,'book-native-model-asset-bounds.json'),JSON.stringify({boundary:'Original book generated fact form reads the same validated static asset metadata as agents and programs. Asset-only envelope, not collision readiness or whole-object streaming.',modelFixture,modelAssetBounds},null,2));
  const authoredBounds=await readResourceFact('object.authoredBounds','Authored object bounds',{target:modelFixture.target});
  assert.equal(authoredBounds.prepared,true);assert.equal(authoredBounds.coordinates,'object');
  const authoredVisual=authoredBounds.visual as unknown as {known:boolean;hasBounds:boolean;min:{x:number;y:number;z:number};max:{x:number;y:number;z:number}};
  assert.equal(authoredVisual.known,true);assert.equal(authoredVisual.hasBounds,true);
  for(const axis of ['x','y','z'] as const){assert.ok(authoredVisual.min[axis]<assetMin[axis]);assert.ok(authoredVisual.max[axis]>assetMax[axis]);}
  await page.getByRole('heading',{name:'Authored object bounds',exact:true}).scrollIntoViewIfNeeded();
  await page.screenshot({path:join(directory,'book-native-authored-bounds.png')});
  await writeFile(join(directory,'book-native-authored-bounds.json'),JSON.stringify({boundary:'Original book generated fact form reads the same prepared authored visual envelope as the headless agent client. Native presence, physics readiness and headset acceptance are separate.',modelFixture,authoredBounds},null,2));
  const collisionBudget=await readResourceFact('runtime.collisionResources','Authored collision resources');
  const collisionOwners:Array<Record<string,string|number>>=[];
  for(let index=0;index<Number(collisionBudget.entries);index++)collisionOwners.push(await readResourceFact('runtime.collisionResource','Authored collision owner',{index:String(index)}));
  const modelOwner=collisionOwners.find(owner=>owner.target===modelFixture.target&&owner.kind==='imported'&&owner.phase==='ready');
  assert.ok(modelOwner);assert.equal(modelOwner.role,'collision');assert.equal(modelOwner.colliders,1);assert.ok(Number(modelOwner.sourceTriangles)>0);assert.equal(String(modelOwner.worldId).length,32);assert.equal(String(modelOwner.regionId).length,32);assert.equal(modelOwner.activeColliders,1);
  await writeFile(join(directory,'book-native-collision-resources.json'),JSON.stringify({boundary:'Original book catalog forms inspect actual native imported collision geometry through the same facts available to agents and programs. Source counts, not measured memory, admission, provider or headset evidence.',budget:collisionBudget,owners:collisionOwners,modelTarget:modelFixture.target},null,2));
 }

 // Guides follow the same native search/inspect path without changing the room.
 const guideRevision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Catalog category',{exact:true}).selectOption('guides');
 await page.getByLabel('Search guides',{exact:true}).fill('guide.program.events');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Events and persistent state.*guide.program.events/}).click();
 await page.getByRole('region',{name:'Guide reference',exact:true}).waitFor();
 const guide=await page.evaluate(()=>window.nativeBookEvidence!().state!.catalog);
 assert.ok(guide?.operation==='inspect'&&guide.category==='guides');
 assert.deepEqual(guide.definition,roomGuide('guide.program.events'));
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).count(),0);
 await page.getByRole('button',{name:'Read guide.program.basic',exact:true}).click();
 await page.waitForFunction(()=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='guides'&&c.capability==='guide.program.basic';});
 const prerequisite=await page.evaluate(()=>window.nativeBookEvidence!().state!.catalog);
 assert.ok(prerequisite?.operation==='inspect'&&prerequisite.category==='guides');assert.deepEqual(prerequisite.definition,roomGuide('guide.program.basic'));
 assert.equal(await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision),guideRevision);
 await page.screenshot({path:join(directory,'book-native-guides.png')});
 await writeFile(join(directory,'book-native-guides.json'),JSON.stringify({boundary:'Original book guide controls with actual native search and inspect. No provider or physical-headset claim.',guide,prerequisite},null,2));


 // Exercise the changed completion contract through the original generated form.
 // All geometry assertions above finish before enabling gravity in this fixture.
 const physicsBefore=await readResourceFact('physics.simulation','Room physics state');
 const changePhysics=async(operation:'start'|'pause')=>{
  await openNamedAction('Start or pause room physics','physics.simulation.set');
  await page!.getByRole('button',{name:'Load current values',exact:true}).click();
  await page!.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  await page!.getByLabel('Action inputs operation',{exact:true}).selectOption(operation);
  return runNamedAction('physics.simulation.set');
 };
 const createWalkingRobot=async(name:string,x:number)=>{
  await openNamedAction('Create object','object.create');
  await page!.getByLabel('Creation kind',{exact:true}).selectOption({label:'Create starter object'});
  await page!.getByLabel('Action inputs templateHash',{exact:true}).selectOption({label:'Box robot'});
  await page!.getByLabel('Action inputs name',{exact:true}).fill(name);
  for(const [key,value] of Object.entries({x,y:0,z:-3,scale:1}))await page!.getByLabel('Action inputs '+key,{exact:true}).fill(String(value));
  return runNamedAction('object.create');
 };
 const gait=await gaitFixture(directory);
 if(gait){
  await openNamedAction('Set imported model geometry','object.model.geometry.set');await chooseObject('Action inputs target',gait.target);
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  await page.getByLabel('Action inputs settings pivot',{exact:true}).selectOption('base');await runNamedAction('object.model.geometry.set');
  await openNamedAction('Arrange or reset objects','object.layout.apply');
  await page.getByText('Action inputs placements · 1 entries',{exact:true}).click();
  await chooseObject('Action inputs placements 1 target',gait.target);
  for(const [key,value] of Object.entries({x:-3,y:0,z:-4}))await page.getByLabel('Action inputs placements 1 position '+key,{exact:true}).fill(String(value));
  for(const [key,value] of Object.entries({x:0,y:0,z:0,w:1}))await page.getByLabel('Action inputs placements 1 rotation '+key,{exact:true}).fill(String(value));
  await page.getByLabel('Action inputs placements 1 scale',{exact:true}).fill('1');await runNamedAction('object.layout.apply');
  const before=await readResourceFact('object.walk.settings','Character walking animation',{target:gait.target});
  const clips=await readResourceFact('object.walk.clips','Character embedded clip choices',{target:gait.target,modelHash:gait.hash,offset:'0'});assert.equal(clips.total,1);
  await openNamedAction('Choose character walking animation','object.walk.select');await page.getByLabel('Character walking animation',{exact:true}).selectOption('1');
  await chooseObject('Action inputs target',gait.target);await page.getByLabel('Action inputs modelHash',{exact:true}).fill(gait.hash);await page.getByLabel('Action inputs clipIndex',{exact:true}).fill('0');
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  const selected=await runNamedAction('object.walk.select');
  await page.getByRole('heading',{name:'Choose character walking animation',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-object-walk.png')});
  await writeFile(join(directory,'book-native-object-walk-selection.json'),JSON.stringify({boundary:'Original generated book forms choose an exact synthetic imported gait through native facts and guarded save; no provider/headset claim.',gait,before,clips,selected},null,2));
  const feetBefore=await readResourceFact('object.feet.settings','Character foot placement',{target:gait.target});
  // Passive facts may describe retired geometry; the action acquires its native target.
  assert.equal(feetBefore.modelHash,gait.hash);
  await openNamedAction('Fit character feet to ground','object.feet.configure');await chooseObject('Action inputs target',gait.target);
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  assert.equal(await page.getByLabel('Action inputs modelHash',{exact:true}).inputValue(),gait.hash);
  assert.equal(Number(await page.getByLabel('Action inputs revision',{exact:true}).inputValue()),feetBefore.revision);
  await page.getByLabel('Action inputs mode',{exact:true}).selectOption('grounded');
  await page.getByLabel('Action inputs leftClearance',{exact:true}).fill('0.01');await page.getByLabel('Action inputs rightClearance',{exact:true}).fill('0.005');
  const feetReceipt=await runNamedAction('object.feet.configure');
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  assert.equal(Number(await page.getByLabel('Action inputs leftClearance',{exact:true}).inputValue()),.01);
  assert.equal(Number(await page.getByLabel('Action inputs rightClearance',{exact:true}).inputValue()),.005);
  await page.getByRole('heading',{name:'Fit character feet to ground',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-object-feet.png')});
  const feetAfter=await readResourceFact('object.feet.settings','Character foot placement',{target:gait.target});
  assert.equal(feetAfter.mode,'grounded');assert.equal(feetAfter.modelHash,gait.hash);assert.equal(feetAfter.revision,feetReceipt.output!.revision);
  await writeFile(join(directory,'book-native-object-feet.json'),JSON.stringify({boundary:'Original generated book controls, current native model/revision and both trims, real ordered completion and fact readback. No provider or headset proof.',gait,before:feetBefore,receipt:feetReceipt,after:feetAfter},null,2));

 }
 // Build aquatic fixtures before the walking fixtures: the later one-shot
 // Undo checks must still address the two newest authored creations.
 // Movement itself does not push an Undo entry.
 const aquaticObjects:string[]=[];
 for(const [name,x,y,scale,z] of [['Book aquatic pool',-1.5,1,1,2],['Book aquatic actor',-1.5,1.4,.5,2],['Book shore actor',-1.5,1.5,.5,2.25],['Book water guide',-.3,1.6,.5,2]] as const){
  await openNamedAction('Create object','object.create');
  await page.getByLabel('Creation kind',{exact:true}).selectOption('0');
  await page.getByLabel('Action inputs shape',{exact:true}).selectOption('block');
  await page.getByLabel('Action inputs name',{exact:true}).fill(name);
  for(const [key,value] of Object.entries({x,y,z,scale}))await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(value));
  const created=await runNamedAction('object.create'),id=created.output!.objectId as string;aquaticObjects.push(id);
  await openNamedAction('Configure object physics','object.physics.configure');await chooseObject('Action inputs target',id);
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
  await page.getByLabel('Action inputs mode',{exact:true}).selectOption('fixed');await page.getByLabel('Action inputs shape',{exact:true}).selectOption('box');await runNamedAction('object.physics.configure');
 }
 const [aquaticPool,aquaticActor,shoreActor,waterGuide]=aquaticObjects;
 await openNamedAction('Configure or fill a liquid container','object.container.edit');await chooseObject('Action inputs target',aquaticPool);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 for(const [key,value] of Object.entries({'frame position y':.2,radius:.5,height:.6,capacityMl:600000,amountMl:400000}))await page.getByLabel('Action inputs definition '+key,{exact:true}).fill(String(value));
 const aquaticContainer=await runNamedAction('object.container.edit');

 const walkingCreated=await createWalkingRobot('Book walking robot',-3),walkingTarget=walkingCreated.output!.objectId as string;
 const walkingObstacle=await createWalkingRobot('Book walking obstacle',-2),walkingObstacleId=walkingObstacle.output!.objectId as string;
 const attentionResults=[];
 for(const actor of [{target:walkingTarget,part:'Head'},...(gait?[{target:gait.target,part:''}]:[])]){
  const before=(await book.observation(actor.target)).position;
  await openNamedAction("Turn a character's attention",'object.look');
  await page.getByLabel('Joint',{exact:true}).selectOption(actor.part?'1':'0');
  await chooseObject('Action inputs target',actor.target);
  if(actor.part)await page.getByLabel('Action inputs joint part',{exact:true}).fill(actor.part);
  await page.getByLabel('Attention',{exact:true}).selectOption('1');
  await chooseObject('Action inputs focus objectId','book');
  await page.getByLabel('Action inputs seconds',{exact:true}).fill('1');
  const receipt=await runNamedAction('object.look');assert.equal(receipt.output!.applied,true);assert.equal(receipt.output!.objectId,'book');
  await page.getByRole('heading',{name:"Turn a character's attention",exact:true}).scrollIntoViewIfNeeded();
  await page.screenshot({path:join(directory,'book-native-object-attention-'+(actor.part?'recipe':'humanoid')+'.png')});
  assert.deepEqual((await book.observation(actor.target)).position,before);
  const stopped=await book.fact('object.attention','Character attention',{target:actor.target},true);
  let retired:Record<string,unknown>|undefined;
  if(stopped.available)assert.equal((stopped.value as {active:boolean}).active,false);
  else{
   retired=await readResourceFact('object.presence','Object saved and native presence',{target:actor.target});
   assert.equal(retired.saved,true);
   assert.ok(retired.nativeInstance===false||retired.active===false,'Unavailable attention must match a retired native instance');
  }
  attentionResults.push({actor,receipt,stopped,retired});
 }
 await writeFile(join(directory,'book-native-object-attention.json'),JSON.stringify({boundary:'Original generated book controls aim both a recipe joint and synthetic imported humanoid through the shared action and fact. No provider or physical-headset proof.',results:attentionResults},null,2));

 const physicsStarted=await changePhysics('start');assert.equal(physicsStarted.output!.running,true);
 await openNamedAction('Move to a place','object.navigate');
 await chooseObject('Action inputs target',walkingTarget);
 for(const [axis,value] of Object.entries({x:-1,y:0,z:-3}))await page.getByLabel('Action inputs destination '+axis,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs speed',{exact:true}).fill('0.65');
 const walkingReceipt=await runNamedAction('object.navigate');
 await page.getByRole('heading',{name:'Move to a place',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-grounded-travel.png')});
 const walkingPosition=(await book.observation(walkingTarget)).position;
 assert.ok(Math.abs(walkingPosition.x+1)<.02&&Math.abs(walkingPosition.y)<.02&&Math.abs(walkingPosition.z+3)<.02);assert.ok(Number(walkingReceipt.output!.distance)>2);

 await writeFile(join(directory,'book-native-grounded-travel.json'),JSON.stringify({boundary:'Original book generated create and walk forms around a solid robot obstacle, with real native arrival, travelled distance and position. Explicit synthetic floor; no provider or headset acceptance.',created:walkingCreated,obstacle:walkingObstacle,receipt:walkingReceipt,position:walkingPosition},null,2));
 // Put the independent leader farther away with the existing walking form.
 await openNamedAction('Move to a place','object.navigate');
 await chooseObject('Action inputs target',walkingTarget);
 for(const [axis,value] of Object.entries({x:1,y:0,z:-3}))await page.getByLabel('Action inputs destination '+axis,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs speed',{exact:true}).fill('1.2');await runNamedAction('object.navigate');
 await openNamedAction('Follow someone','object.follow');
 await page.getByLabel('Leader',{exact:true}).selectOption('1');
 await chooseObject('Action inputs target',walkingObstacleId);
 await chooseObject('Action inputs leader objectId',walkingTarget);
 for(const [key,value] of Object.entries({distance:1,speed:1.2,seconds:5}))await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(value));
 const followingReceipt=await runNamedAction('object.follow');assert.equal(followingReceipt.output!.state,'keepingDistance');assert.ok(Number(followingReceipt.output!.distance)>1.9);
 await page.getByRole('heading',{name:'Follow someone',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-grounded-following.png')});
 const followingPositions={actor:(await book.observation(walkingObstacleId)).position,leader:(await book.observation(walkingTarget)).position};
 const followingGap=Math.hypot(followingPositions.actor.x-followingPositions.leader.x,followingPositions.actor.z-followingPositions.leader.z);assert.ok(followingGap>=.95&&followingGap<=1.12);

 const followingStatus=await readResourceFact('object.navigation','Movement action status',{target:walkingObstacleId});assert.equal(followingStatus.active,false);
 await writeFile(join(directory,'book-native-grounded-following.json'),JSON.stringify({boundary:'Original book generated follow form, live native following, accepted distance and passive status. Explicit synthetic ground, no provider or headset acceptance.',receipt:followingReceipt,positions:followingPositions,gap:followingGap,status:followingStatus},null,2));
 if(gait){
  await openNamedAction('Move to a place','object.navigate');await chooseObject('Action inputs target',gait.target);
  for(const [axis,value] of Object.entries({x:-1,y:0,z:-4}))await page.getByLabel('Action inputs destination '+axis,{exact:true}).fill(String(value));await page.getByLabel('Action inputs speed',{exact:true}).fill('0.5');
  const receipt=await runNamedAction('object.navigate');assert.ok(Number(receipt.output!.distance)>1.9);
  const after=await readResourceFact('object.walk.settings','Character walking animation',{target:gait.target});
  assert.equal((after.selection as unknown as {playbackStatus:string}).playbackStatus,'inactive');
  await writeFile(join(directory,'book-native-object-walk.json'),JSON.stringify({boundary:'The book starts actual native grounded movement with its saved imported gait; completion releases pose ownership. No headset/provider or calibrated gait claim.',gait,receipt,after},null,2));
 }
 await openNamedAction('Move to a place','object.navigate');await page.getByLabel('Movement',{exact:true}).selectOption({label:'Swim inside one habitat'});
 await chooseObject('Action inputs target',aquaticActor);await chooseObject('Action inputs habitat',aquaticPool);
 for(const [axis,value] of Object.entries({x:-1.2,y:1.45,z:2}))await page.getByLabel('Action inputs destination '+axis,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs facing',{exact:true}).selectOption('travel');await page.getByLabel('Action inputs speed',{exact:true}).fill('.3');
 const aquaticReceipt=await runNamedAction('object.navigate');
 assert.ok(Number(aquaticReceipt.output!.distance)>.29);
 await page.getByRole('heading',{name:'Move to a place',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-aquatic-travel.png')});
 const aquaticPosition=(await book.observation(aquaticActor)).position;
 assert.ok(Math.abs(aquaticPosition.x+1.2)<.006&&Math.abs(aquaticPosition.y-1.45)<.006&&Math.abs(aquaticPosition.z-2)<.006);
 await writeFile(join(directory,'book-native-aquatic-travel.json'),JSON.stringify({boundary:'Visible original-book create, physics, liquid and shared movement controls. Actual native turning, submerged movement and receipt. No provider or headset acceptance.',pool:aquaticPool,actor:aquaticActor,container:aquaticContainer,receipt:aquaticReceipt,position:aquaticPosition},null,2));
 await openNamedAction('Move to a place','object.navigate');await page.getByLabel('Movement',{exact:true}).selectOption({label:'Swim at the live surface'});
 await chooseObject('Action inputs target',aquaticActor);await chooseObject('Action inputs habitat',aquaticPool);
 assert.equal(await page.getByLabel('Action inputs destination y',{exact:true}).count(),0);
 for(const [axis,value] of Object.entries({x:-1.45,z:2}))await page.getByLabel('Action inputs destination '+axis,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs immersion',{exact:true}).fill('.6');await page.getByLabel('Action inputs facing',{exact:true}).selectOption('keep');await page.getByLabel('Action inputs speed',{exact:true}).fill('.3');
 const surfaceReceipt=await runNamedAction('object.navigate');assert.ok(Number(surfaceReceipt.output!.distance)>.3);
 await page.getByRole('heading',{name:'Move to a place',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-surface-travel.png')});
 const surfacePosition=(await book.observation(aquaticActor)).position;
 assert.ok(Math.abs(surfacePosition.x+1.45)<.006&&Math.abs(surfacePosition.z-2)<.006&&surfacePosition.y>1.5&&surfacePosition.y<1.7);
 await writeFile(join(directory,'book-native-surface-travel.json'),JSON.stringify({boundary:'Visible original-book movement variant, exact actor/habitat, immersion and x/z controls; actual native gradual rise and surface travel. No provider or headset acceptance.',pool:aquaticPool,actor:aquaticActor,receipt:surfaceReceipt,position:surfacePosition},null,2));
 // Preserve the earlier 3D swim's tilted actor; shore travel requires its own upright walking body.
 // Its 0.065 m cube starts wet; the walking capsule and 0.04 m foot skin put its root at 1.5313457 m for 60% immersion.
 await openNamedAction('Move to a place','object.navigate');await page.getByLabel('Movement',{exact:true}).selectOption({label:'Walk and swim across a shore'});
 await chooseObject('Action inputs target',shoreActor);await chooseObject('Action inputs habitat',aquaticPool);
 await page.getByLabel('Shore destination',{exact:true}).selectOption({label:'Supported ground foot'});
 assert.equal(await page.getByLabel('Action inputs destination y',{exact:true}).count(),1);
 await page.getByLabel('Shore destination',{exact:true}).selectOption({label:'Live water surface'});
 assert.equal(await page.getByLabel('Action inputs destination y',{exact:true}).count(),0);
 for(const [axis,value] of Object.entries({x:-1.2,z:2.25}))await page.getByLabel('Action inputs destination '+axis,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs immersion',{exact:true}).fill('.6');await page.getByLabel('Action inputs facing',{exact:true}).selectOption('keep');await page.getByLabel('Action inputs speed',{exact:true}).fill('.3');
 const shoreReceipt=await runNamedAction('object.navigate');assert.ok(Number(shoreReceipt.output!.distance)>.24);
 await page.getByRole('heading',{name:'Move to a place',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-shore-travel.png')});
 const shorePosition=(await book.observation(shoreActor)).position;
 assert.ok(Math.abs(shorePosition.x+1.2)<.006&&Math.abs(shorePosition.z-2.25)<.006&&Math.abs(shorePosition.y-1.5313457)<.006);
 await writeFile(join(directory,'book-native-shore-travel.json'),JSON.stringify({boundary:'Visible original-book shore variant, typed ground/surface destination controls and a real native buoyant segment using the shared capsule. Full dry/wet transitions run in native and headless tests; no provider or headset acceptance.',pool:aquaticPool,actor:shoreActor,receipt:shoreReceipt,position:shorePosition},null,2));
 await openNamedAction('Follow someone','object.follow');await page.getByLabel('Following movement',{exact:true}).selectOption({label:'Follow at the water surface'});
 await chooseObject('Action inputs target',aquaticActor);await chooseObject('Action inputs habitat',aquaticPool);
 await page.getByLabel('Leader',{exact:true}).selectOption({label:'Another object'});await chooseObject('Action inputs leader objectId',waterGuide);
 for(const [key,value] of Object.entries({distance:.8,speed:.3,seconds:4,immersion:.6}))await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs facing',{exact:true}).selectOption('keep');
 const waterFollowingReceipt=await runNamedAction('object.follow');assert.equal(waterFollowingReceipt.output!.state,'keepingDistance');assert.ok(Number(waterFollowingReceipt.output!.distance)>.2);
 await page.getByRole('heading',{name:'Follow someone',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-water-following.png')});
 const waterFollowingPosition=(await book.observation(aquaticActor)).position;
 assert.ok(Math.abs(waterFollowingPosition.x+1.1)<.04&&Math.abs(waterFollowingPosition.z-2)<.01);
 await writeFile(join(directory,'book-native-water-following.json'),JSON.stringify({boundary:'Original book generated following-mode, leader, habitat and immersion controls, real native surface following and completed receipt. No provider/headset or moving-leader claim.',pool:aquaticPool,actor:aquaticActor,leader:waterGuide,receipt:waterFollowingReceipt,position:waterFollowingPosition},null,2));
 await openNamedAction('Look toward something','avatar.look');
 await page.getByLabel('Attention',{exact:true}).selectOption('2');
 for(const [axis,value] of Object.entries({x:2,y:1.6,z:2}))await page.getByLabel('Action inputs focus point '+axis,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs seconds',{exact:true}).fill('0.8');
 const attentionPoint=await runNamedAction('avatar.look');assert.equal(attentionPoint.output!.focus,'point');assert.equal(attentionPoint.output!.applied,true);
 await page.getByLabel('Attention',{exact:true}).selectOption('1');
 await chooseObject('Action inputs focus objectId','book');
 const attentionObject=await runNamedAction('avatar.look');assert.equal(attentionObject.output!.objectId,'book');assert.equal(attentionObject.output!.applied,true);
 await page.getByRole('heading',{name:'Look toward something',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-native-avatar-attention.png')});
 const attentionStopped=await readResourceFact('avatar.attention','Maestro attention',{target:'maestro'});assert.equal(attentionStopped.active,false);
 await writeFile(join(directory,'book-native-avatar-attention.json'),JSON.stringify({boundary:'Original book schema-generated place/object focus controls, actual Unity gaze receipts and passive stopped fact. No provider or headset claim.',point:attentionPoint,object:attentionObject,stopped:attentionStopped},null,2));
 const physicsPaused=await changePhysics('pause');assert.equal(physicsPaused.output!.running,false);
 // Observe one quiet placement-capture interval before the one-shot Undo.
 const placementSignature=async()=>{const samples=[];for(const id of await book.ids()){const o=await book.observation(id);samples.push([id,o.objectRevision,o.position]);}return JSON.stringify(samples);};
 let placementBefore=await placementSignature(),quietAt=Date.now();const placementDeadline=Date.now()+60000;
 while(Date.now()-quietAt<1250){
  assert.ok(Date.now()<placementDeadline,'Paused native placement did not settle');await new Promise(resolve=>setTimeout(resolve,150));
  const current=await placementSignature();if(current!==placementBefore)quietAt=Date.now();placementBefore=current;
 }
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();
 for(const target of [walkingObstacleId,walkingTarget])await undoRemoved(target);
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 const physicsInputs=await readResourceFact('object.physics.inputs','Object physics inputs',{target:ball.id});assert.equal(physicsInputs.state,'inactive');
 const heldContact=await readResourceFact('object.contact.inputs','Held object contact inputs',{target:ball.id});assert.equal(heldContact.state,'inactive');
 const trackedContact=await readResourceFact('controller.contact.inputs','Hand or controller contact inputs',{hand:'right'});assert.equal(trackedContact.state,'inactive');
 const carriedContact=await readResourceFact('object.attachment.inputs','Carried object contact inputs',{target:ball.id});assert.equal(carriedContact.state,'inactive');
 const recordedContact=await readResourceFact('object.recording.inputs','Recorded playback inputs',{target:ball.id});assert.equal(recordedContact.state,'inactive');
 const authoredContact=await readResourceFact('avatar.authored.inputs','Authored avatar inputs',{});assert.equal(authoredContact.state,'inactive');
 await writeFile(join(directory,'book-native-contact-inputs.json'),JSON.stringify({boundary:'Original generated book fact forms observing real Unity with physics paused. No active headset contact or provider acceptance.',heldContact,trackedContact,carriedContact,recordedContact,authoredContact},null,2));
 const physicsAfter=await readResourceFact('physics.simulation','Room physics state');assert.equal(physicsAfter.running,false);
 await page.getByRole('heading',{name:'Room physics state',exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:join(directory,'book-native-physics.png')});
 await writeFile(join(directory,'book-native-physics.json'),JSON.stringify({boundary:'Original generated book Start/Pause controls, current state guard, completion receipts and actual native simulation state. Synthetic aligned floor; no real scan, provider or headset proof. Dormant loading is covered separately by native regressions.',physicsBefore,physicsStarted,physicsPaused,physicsInputs,physicsAfter},null,2));

 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();
 await page.waitForFunction(()=>window.nativeBookEvidence!().state!.visible===false);
 const commandCount=requests.size;
 await page.reload();await page.waitForFunction(()=>!!window.nativeBookEvidence?.().state);
 await page.getByText('The ball keeps your colour.',{exact:true}).waitFor();
 assert.equal(requests.size,commandCount,'Reload replayed a room command');assert.equal(plannerCalls,expectedPlans);assert.deepEqual(errors,[]);assert.deepEqual((await page.evaluate(()=>window.nativeBookEvidence!())).errors,[]);
 const evidence={boundary:'Real QuestBookSurface, ChatInterface/useTutorConversation, verifier, task service/IndexedDB and Unity app; provider SSE responses are explicitly scripted offline, no real provider, Android texture, headset or scan acceptance.',providerUsed:false,sparseObjectView:!!owner.sparseObjectView,providerRequests,initialObjects,manual:true,importedSound,physicalTools:{shownAndHiddenViaSharedForm:true,savedSceneUnchanged:true},manualForm,portableCapture,presentation,mixedMovement,capture,capturePixelsVerified:true,humanEditPreserved:true,staleAgentPaintRefused:true,creationCompletionObserved:true,creationStartedOnce:true,discoveryBudgetPreservedActions:true,planningCalls:plannerCalls,reloadWithoutReplay:true,initial,working,human,completed,task,requests:[...requests.values()],observations,errors};
 await writeFile(join(directory,'book-journey.json'),JSON.stringify(evidence,null,2));
 console.log('Real native book and original-chat handoff journey passed.');
}catch(error){
 if(page&&!page.isClosed()){await page.screenshot({path:join(directory,'book-failure.png')}).catch(()=>{});console.error(await page.locator('body').innerText().catch(()=>''));}
 await writeFile(join(directory,'book-failure.json'),JSON.stringify({error:String(error),errors,plannerCalls,verificationCalls,normalReplies,requests:[...requests.values()],observations,providerRequests},null,2));throw error;
}finally{
 releasePlan();
 try{await browser?.close();}finally{try{await server?.close();}finally{await channel.stop();}}
}
