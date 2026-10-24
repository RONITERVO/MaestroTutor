// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real Chrome authoring; captured native observations with simulated catalog-query acknowledgements.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/structure-watch');await mkdir(out,{recursive:true});
const native={};for(const name of ['library','saved','armed','disturbed','rebuilt','changed'])native[name]=JSON.parse(await readFile(resolve(out,'native',name+'.json'),'utf8'));
assert.equal(native.saved.catalog.included,true);assert.equal(native.saved.rules.running.length,0);
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});context.setDefaultTimeout(15000);
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_structure-watch',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Included structure behaviour</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_structure-watch');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0;
 async function publish(name,ack=0){assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native[name],session:'f'.repeat(32),revision:++revision,ack,visible:true,workspaceView:'rules'}),true);}
 await publish('saved');const rules=page.getByRole('region',{name:'Behaviour blocks'});
 await rules.getByRole('button',{name:'Reusable modules',exact:true}).click();const library=page.getByRole('region',{name:'Reusable module library'});
 await library.getByRole('button',{name:'Search modules',exact:true}).click();let request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);assert.equal(request.commands[0].catalog.operation,'search');await publish('library',request.sequence);
 await library.getByRole('button',{name:/Structure state waits/}).click();request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);assert.equal(request.commands[0].catalog.operation,'inspect');await publish('saved',request.sequence);
 assert.equal(await library.getByRole('button',{name:'Remove this library copy'}).count(),0);
 await library.getByRole('button',{name:'Replace draft with editable copy'}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-included-module.png')});
 await library.getByRole('button',{name:'Replace draft with editable copy'}).click();await rules.getByRole('button',{name:'Edit full source',exact:true}).click();
 const copied=JSON.parse(await rules.getByLabel('Program JSON',{exact:true}).inputValue());assert.deepEqual(copied,native.saved.catalog.definition.program);assert.equal(await page.evaluate(()=>window.maestroBook.roomSnapshot().request),null);
 await rules.getByRole('button',{name:'Discard editor draft',exact:true}).click();await rules.getByRole('button',{name:'Edit values watch',exact:true}).click();await rules.getByLabel('Initial condition',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-editable-structure-condition.png')});await rules.getByRole('button',{name:'Discard editor draft',exact:true}).click();await rules.getByRole('button',{name:'Discard draft',exact:true}).click();
 await publish('armed',request.sequence);const live=rules.locator('[aria-label="Live program values"]');await live.waitFor();assert((await live.textContent()).includes('waitingForDisturbance'));
 await publish('disturbed',request.sequence);assert((await live.textContent()).includes('state.cycles = 1'));assert((await live.textContent()).includes('waitingForRebuild'));await live.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-structure-disturbed.png')});
 await publish('rebuilt',request.sequence);assert((await live.textContent()).includes('state.cycles = 1'));assert((await live.textContent()).includes('waitingForDisturbance'));
 await publish('changed',request.sequence);assert.equal(await live.count(),0);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:'Real Chrome editing. Native state was captured from PlayMode; only catalog query acknowledgements are simulated.',nativeSources:Object.keys(native),includedSourceCopyMatched:true,copyDidNotSaveOrStart:true,conditionEditable:true,nativeKnockdownAndRearmVisible:true,definitionChangeStops:true,errors},null,2)+'\n');
 console.log('Included native module copied to editable book draft; real knockdown, rebuild and stopped observations displayed.');
}finally{await browser.close();}
