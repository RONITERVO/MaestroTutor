// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Tiny local file contracts only; no model inference, browser or network.
import {afterEach,describe,expect,it,vi} from 'vitest';
import {createHash,webcrypto} from 'node:crypto';
import {get as httpGet} from 'node:http';
import {connect} from 'node:net';
import {mkdtemp,mkdir,readFile,writeFile,rm,symlink} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {dirname,join,resolve} from 'node:path';
import {createContext,runInContext} from 'node:vm';
import {installFirstLearnerSpeechObserver,loadFirstLearnerSpeechAssets} from './first-learner-speech-assets.mjs';

const revision='2575352d61be1bf7225cf8f8b268a4678025fc58';
const modelId='onnx-community/whisper-tiny.en';
const origin=`https://huggingface.co/${modelId}/resolve/`;
const paths=['config.json','generation_config.json','tokenizer.json','tokenizer_config.json',
  'preprocessor_config.json','onnx/encoder_model_q4.onnx','onnx/decoder_model_merged_q4.onnx'];
const metadataPath='onnx/encoder_model_quantized.onnx';
const digest=bytes=>createHash('sha256').update(bytes).digest('hex');
const roots=[],startedAssets=[];
async function fixture() {
  const root=await mkdtemp(join(tmpdir(),'maestro-speech-assets-'));roots.push(root);
  const folder=join(root,'assets'),manifestPath=join(root,'manifest.json');
  await mkdir(join(folder,'onnx'),{recursive:true});
  const files=[];
  for(const path of paths) {
    const body=Buffer.from('Real boundary test bytes for '+path);
    await writeFile(join(folder,path),body);
    files.push({path,url:origin+revision+'/'+path,size:body.length,sha256:digest(body),contentType:path.endsWith('.json')?'application/json':'application/octet-stream'});
  }
  const manifest={version:1,modelId,revision,loader:{package:'@huggingface/transformers',version:'4.3.0'},
    files,totalBytes:files.reduce((sum,file)=>sum+file.size,0),missingFiles:[]};
  const save=()=>writeFile(manifestPath,JSON.stringify(manifest));await save();
  return {root,folder,manifestPath,manifest,save,load:()=>loadFirstLearnerSpeechAssets(folder,manifestPath)};
}
async function addMetadata(f) {
  const body=Buffer.from([8,42,63,97,21]);
  const entry={path:metadataPath,url:origin+revision+'/'+metadataPath,size:body.length,sha256:digest(body),
    firstByteSha256:digest(body.subarray(0,1)),contentType:'application/octet-stream'};
  f.manifest.metadataFiles=[entry];f.manifest.metadataBytes=body.length;
  await writeFile(join(f.folder,metadataPath),body);await f.save();return {body,entry};
}
function route(path='config.json',options={}) {
  const url=options.url??origin+'main/'+path;
  const request={url:()=>url,method:()=>options.method??'GET',headers:()=>options.headers??{},redirectedFrom:()=>options.redirectedFrom??null};
  return {request:()=>request,fulfill:vi.fn(async()=>{}),continue:vi.fn(async()=>{})};
}
afterEach(async()=>{
  for(const assets of startedAssets.splice(0))await assets.close();
  for(const root of roots.splice(0)) {
    expect(dirname(resolve(root))).toBe(resolve(tmpdir()));expect(root.split(/[\\/]/).at(-1)).toMatch(/^maestro-speech-assets-/);
    await rm(root,{recursive:true,force:true});
  }
});

function workerContext(top=true) {
  const created=[];
  class NativeWorker {
    static marker='native-static';
    constructor(...args){this.args=args;this.listeners=new Map();this.sent=[];created.push(this);}
    addEventListener(type,listener){const list=this.listeners.get(type)??[];list.push(listener);this.listeners.set(type,list);}
    postMessage(...args){this.sent.push(args);}
    terminate(){this.terminated=true;}
    emit(type,event){for(const listener of this.listeners.get(type)??[])listener(event);this['on'+type]?.(event);}
  }
  const context=createContext({Worker:NativeWorker,URL,location:{origin:'http://127.0.0.1:5178',href:'http://127.0.0.1:5178/'}});
  runInContext(top?'top=globalThis':'top={}',context);
  runInContext('('+installFirstLearnerSpeechObserver.toString()+')()',context);
  return {context,NativeWorker,created,snapshot:()=>context.__firstLearnerSpeechWorkers()};
}
const workerUrl='/assets/observerWhisper.worker-Abc_123.js';

