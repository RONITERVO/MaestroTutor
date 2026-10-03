// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Replays actual Unity condition-wait observations; Chrome executes no Unity work and does not simulate acknowledgements.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_CONDITIONS_EVIDENCE||'.quest-evidence/conditions');await mkdir(out,{recursive:true});
const native={};for(const name of ['saved','waiting','matched','paused','resumed','missing'])native[name]=JSON.parse(await readFile(resolve(out,'native',name+'.json'),'utf8'));
assert(native.saved.capabilities.includes('conditionWaits.v1'));assert.equal(native.saved.rules.running.length,0);
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});context.setDefaultTimeout(15000);
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_conditions',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Condition wait evidence</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_conditions');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0;
 async function publish(name) {assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native[name],session:'f'.repeat(32),revision:++revision,visible:true,workspaceView:'rules'}),true);}
 await publish('saved');const rules=page.getByRole('region',{name:'Behaviour blocks'});
 await rules.getByRole('button',{name:'Edit values watch',exact:true}).click();
 assert.equal(await rules.getByLabel('Initial condition',{exact:true}).inputValue(),'baseline');
 await rules.getByLabel('Detect condition',{exact:true}).selectOption('either');await rules.getByLabel('Stable seconds value',{exact:true}).fill('0.4');
 await rules.getByLabel('Detect condition',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-condition-inputs.png')});
 await rules.getByLabel('Condition received',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-condition-policy.png')});
 await rules.getByLabel('Detect condition',{exact:true}).selectOption('true');await rules.getByLabel('Stable seconds value',{exact:true}).fill('0.2');
 await rules.getByRole('button',{name:'Update draft',exact:true}).click();await rules.getByRole('button',{name:'Edit full source',exact:true}).click();
 assert.deepEqual(JSON.parse(await rules.getByLabel('Program JSON',{exact:true}).inputValue()),JSON.parse(native.saved.rules.selected.program));
 await rules.getByRole('button',{name:'Discard editor draft',exact:true}).click();await rules.getByRole('button',{name:'Discard draft',exact:true}).click();
 await publish('waiting');const live=rules.locator('[aria-label="Live program values"]');await live.waitFor();assert((await rules.innerText()).includes('Waiting for condition'));await live.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-condition-waiting.png')});
 await publish('matched');assert((await live.textContent()).includes('state.matched = True'));await live.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-condition-matched.png')});
 await publish('paused');assert.equal(await live.count(),0);await publish('resumed');assert.equal(await live.count(),0);await publish('missing');assert((await page.locator('body').innerText()).includes('unavailable'));assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSources:Object.keys(native),browserNativeExecution:false,transportAcknowledgements:false,canonicalEditorRoundTrip:true,nativeConditionTrace:true,pauseClearsRun:true,noResumeOnFocus:true,missingObjectFailureVisible:true,errors},null,2)+'\n');
 console.log('Chrome edited the shared condition and displayed actual native waiting, matched, paused and unavailable outcomes.');
}finally{await browser.close();}
