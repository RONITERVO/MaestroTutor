// Replays native observations; this browser probe does not execute Unity.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_OWNERSHIP_EVIDENCE||'.quest-evidence/room-ownership');await mkdir(out,{recursive:true});
const native={};for(const name of ['held','released'])native[name]=JSON.parse(await readFile(resolve(out,'native',name+'.json'),'utf8'));
assert(native.held.ownership.owners.some(o=>o.role==='grab'));assert(!native.released.ownership.owners.some(o=>o.role==='grab'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});context.setDefaultTimeout(15000);
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_room-ownership',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Room ownership evidence</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_room-ownership');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0;
 async function publish(name,workspaceView='rules') {assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native[name],session:'e'.repeat(32),revision:++revision,visible:true,workspaceView}),true);}
 await publish('held');const rules=page.getByRole('region',{name:'Behaviours and triggers'});await rules.locator('.room-ownership summary').click();
 await rules.getByText('Your grip',{exact:true}).waitFor();await rules.getByText('Your hands',{exact:true}).waitFor();
 await page.screenshot({path:resolve(out,'book-held.png')});
 await publish('released');await rules.getByText('No actions are controlling objects.',{exact:true}).waitFor();
 assert.equal(await rules.getByText('Your grip',{exact:true}).count(),0);await page.screenshot({path:resolve(out,'book-released.png')});
 await publish('held','objects');const objects=page.getByRole('region',{name:'Room objects and parts'});await objects.locator('.room-ownership summary').click();await objects.getByText('Your grip',{exact:true}).waitFor();
 await page.screenshot({path:resolve(out,'book-object-ownership.png')});assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSources:Object.keys(native),browserNativeExecution:false,transportAcknowledgements:'none',views:['behaviours','objects'],errors},null,2)+'\n');
 console.log('Chrome displayed native grip and release evidence in both optional book workspaces.');
}finally{await browser.close();}