async function streamTicket(assets,path='config.json') {
  const source=route(path);await assets.handle(source);
  const url=source.fulfill.mock.calls[0][0].headers.location;
  const local=route('',{url,redirectedFrom:source.request()});assets.observeRequest(local.request());await assets.handle(local);
  return {source,local,url};
}
function cachePage(f,alter=(_path,response)=>response) {
  const match=vi.fn(async(url,options)=>{
    expect(options).toEqual({cacheName:'transformers-cache'});
    const path=paths.find(path=>url===origin+'main/'+path);expect(path).toBeDefined();
    return alter(path,new Response(await readFile(join(f.folder,path))));
  });
  const forbidden=vi.fn(()=>{throw new Error('Cache inspection cannot write, open or fetch');});
  const context=createContext({caches:{match,open:forbidden,put:forbidden,delete:forbidden},fetch:forbidden,
    Uint8Array,crypto:webcrypto,setTimeout,clearTimeout});
  const page={evaluate:async(fn,params)=>{context.__params=params;return JSON.parse(JSON.stringify(await runInContext('('+fn.toString()+')(__params)',context)));}};
  return {page,match,forbidden};
}

describe('exact full model delivery and browser-owned cache proof',()=>{
  it.each(['before','after'])('accepts an exact passive redirected request observed %s HTTP finish without a user route',async order=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const source=route();await assets.handle(source);const url=source.fulfill.mock.calls[0][0].headers.location;
    const local=route('',{url,redirectedFrom:source.request()});
    expect(assets.observeRequest(source.request())).toBe(false);
    if(order==='before')expect(assets.observeRequest(local.request())).toBe(true);
    expect(assets.fullReceipt(local.request())).toBeNull();
    await (await fetch(url)).arrayBuffer();
    if(order==='after'){expect(assets.fullReceipt(local.request())).toBeNull();expect(assets.observeRequest(local.request())).toBe(true);}
    expect(assets.fullReceipt(local.request())).toEqual(assets.evidence().requests[0]);
    expect(local.continue).not.toHaveBeenCalled();expect(local.fulfill).not.toHaveBeenCalled();
    expect(assets.evidence().errors).toEqual([]);
  });
  it.each(['wrong source','same request twice','different request'])('rejects passive observation with %s',async kind=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const source=route();await assets.handle(source);const url=source.fulfill.mock.calls[0][0].headers.location;
    const local=route('',{url,redirectedFrom:source.request()});
    if(kind!=='wrong source')assets.observeRequest(local.request());
    const other=kind==='same request twice'?local:route('',{url,redirectedFrom:kind==='wrong source'?route().request():source.request()});
    expect(()=>assets.observeRequest(other.request())).toThrow(/claim|source redirect/);
    expect(assets.fullReceipt(local.request())).toBeNull();expect(assets.fullReceipt(other.request())).toBeNull();
    expect(assets.evidence().errors).toHaveLength(1);
  });
  it('waits for successful redirect, continuation and HTTP finish before issuing a cloned identity receipt',async()=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    let redirectEntered,releaseRedirect,continueEntered,releaseContinue;
    const redirectStarted=new Promise(done=>{redirectEntered=done;}),redirectWait=new Promise(done=>{releaseRedirect=done;});
    const continueStarted=new Promise(done=>{continueEntered=done;}),continueWait=new Promise(done=>{releaseContinue=done;});
    const source=route();source.fulfill.mockImplementation(async()=>{redirectEntered();await redirectWait;});
    const redirecting=assets.handle(source);await redirectStarted;
    const url=source.fulfill.mock.calls[0][0].headers.location,local=route('',{url,redirectedFrom:source.request()});
    local.continue.mockImplementation(async()=>{continueEntered();await continueWait;});
    assets.observeRequest(local.request());const continuing=assets.handle(local);await continueStarted;
    expect(assets.fullReceipt(local.request())).toBeNull();
    await (await fetch(url)).arrayBuffer();expect(assets.evidence().requests).toHaveLength(1);
    expect(assets.fullReceipt(local.request())).toBeNull();
    releaseRedirect();await redirecting;expect(assets.fullReceipt(local.request())).toBeNull();
    releaseContinue();await continuing;
    const expected=assets.evidence().requests[0],receipt=assets.fullReceipt(local.request());expect(receipt).toEqual(expected);
    receipt.sha256='changed';expect(assets.fullReceipt(local.request())).toEqual(expected);
    expect(assets.fullReceipt(source.request())).toBeNull();
    expect(assets.fullReceipt(route('',{url,redirectedFrom:source.request()}).request())).toBeNull();
  });
  it('invalidates the first receipt as soon as a second full attempt for that path is admitted',async()=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const first=await streamTicket(assets);await (await fetch(first.url)).arrayBuffer();
    expect(assets.fullReceipt(first.local.request())).not.toBeNull();
    const second=await streamTicket(assets);expect(second.url).not.toBe(first.url);
    expect(assets.fullReceipt(first.local.request())).toBeNull();
    await (await fetch(second.url)).arrayBuffer();expect(assets.fullReceipt(second.local.request())).toBeNull();
    expect(assets.evidence().fullAttempts).toEqual([{path:'config.json',count:2}]);
    expect(assets.evidence().requests).toHaveLength(2);expect(assets.evidence().errors).toEqual([]);
  });
  it.each(['wrong source','second claim'])('rejects %s even for an otherwise identical loopback URL',async kind=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const source=route();await assets.handle(source);const url=source.fulfill.mock.calls[0][0].headers.location;
    const original=route('',{url,redirectedFrom:source.request()});
    if(kind==='second claim'){assets.observeRequest(original.request());await assets.handle(original);await (await fetch(url)).arrayBuffer();expect(assets.fullReceipt(original.request())).not.toBeNull();}
    const other=route('',{url,redirectedFrom:kind==='wrong source'?route().request():source.request()});
    await expect(assets.handle(other)).rejects.toThrow('exact source redirect once');
    expect(other.continue).not.toHaveBeenCalled();expect(assets.fullReceipt(other.request())).toBeNull();
    expect(assets.fullReceipt(original.request())).toBeNull();expect(assets.evidence().errors).toHaveLength(1);
  });
  it('serves each claimed HTTP ticket once and invalidates a receipt when the URL is replayed',async()=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const ticket=await streamTicket(assets);await (await fetch(ticket.url)).arrayBuffer();
    expect(assets.fullReceipt(ticket.local.request())).not.toBeNull();
    const replay=await fetch(ticket.url);expect(replay.status).toBe(404);await replay.arrayBuffer();
    expect(assets.fullReceipt(ticket.local.request())).toBeNull();expect(assets.evidence().requests).toEqual([]);
  });
  it.each(['redirect','continuation'])('fails closed if %s rejects after the server has finished its bytes',async kind=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const source=route();let release,entered;
    const started=new Promise(done=>{entered=done;}),wait=new Promise(done=>{release=done;});
    if(kind==='redirect')source.fulfill.mockImplementation(async()=>{entered();await wait;throw new Error('redirect failed');});
    const redirecting=assets.handle(source);if(kind==='redirect')await started;else await redirecting;
    const url=source.fulfill.mock.calls[0][0].headers.location,local=route('',{url,redirectedFrom:source.request()});
    if(kind==='continuation')local.continue.mockImplementation(async()=>{entered();await wait;throw new Error('continuation failed');});
    assets.observeRequest(local.request());const continuing=assets.handle(local);if(kind==='continuation')await started;else await continuing;
    await (await fetch(url)).arrayBuffer();expect(assets.evidence().requests).toHaveLength(1);
    expect(assets.fullReceipt(local.request())).toBeNull();release();
    await expect(kind==='redirect'?redirecting:continuing).rejects.toThrow(kind+' failed');
    expect(assets.fullReceipt(local.request())).toBeNull();expect(assets.evidence().requests).toEqual([]);expect(assets.evidence().errors).toHaveLength(1);
  });
  it('invalidates an already finished receipt on a later error on that same HTTP socket',async()=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const ticket=await streamTicket(assets),url=new URL(ticket.url),socket=connect(Number(url.port),url.hostname);
    socket.on('error',()=>{});
    try {
      await new Promise((accept,reject)=>{socket.once('connect',accept);socket.once('error',reject);});
      socket.write(`GET ${url.pathname} HTTP/1.1\r\nHost: ${url.host}\r\nConnection: keep-alive\r\n\r\n`);
      await new Promise((accept,reject)=>{socket.once('data',accept);socket.once('error',reject);});
      await vi.waitFor(()=>expect(assets.fullReceipt(ticket.local.request())).not.toBeNull());
      socket.write('INVALID HTTP\r\n\r\n');
      await vi.waitFor(()=>expect(assets.evidence().errors.length).toBeGreaterThan(0));
      expect(assets.fullReceipt(ticket.local.request())).toBeNull();expect(assets.evidence().requests).toEqual([]);
    } finally {socket.destroy();}
  });
  it('reads and hashes only the seven exact existing browser cache keys without opening or writing cache state',async()=>{
    const f=await fixture(),assets=await f.load(),{page,match,forbidden}=cachePage(f);
    const proof=await assets.readBrowserCache(page);
    expect(proof).toMatchObject({cacheName:'transformers-cache',modelId,manifestSha256:assets.evidence().manifestSha256,files:assets.evidence().files});
    expect(proof.verifiedAt).toBeGreaterThan(0);expect(match).toHaveBeenCalledTimes(7);expect(forbidden).not.toHaveBeenCalled();
    expect(match.mock.calls.map(([url])=>url)).toEqual(paths.map(path=>origin+'main/'+path));
    expect(assets.evidence().requests).toEqual([]);
  });
  it.each(['absent','status','truncated','changed','overlong','excess reads'])('refuses browser cache %s without a proof or additional file reads',async kind=>{
    const f=await fixture(),assets=await f.load(),first=await readFile(join(f.folder,paths[0]));let canceled=false;
    const {page,match,forbidden}=cachePage(f,()=>{
      if(kind==='absent')return undefined;
      if(kind==='status')return new Response(first,{status:206});
      if(kind==='truncated')return new Response(first.subarray(1));
      if(kind==='changed'){const changed=Buffer.from(first);changed[0]^=1;return new Response(changed);}
      if(kind==='overlong')return new Response(Buffer.concat([first,Buffer.from([1])]));
      return new Response(new ReadableStream({pull(controller){controller.enqueue(new Uint8Array());},cancel(){canceled=true;}}));
    });
    await expect(assets.readBrowserCache(page)).rejects.toThrow();expect(match).toHaveBeenCalledOnce();expect(forbidden).not.toHaveBeenCalled();
    if(kind==='excess reads')expect(canceled).toBe(true);
  });
  it('cancels a stalled cache response at the verification deadline without reading another entry',async()=>{
    const f=await fixture(),assets=await f.load();let canceled=false;
    vi.useFakeTimers();
    try {
      const {page,match}=cachePage(f);
      match.mockImplementation(async()=>new Response(new ReadableStream({cancel(){canceled=true;}})));
      const pending=expect(assets.readBrowserCache(page)).rejects.toThrow('timed out');
      await vi.advanceTimersByTimeAsync(30001);await pending;
      expect(canceled).toBe(true);expect(match).toHaveBeenCalledOnce();
    } finally {vi.useRealTimers();}
  });
});
describe('passive real speech worker observer',()=>{
  it('forwards native constructor arguments, original instances and outgoing messages untouched',()=>{
    const {context,NativeWorker,created,snapshot}=workerContext(),options={type:'module',name:'original'},url=new URL(workerUrl,context.location.origin);
    const worker=new context.Worker(url,options);
    expect(worker).toBe(created[0]);expect(worker).toBeInstanceOf(NativeWorker);expect(Object.getPrototypeOf(worker)).toBe(NativeWorker.prototype);
    expect(worker.args[0]).toBe(url);expect(worker.args[1]).toBe(options);expect(context.Worker.marker).toBe('native-static');
    const input={kind:'transcribe',audio:new ArrayBuffer(8)},transfer=[input.audio];worker.postMessage(input,transfer);
    expect(worker.sent[0][0]).toBe(input);expect(worker.sent[0][1]).toBe(transfer);
    worker.terminate();expect(worker.terminated).toBe(true);expect(snapshot().workers).toHaveLength(1);
    expect(()=>context.Worker(workerUrl)).toThrow();
  });
  it('preserves subclass construction and application message listeners',()=>{
    const {context,NativeWorker}=workerContext();class ExtendedWorker extends context.Worker {custom=true;}
    const worker=new ExtendedWorker(workerUrl,{type:'module'}),received=[];
    expect(worker).toBeInstanceOf(ExtendedWorker);expect(worker).toBeInstanceOf(NativeWorker);expect(worker.custom).toBe(true);
    worker.onmessage=event=>received.push(event);
    const event={data:{kind:'ready',model:modelId,profile:'q4'}};worker.emit('message',event);
    expect(received).toEqual([event]);expect(received[0]).toBe(event);
  });
  it('records authentic ready and inference metadata without retaining transcripts or changing messages',()=>{
    const {context,snapshot}=workerContext(),worker=new context.Worker(workerUrl);
    worker.emit('message',{data:{kind:'loading',file:'weights',progress:50}});
    expect(snapshot().workers[0].ready).toEqual([]);
    worker.emit('message',{data:{kind:'ready',model:modelId,profile:'q4'}});
    worker.emit('message',{data:{kind:'result',requestId:1,text:'private recognized words',elapsedMs:23}});
    worker.emit('message',{data:{kind:'result',requestId:2,text:'',elapsedMs:17}});
    const evidence=snapshot();expect(evidence.overflow).toBe(false);
    expect(evidence.workers[0]).toMatchObject({ready:[{model:modelId,profile:'q4'}],errors:[],results:{count:2,totalElapsedMs:40,maxElapsedMs:23}});
    expect(JSON.stringify(evidence)).not.toContain('private recognized words');
    evidence.workers[0].ready[0].profile='changed';expect(snapshot().workers[0].ready[0].profile).toBe('q4');
  });
  it('records model failures, worker errors and invalid readiness/results as qualification failures',()=>{
    const {context,snapshot}=workerContext(),worker=new context.Worker(workerUrl);
    worker.emit('message',{data:{kind:'ready',model:modelId,profile:'fp32'}});
    worker.emit('message',{data:{kind:'error',message:'weights failed'}});
    worker.emit('error',{message:'worker crashed'});
    worker.emit('message',{data:{kind:'result',requestId:3,text:'words',elapsedMs:NaN}});
    expect(snapshot().workers[0].ready).toEqual([]);expect(snapshot().workers[0].results.count).toBe(0);
    expect(snapshot().workers[0].errors.map(e=>e.kind)).toEqual(['protocol-error','model-error','worker-error','protocol-error']);
  });
  it('leaves other origins, worker names and URL suffixes unobserved',()=>{
    const {context,snapshot,created}=workerContext();
    for(const url of ['/assets/other.worker-Abc.js','https://other.test'+workerUrl,workerUrl+'?changed=1',workerUrl+'#fragment','blob:http://127.0.0.1:5178/worker'])new context.Worker(url);
    expect(created).toHaveLength(5);expect(snapshot().workers).toEqual([]);expect(created.every(w=>w.listeners.size===0)).toBe(true);
  });
  it('does not alter a child frame Worker constructor',()=>{
    const {context,NativeWorker}=workerContext(false);expect(context.Worker).toBe(NativeWorker);expect(context.__firstLearnerSpeechWorkers).toBeUndefined();
  });
  it('bounds observation and marks overflow rather than silently dropping failures',()=>{
    const {context,snapshot,created}=workerContext(),worker=new context.Worker(workerUrl);
    for(let i=0;i<33;i++)worker.emit('error',{message:'failure '+i});
    expect(snapshot().workers[0].errors).toHaveLength(32);expect(snapshot().overflow).toBe(true);
    for(let i=0;i<4;i++)new context.Worker(workerUrl);
    expect(created).toHaveLength(5);expect(snapshot().workers).toHaveLength(4);expect(snapshot().overflow).toBe(true);
  });
});

