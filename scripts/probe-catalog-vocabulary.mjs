// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Replays actual Unity catalog observations; transport acknowledgements are simulated.
// Chrome never executes Unity, produces physics facts or calls a model provider.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_VOCABULARY_EVIDENCE||'.quest-evidence/catalog-vocabulary');await mkdir(out,{recursive:true});
const native={};for(const name of ['events','events-next','contact','facts','false','true','disabled','unavailable','speaking','audio-paused'])native[name]=JSON.parse(await readFile(resolve(out,'native','vocabulary-'+name+'.json'),'utf8'));
assert(native.events.capabilities.includes('catalogVocabulary.v1'));assert.equal(native.false.catalog.value,false);assert.equal(native.true.catalog.value,true);assert.equal(native.disabled.catalog.value,null);
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});context.setDefaultTimeout(15000);
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_catalog-vocabulary',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Catalog vocabulary evidence</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[],queries=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_catalog-vocabulary');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0,ack=0;
 async function publish(name) {assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native[name],session:'f'.repeat(32),ack,revision:++revision,visible:true,workspaceView:'rules'}),true);}
 async function reply(expected,name) {
  await page.waitForFunction(()=>Boolean(window.maestroBook.roomSnapshot().request));
  const request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);
  assert.deepEqual(request.commands,[{action:'catalog',catalog:expected}]);assert.deepEqual(request.conditions,[]);queries.push(expected);ack=request.sequence;
  await publish(name);await page.waitForFunction(()=>!window.maestroBook.roomSnapshot().request);
 }
 await publish('events');await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Catalog category').selectOption('events');await page.getByRole('button',{name:'Search',exact:true}).click();
 await reply({operation:'search',category:'events',query:'',offset:0},'events');
 await page.getByRole('button',{name:'Next events',exact:true}).click();await reply({operation:'search',category:'events',query:'',offset:6},'events-next');
 await page.getByRole('button',{name:'Previous events',exact:true}).click();await reply({operation:'search',category:'events',query:'',offset:0},'events');
 await page.getByRole('button',{name:/Object contact began/}).click();await reply({operation:'inspect',category:'events',capability:'object.collided',version:1},'contact');
 await page.getByRole('region',{name:'Event definition'}).waitFor();assert((await page.getByRole('region',{name:'Event definition'}).textContent()).includes('speed'));
 assert.equal(await page.getByRole('button',{name:'Run action now'}).count(),0);await page.screenshot({path:resolve(out,'book-event-definition.png')});
 await page.getByLabel('Catalog category').selectOption('facts');assert.equal(await page.getByRole('region',{name:'Event definition'}).count(),0);
 await page.getByRole('button',{name:'Search',exact:true}).click();await reply({operation:'search',category:'facts',query:'',offset:0},'facts');
 await page.getByRole('button',{name:/Room surfaces ready/}).click();await reply({operation:'inspect',category:'facts',capability:'physics.ready',version:1},'false');
 const reading=page.getByLabel('Current fact value');await reading.getByText('false',{exact:true}).waitFor();
 await page.screenshot({path:resolve(out,'book-fact-false.png')});await publish('true');await reading.getByText('true',{exact:true}).waitFor();
 assert.equal(await page.evaluate(()=>window.maestroBook.roomSnapshot().request),null);
 await publish('disabled');await reading.getByText('Unavailable',{exact:true}).waitFor();assert(!(await reading.textContent()).includes('true'));
 await page.screenshot({path:resolve(out,'book-fact-unavailable.png')});
 await page.getByRole('button',{name:/Maestro state/}).click();await reply({operation:'inspect',category:'facts',capability:'maestro.state',version:1},'unavailable');
 await publish('speaking');await reading.getByText('"speaking"',{exact:true}).waitFor();await publish('audio-paused');await reading.getByText('Unavailable',{exact:true}).waitFor();
 assert.equal(await page.getByRole('button',{name:'Run action now'}).count(),0);assert.equal(await page.evaluate(()=>window.maestroBook.roomSnapshot().request),null);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSources:Object.keys(native),queries,browserNativeExecution:false,transportAcknowledgements:'simulated',liveFactRefreshWithoutQuery:true,unavailableDistinctFromFalse:true,eventFieldsVisible:true,errors},null,2)+'\n');
 console.log('Chrome browsed scoped native vocabulary and displayed live false/true/unavailable readings. Transport acknowledgements simulated.');
}finally{await browser.close();}
