// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium,type Browser} from 'playwright-core';
import {createServer,type ViteDevServer} from 'vite';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import assert from 'node:assert/strict';
import {RoomProbeChannel} from '../src/headless/roomTransport';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
const directory=process.argv[2];if(!directory)throw new Error('Supply the fresh, explicitly started native probe directory.');
const channel=await RoomProbeChannel.connect(directory,120000);
let browser:Browser|undefined,server:ViteDevServer|undefined;
const requests=new Map<string,unknown>(),observations:RoomAgentState[]=[],providerRequests:unknown[]=[],errors:string[]=[];
let page:Awaited<ReturnType<Browser['newPage']>>|undefined;
let releasePlan:()=>void=()=>{};const blockedPlan=new Promise<void>(resolve=>{releasePlan=resolve;});
let plannerCalls=0,normalReplies=0,verificationCalls=0,planWaiting=false;
try{
 // This immutable verification page needs no watcher or shared optimizer cache.
 server=await createServer({cacheDir:join(directory,'vite-cache'),optimizeDeps:{entries:['test-fixtures/browser/quest-native-book.html']},server:{host:'127.0.0.1',port:0,strictPort:true,watch:null},logLevel:'warn',clearScreen:false});await server.listen();
 const address=server.httpServer!.address();assert.ok(address&&typeof address==='object');const base='http://127.0.0.1:'+address.port;
 browser=await chromium.launch({channel:'chrome',headless:true});
 const context=await browser.newContext({viewport:{width:1440,height:1080},serviceWorkers:'block'});
 page=await context.newPage();page.setDefaultTimeout(20000);page.on('pageerror',error=>errors.push(error.message));
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
   if(plannerCalls===1)text=JSON.stringify({commands:[{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}}]});
   else if(plannerCalls===2){
    const definition=state.catalog?.operation==='inspect'?state.catalog.definition:null;
    assert.ok(definition&&'example' in definition&&definition.example);
    text=JSON.stringify({commands:[{action:'execution',execution:{operation:'start',call:{id:'object.create',version:1,arguments:{...definition.example,name:'Cooperative ball',x:.5,y:1,z:.8}}}}]});
   }else if(plannerCalls===3){
    const ball=state.objects.find(o=>o.name==='Cooperative ball');assert.ok(ball);planWaiting=true;await blockedPlan;
    text=JSON.stringify({commands:[{action:'paint',target:ball.id,color:{r:0,g:1,b:0,a:1}}]});
   }else if(plannerCalls===4){assert.equal(input.receipts.at(-1).ok,false);assert.match(input.receipts.at(-1).status,/target changed/);text=JSON.stringify({commands:[]});}
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
 assert.equal(completed.state!.objects.length,initial.objects.length+1);assert.equal(plannerCalls,4);assert.equal(verificationCalls,1);assert.equal(normalReplies,2);
 await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();
 await page.getByText('Task details',{exact:true}).click();await page.getByText('Recorded action batches: 3.',{exact:true}).waitFor();
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
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Back to chat',exact:true}).first().click();
 await page.waitForFunction(()=>window.nativeBookEvidence!().state!.visible===false);
 const commandCount=requests.size;
 await page.reload();await page.waitForFunction(()=>!!window.nativeBookEvidence?.().state);
 await page.getByText('The ball keeps your colour.',{exact:true}).waitFor();
 assert.equal(requests.size,commandCount,'Reload replayed a room command');assert.equal(plannerCalls,4);assert.deepEqual(errors,[]);assert.deepEqual((await page.evaluate(()=>window.nativeBookEvidence!())).errors,[]);
 const evidence={boundary:'Real QuestBookSurface, ChatInterface/useTutorConversation, verifier, task service/IndexedDB and Unity app; provider SSE responses are explicitly scripted offline, no real provider, Android texture, headset or scan acceptance.',providerUsed:false,providerRequests,manual:true,capture,capturePixelsVerified:true,humanEditPreserved:true,staleAgentPaintRefused:true,reloadWithoutReplay:true,initial,working,human,completed,task,requests:[...requests.values()],observations,errors};
 await writeFile(join(directory,'book-journey.json'),JSON.stringify(evidence,null,2));
 console.log('Real native book and original-chat handoff journey passed.');
}catch(error){
 if(page&&!page.isClosed()){await page.screenshot({path:join(directory,'book-failure.png')}).catch(()=>{});console.error(await page.locator('body').innerText().catch(()=>''));}
 await writeFile(join(directory,'book-failure.json'),JSON.stringify({error:String(error),errors,plannerCalls,verificationCalls,normalReplies,requests:[...requests.values()],observations,providerRequests},null,2));throw error;
}finally{
 releasePlan();
 try{await browser?.close();}finally{try{await server?.close();}finally{await channel.stop();}}
}
