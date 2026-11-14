// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated tiny files and injected HTTP responses only; never network/inference.
import {afterEach,describe,expect,it,vi} from 'vitest';
import {createHash} from 'node:crypto';
import {mkdtemp,mkdir,readFile,readdir,writeFile,rm,symlink} from 'node:fs/promises';
import {tmpdir} from 'node:os';
import {dirname,join,resolve} from 'node:path';
import {fileURLToPath} from 'node:url';
import {spawnSync} from 'node:child_process';
import {prepareFirstLearnerSpeechAssets} from './prepare-first-learner-speech-assets.mjs';
const roots=[],revision='2575352d61be1bf7225cf8f8b268a4678025fc58',modelId='onnx-community/whisper-tiny.en';
const paths=['config.json','generation_config.json','tokenizer.json','tokenizer_config.json',
  'preprocessor_config.json','onnx/encoder_model_q4.onnx','onnx/decoder_model_merged_q4.onnx'];
const metadataPath='onnx/encoder_model_quantized.onnx';
const digest=body=>createHash('sha256').update(body).digest('hex');
async function fixture({cached=false}={}) {
  const root=await mkdtemp(join(tmpdir(),'maestro-prepare-speech-'));roots.push(root);
  const folder=join(root,'.quest-tools','whisper-tiny-en-'+revision),manifestPath=join(root,'test-fixtures/models/whisper-tiny-en.json');
  await mkdir(dirname(manifestPath),{recursive:true});await mkdir(join(folder,'onnx'),{recursive:true});
  const bodies=new Map(),allFiles=[...paths,metadataPath].map(path=>{
    const body=Buffer.from('Tiny exact test asset: '+path);bodies.set(path,body);
    return {path,url:`https://huggingface.co/${modelId}/resolve/${revision}/${path}`,size:body.length,sha256:digest(body),contentType:path.endsWith('.json')?'application/json':'application/octet-stream'};
  });
  const files=allFiles.slice(0,7),metadataFiles=[{...allFiles[7],firstByteSha256:digest(bodies.get(metadataPath).subarray(0,1))}];
  const manifest={version:1,modelId,revision,loader:{package:'@huggingface/transformers',version:'4.3.0',device:'wasm',dtype:{encoder_model:'q4',decoder_model_merged:'q4'}},files,metadataFiles,metadataBytes:metadataFiles[0].size,missingFiles:[],totalBytes:files.reduce((sum,f)=>sum+f.size,0)};
  const save=()=>writeFile(manifestPath,JSON.stringify(manifest));await save();
  if(cached)for(const [path,body] of bodies)await writeFile(join(folder,path),body);
  const fetchImpl=vi.fn(async url=>{
    const entry=allFiles.find(f=>f.url===url);expect(entry).toBeDefined();
    return new Response(bodies.get(entry.path),{status:200,headers:{'content-length':String(entry.size)}});
  });
  return {root,folder,manifestPath,manifest,bodies,fetchImpl,save,
    run:options=>prepareFirstLearnerSpeechAssets({repositoryRoot:root,fetchImpl,...options})};
}
async function temporaryFiles(f) {
  return [...await readdir(f.folder),...await readdir(join(f.folder,'onnx'))].filter(name=>name.includes('.prepare-')&&name.endsWith('.part'));
}
afterEach(async()=>{
  for(const root of roots.splice(0)) {
    expect(dirname(resolve(root))).toBe(resolve(tmpdir()));expect(root.split(/[\\/]/).at(-1)).toMatch(/^maestro-prepare-speech-/);
    await rm(root,{recursive:true,force:true});
  }
});

