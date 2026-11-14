// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Replays actual Unity observations; Chrome does not execute physics or acknowledge commands.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_CONTACT_EVIDENCE||'.quest-evidence/event-fields');await mkdir(out,{recursive:true});
const native={};for(const name of ['waiting','reaction','floor','paused'])native[name]=JSON.parse(await readFile(resolve(out,'native','contact-'+name+'.json'),'utf8'));
assert(native.waiting.capabilities.includes('eventFields.v1'));assert.equal(native.reaction.rules.running[0].state.find(v=>v.name==='lastKind').value,'object');
assert.equal(native.floor.rules.running[0].state.find(v=>v.name==='lastKind').value,'scannedRoom');assert.equal(native.paused.rules.running.length,0);
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});context.setDefaultTimeout(15000);
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_event-fields',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Contact program evidence</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_event-fields');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0;
 async function publish(name) {assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native[name],session:'f'.repeat(32),revision:++revision,visible:true,workspaceView:'rules'}),true);}
 await publish('waiting');const rules=page.getByRole('region',{name:'Behaviour blocks'});
 await rules.getByRole('button',{name:'Edit values contact',exact:true}).click();
 await rules.getByLabel('Event field speed',{exact:true}).selectOption('');
 await rules.getByRole('button',{name:'Update draft',exact:true}).click();
 await rules.getByRole('button',{name:'Edit values contact',exact:true}).click();
 await rules.getByLabel('Event field speed',{exact:true}).selectOption('speed');
 await rules.getByRole('group',{name:'Store event details'}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-contact-fields.png')});
 await rules.getByRole('button',{name:'Update draft',exact:true}).click();
 await rules.getByRole('button',{name:'Edit full source',exact:true}).click();
 const source=await rules.getByLabel('Program JSON',{exact:true}).inputValue();assert.deepEqual(JSON.parse(source),JSON.parse(native.waiting.rules.selected.program));
 await rules.getByRole('button',{name:'Discard editor draft',exact:true}).click();await rules.getByRole('button',{name:'Discard draft',exact:true}).click();
 await publish('reaction');
 const live=rules.locator('[aria-label="Live program values"]');await live.waitFor();assert((await live.textContent()).includes('state.lastKind = object'));
 await live.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-contact-reaction.png')});
 await publish('floor');await rules.locator('[aria-label="Live program values"]').getByText('state.lastKind',{exact:true}).waitFor();
 assert((await rules.locator('[aria-label="Live program values"]').textContent()).includes('scannedRoom'));
 await publish('paused');assert.equal(await rules.locator('[aria-label="Live program values"]').count(),0);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSources:Object.keys(native),browserNativeExecution:false,transportAcknowledgements:'none',canonicalEditorRoundTrip:true,contactTrace:true,pauseClearsRun:true,errors},null,2)+'\n');
 console.log('Chrome edited typed fields and displayed actual native collision/reaction/pause observations.');
}finally{await browser.close();}
