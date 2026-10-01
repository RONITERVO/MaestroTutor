// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Local book facts replay native results; no imports, provider or headset access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/import-readback');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/importReadback.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?importReadback',{waitUntil:'domcontentloaded',timeout:60000});await page.getByRole('button',{name:'Action catalog',exact:true}).click();await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');
 await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('import');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Current model import/}).click();
 const summary=page.getByLabel('Current fact value',{exact:true}).locator('p').filter({hasText:native.summary.requestId});await summary.waitFor();assert.deepEqual(JSON.parse(await summary.innerText()),native.summary);await summary.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-summary.png')});
 await page.getByRole('button',{name:/Imported model motion results/}).click();await page.getByLabel('Fact inputs requestId',{exact:true}).fill(native.summary.requestId);const ids=[];
 for(const item of native.pages){
  await page.getByLabel('Fact inputs motionOffset',{exact:true}).fill(String(item.arguments.motionOffset));await page.getByRole('button',{name:'Read fact',exact:true}).click();
  const value=page.getByLabel('Current fact value',{exact:true}).locator('p').filter({hasText:item.value.motionIds[0]});await value.waitFor();const observed=JSON.parse(await value.innerText());assert.deepEqual(observed,item.value);ids.push(...observed.motionIds);
  if(item.arguments.motionOffset===24){await value.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-last-page.png')});}
 }
 assert.deepEqual(ids,native.execution.selected.output.motionIds);
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands));assert.equal(commands.some(c=>c.action==='execution'),false);
 const queries=commands.filter(c=>c.action==='catalog'&&c.catalog.capability==='model.import.motions'&&c.catalog.arguments?.requestId===native.summary.requestId).map(c=>c.catalog.arguments);assert.deepEqual(queries,native.pages.map(p=>p.arguments));
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,queries,ids,summary:native.summary,acknowledgement:'native capture replay; no provider or headset execution',errors},null,2)+'\n');console.log('Book inspected the bounded import summary and all 32 exact motion IDs across four pages.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
