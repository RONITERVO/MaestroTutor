// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Exact local assets for the unchanged real Whisper worker in offline journeys.
import assert from 'node:assert/strict';
import {createHash,randomUUID} from 'node:crypto';
import {createServer} from 'node:http';
import {lstat,readFile,realpath} from 'node:fs/promises';
import {isAbsolute,relative,resolve,sep} from 'node:path';
import {fileURLToPath} from 'node:url';

const MODEL='onnx-community/whisper-tiny.en';
const HOST='https://huggingface.co';
const PREFIX=`/${MODEL}/`;
const REQUIRED=[
  'config.json','generation_config.json','tokenizer.json','tokenizer_config.json',
  'preprocessor_config.json','onnx/encoder_model_q4.onnx','onnx/decoder_model_merged_q4.onnx',
];
const METADATA_PATH='onnx/encoder_model_quantized.onnx';
const hash=bytes=>createHash('sha256').update(bytes).digest('hex');
function safePath(value) {
  assert(typeof value==='string'&&/^(?:onnx\/)?[a-zA-Z0-9_-]+\.(?:json|onnx)$/.test(value),'Unapproved model-relative file path');
  return value;
}
function contained(root,file) {
  const rel=relative(root,file);
  assert(rel!==''&&!isAbsolute(rel)&&rel!=='..'&&!rel.startsWith('..'+sep),'Model file escapes its explicit asset folder');
}

/** Serialize with context.addInitScript. Observe actual worker responses while
 * forwarding every constructor argument, native instance and subclass unchanged. */
export function installFirstLearnerSpeechObserver() {
  if(globalThis!==globalThis.top)return;
  const NativeWorker=globalThis.Worker;
  if(typeof NativeWorker!=='function')throw new Error('The real Worker constructor is unavailable');
  if(Object.hasOwn(globalThis,'__firstLearnerSpeechWorkers'))throw new Error('Speech worker observation is already installed');
  const workers=[];let overflow=false;
  const snapshot=()=>({version:1,overflow,workers:workers.map(w=>({id:w.id,url:w.url,
    ready:w.ready.map(r=>({...r})),errors:w.errors.map(e=>({...e})),results:{...w.results}}))});
  Object.defineProperty(globalThis,'__firstLearnerSpeechWorkers',{value:snapshot,configurable:false});
  function ObservedWorker(...args) {
    if(!new.target)return Reflect.apply(NativeWorker,this,args);
    const worker=Reflect.construct(NativeWorker,args,new.target===ObservedWorker?NativeWorker:new.target);
    let url;
    try{url=new URL(String(args[0]),globalThis.location.href);}catch{return worker;}
    if(url.origin!==globalThis.location.origin||url.search||url.hash
      ||!/^\/assets\/observerWhisper\.worker-[A-Za-z0-9_-]+\.js$/.test(url.pathname))return worker;
    if(workers.length>=4){overflow=true;return worker;}
    const record={id:workers.length+1,url:url.href,ready:[],errors:[],results:{count:0,totalElapsedMs:0,maxElapsedMs:0}};
    workers.push(record);
    const fail=(kind,message)=>{
      if(record.errors.length>=32){overflow=true;return;}
      record.errors.push({kind,message:String(message).slice(0,2048)});
    };
    worker.addEventListener('message',event=>{
      const message=event.data;
      if(!message||typeof message!=='object')return;
      if(message.kind==='ready') {
        if(message.model!=='onnx-community/whisper-tiny.en'||message.profile!=='q4'){fail('protocol-error','Unexpected speech model/profile became ready');return;}
        if(record.ready.length>=8){overflow=true;return;}
        record.ready.push({model:message.model,profile:message.profile});
      } else if(message.kind==='error')fail('model-error',message.message??'Local Whisper reported an error');
      else if(message.kind==='result') {
        if(!Number.isSafeInteger(message.requestId)||message.requestId<1||typeof message.text!=='string'
          ||!Number.isFinite(message.elapsedMs)||message.elapsedMs<0){fail('protocol-error','Malformed speech inference result');return;}
        if(record.results.count>=1024){overflow=true;return;}
        record.results.count++;record.results.totalElapsedMs+=message.elapsedMs;
        record.results.maxElapsedMs=Math.max(record.results.maxElapsedMs,message.elapsedMs);
      }
    });
    worker.addEventListener('error',event=>fail('worker-error',event.message??'Speech worker failed'));
    return worker;
  }
  Object.setPrototypeOf(ObservedWorker,NativeWorker);
  ObservedWorker.prototype=NativeWorker.prototype;
  globalThis.Worker=ObservedWorker;
}

