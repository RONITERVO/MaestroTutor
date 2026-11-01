// Chrome replays genuine native observations. Only transport acknowledgements are simulated.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_ROOM_SESSION_EVIDENCE||'.quest-evidence/room-session');await mkdir(out,{recursive:true});
const native={};for(const name of ['initial','starting','begun','created','saved','later','ended','failed-save','failed-start'])native[name]=JSON.parse(await readFile(resolve(out,'native',name+'.json'),'utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_room-session',route=>route.fulfill({contentType:'text/html',body:'<!doctype html><title>Room session evidence</title><div id="root"></div><script type="module">'+"import RefreshRuntime from '/@react-refresh';"+'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;</script>'}));
 const page=await context.newPage(),errors=[],requests=[];page.on('pageerror',error=>errors.push(error.message));await page.goto(base+'/_room-session');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));let revision=0,ack=0;
 async function publish(name){const value={...native[name],session:'e'.repeat(32),revision:++revision,ack,visible:true,workspaceView:'objects'};assert.equal(await page.evaluate(v=>window.maestroBook.roomState(v),value),true);}
 async function click(name,operation,source,next){await page.getByRole('button',{name,exact:true}).click();await page.waitForFunction(()=>Boolean(window.maestroBook.roomSnapshot().request));
  const request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);assert.equal(request.commands.length,1);const action=request.commands[0];
  assert.equal(action.action,'execution');assert.deepEqual(action.execution.call,{id:'room.session',version:1,arguments:{operation,sessionId:native[source].temporaryRoom.id}});
  assert.equal(action.execution.runId,native[source].execution.nextRunId);requests.push(request);ack=request.sequence;await publish(next);
 }
 await publish('initial');await page.getByRole('button',{name:'Begin temporary room',exact:true}).waitFor();
 await click('Begin temporary room','begin','initial','starting');
 await page.getByText('Saving the starting room. New edits are already temporary; this write can finish after Stop.',{exact:true}).waitFor();
 assert.equal(await page.getByRole('button',{name:'Save snapshot',exact:true}).isDisabled(),true);await page.screenshot({path:resolve(out,'book-starting.png')});
 await publish('begun');await page.getByText('Starting room saved. New edits remain temporary.',{exact:true}).waitFor();await publish('created');
 await page.getByRole('button',{name:'Save snapshot',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-temporary.png')});
 await click('Save snapshot','keep','created','saved');await page.getByText('Snapshot 1 saved. Later edits are still temporary.',{exact:true}).waitFor();
 await page.screenshot({path:resolve(out,'book-saved-snapshot.png')});
 await publish('later');await click('Discard unsaved & end','discard','later','ended');await page.getByRole('button',{name:'Begin temporary room',exact:true}).waitFor();
 await publish('failed-save');await page.getByRole('button',{name:'Save snapshot',exact:true}).waitFor();await page.screenshot({path:resolve(out,'book-failed-save.png')});
 await publish('failed-start');await page.getByText(/Starting room was not saved/).waitFor();await page.screenshot({path:resolve(out,'book-failed-start.png')});
 assert.deepEqual(errors,[]);await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSources:Object.keys(native),requests,transportAcknowledgements:'simulated',browserNativeExecution:false,errors},null,2)+'\n');
 console.log('Chrome sent exact session IDs and native-issued execution IDs through the shared bridge; displayed saved and failed native snapshots.');
}finally{await browser.close();}
