// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Explicit public-asset setup only; never called automatically by a probe.
import assert from 'node:assert/strict';
import {createHash,randomUUID} from 'node:crypto';
import {createReadStream} from 'node:fs';
import {lstat,mkdir,open,readFile,realpath,rename,unlink} from 'node:fs/promises';
import {dirname,isAbsolute,join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {loadFirstLearnerSpeechAssets} from './first-learner-speech-assets.mjs';

const MODEL='onnx-community/whisper-tiny.en';
const REQUIRED=['config.json','generation_config.json','tokenizer.json','tokenizer_config.json',
  'preprocessor_config.json','onnx/encoder_model_q4.onnx','onnx/decoder_model_merged_q4.onnx'];
const METADATA='onnx/encoder_model_quantized.onnx';
const MAX_BYTES=150_000_000,MAX_REDIRECTS=5,DEFAULT_TIMEOUT_MS=120_000;
const HOSTS=new Set(['huggingface.co','cdn-lfs.huggingface.co','cdn-lfs-us-1.huggingface.co',
  'cdn-lfs-eu-1.huggingface.co','cdn-lfs.hf.co','cdn-lfs-us-1.hf.co','cdn-lfs-eu-1.hf.co',
  'cas-bridge.xethub.hf.co','us.aws.cdn.hf.co','eu.aws.cdn.hf.co']);
const digest=bytes=>createHash('sha256').update(bytes).digest('hex');
async function info(path) {
  try{return await lstat(path);}catch(error){if(error.code==='ENOENT')return null;throw error;}
}
async function directory(path) {
  const current=await info(path);
  if(!current)await mkdir(path);
  const checked=await lstat(path);
  assert(checked.isDirectory()&&!checked.isSymbolicLink(),'Cache directories must be real directories, not links');
  assert.equal(await realpath(path),path,'Cache directory resolves outside its declared location');
}
function publicUrl(raw) {
  assert(typeof raw==='string'&&raw.length<=16_384,'Invalid public asset URL');
  const url=new URL(raw);
  assert(url.protocol==='https:'&&HOSTS.has(url.hostname)&&!url.port&&!url.username&&!url.password&&!url.hash,
    'Asset redirect is outside the approved HTTPS Hugging Face/CDN hosts');
  return url;
}
function validateManifest(manifest) {
  assert.equal(manifest.version,1);assert.equal(manifest.modelId,MODEL);
  assert(typeof manifest.revision==='string'&&/^[a-f0-9]{40}$/.test(manifest.revision),'Manifest must pin an immutable revision');
  assert.equal(manifest.loader?.package,'@huggingface/transformers');assert.equal(manifest.loader.version,'4.3.0');
  assert.equal(manifest.loader.device,'wasm');
  assert.deepEqual(manifest.loader.dtype,{encoder_model:'q4',decoder_model_merged:'q4'});
  assert(Array.isArray(manifest.files)&&manifest.files.length===7,'Expected exactly seven q4 assets');
  assert(Array.isArray(manifest.missingFiles)&&manifest.missingFiles.length===0,'Missing model files are not admitted');
  assert(Array.isArray(manifest.metadataFiles)&&manifest.metadataFiles.length===1,'Expected the single pinned metadata-only asset');
  assert.equal(manifest.metadataFiles[0].path,METADATA,'Unexpected metadata-only asset');
  assert(typeof manifest.metadataFiles[0].firstByteSha256==='string'&&/^[a-f0-9]{64}$/.test(manifest.metadataFiles[0].firstByteSha256),'Expected exact metadata first-byte SHA256');
  const seen=new Set();let total=0;
  for(const entry of [...manifest.files,...manifest.metadataFiles]) {
    assert((REQUIRED.includes(entry.path)||entry.path===METADATA)&&!seen.has(entry.path),'Unexpected or duplicate model-relative asset');seen.add(entry.path);
    assert(Number.isSafeInteger(entry.size)&&entry.size>0&&entry.size<=MAX_BYTES,'Invalid bounded asset byte size');
    assert(typeof entry.sha256==='string'&&/^[a-f0-9]{64}$/.test(entry.sha256),'Expected exact SHA256');
    assert(['application/json','text/plain; charset=utf-8','application/octet-stream'].includes(entry.contentType),'Unexpected asset content type');
    assert.equal(entry.url,`https://huggingface.co/${MODEL}/resolve/${manifest.revision}/${entry.path}`,'Asset URL must name its exact pinned file');
    publicUrl(entry.url);total+=entry.size;assert(total<=MAX_BYTES,'Offline asset budget exceeds 150 MB');
  }
  assert.equal(manifest.totalBytes,manifest.files.reduce((sum,entry)=>sum+entry.size,0),'Manifest total does not match its seven runtime assets');
  assert.equal(manifest.metadataBytes,manifest.metadataFiles[0].size,'Manifest metadata total does not match its separate asset');
  assert.equal(total,manifest.totalBytes+manifest.metadataBytes);
}
async function validExisting(path,entry) {
  const current=await info(path);
  if(!current)return false;
  assert(current.isFile()&&!current.isSymbolicLink(),'Asset destination must be an ordinary file');
  if(current.size!==entry.size)return false;
  const hash=createHash('sha256');let bytes=0,firstByteSha256;
  for await(const chunk of createReadStream(path)) {
    if(bytes===0&&chunk.length)firstByteSha256=digest(chunk.subarray(0,1));
    bytes+=chunk.length;if(bytes>entry.size)return false;hash.update(chunk);
  }
  const valid=bytes===entry.size&&hash.digest('hex')===entry.sha256;
  if(valid&&entry.firstByteSha256)assert.equal(firstByteSha256,entry.firstByteSha256,'Metadata first-byte digest contradicts its verified complete asset');
  return valid;
}
function bounded(promise,signal) {
  if(signal.aborted)return Promise.reject(new Error('Public asset download timed out'));
  let abort;
  const deadline=new Promise((_,reject)=>{abort=()=>reject(new Error('Public asset download timed out'));signal.addEventListener('abort',abort,{once:true});});
  return Promise.race([promise,deadline]).finally(()=>signal.removeEventListener('abort',abort));
}
async function download(entry,destination,fetchImpl,timeoutMs) {
  const controller=new AbortController(),timer=setTimeout(()=>controller.abort(),timeoutMs);
  const temporary=join(dirname(destination),'.'+entry.path.split('/').at(-1)+'.prepare-'+randomUUID()+'.part');
  let owned=false,handle,reader,response;
  try {
    let url=publicUrl(entry.url);
    for(let redirects=0;;redirects++) {
      response=await bounded(Promise.resolve().then(()=>fetchImpl(url.href,{method:'GET',redirect:'manual',credentials:'omit',
        referrerPolicy:'no-referrer',headers:{accept:'application/octet-stream','accept-encoding':'identity'},signal:controller.signal})),controller.signal);
      assert(!response.redirected,'Fetch followed an unchecked redirect');
      if([301,302,303,307,308].includes(response.status)) {
        void response.body?.cancel().catch(()=>{});
        assert(redirects<MAX_REDIRECTS,'Too many public asset redirects');
        const location=response.headers.get('location');assert(location,'Redirect has no location');
        url=publicUrl(new URL(location,url).href);continue;
      }
      assert.equal(response.status,200,'Public asset GET did not return 200');break;
    }
    const encoding=response.headers.get('content-encoding');assert(!encoding||encoding==='identity','Expected uncompressed exact asset bytes');
    const length=response.headers.get('content-length');
    if(length!==null)assert(/^\d+$/.test(length)&&Number(length)===entry.size,'Public asset Content-Length differs from manifest');
    assert(response.body&&typeof response.body.getReader==='function','Public asset response has no readable body');
    handle=await open(temporary,'wx',0o600);owned=true;
    reader=response.body.getReader();let bytes=0,firstByteSha256;const hash=createHash('sha256');
    for(;;) {
      const next=await bounded(reader.read(),controller.signal);if(next.done)break;
      assert(next.value instanceof Uint8Array,'Public asset body must contain bytes');
      if(bytes===0&&next.value.byteLength)firstByteSha256=digest(next.value.subarray(0,1));
      bytes+=next.value.byteLength;assert(bytes<=entry.size,'Public asset stream exceeds declared size');
      hash.update(next.value);
      let offset=0;
      while(offset<next.value.byteLength) {
        const written=await handle.write(next.value,offset,next.value.byteLength-offset);
        assert(written.bytesWritten>0,'Asset write made no progress');offset+=written.bytesWritten;
      }
    }
    assert.equal(bytes,entry.size,'Public asset stream is incomplete');
    assert.equal(hash.digest('hex'),entry.sha256,'Public asset SHA256 differs from manifest');
    if(entry.firstByteSha256)assert.equal(firstByteSha256,entry.firstByteSha256,'Public metadata first-byte SHA256 differs from manifest');
    await handle.sync();await handle.close();handle=null;
    // The old file survives every preceding failure. Rename stays on this filesystem.
    const old=await info(destination);assert(!old||(old.isFile()&&!old.isSymbolicLink()),'Asset destination changed to a non-file');
    await rename(temporary,destination);owned=false;
    return {path:entry.path,status:'downloaded',bytes,sha256:entry.sha256};
  } finally {
    clearTimeout(timer);controller.abort();
    if(reader){void reader.cancel().catch(()=>{});reader.releaseLock();}
    else if(response?.body)void response.body.cancel().catch(()=>{});
    if(handle)await handle.close();
    if(owned)await unlink(temporary);
  }
}

/** Injectable root/fetch exist for isolated tiny fixture tests. The CLI below
 * accepts no overrides, secrets, environment configuration or user input. */
export async function prepareFirstLearnerSpeechAssets({repositoryRoot,fetchImpl=globalThis.fetch,timeoutMs=DEFAULT_TIMEOUT_MS}) {
  assert(isAbsolute(repositoryRoot),'Use an absolute repository root');
  assert(typeof fetchImpl==='function','A fetch implementation is required');
  assert(Number.isSafeInteger(timeoutMs)&&timeoutMs>0&&timeoutMs<=DEFAULT_TIMEOUT_MS,'Invalid download timeout');
  const root=await realpath(repositoryRoot),manifestPath=join(root,'test-fixtures/models/whisper-tiny-en.json');
  const manifestInfo=await lstat(manifestPath);
  assert(manifestInfo.isFile()&&!manifestInfo.isSymbolicLink()&&manifestInfo.size<=128_000,'Expected bounded committed manifest');
  const manifestBytes=await readFile(manifestPath);assert(manifestBytes.length<=128_000);
  const manifest=JSON.parse(manifestBytes.toString('utf8'));validateManifest(manifest);
  const tools=join(root,'.quest-tools'),folder=join(tools,'whisper-tiny-en-'+manifest.revision);
  await directory(tools);await directory(folder);await directory(join(folder,'onnx'));
  const allFiles=[];
  for(const entry of [...manifest.files,...manifest.metadataFiles]) {
    const destination=join(folder,...entry.path.split('/'));
    if(await validExisting(destination,entry))allFiles.push({path:entry.path,status:'cached',bytes:entry.size,sha256:entry.sha256});
    else allFiles.push(await download(entry,destination,fetchImpl,timeoutMs));
  }
  assert.equal(digest(await readFile(manifestPath)),digest(manifestBytes),'Committed manifest changed during preparation');
  const verified=await loadFirstLearnerSpeechAssets(folder,manifestPath);
  assert.equal(verified.evidence().manifestSha256,digest(manifestBytes),'Verification used a changed manifest');
  return {status:'verified',modelId:manifest.modelId,revision:manifest.revision,folder,
    manifestSha256:digest(manifestBytes),totalBytes:manifest.totalBytes,metadataBytes:manifest.metadataBytes,
    downloaded:allFiles.filter(f=>f.status==='downloaded').length,cached:allFiles.filter(f=>f.status==='cached').length,
    files:allFiles.filter(f=>REQUIRED.includes(f.path)),metadataFiles:allFiles.filter(f=>f.path===METADATA),
    runtimeExecuted:false};
}

if(process.argv[1]&&resolve(process.argv[1])===fileURLToPath(import.meta.url)) {
  try {
    assert(process.argv.length===2||(process.argv.length===3&&process.argv[2]==='--help'),
      'Usage: node scripts/prepare-first-learner-speech-assets.mjs [--help]; paths/URLs are fixed by the committed manifest');
    if(process.argv[2]==='--help')process.stdout.write('Download and verify seven pinned Whisper q4 runtime assets plus one metadata-only asset (under 150 MB) into this repository .quest-tools cache. No inference or probe starts.\n');
    else process.stdout.write(JSON.stringify(await prepareFirstLearnerSpeechAssets({repositoryRoot:resolve(dirname(fileURLToPath(import.meta.url)),'..')}),null,2)+'\n');
  } catch(error) {process.stderr.write('Speech asset preparation failed: '+error.message+'\n');process.exitCode=1;}
}
