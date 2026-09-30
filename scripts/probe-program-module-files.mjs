// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real native definition/receipt; browser transport acknowledgements are simulated.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['127.0.0.1','localhost'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/module-files');await mkdir(out,{recursive:true});
const file=JSON.parse(await readFile(resolve(out,'native/module-file.json'),'utf8')),receipt=JSON.parse(await readFile(resolve(out,'native/receipt.json'),'utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});await context.route('**/*',r=>['localhost','127.0.0.1'].includes(new URL(r.request().url()).hostname)?r.continue():r.abort());
 await context.route(base+'/_module-files',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><title>Maestro module files</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_module-files');
 await page.evaluate(async({file,receipt})=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {ProgramModuleLibrary}=await import('/src/platform/quest/ProgramModuleLibrary.tsx');const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {RoomAgentClient}=await import('/src/platform/quest/roomAgentBridge.ts');await import('/src/app/index.css');await import('/src/platform/quest/roomWorkspace.css');
  const client=new RoomAgentClient();let state={version:1,session:'a'.repeat(32),revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',created:[],objects:[],visible:true,canUndo:false,canRedo:false,physicsRunning:false,capabilities:['catalog.v1','moduleLibrary.v1','moduleLibraryFiles.v1','execution.v1','executionReceipts.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:receipt.id,storageError:null}};
  if(!client.receive(state))throw new Error('Invalid initial scene');window.requests=[];window.holdImport=true;window.savedDrafts=[];
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('div',{className:'room-workspace rule-workspace',style:{position:'relative',display:'block',padding:20}},React.createElement(ProgramModuleLibrary,{client,sequence:{id:'b'.repeat(32),name:'Caller',program:JSON.stringify(file.definition.program),repeat:false,interruption:0},rulesRevision:1,dirty:false,disabled:false,onChange:value=>window.savedDrafts.push(value),onClose:()=>{}}))));
  let last=0;
  setInterval(()=>{const request=client.snapshot().request;if(request&&request.sequence!==last){const command=request.commands[0];if(command.action==='execution'&&window.holdImport){if(!window.requests.some(r=>r.sequence===request.sequence))window.requests.push(request);return;}last=request.sequence;
   if(!window.requests.some(r=>r.sequence===request.sequence))window.requests.push(request);state={...state,ack:request.sequence};
   if(command.catalog?.operation==='search')state.catalog={operation:'search',category:'modules',query:'',offset:0,pageSize:6,total:1,entries:[{id:file.hash,version:1,label:file.definition.name}],revision:2,ready:true,pending:false,status:'Found'};
   if(command.catalog?.operation==='inspect')state.catalog={operation:'inspect',category:'modules',capability:file.hash,version:1,definition:file.definition,revision:2,ready:true,pending:false,status:'Inspected'};
   if(command.action==='execution'){const {call,...summary}=receipt;state.execution={selected:receipt,running:[],outcomes:[summary],nextRunId:'e'.repeat(32),storageError:null};}
  }state={...state,revision:state.revision+1};if(!client.receive(state))throw new Error('Invalid simulated native response');},50);
  window.exported='';window.holdFinish=true;window.finishRequested=false;window.bytes=0;window.chunks=[];
  setInterval(()=>{if(!window.maestroBook)return;const r=window.maestroBook.fileExportPoll();if(!r)return;
   if(r.operation==='finish'&&window.holdFinish){window.finishRequested=true;return;}
   if(r.operation==='open'){window.chunks=[];window.bytes=0;}
   if(r.operation==='chunk'){const bytes=Uint8Array.from(atob(r.data),c=>c.charCodeAt(0));window.chunks.push(bytes);window.bytes+=bytes.length;}
   if(r.operation==='finish')window.exported=new TextDecoder().decode(Uint8Array.from(window.chunks.flatMap(v=>Array.from(v))));
   window.maestroBook.fileExportResult({version:1,id:r.id,sequence:r.sequence,ok:true,bytes:window.bytes,...(r.operation==='finish'?{location:'Downloads/Maestro/portable.json'}:{})});
  },10);
 },{file,receipt});
 const click=name=>page.getByRole('button',{name,exact:true}).click();
 await click('Search modules');await page.getByRole('button',{name:new RegExp(file.definition.name+' ')}).click();await page.getByLabel('Inspected library module').waitFor();
 await click('Export module file');await page.waitForFunction(()=>window.finishRequested);assert.equal(await page.getByText('Saved: Downloads/Maestro/portable.json',{exact:true}).count(),0);
 await page.evaluate(()=>{window.holdFinish=false;});await page.getByText('Saved: Downloads/Maestro/portable.json',{exact:true}).waitFor();
 const exported=await page.evaluate(()=>window.exported);assert.deepEqual(JSON.parse(exported),file);
 await page.getByLabel('Module file',{exact:true}).setInputFiles({name:'portable.json',mimeType:'application/json',buffer:Buffer.from(exported)});
 await page.getByLabel('Module file preview').waitFor();assert.equal((await page.evaluate(()=>window.requests)).filter(r=>r.commands[0].action==='execution').length,0);
 await page.getByLabel('Module file preview').evaluate(e=>e.scrollIntoView({block:'start'}));await page.screenshot({path:resolve(out,'file-preview.png')});
 await click('Import file to library');await page.waitForFunction(()=>window.requests.some(r=>r.commands[0].action==='execution'));assert.equal(await page.getByLabel('Library action result').count(),0);
 await page.evaluate(()=>{window.holdImport=false;});await page.getByLabel('Library action result').waitFor();assert((await page.getByLabel('Library action result').textContent()).includes(file.hash));
 await page.getByLabel('Library action result').evaluate(e=>e.scrollIntoView({block:'start'}));await page.screenshot({path:resolve(out,'import-receipt.png')});
 const requests=await page.evaluate(()=>window.requests),imports=requests.filter(r=>r.commands[0].action==='execution');assert.equal(imports.length,1);assert.deepEqual(imports[0].commands[0].execution.call,receipt.call);assert.deepEqual(await page.evaluate(()=>window.savedDrafts),[]);
 const bad={...file,hash:'0'.repeat(64)};await page.getByLabel('Module file',{exact:true}).setInputFiles({name:'bad.json',mimeType:'application/json',buffer:Buffer.from(JSON.stringify(bad))});
 await page.getByRole('status').filter({hasText:'mismatched content ID'}).waitFor();assert.equal(await page.getByLabel('Module file preview').count(),0);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeDefinitionAndReceipt:true,simulatedNativeTransport:true,browserNativeExecution:false,exactExportRoundTrip:true,noSuccessBeforeClose:true,selectionOnlyPreviews:true,importUsesSharedReceipt:true,noBehaviourDraftChange:true,tamperedFileRejected:true,requests,errors},null,2)+'\n');console.log('Book module export/import verified with real native definition and receipt; acknowledgements simulated.');
}finally{await browser.close();}