/** Validate every asset before browser launch. Later replies use immutable bytes
 * directly or through the owned loopback server, without reading files again. */
export async function loadFirstLearnerSpeechAssets(folder,manifestPath) {
  assert(isAbsolute(folder)&&isAbsolute(manifestPath),'Use explicit absolute asset and committed-manifest paths');
  const manifestInfo=await lstat(manifestPath);
  assert(manifestInfo.isFile()&&!manifestInfo.isSymbolicLink()&&manifestInfo.size<=128000,'Expected a bounded manifest file');
  const manifestBytes=await readFile(manifestPath),manifest=JSON.parse(manifestBytes.toString('utf8'));
  assert(manifestBytes.length<=128000,'Manifest changed beyond its bounded size');
  assert.equal(manifest.version,1);assert.equal(manifest.modelId,MODEL);
  assert(typeof manifest.revision==='string'&&/^[0-9a-f]{40}$/.test(manifest.revision),'Expected immutable model revision');
  assert.equal(manifest.loader?.package,'@huggingface/transformers');assert.equal(manifest.loader.version,'4.3.0');
  assert(Array.isArray(manifest.files)&&manifest.files.length===REQUIRED.length,'Expected the complete seven-file q4 model');
  const metadata=manifest.metadataFiles??[];
  assert(Array.isArray(metadata)&&metadata.length<=1,'Metadata probes must be explicit and bounded');
  const missing=manifest.missingFiles??[];
  assert(Array.isArray(missing)&&missing.length<=8,'Optional missing files must be explicit and bounded');
  const root=await realpath(folder),rootInfo=await lstat(root);assert(rootInfo.isDirectory());
  const entries=new Map(),verified=[],verifiedMetadata=[];let total=0,metadataTotal=0;
  const put=(entry,body,status,rangeOnly=false,totalSize=body.length)=>{
    const path=safePath(entry.path),url=`${HOST}${PREFIX}resolve/main/${path}`;
    assert.equal(entry.url,`${HOST}${PREFIX}resolve/${manifest.revision}/${path}`,'Manifest provenance must name the pinned revision');
    assert(!entries.has(url),'Duplicate runtime model path');
    entries.set(url,{path,body,status,rangeOnly,totalSize,contentType:entry.contentType??'application/octet-stream'});
  };
  for(const {entry,rangeOnly} of [...manifest.files.map(entry=>({entry,rangeOnly:false})),...metadata.map(entry=>({entry,rangeOnly:true}))]) {
    const path=safePath(entry.path);assert(rangeOnly?path===METADATA_PATH:REQUIRED.includes(path),'Unexpected model asset');
    assert(Number.isSafeInteger(entry.size)&&entry.size>0&&entry.size<=256*1024*1024,'Invalid asset byte size');
    if(rangeOnly)metadataTotal+=entry.size;else total+=entry.size;
    assert(total+metadataTotal<=256*1024*1024,'Model exceeds bounded offline asset budget');
    assert(typeof entry.sha256==='string'&&/^[0-9a-f]{64}$/i.test(entry.sha256),'Expected exact SHA256');
    assert(['application/json','text/plain; charset=utf-8','application/octet-stream'].includes(entry.contentType),'Unexpected model content type');
    const local=resolve(root,...path.split('/'));contained(root,local);
    const info=await lstat(local);assert(info.isFile()&&!info.isSymbolicLink(),'Model asset must be a regular file');
    const canonical=await realpath(local);contained(root,canonical);
    assert.equal(info.size,entry.size,'Local model size differs from manifest');
    const body=await readFile(canonical);assert.equal(body.length,entry.size);
    const sha256=hash(body);assert.equal(sha256,entry.sha256.toLowerCase(),'Local model digest differs from manifest');
    if(rangeOnly) {
      assert.equal(entry.contentType,'application/octet-stream','Metadata probe must retain its ONNX content type');
      const firstByte=Buffer.from(body.subarray(0,1)),firstByteSha256=hash(firstByte);
      assert.equal(firstByteSha256,entry.firstByteSha256,'Metadata first byte differs from manifest');
      put(entry,firstByte,200,true,body.length);verifiedMetadata.push({path,bytes:body.length,sha256,firstByteSha256});
    } else {put(entry,body,200);verified.push({path,bytes:body.length,sha256});}
  }
  assert.equal(total,manifest.totalBytes,'Manifest total differs from verified assets');
  assert.equal(metadataTotal,manifest.metadataBytes??0,'Manifest metadata total differs from verified assets');
  for(const entry of missing) {
    assert.equal(entry.status,404,'Only documented missing optional files may return404');
    assert(!REQUIRED.includes(entry.path),'A required model file cannot be declared missing');
    assert.notEqual(entry.path,METADATA_PATH,'An existing pinned metadata file cannot be declared missing');
    put(entry,Buffer.alloc(0),404);
  }
  const requests=[],errors=[],sockets=new Set(),localEntries=new Map(),fullAttempts=new Map();
  const socketTickets=new WeakMap(),failedSockets=new WeakSet();
  const metadataReceipts=new WeakMap(),fullTickets=new WeakMap();
  let server,origin,starting,closing,stopped=false,redirects=0,active=0,fullRequests=0;
  const fail=error=>{if(errors.length<32)errors.push(String(error?.message??error).slice(0,2048));else if(errors.length===32)errors.push('Speech asset error limit exceeded');};
  const invalidate=ticket=>{
    ticket.invalid=true;
    if(ticket.row){const index=requests.indexOf(ticket.row);if(index>=0)requests.splice(index,1);}
  };
  const failSocket=(socket,error)=>{
    if(failedSockets.has(socket))return;
    failedSockets.add(socket);
    // HTTP finish proves the bytes reached the OS, not that a resetting peer
    // consumed them. A later connection error invalidates its delivery receipts.
    for(const ticket of socketTickets.get(socket)??[])invalidate(ticket);
    fail(error);
  };
  const responseHeaders=length=>({'access-control-allow-origin':'*',
    'access-control-expose-headers':'Content-Length, Content-Range, Accept-Ranges',
    'content-length':String(length),'cache-control':'no-store','accept-ranges':'bytes'});
  const requestEvidence=(entry,body,status,range)=>({path:entry.path,method:'GET',range,status,bytes:body.length,sha256:hash(body)});
  const claim=(request,fromRoute=false)=>{
    const raw=request.url(),url=new URL(raw);
    if(!origin||url.origin!==origin)return null;
    const ticket=localEntries.get(url.pathname);
    try {
      assert(!stopped&&raw===url.href&&!url.search&&!url.hash&&!url.username&&!url.password,'Unexpected loopback model URL');
      assert(ticket,'Unmanifested loopback model URL');
      const headers=request.headers();
      assert(request.method()==='GET'&&!headers.range&&!headers.authorization&&!headers.cookie,'Unexpected loopback model request');
      assert(!ticket.invalid&&(!ticket.request||ticket.request===request)
        &&request.redirectedFrom()===ticket.source,'Loopback model request must claim its exact source redirect once');
      assert(fromRoute?!ticket.routeStarted:!ticket.observed,'Repeated loopback model request claim');
      ticket.request=request;fullTickets.set(request,ticket);
      if(fromRoute)ticket.routeStarted=true;else ticket.observed=true;
      return ticket;
    } catch(error){if(ticket)invalidate(ticket);fail(error);throw error;}
  };
  const start=()=>{
    assert(!stopped,'Speech asset server has been closed');
    if(starting)return starting;
    starting=new Promise((accept,reject)=>{
      server=createServer({maxHeaderSize:8192},(request,response)=>{
        const ticket=localEntries.get(request.url),entry=ticket?.entry;
        if(stopped||request.method!=='GET'||request.headers.host!==new URL(origin).host||!ticket||ticket.invalid||ticket.used
          ||request.headers.range||request.headers.authorization||request.headers.cookie||active>=8||++fullRequests>256) {
          if(ticket)invalidate(ticket);
          fail('Rejected unexpected loopback speech asset request');response.writeHead(404,{'content-length':'0','connection':'close'});response.end();return;
        }
        ticket.used=true;socketTickets.get(request.socket).add(ticket);
        active++;
        const timer=setTimeout(()=>{invalidate(ticket);fail('Loopback speech asset response timed out');response.destroy();},120000);
        response.once('finish',()=>{
          if(ticket.invalid||failedSockets.has(request.socket))return;
          if(requests.length>=256)fail('Unexpected repeated model fetches');
          else {ticket.finished=true;ticket.row=Object.freeze(requestEvidence(entry,entry.body,200,null));requests.push(ticket.row);}
        });
        response.once('close',()=>{clearTimeout(timer);active--;if(!response.writableFinished)failSocket(request.socket,'Loopback speech asset response aborted before completion');});
        response.once('error',error=>failSocket(request.socket,error));
        response.writeHead(200,{...responseHeaders(entry.body.length),'content-type':entry.contentType});
        response.end(entry.body);
      });
      server.requestTimeout=120000;server.headersTimeout=30000;server.keepAliveTimeout=1000;
      server.on('connection',socket=>{sockets.add(socket);socketTickets.set(socket,new Set());socket.once('close',()=>sockets.delete(socket));socket.on('error',error=>failSocket(socket,error));});
      server.on('clientError',(error,socket)=>{failSocket(socket,error);socket.destroy();});
      server.on('upgrade',(_request,socket)=>{fail('Unexpected loopback speech asset upgrade');socket.destroy();});
      server.on('error',error=>{fail(error);reject(error);});
      server.listen(0,'127.0.0.1',()=>{
        const address=server.address();origin=`http://127.0.0.1:${address.port}`;
        accept({origin});
      });
    });
    return starting;
  };
  const close=()=>{
    if(closing)return closing;
    stopped=true;
    closing=(async()=>{
      if(starting)await starting.catch(()=>{});
      for(const socket of sockets)socket.destroy();
      if(server?.listening)await new Promise((accept,reject)=>server.close(error=>error?reject(error):accept()));
    })();
    return closing;
  };
  return {
    start,close,
    // Playwright auto-continues redirects without invoking user route handlers.
    // Its passive request event is the identity proof; it may arrive after HTTP.
    observeRequest(request) {return claim(request)!==null;},
    metadataReceipt(request) {const receipt=metadataReceipts.get(request);return receipt?{...receipt}:null;},
    fullReceipt(request) {
      const ticket=fullTickets.get(request);
      return ticket&&!ticket.invalid&&ticket.redirected&&ticket.observed&&ticket.finished
        &&(!ticket.routeStarted||ticket.continued)
        &&fullAttempts.get(ticket.entry.path)===1?{...ticket.row}:null;
    },
    async readBrowserCache(page) {
      const proof=await page.evaluate(async({cacheName,files})=>{
        const deadline=Date.now()+30000,verified=[];
        const bounded=async operation=>{
          const remaining=deadline-Date.now();if(remaining<=0)throw new Error('Speech cache verification timed out');
          let timer;
          try{return await Promise.race([operation,new Promise((_,reject)=>{timer=setTimeout(()=>reject(new Error('Speech cache verification timed out')),remaining);})]);}
          finally{clearTimeout(timer);}
        };
        for(const file of files) {
          const response=await bounded(globalThis.caches.match(file.url,{cacheName}));
          if(!response||response.status!==200||!response.body)throw new Error('Missing complete browser speech cache entry: '+file.path);
          const bytes=new Uint8Array(file.bytes),reader=response.body.getReader();let loaded=0,reads=0,complete=false;
          try {
            for(;;) {
              if(++reads>8192)throw new Error('Speech cache read count exceeded: '+file.path);
              const {done,value}=await bounded(reader.read());
              if(done){complete=true;break;}
              if(!(value instanceof Uint8Array)||loaded+value.byteLength>bytes.length)throw new Error('Speech cache bytes exceed manifest: '+file.path);
              bytes.set(value,loaded);loaded+=value.byteLength;
            }
          } finally {
            if(!complete)void reader.cancel().catch(()=>{});
            reader.releaseLock();
          }
          if(loaded!==file.bytes)throw new Error('Speech cache size differs from manifest: '+file.path);
          const digest=await bounded(globalThis.crypto.subtle.digest('SHA-256',bytes));
          const sha256=Array.from(new Uint8Array(digest),b=>b.toString(16).padStart(2,'0')).join('');
          if(sha256!==file.sha256)throw new Error('Speech cache digest differs from manifest: '+file.path);
          verified.push({path:file.path,bytes:loaded,sha256});
        }
        return {cacheName,verifiedAt:Date.now(),files:verified};
      },{cacheName:'transformers-cache',files:verified.map(file=>({...file,url:`${HOST}${PREFIX}resolve/main/${file.path}`}))});
      assert.equal(proof.cacheName,'transformers-cache');assert(Number.isSafeInteger(proof.verifiedAt)&&proof.verifiedAt>0);
      assert.deepEqual(proof.files,verified,'Browser cache proof differs from verified assets');
      return {...proof,modelId:MODEL,manifestSha256:hash(manifestBytes)};
    },
    async handle(route) {
      const request=route.request(),raw=request.url(),url=new URL(raw);
      if(origin&&url.origin===origin) {
        const ticket=claim(request,true);
        try {
          await route.continue();ticket.continued=true;return true;
        } catch(error){if(ticket)invalidate(ticket);fail(error);throw error;}
      }
      if(url.hostname!=='huggingface.co'||!url.pathname.startsWith(PREFIX))return false;
      assert(!stopped,'Speech asset server has been closed');
      assert.equal(url.origin,HOST,'Unexpected model origin');
      assert.equal(raw,url.href,'Noncanonical model URL');
      assert.equal(request.method(),'GET','Only declared model GET requests are permitted');
      assert(!url.search&&!url.hash&&!url.username&&!url.password,'Unexpected model URL suffix or credentials');
      const entry=entries.get(raw);assert(entry,'Unmanifested model URL');
      const headers=request.headers(),range=headers.range;
      assert(range===undefined||range==='bytes=0-0','Only the loader first-byte metadata range is permitted');
      assert(!entry.rangeOnly||range==='bytes=0-0','Metadata-only assets cannot be loaded as inference models');
      assert(!headers.authorization&&!headers.cookie,'Offline public model assets must not carry credentials');
      assert(requests.length<256,'Unexpected repeated model fetches');
      if(range===undefined&&entry.status===200)fullAttempts.set(entry.path,(fullAttempts.get(entry.path)??0)+1);
      if(starting&&range===undefined&&entry.status===200) {
        await starting;assert(!stopped,'Speech asset server has been closed');
        assert(++redirects<=256,'Unexpected repeated model redirects');
        const localPath='/'+randomUUID()+'/'+entry.path;
        const ticket={entry,source:request,request:null,redirected:false,observed:false,routeStarted:false,continued:false,finished:false,invalid:false,used:false,row:null};
        localEntries.set(localPath,ticket);
        try {
          await route.fulfill({status:307,headers:{location:origin+localPath,'access-control-allow-origin':'*','cache-control':'no-store','content-length':'0'},body:''});
          ticket.redirected=true;
        } catch(error){invalidate(ticket);fail(error);throw error;}
        return true;
      }
      const partial=range!==undefined&&entry.status===200;
      const body=partial?entry.body.subarray(0,1):entry.body;
      const status=partial?206:entry.status;
      const headersOut={...responseHeaders(body.length),
        ...(partial?{'content-range':`bytes 0-0/${entry.totalSize}`}:{})
      };
      await route.fulfill({status,contentType:entry.contentType,headers:headersOut,body});
      assert(requests.length<256,'Unexpected repeated model fetches');
      const receipt=requestEvidence(entry,body,status,range??null);requests.push(receipt);
      if(partial)metadataReceipts.set(request,Object.freeze({...receipt}));
      return true;
    },
    evidence:()=>({modelId:MODEL,revision:manifest.revision,loader:{...manifest.loader},
      manifestSha256:hash(manifestBytes),totalBytes:total,files:verified.map(x=>({...x})),
      metadataBytes:metadataTotal,metadataFiles:verifiedMetadata.map(x=>({...x})),
      fullAttempts:[...fullAttempts].map(([path,count])=>({path,count})),
      errors:[...errors],
      missingFiles:missing.map(x=>({path:x.path,status:404})),requests:requests.map(x=>({...x})),
      boundary:'Hash-verified official local model assets only; the production worker, WASM inference and recognition results are unchanged. No remote model requests are allowed.'}),
  };
}

if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  assert(process.argv.length===5&&process.argv[2]==='--verify','Usage: --verify <absoluteFolder> <absoluteManifest>');
  const assets=await loadFirstLearnerSpeechAssets(process.argv[3],process.argv[4]);
  const {modelId,revision,totalBytes,metadataBytes}=assets.evidence();
  process.stdout.write(JSON.stringify({modelId,revision,verifiedBytes:totalBytes+metadataBytes,modelBytes:totalBytes,metadataBytes})+'\n');
}
