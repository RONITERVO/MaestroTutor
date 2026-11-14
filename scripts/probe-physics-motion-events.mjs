// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Replays actual Unity physics-motion observations; Chrome executes no Unity work and acknowledges no commands.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_MOTION_EVENTS_EVIDENCE||'.quest-evidence/physics-motion');await mkdir(out,{recursive:true});
const native={};for(const name of ['paused-physics','falling','settled','moving','paused-app','missing'])native[name]=JSON.parse(await readFile(resolve(out,'native',name+'.json'),'utf8'));
assert(native.falling.capabilities.includes('eventSubscriptions.v1'));assert.equal(native.settled.rules.running[0].state.find(v=>v.name==='landed').value,'True');
assert.equal(native.moving.rules.running[0].state.find(v=>v.name==='movingAgain').value,'True');assert.equal(native['paused-app'].rules.running.length,0);assert.equal(native.missing.rules.running.length,0);
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});context.setDefaultTimeout(15000);
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_physics-motion-events',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Physics motion evidence</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(e.message));await page.goto(base+'/_physics-motion-events');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0;
 async function publish(name) {assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),{...native[name],session:'f'.repeat(32),revision:++revision,visible:true,workspaceView:'rules'}),true);}
 await publish('paused-physics');const rules=page.getByRole('region',{name:'Behaviour blocks'});
 await rules.getByRole('button',{name:'Edit values settling',exact:true}).click();
 await rules.getByLabel('Speed threshold (m/s)',{exact:true}).fill('0.04');
 await rules.getByLabel('Quiet period (seconds)',{exact:true}).fill('0.8');
 await rules.getByRole('group',{name:'Event subscription',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-motion-inputs.png')});
 await rules.getByRole('button',{name:'Update draft',exact:true}).click();
 await rules.getByRole('button',{name:'Edit values settling',exact:true}).click();
 await rules.getByLabel('Speed threshold (m/s)',{exact:true}).fill('0.05');await rules.getByLabel('Quiet period (seconds)',{exact:true}).fill('0.3');
 await rules.getByRole('button',{name:'Update draft',exact:true}).click();
 await rules.getByRole('button',{name:'Edit full source',exact:true}).click();
 const source=await rules.getByLabel('Program JSON',{exact:true}).inputValue();assert.deepEqual(JSON.parse(source),JSON.parse(native['paused-physics'].rules.selected.program));
 await rules.getByRole('button',{name:'Discard editor draft',exact:true}).click();await rules.getByRole('button',{name:'Discard draft',exact:true}).click();
 await publish('settled');
 const live=rules.locator('[aria-label="Live program values"]');await live.waitFor();assert((await live.textContent()).includes('state.landed = True'));
 await live.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-motion-settled.png')});
 await publish('moving');await rules.locator('[aria-label="Live program values"]').getByText('state.movingAgain',{exact:true}).waitFor();
 assert((await rules.locator('[aria-label="Live program values"]').textContent()).includes('state.movingAgain = True'));
 await publish('paused-app');assert.equal(await rules.locator('[aria-label="Live program values"]').count(),0);await publish('missing');assert((await page.locator('body').innerText()).includes('disabled'));assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSources:Object.keys(native),browserNativeExecution:false,transportAcknowledgements:'none',canonicalEditorRoundTrip:true,nativeSettledAndMovingTrace:true,pauseClearsRun:true,missingObjectFailureVisible:true,errors},null,2)+'\n');
 console.log('Chrome edited motion thresholds and displayed actual native falling/settling/new-throw/pause observations.');
}finally{await browser.close();}
