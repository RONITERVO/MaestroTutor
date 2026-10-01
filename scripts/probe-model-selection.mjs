// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Local generated book controls replaying native captures. No headset picker/provider.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/model-selection');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/modelSelection.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?modelSelection',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('import');
  await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Current model import/}).click();
  return JSON.parse(await page.getByLabel('Current fact value',{exact:true}).locator('p').innerText());
 }
 async function edit(operation){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('model.import');
  await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Import a model together/}).click();await page.getByText('Edit action fields',{exact:true}).click();
  await page.getByLabel('Import operation',{exact:true}).selectOption(String(operation));
 }
 const before=await observe();assert.deepEqual(before,native.before);await edit(0);await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:native.preview.requestId}).waitFor();
 const preview=await observe();assert.deepEqual(preview,native.preview);await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-preview.png')});
 await edit(2);await page.getByLabel('Action inputs requestId',{exact:true}).fill(preview.requestId);await page.getByLabel('Action inputs modelHash',{exact:true}).fill(preview.preview.modelHash);
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:native.after.accepted.objectId}).waitFor();
 await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book.png')});const after=await observe();assert.deepEqual(after,native.after);
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[native.select,native.accept].map(v=>({action:'execution',execution:{operation:'start',call:v.selected.call,runId:v.selected.id}})));
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,commands,before,preview,after,acknowledgement:'native capture replay; no system picker, provider or headset execution',errors},null,2)+'\n');
 console.log('Book selected a native import request, inspected the preview and accepted the exact model identity.');
}finally{await browser.close();}
