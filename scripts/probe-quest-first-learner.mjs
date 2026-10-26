// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Offline production-entry composition check. The application owns onboarding,
// storage, language selection, chat dispatch, verification and artifact rendering.
// Only remote HTTP and triggered-TTS transport are scripted. No store seeding, component fixture or native
// room success is injected. This is not a device/provider/release-bundle test.
import {chromium} from 'playwright-core';
import {build, preview} from 'vite';
import {mkdir, readFile, writeFile} from 'node:fs/promises';
import {resolve, dirname} from 'node:path';
import {fileURLToPath} from 'node:url';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
import {firstLearnerSpeech} from './first-learner-speech.mjs';

const root = resolve(dirname(fileURLToPath(import.meta.url)), '..');
/**
 * @typedef {{body:any,schema:any,bodyText:string,systemText:string}} ScriptedRequest
 * @typedef {{kind:string,text:string}} ScriptedReply
 * @typedef {object} NativeFirstLearnerAdapter
 * @property {(page:import('playwright-core').Page,origin:string)=>Promise<void>} attach
 * @property {()=>Promise<void>} beforeFirstMessage
 * @property {(request:ScriptedRequest)=>Promise<ScriptedReply|null>} stream
 * @property {(bodyText:string)=>string|null} imagePurpose
 * @property {(input:{fakeKey:string,errors:string[],out:string})=>Promise<any>} [offlineLive]
 * @property {()=>Promise<any>} [offlineSpeechAssets]
 * @property {()=>Promise<void>} afterLesson
 * @property {()=>Promise<void>} beforeReload
 * @property {()=>Promise<void>} afterReload
 * @property {()=>Promise<void>} close
 * @property {()=>{nativeProbeId:string,qualified:boolean,[key:string]:unknown}} evidence
 */
