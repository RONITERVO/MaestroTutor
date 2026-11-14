// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Local book controls; native receipt/fact replay, no real picker/provider/headset.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/batch-import');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/motionBatchImport.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?motionBatch',{waitUntil:'domcontentloaded',timeout:60000});await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function browse(category,query,label){await page.getByLabel('Catalog category',{exact:true}).selectOption(category);await page.getByRole('textbox',{name:'Search '+category,exact:true}).fill(query);await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:new RegExp(label)}).click();}
 async function observe(expected){await browse('facts','batch','Animation collection status');const value=page.getByLabel('Current fact value',{exact:true}).locator('p').filter({hasText:'\"phase\":\"'+expected.phase+'\"'});await value.waitFor();return JSON.parse(await value.innerText());}
 async function edit(operation){await browse('actions','motion.import.batch','Import an animation collection');await page.getByText('Edit action fields',{exact:true}).click();await page.getByLabel('Batch operation',{exact:true}).selectOption(String(operation));}
 async function run(id){await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:id}).waitFor();}
 assert.deepEqual(await observe(native.before),native.before);await edit(0);await run(native.ready.requestId);assert.deepEqual(await observe(native.ready),native.ready);
 await edit(1);await page.getByLabel('Action inputs requestId',{exact:true}).fill(native.ready.requestId);await page.getByLabel('Action inputs version',{exact:true}).fill(String(native.ready.version));await page.getByLabel('Action inputs category',{exact:true}).fill('gesture');await run(native.ready.requestId);assert.deepEqual(await observe(native.tagged),native.tagged);
 await edit(2);await page.getByLabel('Action inputs requestId',{exact:true}).fill(native.tagged.requestId);await page.getByLabel('Action inputs version',{exact:true}).fill(String(native.tagged.version));await run(native.ready.requestId);assert.deepEqual(await observe(native.after),native.after);
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-status.png')});
 await browse('facts','batch','Animation import file result');await page.getByLabel('Fact inputs requestId',{exact:true}).fill(native.after.requestId);await page.getByLabel('Fact inputs index',{exact:true}).fill('0');await page.getByLabel('Fact inputs motionOffset',{exact:true}).fill('0');await page.getByRole('button',{name:'Read fact',exact:true}).click();
 const fileValue=page.getByLabel('Current fact value',{exact:true}).locator('p').filter({hasText:native.file.motionIds[0]});await fileValue.waitFor();assert.deepEqual(JSON.parse(await fileValue.innerText()),native.file);await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-file.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[native.select,native.category,native.start].map(v=>({action:'execution',execution:{operation:'start',call:v.selected.call,runId:v.selected.id}})));
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,commands,after:native.after,file:native.file,acknowledgement:'native capture replay; no system picker, provider or headset execution',errors},null,2)+'\n');console.log('Book selected, tagged and started the native batch, then inspected exact per-file motion IDs.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());await writeFile(resolve(out,'requests.json'),JSON.stringify(await page.evaluate(()=>window.maestroWorkspaceRequests),null,2));}throw error;}finally{await browser.close();}
