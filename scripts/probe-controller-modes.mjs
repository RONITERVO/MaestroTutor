// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated book fields replay actual native receipts; no provider or headset access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_PROBE_OUTPUT||'.quest-evidence/controller-modes');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/controllerModes.json','utf8'));
const views=[native.enable,native.virtualView,native.user,native.mixed];
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?controllerModes',{waitUntil:'domcontentloaded',timeout:60000});await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('controller.mode');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Live movement and room view/}).click();
  return JSON.parse(await page.getByLabel('Current fact value',{exact:true}).locator('p').innerText());
 }
 const before=await observe();assert.deepEqual(before,native.before);let current=before;
 for(const view of views){
  assert.equal(view.selected.call.arguments.stateId,current.stateId);
  await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('controller.mode.set');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Change movement or room view/}).click();await page.getByText('Edit action fields',{exact:true}).click();
  await page.getByLabel('Action inputs operation',{exact:true}).selectOption(view.selected.call.arguments.operation);await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).inputValue(),current.stateId);
  await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:view.selected.output.stateId}).waitFor();
  if(view===native.virtualView)await page.screenshot({path:resolve(out,'book-mode-action.png')});
  current=await observe();assert.deepEqual(current,view.selected.output);
 }
 assert.deepEqual(current,native.after);await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-mode-readback.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,views.map(v=>({action:'execution',execution:{operation:'start',call:v.selected.call,runId:v.selected.id}})));
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,commands,before,after:current,acknowledgement:'native capture replay; no controller, provider or headset execution',errors},null,2)+'\n');
 console.log('Book executed four explicit mode transitions using fresh captured native identities.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
