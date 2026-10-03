// Real Chrome replay of the native physical-tray result. No provider or device access.
import {chromium} from 'playwright-core';
import {readFile,writeFile,mkdir} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/native-quick-edits');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile(resolve(out,'book-after-native-edit.json'),'utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 await context.route(base+'/_native-quick-edits',route=>route.fulfill({contentType:'text/html',body:
  '<!doctype html><title>Native quick edit handoff</title><div id="root"></div><script type="module">'+
  "import RefreshRuntime from '/@react-refresh';"+
  'RefreshRuntime.injectIntoGlobalHook(window);window.$RefreshReg$=()=>{};window.$RefreshSig$=()=>type=>type;window.__vite_plugin_react_preamble_installed__=true;'+
  '</script>'}));
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(error.message));
 await page.goto(base+'/_native-quick-edits');
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
 await page.getByRole('button',{name:'Edit values block_1',exact:true}).click();
 assert.equal(await page.getByLabel('yaw',{exact:true}).inputValue(),'105');
 await page.getByLabel('yaw',{exact:true}).fill('120');
 await page.screenshot({path:resolve(out,'book-native-rotation.png')});
 await page.getByRole('button',{name:'Update draft',exact:true}).click();
 await page.getByText('Rotate object',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-native-block.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>Boolean(window.maestroBook.roomSnapshot().request));
 const request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);
 const expected=JSON.parse(native.rules.selected.program);expected.functions[0].body[0].arguments.yaw=120;
 assert.equal(request.commands.length,1);assert.equal(request.commands[0].action,'rules');assert.equal(request.commands[0].rule.action,'edit');
 assert.equal(request.commands[0].rule.revision,native.rules.revision);
 assert.deepEqual(JSON.parse(request.commands[0].rule.edits[0].sequence.program),expected);
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({nativeSource:'book-after-native-edit.json',transportSession:'substituted',request,execution:'not attempted',acknowledgement:'not simulated',errors},null,2)+'\n');
 console.log('Chrome opened the native-edited program and preserved its node/resource identity in a further human edit.');
}finally{await browser.close();}
