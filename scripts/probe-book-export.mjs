// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Chrome UI/IndexedDB verification with a simulated Android export transport.
// Native MediaStore semantics are checked separately by Android tests; no headset access.
import {chromium} from 'playwright-core';
import {mkdir,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_BOOK_EXPORT_EVIDENCE||'.quest-evidence/book-export');await mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024},locale:'en-US'});context.setDefaultTimeout(15000);
 await context.route('**/*',r=>['localhost','127.0.0.1'].includes(new URL(r.request().url()).hostname)?r.continue():r.abort());
 await context.route(base+'/_book-export',r=>r.fulfill({contentType:'text/html',body:'<!doctype html><title>Quest export verification</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_book-export');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {default:SessionControls}=await import('/src/features/session/components/SessionControls.tsx');
  const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');const chats=await import('/src/features/chat/services/chatHistory.ts');const {setGlobalProfileDB}=await import('/src/features/session/services/globalProfile.ts');await import('/src/app/index.css');
  window.historyFixture=[{id:'u',role:'user',text:'Let’s practise Finnish. 🎵 日本語',timestamp:1},{id:'a',role:'assistant',text:'Hyvää iltaa!',timestamp:2}];
  await chats.saveChatHistoryDB('pair',window.historyFixture);await setGlobalProfileDB('Finnish learner 🎵 '.repeat(3000));
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'pair'},messages:window.historyFixture,isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false});
  window.alerts=[];window.alert=v=>window.alerts.push(v);window.showSaveFilePicker=undefined;
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('div',{className:'bg-page-bg notebook-lines',style:{minHeight:'100%',padding:24}},React.createElement(SessionControls),React.createElement('p',null,'Let’s practise Finnish. 🎵 日本語'),React.createElement('p',null,'Hyvää iltaa!'))));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));
 await page.evaluate(()=>{
  window.holdFinish=true;window.finishedRequest=null;window.exportFiles=[];window.exportChunks=[];window.exportBytes=0;window.exportName='';window.exportFailure=false;
  window.exportTimer=setInterval(()=>{
   const r=window.maestroBook.fileExportPoll();if(!r)return;
   if(r.operation==='finish'&&window.holdFinish){window.finishedRequest=r;return;}
   if(r.operation==='open'){window.exportName=r.name;window.exportChunks=[];window.exportBytes=0;}
   if(r.operation==='chunk'){if(r.offset!==window.exportBytes)throw new Error('Unexpected byte offset');const b=Uint8Array.from(atob(r.data),c=>c.charCodeAt(0));window.exportChunks.push(b);window.exportBytes+=b.length;}
   if(r.operation==='finish')window.exportFiles.push({name:window.exportName,text:new TextDecoder().decode(Uint8Array.from(window.exportChunks.flatMap(v=>Array.from(v))))});
   window.maestroBook.fileExportResult({version:1,id:r.id,sequence:r.sequence,ok:!window.exportFailure,bytes:window.exportBytes,...(r.operation==='finish'?{location:'Downloads/Maestro/'+window.exportName}:{}),...(window.exportFailure?{error:'Test storage failure'}:{})});
  },5);
  window.maestroBook.fileExportPoll();
 });
 await page.getByTitle('All Chats Controls',{exact:true}).click();await page.getByTitle('Save All Chats',{exact:true}).click();const input=page.getByPlaceholder('Type "SAVE" to confirm',{exact:true});await input.fill('SAVE');await input.press('Enter');
 await page.waitForFunction(()=>Boolean(window.finishedRequest));assert.equal(await page.getByRole('status').filter({hasText:'Saved: Downloads/Maestro/'}).count(),0);await page.screenshot({path:resolve(out,'saving-before-receipt.png')});
 await page.evaluate(()=>{window.holdFinish=false;});await page.getByRole('status').filter({hasText:'Saved: Downloads/Maestro/'}).waitFor();await page.screenshot({path:resolve(out,'saved-receipt.png')});
 const exported=await page.evaluate(()=>({file:window.exportFiles[0],bytes:window.exportBytes,chunks:window.exportChunks.length,alerts:window.alerts}));assert.deepEqual(exported.alerts,[]);assert(exported.chunks>3);assert.equal(Buffer.byteLength(exported.file.text),exported.bytes);
 // Restore the exact captured bytes through the original Load All controls.
 await page.evaluate(async()=>{const chats=await import('/src/features/chat/services/chatHistory.ts');const {useMaestroStore}=await import('/src/store/index.ts');await chats.clearAndSaveAllHistoriesDB({obsolete:[{id:'old',role:'user',text:'old',timestamp:3}]});useMaestroStore.setState({messages:[]});});
 await page.locator('input[type=file][accept=".ndjson,.jsonl"]').first().setInputFiles({name:exported.file.name,mimeType:'application/x-ndjson',buffer:Buffer.from(exported.file.text)});
 const load=page.getByPlaceholder('Type "LOAD" to confirm',{exact:true});await load.fill('LOAD');await load.press('Enter');
 await page.waitForFunction(()=>window.alerts.some(x=>String(x).includes('Successfully loaded'))||window.alerts.length>0);
 await page.waitForFunction(()=>!document.querySelector('input[placeholder=\'Type "LOAD" to confirm\']'));
 const restored=await page.evaluate(async()=>{const chats=await import('/src/features/chat/services/chatHistory.ts');return {history:await chats.getChatHistoryDB('pair'),pairs:Object.keys(await chats.getAllChatHistoriesDB()),expected:window.historyFixture,alerts:window.alerts};});assert.deepEqual(restored.history,restored.expected);assert.deepEqual(restored.pairs,['pair']);assert.deepEqual(restored.alerts,['Successfully loaded and replaced 1 chat sessions!']);
 await page.waitForFunction(()=>!document.querySelector('input[placeholder=\'Type "LOAD" to confirm\']'));
 await page.evaluate(()=>{window.exportFailure=true;});await page.getByTitle('Save All Chats',{exact:true}).click();const again=page.getByPlaceholder('Type "SAVE" to confirm',{exact:true});await again.fill('SAVE');await again.press('Enter');await page.waitForFunction(()=>window.alerts.some(x=>String(x).includes('Test storage failure')));assert.equal(await page.getByRole('status').filter({hasText:'Saved: Downloads/Maestro/'}).count(),0);
 // Mandatory pre-load export failure must stop before replacement.
 await page.locator('input[type=file][accept=".ndjson,.jsonl"]').first().setInputFiles({name:exported.file.name,mimeType:'application/x-ndjson',buffer:Buffer.from(exported.file.text)});
 const blockedLoad=page.getByPlaceholder('Type "LOAD" to confirm',{exact:true});await blockedLoad.fill('LOAD');await blockedLoad.press('Enter');
 await page.getByRole('status').filter({hasText:'A backup was not completed.'}).waitFor();
 assert.equal(await page.evaluate(async()=>{const chats=await import('/src/features/chat/services/chatHistory.ts');return JSON.stringify(await chats.getChatHistoryDB('pair'));}),JSON.stringify(restored.expected));
 await page.getByRole('button',{name:'Cancel action',exact:true}).click();
 // Reset must not clear anything after an export failure.
 await page.evaluate(()=>{window.deleteAttempts=0;indexedDB.deleteDatabase=()=>{window.deleteAttempts++;throw new Error('Unexpected deferred delete');};});
 await page.getByTitle('Backup & Reset',{exact:true}).click();const reset=page.getByPlaceholder('Type "DELETE" to confirm',{exact:true});await reset.fill('DELETE');await reset.press('Enter');
 await page.getByRole('alert').filter({hasText:'A backup was not completed.'}).waitFor();await page.screenshot({path:resolve(out,'reset-backup-failed.png')});
 const snapshot=()=>page.evaluate(async()=>{const {openDB}=await import('/src/core/db/index.ts');const db=await openDB();return await new Promise((resolve,reject)=>{const tx=db.transaction(Array.from(db.objectStoreNames),'readonly');const requests=Array.from(db.objectStoreNames).map(name=>[name,tx.objectStore(name).getAll()]);tx.oncomplete=()=>{db.close();resolve(JSON.stringify(requests.map(([name,r])=>[name,r.result])));};tx.onabort=()=>reject(tx.error);});});
 const beforeReset=await snapshot();
 // An actual IndexedDB abort rolls back every clear, without a delayed delete request.
 await page.evaluate(()=>{window.exportFailure=false;window.normalClear=IDBObjectStore.prototype.clear;IDBObjectStore.prototype.clear=function(){const request=Reflect.apply(window.normalClear,this,[]);if(this.name==='chatHistories')request.addEventListener('success',()=>this.transaction.abort(),{once:true});return request;};});
 await reset.press('Enter');await page.getByRole('alert').filter({hasText:'Reset failed.'}).waitFor();
 await page.evaluate(()=>{IDBObjectStore.prototype.clear=window.normalClear;});assert.equal(await snapshot(),beforeReset);
 // A synchronous exception after a queued clear must also roll back all stores.
 await page.evaluate(()=>{let count=0;IDBObjectStore.prototype.clear=function(){if(++count===2)throw new Error('Injected synchronous clear failure');return Reflect.apply(window.normalClear,this,[]);};});
 await reset.press('Enter');await page.getByRole('alert').filter({hasText:'Injected synchronous clear failure'}).waitFor();
 await page.evaluate(()=>{IDBObjectStore.prototype.clear=window.normalClear;});assert.equal(await snapshot(),beforeReset);
 // A conversation mutation while clearing also aborts the transaction.
 await page.evaluate(async()=>{const {useMaestroStore}=await import('/src/store/index.ts');window.resetStore=useMaestroStore;IDBObjectStore.prototype.clear=function(){const request=Reflect.apply(window.normalClear,this,[]);if(this.name==='chatHistories')request.addEventListener('success',()=>{const old=window.resetStore.getState().messages;window.resetStore.setState({messages:[...old,{id:'new-during-reset',role:'user',text:'Keep this new message',timestamp:3}]});},{once:true});return request;};});
 await reset.press('Enter');await page.getByRole('alert').filter({hasText:'Nothing was reset.'}).waitFor();await page.evaluate(()=>{IDBObjectStore.prototype.clear=window.normalClear;});assert.equal(await snapshot(),beforeReset);
 assert.equal(await page.evaluate(()=>window.resetStore.getState().messages.at(-1).id),'new-during-reset');assert.equal(await page.evaluate(()=>window.deleteAttempts),0);
 await page.screenshot({path:resolve(out,'reset-change-preserved.png')});
 // A stable confirmed backup permits the real reset and a single reload.
 const reloaded=page.waitForEvent('framenavigated',frame=>frame===page.mainFrame());await reset.press('Enter');await reloaded;
 const cleared=await page.evaluate(async()=>{const {openDB}=await import('/src/core/db/index.ts');const db=await openDB();return await new Promise(resolve=>{const tx=db.transaction(Array.from(db.objectStoreNames),'readonly');const counts=Array.from(db.objectStoreNames).map(name=>tx.objectStore(name).count());tx.oncomplete=()=>{db.close();resolve(counts.every(r=>r.result===0));};});});assert.equal(cleared,true);
 const profileOnly=await page.evaluate(async()=>{const {stageBackup,commitBackupStage}=await import('/src/features/session/services/backupArchive.ts');const {getGlobalProfileDB}=await import('/src/features/session/services/globalProfile.ts');const rows=[{type:'header',format:'ndjson-v1',taskArchiveVersion:1},{type:'globalProfile',text:'Profile before first lesson'},{type:'end',chats:0,tasks:0}];const stage=await stageBackup(async visit=>{for(const row of rows)await visit(JSON.stringify(row));});const result=await commitBackupStage(stage);return {result,profile:(await getGlobalProfileDB()).text};});assert.deepEqual(profileOnly,{result:{chats:0,tasks:0},profile:'Profile before first lesson'});
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({simulatedNativeTransport:true,nativeExecution:false,originalSaveAllControls:true,noReceiptBeforeFinish:true,originalLoadAllRestoresCapturedBytes:true,failedExportDoesNotShowSuccess:true,requiredBackupBlocksLoadAndReset:true,resetAbortAtomic:true,synchronousClearFailureAtomic:true,concurrentConversationChangeAbortsReset:true,noDeleteDatabaseRequest:true,successfulResetReloads:true,profileOnlyRestore:true,utf8Bytes:exported.bytes,chunks:exported.chunks,errors},null,2)+'\n');
 console.log('Original Save/Load/Reset controls passed: confirmed export, failure preservation, atomic reset rollback, concurrent-change guard, successful reset/reload and zero-chat profile restore. Native transport was simulated.');
}finally{await browser.close();}
