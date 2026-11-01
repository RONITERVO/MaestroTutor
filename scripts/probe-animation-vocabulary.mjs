// Real Chrome replay of the native physical-tray result. No provider or device access.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/animation-vocabulary');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile(resolve(out,'native/quick/animation-book.json'),'utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_animation-vocabulary',route=>route.fulfill({contentType:'text/html',body:
  '<!doctype html><title>Native quick edit handoff</title><div id="root"></div><script type="module">'+
  "import RefreshRuntime from '/@react-refresh';"+
  'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;'+
  '</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(error.message));
 await page.goto(base+'/_animation-vocabulary');
 await page.evaluate(async()=>{
  const React=(await import('/node_modules/.vite/deps/react.js')).default;
  const {createRoot}=(await import('/node_modules/.vite/deps/react-dom_client.js')).default;
  const {QuestBookSurface}=await import('/src/platform/quest/QuestBookSurface.tsx');
  const {useMaestroStore,initialSettings}=await import('/src/store/index.ts');
  await import('/src/app/index.css');
  useMaestroStore.setState({settings:{...initialSettings,selectedLanguagePairId:'es-en'},isSettingsLoaded:true,needsLanguageSelection:false,isLoadingHistory:false,messages:[]});
  createRoot(document.getElementById('root')).render(React.createElement(QuestBookSurface,null,React.createElement('p',null,'Familiar conversation')));
 });
 await page.waitForFunction(()=>Boolean(window.maestroBook));
 // The native test has no browser transport session; substitute only its envelope identity.
 const state={...native,session:'e'.repeat(32),revision:1};
 assert.equal(await page.evaluate(value=>window.maestroBook.roomState({...value,visible:false}),state),true);
 await page.getByText('Familiar conversation',{exact:true}).first().waitFor();
 assert.equal(await page.evaluate(value=>window.maestroBook.roomState({...value,revision:2}),state),true);
 await page.getByRole('button',{name:'Functions & code',exact:true}).click();
 await page.getByRole('button',{name:'Edit values wave',exact:true}).click();
 assert.equal(await page.getByLabel('Source and channel',{exact:true}).inputValue(),'1');
 assert.equal(await page.getByLabel('source.gesture',{exact:true}).inputValue(),'pointing');
 await page.getByLabel('Source and channel',{exact:true}).selectOption('0');
 assert.equal(await page.getByLabel('source.gesture',{exact:true}).inputValue(),'pointing');
 await page.getByLabel('source.gesture input mode',{exact:true}).selectOption('expression');
 await page.getByLabel('source.gesture value',{exact:true}).fill('greeting');
 await page.getByLabel('seconds',{exact:true}).fill('3');
 await page.getByLabel('Source and channel',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-animation-source.png')});
 await page.getByRole('button',{name:'Update draft',exact:true}).click();
 await page.getByRole('button',{name:'Edit values wave',exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-animation-block.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>Boolean(window.maestroBook.roomSnapshot().request));
 const request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);
 const expected=JSON.parse(native.rules.selected.program);const node=expected.functions[0].body[0];node.arguments.seconds=3;node.arguments.channel='wholeTarget';node.bindings={'source.gesture':{value:'greeting'}};
 assert.equal(request.commands.length,1);assert.equal(request.commands[0].action,'rules');assert.equal(request.commands[0].rule.action,'edit');
 assert.equal(request.commands[0].rule.revision,native.rules.revision);
 assert.deepEqual(JSON.parse(request.commands[0].rule.edits[0].sequence.program),expected);
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSource:'native/quick/animation-book.json',transportSession:'substituted',request,execution:'not attempted',acknowledgement:'not simulated',errors},null,2)+'\n');
 console.log('Chrome selected an animation source/channel, authored a nested scalar expression and sent the exact canonical program without playback.');
}finally{await browser.close();}