describe('offline real speech asset boundary',()=>{
  it('issues an immutable request-identity metadata receipt only after206 fulfillment succeeds',async()=>{
    const f=await fixture(),assets=await f.load(),r=route('config.json',{headers:{range:'bytes=0-0'}});
    let release,entered;const started=new Promise(done=>{entered=done;}),wait=new Promise(done=>{release=done;});
    r.fulfill.mockImplementation(async()=>{entered();await wait;});
    const processing=assets.handle(r);await started;
    expect(assets.metadataReceipt(r.request())).toBeNull();expect(assets.evidence().requests).toEqual([]);
    release();await processing;
    const expected={path:'config.json',method:'GET',range:'bytes=0-0',status:206,bytes:1,
      sha256:digest((await readFile(join(f.folder,'config.json'))).subarray(0,1))};
    const receipt=assets.metadataReceipt(r.request());expect(receipt).toEqual(expected);
    receipt.sha256='changed';expect(assets.metadataReceipt(r.request())).toEqual(expected);
    expect(assets.metadataReceipt(route('config.json',{headers:{range:'bytes=0-0'}}).request())).toBeNull();
  });
  it('does not issue metadata receipts or completed rows for a rejected fulfillment',async()=>{
    const f=await fixture(),assets=await f.load(),r=route('tokenizer.json',{headers:{range:'bytes=0-0'}});
    r.fulfill.mockRejectedValue(new Error('Simulated closed route'));
    await expect(assets.handle(r)).rejects.toThrow('Simulated closed route');
    expect(assets.metadataReceipt(r.request())).toBeNull();expect(assets.evidence().requests).toEqual([]);
  });
  it('never grants metadata receipts for full downloads, redirects or declared404 responses',async()=>{
    const f=await fixture();f.manifest.missingFiles=[{path:'processor_config.json',url:origin+revision+'/processor_config.json',status:404}];await f.save();
    const assets=await f.load(),full=route(),missing=route('processor_config.json',{headers:{range:'bytes=0-0'}});
    await assets.handle(full);await assets.handle(missing);
    expect(assets.metadataReceipt(full.request())).toBeNull();expect(assets.metadataReceipt(missing.request())).toBeNull();
    startedAssets.push(assets);await assets.start();const redirected=route('tokenizer.json');await assets.handle(redirected);
    expect(redirected.fulfill.mock.calls[0][0].status).toBe(307);expect(assets.metadataReceipt(redirected.request())).toBeNull();
  });
  it('streams immutable verified model bytes through its own exact loopback endpoint',async()=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);
    const first=await assets.start();expect(await assets.start()).toEqual(first);
    expect(first.origin).toMatch(/^http:\/\/127\.0\.0\.1:\d+$/);
    const r=route();await assets.handle(r);
    const redirect=r.fulfill.mock.calls[0][0];expect(redirect.status).toBe(307);expect(redirect.body).toBe('');
    expect(new URL(redirect.headers.location).origin).toBe(first.origin);
    expect(new URL(redirect.headers.location).pathname).toMatch(/^\/[a-f0-9-]+\/config\.json$/);
    expect(assets.evidence().requests).toEqual([]);
    const local=route('',{url:redirect.headers.location,redirectedFrom:r.request()});assets.observeRequest(local.request());await assets.handle(local);
    expect(local.continue).toHaveBeenCalledOnce();expect(local.fulfill).not.toHaveBeenCalled();
    const original=await readFile(join(f.folder,'config.json'));await writeFile(join(f.folder,'config.json'),'changed after preflight');
    const response=await fetch(redirect.headers.location),body=Buffer.from(await response.arrayBuffer());
    expect(response.status).toBe(200);expect(body).toEqual(original);
    expect(response.headers.get('content-length')).toBe(String(original.length));expect(response.headers.get('access-control-allow-origin')).toBe('*');
    expect(assets.evidence().requests).toEqual([{path:'config.json',method:'GET',range:null,status:200,bytes:original.length,sha256:digest(original)}]);
    expect(assets.fullReceipt(local.request())).toEqual(assets.evidence().requests[0]);
    expect(assets.fullReceipt(r.request())).toBeNull();
    expect(assets.evidence().errors).toEqual([]);
    await assets.close();await assets.close();
    await expect(fetch(redirect.headers.location)).rejects.toThrow();
    await expect(assets.handle(route())).rejects.toThrow('closed');expect(()=>assets.start()).toThrow('closed');
  });
  it('keeps first-byte ranges direct and exposes no full quantized loopback endpoint',async()=>{
    const f=await fixture(),{entry}=await addMetadata(f),assets=await f.load();startedAssets.push(assets);await assets.start();
    const metadata=route(metadataPath,{headers:{range:'bytes=0-0'}});await assets.handle(metadata);
    expect(metadata.fulfill.mock.calls[0][0]).toMatchObject({status:206,body:Buffer.from([8])});
    const whole=route(metadataPath);await expect(assets.handle(whole)).rejects.toThrow('Metadata-only');expect(whole.fulfill).not.toHaveBeenCalled();
    const r=route();await assets.handle(r);const known=r.fulfill.mock.calls[0][0].headers.location;
    const invalid=known.replace(/config\.json$/,metadataPath);
    await expect(assets.handle(route('',{url:invalid}))).rejects.toThrow('Unmanifested loopback');
    const rejected=await fetch(invalid);expect(rejected.status).toBe(404);expect((await rejected.arrayBuffer()).byteLength).toBe(0);
    expect(assets.evidence().requests).toEqual([{path:metadataPath,method:'GET',range:'bytes=0-0',status:206,bytes:1,sha256:entry.firstByteSha256}]);
    expect(assets.evidence().errors).toHaveLength(2);
  });
  it('continues only exact admitted loopback GET requests and records unexpected paths or methods',async()=>{
    const f=await fixture(),assets=await f.load();startedAssets.push(assets);await assets.start();
    const r=route();await assets.handle(r);const url=r.fulfill.mock.calls[0][0].headers.location;
    for(const options of [{url:url+'?x=1'},{url:url+'#fragment'},{url,method:'POST'},{url,headers:{range:'bytes=0-0'}},
      {url:url.replace('127.0.0.1','user:secret@127.0.0.1')},{url,headers:{authorization:'secret'}},{url,headers:{cookie:'secret'}}]) {
      const rejected=route('',options);await expect(assets.handle(rejected)).rejects.toThrow();expect(rejected.continue).not.toHaveBeenCalled();
    }
    const external=route('',{url:'http://127.0.0.1:1/unknown'});expect(await assets.handle(external)).toBe(false);expect(external.continue).not.toHaveBeenCalled();
    const response=await fetch(url,{method:'POST'});expect(response.status).toBe(404);await response.arrayBuffer();
    expect(assets.evidence().requests).toEqual([]);expect(assets.evidence().errors).toHaveLength(8);
  });
  it('fails qualification if an admitted full HTTP response is aborted',async()=>{
    const f=await fixture(),entry=f.manifest.files[6],body=Buffer.alloc(16*1024*1024,23);
    f.manifest.totalBytes+=body.length-entry.size;entry.size=body.length;entry.sha256=digest(body);
    await writeFile(join(f.folder,entry.path),body);await f.save();
    const assets=await f.load();startedAssets.push(assets);await assets.start();
    const r=route(entry.path);await assets.handle(r);const url=r.fulfill.mock.calls[0][0].headers.location;
    const local=route('',{url,redirectedFrom:r.request()});assets.observeRequest(local.request());await assets.handle(local);
    await new Promise((accept,reject)=>{
      const request=httpGet(url,response=>{response.once('data',()=>{response.destroy();accept();});response.once('error',()=>{});});request.once('error',reject);
    });
    await vi.waitFor(()=>expect(assets.evidence().errors.some(error=>/aborted before completion|ECONNRESET/.test(error))).toBe(true));
    expect(assets.evidence().requests).toEqual([]);
    expect(assets.fullReceipt(local.request())).toBeNull();
    await assets.close();
  });
  it('verifies the complete bundle and serves exact full bytes with manifest provenance',async()=>{
    const f=await fixture(),assets=await f.load(),r=route();
    expect(await assets.handle(r)).toBe(true);
    const response=r.fulfill.mock.calls[0][0];
    expect(response.status).toBe(200);expect(response.body).toEqual(await readFile(join(f.folder,'config.json')));
    expect(response.headers['content-length']).toBe(String(f.manifest.files[0].size));
    expect(assets.evidence()).toMatchObject({modelId,revision,totalBytes:f.manifest.totalBytes,manifestSha256:digest(await readFile(f.manifestPath))});
    expect(assets.evidence().files).toHaveLength(7);expect(assets.evidence().requests[0].sha256).toBe(f.manifest.files[0].sha256);
  });
  it('answers the actual loader first-byte metadata range with exact total size',async()=>{
    const f=await fixture(),assets=await f.load(),r=route('onnx/encoder_model_q4.onnx',{headers:{range:'bytes=0-0'}});
    await assets.handle(r);const response=r.fulfill.mock.calls[0][0],entry=f.manifest.files[5];
    expect(response.status).toBe(206);expect(response.body).toEqual((await readFile(join(f.folder,entry.path))).subarray(0,1));
    expect(response.headers).toMatchObject({'content-range':`bytes 0-0/${entry.size}`,'content-length':'1'});
  });
  it('verifies the extra metadata source in full but serves only its authentic first byte',async()=>{
    const f=await fixture(),{body,entry}=await addMetadata(f),assets=await f.load();
    const r=route(metadataPath,{headers:{range:'bytes=0-0'}});await assets.handle(r);
    expect(r.fulfill.mock.calls[0][0]).toMatchObject({status:206,body:body.subarray(0,1),
      headers:{'content-range':`bytes 0-0/${body.length}`,'content-length':'1'}});
    expect(assets.evidence()).toMatchObject({totalBytes:f.manifest.totalBytes,metadataBytes:body.length,
      metadataFiles:[{path:metadataPath,bytes:body.length,sha256:entry.sha256,firstByteSha256:entry.firstByteSha256}]});
    expect(assets.evidence().files).toHaveLength(7);
    expect(assets.evidence().requests).toEqual([{path:metadataPath,method:'GET',range:'bytes=0-0',status:206,bytes:1,sha256:entry.firstByteSha256}]);
    await writeFile(join(f.folder,metadataPath),Buffer.alloc(body.length));
    const after=route(metadataPath,{headers:{range:'bytes=0-0'}});await assets.handle(after);
    expect(after.fulfill.mock.calls[0][0].body).toEqual(body.subarray(0,1));
    for(const headers of [{},{range:'bytes=0-1'},{range:'bytes=1-1'}]) {
      const forbidden=route(metadataPath,{headers});await expect(assets.handle(forbidden)).rejects.toThrow();
      expect(forbidden.fulfill).not.toHaveBeenCalled();
    }
  });
  it.each(['tail corruption','size','missing','first byte hash','total','duplicate','unlisted'])('rejects metadata %s before exposing the model handler',async kind=>{
    const f=await fixture(),{body,entry}=await addMetadata(f),local=join(f.folder,metadataPath);
    if(kind==='tail corruption'){body[body.length-1]^=1;await writeFile(local,body);}
    if(kind==='size')await writeFile(local,Buffer.concat([body,Buffer.from([0])]));
    if(kind==='missing')await rm(local);
    if(kind==='first byte hash')entry.firstByteSha256='0'.repeat(64);
    if(kind==='total')f.manifest.metadataBytes++;
    if(kind==='duplicate')f.manifest.metadataFiles.push({...entry});
    if(kind==='unlisted')entry.path='onnx/other_metadata.onnx';
    await f.save();await expect(f.load()).rejects.toThrow();
  });
  it('cannot disguise the real pinned quantized metadata asset as a missing file',async()=>{
    const f=await fixture();f.manifest.missingFiles=[{path:metadataPath,url:origin+revision+'/'+metadataPath,status:404}];await f.save();
    await expect(f.load()).rejects.toThrow('existing pinned metadata');
  });
  it('preserves the official Hugging Face JSON plain-text content type',async()=>{
    const f=await fixture();f.manifest.files[0].contentType='text/plain; charset=utf-8';await f.save();
    const assets=await f.load(),r=route();await assets.handle(r);
    expect(r.fulfill.mock.calls[0][0].contentType).toBe('text/plain; charset=utf-8');
  });
  it('serves404 only for an explicitly declared missing optional file',async()=>{
    const f=await fixture();f.manifest.missingFiles.push({path:'processor_config.json',url:origin+revision+'/processor_config.json',status:404});await f.save();
    const assets=await f.load(),r=route('processor_config.json',{headers:{range:'bytes=0-0'}});await assets.handle(r);
    expect(r.fulfill.mock.calls[0][0]).toMatchObject({status:404,body:Buffer.alloc(0)});
    await expect(assets.handle(route('other_config.json'))).rejects.toThrow('Unmanifested');
  });
  it.each(['corruption','size','missing'])('rejects %s before exposing a handler',async kind=>{
    const f=await fixture(),file=join(f.folder,paths[0]),body=await readFile(file);
    if(kind==='corruption'){body[0]^=1;await writeFile(file,body);}
    if(kind==='size')await writeFile(file,Buffer.concat([body,Buffer.from('extra')]));
    if(kind==='missing')await rm(file);
    await expect(f.load()).rejects.toThrow();
  });
  it.each(['duplicate','traversal','unlisted','wrong provenance','wrong total'])('rejects manifest %s',async kind=>{
    const f=await fixture();
    if(kind==='duplicate')f.manifest.files[1]={...f.manifest.files[0]};
    if(kind==='traversal')f.manifest.files[0].path='../config.json';
    if(kind==='unlisted')f.manifest.files[0].path='other.json';
    if(kind==='wrong provenance')f.manifest.files[0].url=origin+'main/config.json';
    if(kind==='wrong total')f.manifest.totalBytes++;
    await f.save();await expect(f.load()).rejects.toThrow();
  });
  it('rejects an ONNX directory junction that escapes the explicit model folder',async()=>{
    const f=await fixture(),outside=join(f.root,'outside');await mkdir(outside);
    for(const path of paths.filter(x=>x.startsWith('onnx/')))await writeFile(join(outside,path.slice(5)),await readFile(join(f.folder,path)));
    const onnx=join(f.folder,'onnx');expect(dirname(onnx)).toBe(f.folder);await rm(onnx,{recursive:true});
    await symlink(outside,onnx,process.platform==='win32'?'junction':'dir');
    await expect(f.load()).rejects.toThrow('escapes');
  });
  it('keeps verified bytes immutable after on-disk replacement',async()=>{
    const f=await fixture(),assets=await f.load(),file=join(f.folder,paths[0]),original=await readFile(file);
    await writeFile(file,'replacement');const r=route();await assets.handle(r);
    expect(r.fulfill.mock.calls[0][0].body).toEqual(original);expect(assets.evidence().requests[0].sha256).toBe(digest(original));
  });
  it.each([
    ['unknown file',{url:origin+'main/missing.json'}],['wrong revision',{url:origin+revision+'/config.json'}],
    ['query',{url:origin+'main/config.json?download=true'}],['encoded traversal',{url:origin+'main/onnx/%2e%2e/config.json'}],
    ['HTTP',{url:origin.replace('https:','http:')+'main/config.json'}],
    ['URL credentials',{url:(origin+'main/config.json').replace('https://','https://user:secret@')}],
    ['HEAD',{method:'HEAD'}],['POST',{method:'POST'}],['larger range',{headers:{range:'bytes=0-100'}}],
    ['suffix range',{headers:{range:'bytes=-1'}}],['authorization',{headers:{authorization:'Bearer forbidden'}}],
    ['cookie',{headers:{cookie:'session=forbidden'}}],
  ])('rejects unexpected %s',async(_name,options)=>{
    const f=await fixture(),assets=await f.load(),r=route('config.json',options);
    await expect(assets.handle(r)).rejects.toThrow();expect(r.fulfill).not.toHaveBeenCalled();
  });
  it('leaves unrelated URLs to the caller deny boundary without serving them',async()=>{
    const f=await fixture(),assets=await f.load();
    for(const url of ['https://example.test/config.json','https://huggingface.co/other/model/resolve/main/config.json']) {
      const r=route('',{url});expect(await assets.handle(r)).toBe(false);expect(r.fulfill).not.toHaveBeenCalled();
    }
  });
});
