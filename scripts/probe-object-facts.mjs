// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Replays actual Unity object-position observations; Chrome executes no Unity work; catalog acknowledgements are simulated.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_OBJECT_FACTS_EVIDENCE||'.quest-evidence/object-facts');await mkdir(out,{recursive:true});
const native={};for(const name of ['definition','before','moving','paused','resumed','missing','disabled','failed'])native[name]=JSON.parse(await readFile(resolve(out,'native',name+'.json'),'utf8'));
assert(native.before.capabilities.includes('factQueries.v1'));assert.equal(native.moving.rules.running.find(r=>r.sequenceId===native.moving.rules.selected.id).state.find(v=>v.name==='moved').value,'True');
assert.equal(native.paused.catalog.available,false);assert.equal(native.failed.rules.running.length,0);
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});context.setDefaultTimeout(15000);
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_object-facts',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Object fact evidence</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_object-facts');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0;
 async function publish(name) {assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native[name],session:'f'.repeat(32),revision:++revision,visible:true,workspaceView:'rules'}),true);}
 await publish('before');const rules=page.getByRole('region',{name:'Behaviour blocks'}),target=native.before.catalog.arguments.target;
 await rules.getByRole('button',{name:'Edit values read_before',exact:true}).click();
 await rules.getByLabel('Assigned value fact target mode',{exact:true}).selectOption('literal');await rules.getByLabel('Assigned value fact target',{exact:true}).selectOption('maestro');
 await rules.getByLabel('Assigned value fact target',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-position-inputs.png')});
 await rules.getByLabel('Assigned value fact target',{exact:true}).selectOption(target);await rules.getByLabel('Assigned value fact target mode',{exact:true}).selectOption('expression');await rules.getByLabel('Assigned value fact target expression source',{exact:true}).selectOption('state:target');
 await rules.getByRole('button',{name:'Update draft',exact:true}).click();await rules.getByRole('button',{name:'Edit full source',exact:true}).click();
 assert.deepEqual(JSON.parse(await rules.getByLabel('Program JSON',{exact:true}).inputValue()),JSON.parse(native.before.rules.selected.program));
 await rules.getByRole('button',{name:'Discard editor draft',exact:true}).click();await rules.getByRole('button',{name:'Discard draft',exact:true}).click();
 await publish('moving');const live=rules.locator('[aria-label="Live program values"]');await live.waitFor();assert((await live.textContent()).includes('state.moved = True'));await live.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-position-live.png')});
 await publish('paused');assert.equal(await live.count(),0);await publish('missing');await publish('failed');assert((await page.locator('body').innerText()).includes('unavailable'));
 // The definition and readings below are actual Unity observations. Only the
 // catalog search page and transport acknowledgements are browser simulations.
 await publish('before');await page.getByRole('button',{name:'Action catalog',exact:true}).click();await page.getByLabel('Catalog category').selectOption('facts');
 const requests=[];
 async function acknowledge(catalog){await page.waitForFunction(()=>Boolean(window.maestroBook.roomSnapshot().request));const request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);requests.push(request);assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native.before,session:'f'.repeat(32),revision:++revision,ack:request.sequence,ok:true,catalog,visible:true,workspaceView:'rules'}),true);}
 await page.getByRole('button',{name:'Search',exact:true}).click();const definition=native.definition.catalog.definition;
 await acknowledge({operation:'search',category:'facts',query:'',offset:0,pageSize:6,total:1,entries:[{id:definition.id,version:definition.version,label:definition.label}],status:'Simulated search; actual native definition'});
 await page.getByRole('button',{name:new RegExp(definition.label)}).click();await acknowledge(native.definition.catalog);
 await page.getByLabel('Fact inputs target',{exact:true}).selectOption(target);await page.getByRole('button',{name:'Read fact',exact:true}).click();await acknowledge(native.before.catalog);
 await page.getByLabel('Current fact value',{exact:true}).getByText('Current value',{exact:true}).waitFor();await page.getByLabel('Current fact value').scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-position-catalog.png')});
 await page.getByLabel('Fact inputs target',{exact:true}).selectOption('maestro');assert((await page.getByLabel('Current fact value').textContent()).includes('Not read yet'));
 assert.equal(requests.at(-1).commands[0].catalog.arguments.target,target);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSources:Object.keys(native),browserNativeExecution:false,transportAcknowledgements:'simulated for catalog only',requests,canonicalEditorRoundTrip:true,nativePositionTrace:true,previousTargetReadingHidden:true,pauseClearsRun:true,missingObjectFailureVisible:true,errors},null,2)+'\n');
 console.log('Chrome edited fact targets, displayed actual native snapshots and inspected recorded native catalog readings.');
}finally{await browser.close();}
