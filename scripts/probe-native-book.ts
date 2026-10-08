// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium,type Browser} from 'playwright-core';
import {createServer,type ViteDevServer} from 'vite';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import assert from 'node:assert/strict';
import {RoomProbeChannel} from '../src/headless/roomTransport';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import {ROOM_TASK_LIMITS} from '../shared/roomTaskBudget';
const directory=process.argv[2];if(!directory)throw new Error('Supply the fresh, explicitly started native probe directory.');
const channel=await RoomProbeChannel.connect(directory,120000);
let browser:Browser|undefined,server:ViteDevServer|undefined;
const requests=new Map<string,unknown>(),observations:RoomAgentState[]=[],providerRequests:unknown[]=[],errors:string[]=[];
let page:Awaited<ReturnType<Browser['newPage']>>|undefined;
let releasePlan:()=>void=()=>{};const blockedPlan=new Promise<void>(resolve=>{releasePlan=resolve;});
let plannerCalls=0,normalReplies=0,verificationCalls=0,planWaiting=false;
const discoveryReads=ROOM_TASK_LIMITS.queryBatches,expectedPlans=discoveryReads+3;
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
  if(result.state){const state=result.state as RoomAgentState,previous=observations.at(-1);if(previous?.session!==state.session||previous.revision!==state.revision)observations.push(structuredClone(state));}
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
   assert.deepEqual(input.budget,{planningCalls:ROOM_TASK_LIMITS.planningCalls+1-plannerCalls,queryBatches:Math.max(0,discoveryReads+1-plannerCalls),actionBatches:ROOM_TASK_LIMITS.actionBatches-Math.max(0,plannerCalls-discoveryReads-1)});
   if(plannerCalls===1||plannerCalls===4)text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'search',query:plannerCalls===1?'object.create':'object.color',offset:0}}]});
   else if(plannerCalls===5)text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'inspect',capability:'object.color.set',version:1}}]});
   else if(plannerCalls===3){
    const definition=state.catalog?.operation==='inspect'?state.catalog.definition:null;assert.ok(definition&&'example' in definition&&definition.example);
    text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'check',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Cooperative ball',x:.5,y:1,z:.8}}}}]});
   }else if(plannerCalls<=discoveryReads)text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}}]});
   else if(plannerCalls===discoveryReads+1){
    const definition=state.catalog?.operation==='inspect'?state.catalog.definition:null;
    assert.ok(definition&&'example' in definition&&definition.example);
    text=JSON.stringify({commands:[{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Cooperative ball',x:.5,y:1,z:.8}}}}]});
   }else if(plannerCalls===discoveryReads+2){
    const ball=state.objects.find(o=>o.name==='Cooperative ball');assert.ok(ball);planWaiting=true;await blockedPlan;
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
 const initial=await page.evaluate(()=>window.nativeBookEvidence!().state!);
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
 await page.screenshot({path:join(directory,'book-native-edit.png')});
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();
 await page.waitForFunction(revision=>window.nativeBookEvidence!().state!.sceneRevision>revision,edited.sceneRevision);
 const restored=await page.evaluate(()=>window.nativeBookEvidence!().state!);assert.deepEqual(restored.inspection!.recipe,robot.recipe);
 await page.getByRole('button',{name:'Undo',exact:true}).first().click();
 await page.waitForFunction(id=>!window.nativeBookEvidence!().state!.objects.some(o=>o.id===id),robot.id);
 await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();
 console.log('Actual book create/edit/Undo passed.');
 await writeFile(join(directory,'manual-book.json'),JSON.stringify({boundary:'Real book UI and Unity room; no native replay, headset or live provider.',initial,robot,edited,restored},null,2));
 // The ordinary chat hook, verifier, task coordinator and browser provider client all run.
 const input=page.getByRole('textbox').filter({visible:true}).last();await input.fill('Create a ball, then make it green.');
 await input.press('Enter');
 await page.waitForFunction(()=>window.nativeBookEvidence!().agentWorking,{},{timeout:30000});
 const waitingDeadline=Date.now()+30000;while(!planWaiting&&Date.now()<waitingDeadline)await new Promise(resolve=>setTimeout(resolve,100));assert.ok(planWaiting,'Planner did not reach the live edit boundary');
 const working=await page.evaluate(()=>window.nativeBookEvidence!());assert.equal(working.inputBlocked,false);
 const ball=working.state!.objects.find(o=>o.name==='Cooperative ball');assert.ok(ball);
 await page.evaluate(()=>window.maestroBook!.command({version:1,type:'workspace.open'}));
 await page.getByRole('button',{name:/Cooperative ball/}).click();
 await page.getByRole('button',{name:'Purple',exact:true}).click();
 await page.waitForFunction(id=>{const ball=window.nativeBookEvidence!().state!.objects.find(o=>o.id===id);return !!ball&&Math.abs(ball.color.r-.47)<.0001;},ball.id);
 const human=await page.evaluate(()=>window.nativeBookEvidence!().state!);
 await page.screenshot({path:join(directory,'book-human-takeover.png')});
 releasePlan();
 await page.waitForFunction(()=>window.nativeBookEvidence!().messages.some(m=>m.agentTask?.phase==='completed'),{},{timeout:30000});
 const completed=await page.evaluate(()=>window.nativeBookEvidence!());assert.equal(completed.agentWorking,false);
 const taskMessage=completed.messages.find(m=>m.agentTask?.phase==='completed')!;
 const task=await page.evaluate(id=>window.nativeBookTask!(id),taskMessage.agentTask!.id);assert.ok(task);
 assert.equal(task.handoff.input.prompt,'Create a ball, then make it green.');assert.equal(task.phase,'completed');
 const failedPaint=task.operations.find(o=>o.commands[0].action==='paint');assert.ok(failedPaint?.receipt&&!failedPaint.receipt.ok,'Stale planned paint must be refused after the human edit');
 assert.deepEqual(completed.state!.objects.find(o=>o.id===ball.id)!.color,human.objects.find(o=>o.id===ball.id)!.color);
 assert.equal(completed.state!.objects.length,initial.objects.length+1);assert.equal(plannerCalls,expectedPlans);assert.equal(verificationCalls,1);assert.equal(normalReplies,2);
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
 await page.getByLabel('Action inputs target',{exact:true}).selectOption(ball.id);
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
 await page.getByLabel('Action inputs target',{exact:true}).selectOption(ball.id);
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
 await page.getByLabel('Action inputs members 1 target',{exact:true}).selectOption(ball.id);
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
 const runNamedAction=async(id:string)=>{
  const previous=await page!.evaluate(()=>window.nativeBookEvidence!().state!.execution?.selected?.id);
  await page!.getByRole('button',{name:'Run action now',exact:true}).click();
  await page!.waitForFunction(({id,previous})=>{const r=window.nativeBookEvidence!().state!.execution?.selected;return r?.id!==previous&&r?.capability===id&&r.phase==='completed';},{id,previous});
  return page!.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 };
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
 await openNamedAction('Save environment profile','environment.profile.save');
 await page.getByLabel('Action inputs name',{exact:true}).fill('Virtual terrain only');
 await page.getByLabel('Action inputs realCollisions',{exact:true}).selectOption('false');
 const collisionProfile=await runNamedAction('environment.profile.save');
 await openNamedAction('Choose object environment','object.environment.assign');
 await page.getByLabel('Action inputs target',{exact:true}).selectOption(ball.id);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved collision profile',exact:true}).click();
 await page.getByLabel('Choose collision profile',{exact:true}).selectOption(JSON.stringify([collisionProfile.output!.id,collisionProfile.output!.revision]));
 const beforeCollisionChoice=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
 assert.equal(beforeCollisionChoice.capability,'environment.profile.save');
 const collisionBinding=await runNamedAction('object.environment.assign');
 assert.equal(collisionBinding.call.arguments.profileId,collisionProfile.output!.id);
 await openNamedAction('Create or edit a reusable sound','audio.source.edit');
 await page.getByLabel('Action inputs definition name',{exact:true}).fill('Friendly ball beep');
 const sound=await runNamedAction('audio.source.edit');
 await openNamedAction('Attach a sound to an object','object.audioEmitter.edit');
 await page.getByLabel('Action inputs target',{exact:true}).selectOption(ball.id);
 await page.getByLabel('Action inputs definition joint',{exact:true}).selectOption('');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Load saved sound',exact:true}).click();
 await page.getByLabel('Choose sound',{exact:true}).selectOption(JSON.stringify([sound.output!.id,null]));
 const soundBinding=await runNamedAction('object.audioEmitter.edit');assert.equal((soundBinding.call.arguments.definition as {source:string}).source,sound.output!.id);
 await writeFile(join(directory,'book-native-resource-choices.json'),JSON.stringify({boundary:'Named resource choices through real book forms and desktop Unity; sound assignment does not play audio. No provider or headset proof.',visualLayer,visualBinding,appearance,boundAppearance,collisionProfile,collisionBinding,sound,soundBinding},null,2));
 await page.getByRole('region',{name:'Sound choice',exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:join(directory,'book-native-resource-choices.png')});
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


 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();
 await page.waitForFunction(()=>window.nativeBookEvidence!().state!.visible===false);
 const commandCount=requests.size;
 await page.reload();await page.waitForFunction(()=>!!window.nativeBookEvidence?.().state);
 await page.getByText('The ball keeps your colour.',{exact:true}).waitFor();
 assert.equal(requests.size,commandCount,'Reload replayed a room command');assert.equal(plannerCalls,expectedPlans);assert.deepEqual(errors,[]);assert.deepEqual((await page.evaluate(()=>window.nativeBookEvidence!())).errors,[]);
 const evidence={boundary:'Real QuestBookSurface, ChatInterface/useTutorConversation, verifier, task service/IndexedDB and Unity app; provider SSE responses are explicitly scripted offline, no real provider, Android texture, headset or scan acceptance.',providerUsed:false,providerRequests,manual:true,physicalTools:{shownAndHiddenViaSharedForm:true,savedSceneUnchanged:true},manualForm,portableCapture,presentation,mixedMovement,capture,capturePixelsVerified:true,humanEditPreserved:true,staleAgentPaintRefused:true,discoveryBudgetPreservedActions:true,planningCalls:plannerCalls,reloadWithoutReplay:true,initial,working,human,completed,task,requests:[...requests.values()],observations,errors};
 await writeFile(join(directory,'book-journey.json'),JSON.stringify(evidence,null,2));
 console.log('Real native book and original-chat handoff journey passed.');
}catch(error){
 if(page&&!page.isClosed()){await page.screenshot({path:join(directory,'book-failure.png')}).catch(()=>{});console.error(await page.locator('body').innerText().catch(()=>''));}
 await writeFile(join(directory,'book-failure.json'),JSON.stringify({error:String(error),errors,plannerCalls,verificationCalls,normalReplies,requests:[...requests.values()],observations,providerRequests},null,2));throw error;
}finally{
 releasePlan();
 try{await browser?.close();}finally{try{await server?.close();}finally{await channel.stop();}}
}