/** @param {{out?:string,native?:NativeFirstLearnerAdapter}} options */
export async function runQuestFirstLearner(options={}) {
const nativeAdapter=options.native;
const out = resolve(options.out || process.env.MAESTRO_FIRST_LEARNER_EVIDENCE || resolve(root, '.quest-evidence/first-learner'));
await mkdir(resolve(out, 'empty-env'), {recursive:true});
const fakeKey = 'maestro-offline-first-learner-not-a-real-key';
const learner = "Hi! This is my very first lesson. I speak English and want to learn Spanish. I do not know any Spanish yet. Where do we start?";
const toolPrompt = 'A cozy Spanish cafe for a first lesson.';
const welcome = '¡Hola! Empecemos con una palabra fácil.\n[en] Hello! Let us start with one easy word.';
const card = '<!doctype html><html><body><main><h1>My first Spanish card</h1><button id="card" onclick="this.textContent=\'Hello\'">Hola</button></main></body></html>';
const requests = [], blocked = [], errors = [], consoleErrors = [], localFailures = [], unexpected = [];
const uploads = new Map();
const networkEvents=[], networkPending=new Map(), lifecycleEvents=[], networkSettlements=[], networkFailures=[], browserCacheVerifications=[];
const networkFailureRequests=new Map(),networkRequestIds=new WeakMap();
let networkReconciliations=[],browserCacheProof=null;
let networkGeneration=0,networkCleanup=false;
const pixel = 'iVBORw0KGgoAAAANSUhEUgAAAAEAAAABCAQAAAC1HAwCAAAAC0lEQVR42mP8/x8AAwMCAO+XAoYAAAAASUVORK5CYII=';
const modelRegistry = await readFile(resolve(root,'public/gemini-models.json'));
const pending = new Set();
let browser, server, page, status = 'failed';
const started = Date.now();
const scriptSha256=createHash('sha256').update(await readFile(fileURLToPath(import.meta.url))).digest('hex');
const cleanupErrors=[];
let speechGesture=null;
const speech=await firstLearnerSpeech(fakeKey,errors);
const live=await nativeAdapter?.offlineLive?.({fakeKey,errors,out});
const speechAssets=await nativeAdapter?.offlineSpeechAssets?.();
let stage = 'server';
const checkpoint = value => { stage = value; console.log(value); };
const snapshot = async () => page.evaluate(() => window.maestroBook?.snapshot());
let speechReadyObserved=false;
const reconcileNetworkFailures=()=>{
  networkReconciliations=networkFailures.filter(f=>!f.endedDuringCleanup).map(f=>{
    assert(f.status===200&&f.error==='net::ERR_ABORTED','An admitted HTTP request failed');
    const receipt=speechAssets.fullReceipt(networkFailureRequests.get(f.id));
    assert(receipt&&receipt.status===200&&receipt.range===null,'Failed request lacks exact sole-attempt full delivery');
    const source=networkEvents.find(e=>e.id===f.redirectedFromId);
    assert(source&&source.origin==='https://huggingface.co'&&source.path==='/onnx-community/whisper-tiny.en/resolve/main/'+receipt.path&&source.status===307&&source.state==='finished','Failed request lost its exact source redirect');
    const cached=browserCacheProof?.files.find(x=>x.path===receipt.path);
    assert(cached&&cached.bytes===receipt.bytes&&cached.sha256===receipt.sha256,'Failed request lacks verified browser-consumed bytes');
    return {...f,receipt,cacheVerifiedAt:networkReconciliations.find(r=>r.id===f.id)?.cacheVerifiedAt??browserCacheProof.verifiedAt,reason:'verified-browser-cache-after-full-delivery'};
  });
};
const waitForSettledNetwork=async()=>{
  if(!speechAssets){await page.waitForLoadState('networkidle',{timeout:15000});return;}
  const began=Date.now(),deadline=began+15000;let quietSince=null,quietGeneration=-1,lastPending=[];
  while(Date.now()<deadline){
    const workers=await page.evaluate(()=>globalThis.__firstLearnerSpeechWorkers());
    assert.equal(workers.version,1);assert.equal(workers.overflow,false);
    assert(workers.workers.every(w=>w.errors.length===0),'Actual speech worker reported an error');
    const ready=w=>w.ready.some(r=>r.model==='onnx-community/whisper-tiny.en'&&r.profile==='q4');
    speechReadyObserved ||= workers.workers.some(ready);
    const assets=speechAssets.evidence();assert.deepEqual(assets.errors,[]);assert.deepEqual(errors,[]);assert.deepEqual(unexpected,[]);
    const complete=assets.files.length===7&&assets.files.every(f=>assets.requests.some(r=>r.path===f.path&&r.status===200&&r.range===null&&r.bytes===f.bytes&&r.sha256===f.sha256));
    if(complete&&speechReadyObserved&&workers.workers.every(ready)){
      // The installed loader writes its fully consumed buffers to CacheStorage.
      // Independently hash those real bytes; never seed or fetch cache entries.
      if(!browserCacheProof){browserCacheProof=await speechAssets.readBrowserCache(page);browserCacheVerifications.push(browserCacheProof);}
      reconcileNetworkFailures();
    }
    const metadata=[];lastPending=[];
    for(const [request,event] of networkPending){
      const receipt=speechAssets.metadataReceipt(request);
      if(event.status===206&&receipt){
        assert.equal(receipt.range,'bytes=0-0');assert.equal(receipt.bytes,1);assert.equal(receipt.status,206);
        metadata.push({...event,receipt});
      }else lastPending.push({...event});
    }
    // The production loader intentionally leaves 206 bodies unread after using
    // their headers. Preserve these as pending; only successful same-request
    // first-byte fulfillment may be excluded. Every other HTTP request drains.
    if(complete&&speechReadyObserved&&workers.workers.every(ready)&&lastPending.length===0){
      if(quietGeneration!==networkGeneration){quietGeneration=networkGeneration;quietSince=Date.now();}
      if(Date.now()-quietSince>=500){
        const record={stage,at:Date.now(),observedPendingIds:[...networkPending.values()].map(e=>e.id),elapsedMs:Date.now()-began,quietMs:Date.now()-quietSince,networkGeneration,manifestSha256:assets.manifestSha256,fullFiles:assets.files.length,cacheVerifiedAt:browserCacheProof.verifiedAt,workers,pendingMetadata:metadata};
        const previous=networkSettlements.at(-1);
        if(previous?.stage===stage)networkSettlements[networkSettlements.length-1]={...record,earlierSettledAt:previous.at};
        else networkSettlements.push(record);
        return;
      }
    }else {quietSince=null;quietGeneration=-1;}
    await new Promise(resolve=>setTimeout(resolve,50));
  }
  throw new Error('Network/model settlement timed out; non-metadata requests still pending: '+JSON.stringify(lastPending));
};
try {
  await speechAssets?.start?.();
  // Do not load private .env files or inherit VITE_* access configuration.
  const config = {root, configFile:resolve(root,'vite.config.ts'),
    envDir:resolve(out,'empty-env'), envPrefix:'MAESTRO_OFFLINE_UNUSED_',
    build:{outDir:resolve(out,'web'), emptyOutDir:false}, logLevel:'warn'};
  await build(config);
  server = await preview({...config, preview:{host:'127.0.0.1',port:0}});
  const address = server.httpServer.address();
  assert(address && typeof address === 'object');
  const origin = 'http://127.0.0.1:' + address.port;
  browser = await chromium.launch({channel:'chrome', headless:true,...(live?{args:live.launchArgs}: {})});
  browser.on('disconnected',()=>lifecycleEvents.push({event:'browser-disconnected',stage,at:Date.now()}));
  const context = await browser.newContext({viewport:{width:1536,height:1024}, locale:'en-US'});
  context.on('close',()=>lifecycleEvents.push({event:'context-close',stage,at:Date.now()}));
  context.setDefaultTimeout(15000);
  // Include worker/model traffic and distinguish simultaneous identical URLs.
  // Record only request metadata, never credentials, queries or payloads.
  context.on('request',request=>{
    try{speechAssets?.observeRequest(request);}catch(error){errors.push('Speech asset request identity: '+String(error));}
    if(networkEvents.length>=5000){if(!errors.includes('HTTP evidence overflow'))errors.push('HTTP evidence overflow');return;}
    const url=new URL(request.url()),entry={id:networkEvents.length+1,stage,startedAt:Date.now(),method:request.method(),origin:url.origin,path:url.pathname,type:request.resourceType(),state:'pending'};
    const from=request.redirectedFrom();if(from)entry.redirectedFromId=networkRequestIds.get(from);
    networkRequestIds.set(request,entry.id);networkEvents.push(entry);networkPending.set(request,entry);networkGeneration++;
  });
  context.on('response',response=>{const entry=networkPending.get(response.request());if(entry){entry.status=response.status();networkGeneration++;}});
  const endRequest=(request,state)=>{
    const entry=networkPending.get(request);if(!entry)return;
    entry.state=state;entry.endedAt=Date.now();entry.endedStage=stage;entry.endedDuringCleanup=networkCleanup;networkGeneration++;
    if(state==='failed'){
      entry.error=request.failure()?.errorText;
      const fontDenied=entry.origin==='https://fonts.googleapis.com'&&entry.path==='/css2'&&['net::ERR_BLOCKED_BY_CLIENT','net::ERR_BLOCKED_BY_CLIENT.Inspector'].includes(entry.error);
      const placeholderAborted=entry.origin===origin&&entry.error==='net::ERR_ABORTED'&&(entry.path.startsWith('/animations/')||entry.path==='/artifact-loading-scene.svg');
      if(!fontDenied&&!placeholderAborted){networkFailures.push({...entry});networkFailureRequests.set(entry.id,request);}
    }
    networkPending.delete(request);
  };
  context.on('requestfinished',request=>endRequest(request,'finished'));
  context.on('requestfailed',request=>endRequest(request,'failed'));
  await live?.observeCapture(context);
  // Playwright's serviceWorkers:block probe reads navigator.serviceWorker in
  // opaque artifact frames and throws. Deny new registrations at the browser
  // boundary without accessing that forbidden getter; this context starts empty.
  await context.addInitScript(() => {
    try {
      const prototype = globalThis.ServiceWorkerContainer?.prototype;
      if(prototype) Object.defineProperty(prototype,'register',{configurable:true,value() {
        return Promise.reject(new Error('Offline regression blocks service worker registration'));
      }});
    } catch { /* opaque sandbox has no service-worker capability */ }
  });
  await context.routeWebSocket('**/*', ws => {
    // Triggered TTS is a bounded local SDK peer; every other socket is rejected.
    if(live?.handle(ws,speech))return;
    if(speech.handle(ws))return;
    const socketUrl=new URL(ws.url());
    const attempt={transport:'websocket',origin:socketUrl.origin,path:socketUrl.pathname,keyNames:[...socketUrl.searchParams.keys()],fakeKeyMatches:socketUrl.searchParams.get('key')===fakeKey};
    blocked.push(attempt); unexpected.push(attempt);
    ws.close();
  });
  await context.route('**/*', async route => {
    try {
    const request = route.request(), url = new URL(request.url());
    if (url.origin === origin) return route.continue();
    if(await speechAssets?.handle(route))return;
    if(url.hostname==='chatwithmaestro.com' && url.pathname==='/gemini-models.json' && request.method()==='GET') {
      return route.fulfill({contentType:'application/json',body:modelRegistry});
    }
    if(url.hostname==='generativelanguage.googleapis.com' && url.pathname==='/upload/v1beta/files' && request.method()==='POST') {
      assert.equal(request.headers()['x-goog-api-key'],fakeKey);
      assert.equal(request.headers()['x-goog-upload-command'],'start');
      const id=String(uploads.size+1), mimeType=request.headers()['x-goog-upload-header-content-type'];
      const size=Number(request.headers()['x-goog-upload-header-content-length']);
      const displayName=request.postDataJSON()?.file?.displayName;
      assert(size>0); assert(mimeType);
      uploads.set(id,{id,mimeType,size,displayName,complete:false}); requests.push({kind:'upload-start',id,mimeType,size,displayName});
      return route.fulfill({contentType:'application/json',headers:{'access-control-allow-origin':'*','access-control-expose-headers':'x-goog-upload-url','x-goog-upload-url':'https://generativelanguage.googleapis.com/offline-upload/'+id},body:'{}'});
    }
    if(url.hostname==='generativelanguage.googleapis.com' && url.pathname.startsWith('/offline-upload/') && request.method()==='POST') {
      const id=url.pathname.split('/').at(-1), upload=uploads.get(id);
      assert(upload&&!upload.complete); assert.equal(request.headers()['x-goog-upload-offset'],'0');
      assert.equal(request.headers()['x-goog-upload-command'],'upload, finalize');
      const bytes=request.postDataBuffer(); assert.equal(bytes?.length,upload.size);
      upload.complete=true; upload.file={name:'files/offline-'+id,uri:'https://generativelanguage.googleapis.com/v1beta/files/offline-'+id,mimeType:upload.mimeType,state:'ACTIVE'};
      requests.push({kind:'upload-complete',id,mimeType:upload.mimeType,displayName:upload.displayName,size:bytes.length,sha256:createHash('sha256').update(bytes).digest('hex')});
      return route.fulfill({contentType:'application/json',headers:{'access-control-allow-origin':'*','access-control-expose-headers':'x-goog-upload-status','x-goog-upload-status':'final'},body:JSON.stringify({file:upload.file})});
    }
    if(url.hostname==='generativelanguage.googleapis.com' && url.pathname.startsWith('/v1beta/files/offline-') && request.method()==='GET') {
      const upload=uploads.get(url.pathname.split('offline-').at(-1)); assert(upload?.complete);
      return route.fulfill({contentType:'application/json',body:JSON.stringify(upload.file)});
    }
    if(url.hostname==='generativelanguage.googleapis.com' && url.pathname.endsWith(':generateContent') && request.method()==='POST') {
      assert.equal(request.headers()['x-goog-api-key'],fakeKey);
      const body=request.postDataJSON(); assert.deepEqual(body.generationConfig?.responseModalities,['IMAGE']);
      const assistantTool=requests.some(r=>r.kind==='verifier');
      const bodyText=JSON.stringify(body);
      const nativePurpose=nativeAdapter?.imagePurpose(bodyText);
      const purpose=nativePurpose || (assistantTool?'assistant-tool':'user-default');
      if(!nativePurpose)assert(bodyText.includes(assistantTool?toolPrompt:learner));
      assert.equal(requests.filter(r=>r.kind==='image-generation'&&r.purpose===purpose).length,0);
      requests.push({kind:'image-generation',purpose,hasLearnerRequest:bodyText.includes(learner),hasToolPrompt:bodyText.includes(toolPrompt)});
      return route.fulfill({contentType:'application/json',body:JSON.stringify({candidates:[{content:{role:'model',parts:[{inlineData:{mimeType:'image/png',data:pixel}}]},finishReason:'STOP'}],modelVersion:'offline-image-boundary'})});
    }
    if (url.hostname === 'generativelanguage.googleapis.com' && url.pathname === '/v1beta/models' && request.method() === 'GET') {
      assert.equal(request.headers()['x-goog-api-key'], fakeKey);
      requests.push({kind:'key-validation'});
      return route.fulfill({contentType:'application/json', body:'{"models":[]}'});
    }
    if (url.hostname === 'generativelanguage.googleapis.com' && url.pathname.endsWith(':streamGenerateContent') && request.method() === 'POST') {
      assert.equal(request.headers()['x-goog-api-key'], fakeKey);
      const body = request.postDataJSON();
      const schema = body.generationConfig?.responseJsonSchema ?? body.generationConfig?.responseSchema;
      const bodyText = JSON.stringify(body);
      const systemText = JSON.stringify(body.systemInstruction || '');
      let text, kind;
      const nativeReply=await nativeAdapter?.stream({body,schema,bodyText,systemText});
      if(nativeReply) { ({text,kind}=nativeReply); }
      else if (schema?.properties?.suggestions) {
        kind = 'verifier';
        assert.equal(requests.filter(r=>r.kind==='tutor').length,1);
        assert.equal(requests.filter(r=>r.kind==='verifier').length,0);
        text = JSON.stringify({suggestions:[{target:'Hola.',native:'Hello.'}], reengagementSeconds:0,
          chatSummary:'A first Spanish lesson for an English speaker.',globalProfile:'',
          artifact:{mimeType:'text/html',fileName:'first-spanish-card.html',encoding:'text',content:card},toolRequest:{tool:'image',prompt:toolPrompt}});
      } else {
        assert(!schema?.properties?.commands, 'No native planner call is expected without native state');
        kind = 'tutor';
        assert.equal(requests.filter(r=>r.kind==='tutor').length,0);
        text = welcome;
      }
      requests.push({kind,hasLearnerRequest:bodyText.includes(learner),
        hasEnglish:systemText.includes('English'),hasSpanish:systemText.includes('Spanish'),
        bodySha256:createHash('sha256').update(bodyText).digest('hex')});
      const response = {candidates:[{content:{role:'model',parts:[{text}]},finishReason:'STOP'}],
        usageMetadata:{promptTokenCount:1,candidatesTokenCount:1,totalTokenCount:2},modelVersion:'offline-production-entry-script'};
      if(['tutor','native-tutor','native-result'].includes(kind))speech.allowText(text);
      return route.fulfill({contentType:'text/event-stream',body:'data: '+JSON.stringify(response)+'\n\n'});
    }
    const knownDenied = (url.hostname==='fonts.googleapis.com' && url.pathname==='/css2') ||
      (!speechAssets && url.hostname==='huggingface.co' && url.pathname.startsWith('/onnx-community/whisper-tiny.en/resolve/main/'));
    if(!knownDenied)unexpected.push({method:request.method(),origin:url.origin,path:url.pathname});
    blocked.push({transport:'http',method:request.method(),origin:url.origin,path:url.pathname});
    return route.abort('blockedbyclient');
    } catch(error) {
      errors.push('Scripted boundary: '+String(error.stack||error));
      await route.abort('failed').catch(()=>{});
    }
  });
  page = await context.newPage();
  page.on('crash',()=>lifecycleEvents.push({event:'page-crash',stage,at:Date.now()}));
  page.on('close',()=>lifecycleEvents.push({event:'page-close',stage,at:Date.now()}));
  page.on('request', r => {if(r.url().startsWith(origin)) pending.add(r.url());});
  page.on('requestfinished', r => pending.delete(r.url()));
  page.on('requestfailed', r => {pending.delete(r.url()); if(r.url().startsWith(origin)) localFailures.push({url:r.url(),error:r.failure()?.errorText});});
  page.on('response', r => {if(r.url().startsWith(origin)&&r.status()>=400)localFailures.push({url:r.url(),status:r.status()});});
  page.on('pageerror', error => errors.push(error.message));
  page.on('console', message => { if(message.type()==='error') consoleErrors.push(message.text()); });
  await nativeAdapter?.attach(page,origin);
  checkpoint('adult-gate');
  await page.goto(origin+'/?surface=quest-book',{waitUntil:'domcontentloaded',timeout:60000});
  await page.getByRole('checkbox',{name:'I am 18 years of age or older.'}).waitFor();
  assert.equal(requests.length,0,'No provider requests before explicit adult confirmation');
  assert.equal(await page.locator('textarea').count(),0);
  const open = page.getByRole('button',{name:'Open my Maestro book',exact:true});
  assert.equal(await open.isDisabled(),true);
  await page.getByRole('checkbox',{name:'I am 18 years of age or older.'}).check();
  await open.click();
  checkpoint('access');
  const keyInput = page.getByPlaceholder('Paste your API key here',{exact:true});
  await keyInput.waitFor();
  await keyInput.fill(fakeKey);
  await keyInput.press('Enter');
  await keyInput.waitFor({state:'hidden'});
  assert.equal(requests.filter(r=>r.kind==='key-validation').length,1);
  checkpoint('language');
  const native = page.getByRole('listbox',{name:'Native language',exact:true});
  await native.getByRole('option',{name:'English (US)',exact:true}).click();
  await page.getByRole('listbox',{name:'Target language',exact:true}).getByRole('option',{name:'Spanish (ES)',exact:true}).click();
  await page.getByRole('button',{name:'Confirm language selection',exact:true}).click();
  await page.getByTitle('Current Target Language: Spanish (ES)',{exact:true}).waitFor();
  await nativeAdapter?.beforeFirstMessage();
  checkpoint('first-message');
  const composer = page.locator('textarea').filter({visible:true});
  await composer.fill(learner);
  await page.getByRole('button',{name:'Send message',exact:true}).click();
  await page.getByText('Hello! Let us start with one easy word.',{exact:false}).first().waitFor();
  await page.waitForFunction(()=>window.maestroBook?.snapshot()?.historyTotal>=2);
  await page.getByRole('button',{name:/Resume and play: first-spanish-card/}).click();
  const frame = page.frameLocator('iframe[title*="first-spanish-card"]');
  await frame.getByRole('button',{name:'Hola',exact:true}).click();
  await frame.getByRole('button',{name:'Hello',exact:true}).waitFor();
  await page.waitForFunction(()=>Array.from(document.querySelectorAll('img[alt="Preview"]')).filter(img=>img.complete&&img.naturalWidth>0).length===2);
  assert.equal(requests.filter(r=>r.kind==='image-generation').length,2);
  assert.deepEqual(requests.filter(r=>r.kind==='image-generation').map(r=>r.purpose),['user-default','assistant-tool']);
  await waitForSettledNetwork();
  assert([...uploads.values()].every(u=>u.complete),'Every admitted upload completed');
  // Automatic reply speech may have already populated the audio cache. First
  // let it finish, then prove the learner's click starts a new speaking phase.
  // Cached playback is valid and need not create another provider socket.
  await speech.settled();
  await page.waitForFunction(()=>window.maestroBook?.snapshot().activity==='idle');
  const speechBeforeClick=speech.evidence().sessions.length;
  const firstLine=page.getByText(welcome.split('\n')[0],{exact:true}).locator('..');
  await Promise.all([
    page.waitForFunction(()=>window.maestroBook?.snapshot().activity==='speaking',null,{timeout:10000}),
    firstLine.click(),
  ]);
  await speech.settled();
  await page.waitForFunction(()=>window.maestroBook?.snapshot().activity==='idle');
  assert(speech.evidence().sessions.length>0,'No scripted TTS session supplied the lesson audio');
  speechGesture={activityBefore:'idle',activityObserved:'speaking',activityAfter:'idle',sessionsBefore:speechBeforeClick,sessionsAfter:speech.evidence().sessions.length};
  await page.screenshot({path:resolve(out,'first-lesson.png')});
  assert.equal(requests.filter(r=>r.kind==='tutor'&&r.hasLearnerRequest).length,1);
  assert.equal(requests.filter(r=>r.kind==='verifier'&&r.hasLearnerRequest).length,1);
  const tutor = requests.find(r=>r.kind==='tutor');
  assert.equal(tutor.hasEnglish,true); assert.equal(tutor.hasSpanish,true);
  if(nativeAdapter) { checkpoint('native-room'); await nativeAdapter.afterLesson(); }
  await waitForSettledNetwork();
  assert([...uploads.values()].every(u=>u.complete),'Every admitted upload completed before reload');
  const before = await snapshot();
  const previewCount=await page.locator('img[alt="Preview"]').count();
  const beforeReloadCount = requests.length;
  const beforeReloadSpeechCount = speech.evidence().sessions.length;
  await live?.settled();
  const beforeReloadLiveCount=live?.evidence().sessions.length??0;
  await speech.settled();
  await nativeAdapter?.beforeReload();
  checkpoint('reload');
  browserCacheProof=null;
  await page.reload({waitUntil:'domcontentloaded'});
  await page.getByRole('checkbox',{name:'I am 18 years of age or older.'}).waitFor();
  assert.equal(await page.locator('textarea').count(),0,'Reload requires fresh audience consent');
  assert.equal(requests.length,beforeReloadCount);
  await page.getByRole('checkbox',{name:'I am 18 years of age or older.'}).check();
  await page.getByRole('button',{name:'Open my Maestro book',exact:true}).click();
  await page.getByText(learner,{exact:true}).first().waitFor();
  await page.getByText('Hello! Let us start with one easy word.',{exact:false}).first().waitFor();
  await composer.waitFor();
  await page.getByTitle('Current Target Language: Spanish (ES)',{exact:true}).waitFor();
  await page.getByRole('button',{name:/Resume and play: first-spanish-card/}).click();
  await frame.getByRole('button',{name:'Hola',exact:true}).click();
  await frame.getByRole('button',{name:'Hello',exact:true}).waitFor();
  assert.equal(await keyInput.count(),0,'Saved test access restored without re-entering a key');
  assert.equal(await native.count(),0,'Selected language pair restored without first-run selector');
  await page.waitForFunction(count=>Array.from(document.querySelectorAll('img[alt="Preview"]')).filter(img=>img.complete&&img.naturalWidth>0).length===count,previewCount);
  await nativeAdapter?.afterReload();
  await waitForSettledNetwork();
  assert([...uploads.values()].every(u=>u.complete),'Every restored-session upload completed');
  const after = await snapshot();
  assert.equal(after.historyTotal,before.historyTotal);
  const restoredRequests=requests.slice(beforeReloadCount);
  // The current avatar service deliberately caches its overlay URI only in memory.
  // Rebuilding that exact overlay is maintenance, not replaying the lesson/tools.
  assert([0,2].includes(restoredRequests.length),'Only an optional avatar overlay refresh may recur');
  if(restoredRequests.length) {
    assert.deepEqual(restoredRequests.map(r=>r.kind),['upload-start','upload-complete']);
    assert(restoredRequests.every(r=>r.displayName==='maestro-avatar-overlay'));
    const firstOverlay=requests.slice(0,beforeReloadCount).find(r=>r.kind==='upload-complete'&&r.displayName==='maestro-avatar-overlay');
    assert(firstOverlay);
    assert.equal(restoredRequests[1].sha256,firstOverlay.sha256);
    assert.equal(restoredRequests[1].size,firstOverlay.size);
  }
  await speech.settled();
  assert.equal(speech.evidence().sessions.length,beforeReloadSpeechCount,'Reload must not replay triggered speech');
  await live?.settled();
  assert.equal(live?.evidence().sessions.length??0,beforeReloadLiveCount,'Reload must not replay Live');
  assert.deepEqual(errors,[]); assert.deepEqual(unexpected,[]);
  if(speechAssets)assert.deepEqual(speechAssets.evidence().errors,[]);
  assert.equal(context.serviceWorkers().length,0);
  // Transient image-loading artwork can be removed before its local fetch ends.
  // Only explicit UI placeholders/animation cancellations are expected; HTTP errors still fail.
  assert.deepEqual(localFailures.filter(f=>!(f.error==='net::ERR_ABORTED'&&(new URL(f.url).pathname.startsWith('/animations/')||new URL(f.url).pathname==='/artifact-loading-scene.svg'))),[]);
  assert(consoleErrors.every(message=>message.startsWith('Failed to load resource: net::ERR_BLOCKED_BY_CLIENT')));
  await page.screenshot({path:resolve(out,'restored-lesson.png')});
  // No awaited UI work follows final network qualification. Keep the latest
  // reload checkpoint, including the earlier settlement timestamp for audit.
  await waitForSettledNetwork();
  assert.equal(speech.evidence().sessions.length,beforeReloadSpeechCount,'Late triggered speech replay');
  assert.equal(live?.evidence().sessions.length??0,beforeReloadLiveCount,'Late Live replay');
  if(speechAssets)reconcileNetworkFailures();
  status='passed';
} catch(error) {
  errors.push(String(error.stack || error));
  console.error('Failed at '+stage+': '+error.message);
  console.error(JSON.stringify({pending:[...pending],localFailures,errors,consoleErrors,blocked}));
  await writeFile(resolve(out,'failure-network.json'),JSON.stringify({pending:[...pending],lifecycleEvents,networkSettlements,networkFailures,networkReconciliations,browserCacheVerifications,networkEvents,networkPending:[...networkPending.values()],localFailures,errors,consoleErrors,blocked},null,2));
  if(page) {
    await page.screenshot({path:resolve(out,'failure.png')}).catch(()=>{});
    await writeFile(resolve(out,'failure-dom.txt'),await page.locator('body').innerText().catch(()=>'')).catch(()=>{});
  }
} finally {
  networkCleanup=true;
  try { await nativeAdapter?.close(); } catch(error) { cleanupErrors.push('native: '+String(error)); }
  try { await browser?.close(); } catch(error) { cleanupErrors.push('browser: '+String(error)); }
  try { await speechAssets?.close?.(); } catch(error) { cleanupErrors.push('speech-assets: '+String(error)); }
  if(server) {
    try {
      server.httpServer.closeAllConnections();
      await new Promise((resolve,reject)=>server.httpServer.close(error=>error?reject(error):resolve()));
    } catch(error) { cleanupErrors.push('server: '+String(error)); }
  }
  if(cleanupErrors.length||errors.length||unexpected.length||speechAssets?.evidence().errors.length)status='failed';
  await writeFile(resolve(out,'result.json'),JSON.stringify({status,stage,durationMs:Date.now()-started,scriptSha256,cleanupErrors,
    entry:'index.html -> index.tsx -> QuestAudienceGate -> QuestBookSurface -> App',
    productionComposition:true,productionBundle:true,nativeExecution:status==='passed'&&nativeAdapter?.evidence().qualified===true,physicalDevice:false,
    ...(nativeAdapter?{nativeProbeId:nativeAdapter.evidence().nativeProbeId,native:nativeAdapter.evidence()}:{}),
    realProvider:false,scriptedByokBoundary:true,storeSeeding:false,
    unqualified:[...(nativeAdapter?['Android WebView/native speech','real scanned room']:['native room actions']),'managed access','real provider quality',...(live?['physical microphone/camera capture','Live speech quality']:['Live/media capture']),...(speechAssets?['speech recognition quality (synthetic microphone)']:['speech recognition (remote model download denied)']),'image quality (one-pixel scripted response)'],
    speech:speech.evidence(),...(speechAssets?{speechAssets:speechAssets.evidence()}:{}),...(live?{live:live.evidence()}:{}),speechGesture,requests,blocked,unexpected,errors,consoleErrors,localFailures,pending:[...pending],lifecycleEvents,networkSettlements,networkFailures,networkReconciliations,browserCacheVerifications,networkEvents,networkPending:[...networkPending.values()]},null,2)+'\n');
}
if(status!=='passed') throw new Error('Production-entry learner regression failed at '+stage+'; see '+out);
console.log('Production-entry first learner passed with scripted network only: '+out);

}

if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url))await runQuestFirstLearner();