describe('explicit pinned public speech asset preparation',()=>{
  it('fetches only the seven pinned runtime files and separate metadata file and verifies the complete bundle through the real loader',async()=>{
    const f=await fixture(),result=await f.run();
    expect(result).toMatchObject({status:'verified',downloaded:8,cached:0,totalBytes:f.manifest.totalBytes,metadataBytes:f.manifest.metadataBytes,runtimeExecuted:false});
    expect(result.manifestSha256).toBe(digest(await readFile(f.manifestPath)));
    expect(f.fetchImpl.mock.calls.map(c=>c[0])).toEqual([...f.manifest.files,...f.manifest.metadataFiles].map(x=>x.url));
    for(const [,options] of f.fetchImpl.mock.calls)expect(options).toMatchObject({method:'GET',redirect:'manual',credentials:'omit',referrerPolicy:'no-referrer',headers:{accept:'application/octet-stream','accept-encoding':'identity'}});
    for(const [path,body] of f.bodies)expect(await readFile(join(f.folder,path))).toEqual(body);
    expect(await temporaryFiles(f)).toEqual([]);
  });
  it('skips only valid cached bytes and leaves unknown files untouched',async()=>{
    const f=await fixture({cached:true});await writeFile(join(f.folder,'user-note.txt'),'keep');
    await writeFile(join(f.folder,'.someone-elses.part'),'unrelated');
    const result=await f.run();expect(result.cached).toBe(8);expect(result.downloaded).toBe(0);expect(f.fetchImpl).not.toHaveBeenCalled();
    expect(await readFile(join(f.folder,'user-note.txt'),'utf8')).toBe('keep');
    expect(await readFile(join(f.folder,'.someone-elses.part'),'utf8')).toBe('unrelated');
  });
  it('replaces one corrupt ordinary file only after its new bytes pass validation',async()=>{
    const f=await fixture({cached:true}),destination=join(f.folder,paths[0]),old=Buffer.alloc(f.manifest.files[0].size,65);
    await writeFile(destination,old);
    f.fetchImpl.mockImplementationOnce(async()=>{expect(await readFile(destination)).toEqual(old);return new Response(f.bodies.get(paths[0]));});
    const result=await f.run();expect(result).toMatchObject({downloaded:1,cached:7});expect(f.fetchImpl).toHaveBeenCalledTimes(1);
    expect(await readFile(destination)).toEqual(f.bodies.get(paths[0]));expect(await temporaryFiles(f)).toEqual([]);
  });
  it.each(['digest','truncated','overflow','header-size','status','missing-body','compressed'])('preserves the old invalid file and cleans only its own temporary file on %s failure',async kind=>{
    const f=await fixture({cached:true}),destination=join(f.folder,paths[0]),entry=f.manifest.files[0],old=Buffer.from('keep invalid existing data');
    await writeFile(destination,old);await writeFile(join(f.folder,'.unrelated.part'),'keep');
    f.fetchImpl.mockImplementation(async()=>{
      if(kind==='status')return new Response('failure',{status:503});
      if(kind==='missing-body')return new Response(null,{status:200});
      if(kind==='header-size')return new Response(f.bodies.get(paths[0]),{headers:{'content-length':String(entry.size+1)}});
      if(kind==='compressed')return new Response(f.bodies.get(paths[0]),{headers:{'content-encoding':'gzip'}});
      const body=kind==='digest'?Buffer.alloc(entry.size,33):kind==='truncated'?Buffer.alloc(entry.size-1):Buffer.alloc(entry.size+1);
      return new Response(body);
    });
    await expect(f.run()).rejects.toThrow();expect(f.fetchImpl).toHaveBeenCalledTimes(1);
    expect(await readFile(destination)).toEqual(old);expect(await readFile(join(f.folder,'.unrelated.part'),'utf8')).toBe('keep');
    expect(await temporaryFiles(f)).toEqual([]);
  });
  it('retains completed earlier downloads when a later file fails without retrying',async()=>{
    const f=await fixture();f.fetchImpl.mockImplementationOnce(async()=>new Response(f.bodies.get(paths[0]))).mockImplementationOnce(async()=>new Response('failed',{status:500}));
    await expect(f.run()).rejects.toThrow();expect(f.fetchImpl).toHaveBeenCalledTimes(2);
    expect(await readFile(join(f.folder,paths[0]))).toEqual(f.bodies.get(paths[0]));expect(await temporaryFiles(f)).toEqual([]);
  });
  it('follows a bounded official CDN redirect manually and still verifies its bytes',async()=>{
    const f=await fixture(),cdn='https://us.aws.cdn.hf.co/xet-bridge/test?signature=public-download';
    f.fetchImpl.mockImplementationOnce(async()=>new Response(null,{status:302,headers:{location:cdn}}))
      .mockImplementationOnce(async url=>{expect(url).toBe(cdn);return new Response(f.bodies.get(paths[0]));});
    const result=await f.run();expect(result.downloaded).toBe(8);expect(f.fetchImpl).toHaveBeenCalledTimes(9);
  });
  it.each(['http://huggingface.co/file','https://example.com/file','https://huggingface.co.evil.test/file',
    'https://user:password@huggingface.co/file','https://huggingface.co:444/file','https://huggingface.co/file#fragment'])('refuses redirect outside the public HTTPS boundary: %s',async location=>{
    const f=await fixture();f.fetchImpl.mockImplementation(async()=>new Response(null,{status:302,headers:{location}}));
    await expect(f.run()).rejects.toThrow();expect(f.fetchImpl).toHaveBeenCalledTimes(1);expect(await temporaryFiles(f)).toEqual([]);
  });
  it('rejects redirect loops and automatically followed redirects',async()=>{
    const f=await fixture();f.fetchImpl.mockImplementation(async()=>new Response(null,{status:302,headers:{location:f.manifest.files[0].url}}));
    await expect(f.run()).rejects.toThrow('Too many');expect(f.fetchImpl).toHaveBeenCalledTimes(6);
    const response=new Response(f.bodies.get(paths[0]));Object.defineProperty(response,'redirected',{value:true});
    f.fetchImpl.mockClear().mockImplementation(async()=>response);
    await expect(f.run()).rejects.toThrow('unchecked redirect');expect(f.fetchImpl).toHaveBeenCalledTimes(1);
  });
  it('bounds waiting for response headers without retrying',async()=>{
    const f=await fixture();f.fetchImpl.mockImplementation(()=>new Promise(()=>{}));
    await expect(f.run({timeoutMs:20})).rejects.toThrow('timed out');expect(f.fetchImpl).toHaveBeenCalledTimes(1);expect(await temporaryFiles(f)).toEqual([]);
  });
  it('bounds a stalled response stream and preserves the original invalid file',async()=>{
    const f=await fixture({cached:true}),destination=join(f.folder,paths[0]);await writeFile(destination,'original invalid');let cancelled=false;
    f.fetchImpl.mockImplementation(async()=>new Response(new ReadableStream({pull:()=>new Promise(()=>{}),cancel(){cancelled=true;}})));
    await expect(f.run({timeoutMs:40})).rejects.toThrow('timed out');expect(cancelled).toBe(true);
    expect(await readFile(destination,'utf8')).toBe('original invalid');expect(await temporaryFiles(f)).toEqual([]);
  });
  it.each(['traversal','duplicate','unpinned','wrong-url','hash','total','missing','too-large','profile'])('rejects manifest %s before fetching',async kind=>{
    const f=await fixture();
    if(kind==='traversal')f.manifest.files[0].path='../outside.json';
    if(kind==='duplicate')f.manifest.files[1]=f.manifest.files[0];
    if(kind==='unpinned')f.manifest.revision='main';
    if(kind==='wrong-url')f.manifest.files[0].url='https://example.com/config.json';
    if(kind==='hash')f.manifest.files[0].sha256='bad';
    if(kind==='total')f.manifest.totalBytes++;
    if(kind==='missing')f.manifest.missingFiles=[{path:'extra.json',status:404}];
    if(kind==='too-large')f.manifest.files[0].size=150_000_001;
    if(kind==='profile')f.manifest.loader.dtype.encoder_model='fp32';
    await f.save();await expect(f.run()).rejects.toThrow();expect(f.fetchImpl).not.toHaveBeenCalled();
  });
  it.each(['missing','unknown','wrong-total','bad-first-byte-hash'])('rejects metadata manifest %s before fetching',async kind=>{
    const f=await fixture();
    if(kind==='missing')f.manifest.metadataFiles=[];
    if(kind==='unknown')f.manifest.metadataFiles[0].path='onnx/decoder_model_quantized.onnx';
    if(kind==='wrong-total')f.manifest.metadataBytes++;
    if(kind==='bad-first-byte-hash')f.manifest.metadataFiles[0].firstByteSha256='bad';
    await f.save();await expect(f.run()).rejects.toThrow();expect(f.fetchImpl).not.toHaveBeenCalled();
  });
  it('keeps metadata provenance separate while final verification checks its first byte',async()=>{
    const f=await fixture(),result=await f.run();
    expect(result.files).toHaveLength(7);expect(result.metadataFiles).toHaveLength(1);
    expect(result.metadataFiles[0].path).toBe(metadataPath);expect(result.totalBytes+result.metadataBytes).toBe([...f.bodies.values()].reduce((sum,b)=>sum+b.length,0));
    f.manifest.metadataFiles[0].firstByteSha256='c'.repeat(64);await f.save();
    await expect(f.run()).rejects.toThrow();
  });
  it('refuses cache directory junctions instead of writing through them',async()=>{
    const f=await fixture(),outside=join(f.root,'outside'),onnx=join(f.folder,'onnx');await mkdir(outside);await writeFile(join(outside,'keep.txt'),'untouched');
    expect(dirname(onnx)).toBe(f.folder);await rm(onnx,{recursive:true});await symlink(outside,onnx,process.platform==='win32'?'junction':'dir');
    await expect(f.run()).rejects.toThrow('not links');expect(f.fetchImpl).not.toHaveBeenCalled();
    expect(await readdir(outside)).toEqual(['keep.txt']);
  });
  it('refuses non-file destinations instead of deleting them',async()=>{
    const f=await fixture();await mkdir(join(f.folder,paths[0]));await writeFile(join(f.folder,paths[0],'keep.txt'),'keep');
    await expect(f.run()).rejects.toThrow('ordinary file');expect(f.fetchImpl).not.toHaveBeenCalled();
    expect(await readFile(join(f.folder,paths[0],'keep.txt'),'utf8')).toBe('keep');
  });
  it('refuses a manifest change before final qualification',async()=>{
    const f=await fixture();f.fetchImpl.mockImplementationOnce(async()=>{await writeFile(f.manifestPath,JSON.stringify({...f.manifest,totalBytes:0}));return new Response(f.bodies.get(paths[0]));});
    await expect(f.run()).rejects.toThrow('manifest changed');expect(await temporaryFiles(f)).toEqual([]);
  });
  it('offers help without setup and refuses CLI path or URL overrides',()=>{
    const script=fileURLToPath(new URL('./prepare-first-learner-speech-assets.mjs',import.meta.url));
    const help=spawnSync(process.execPath,[script,'--help'],{encoding:'utf8'});expect(help.status).toBe(0);expect(help.stdout).toContain('No inference');
    const override=spawnSync(process.execPath,[script,'--url','https://example.com/'],{encoding:'utf8'});expect(override.status).toBe(1);expect(override.stderr).toContain('paths/URLs are fixed');
  });
});
