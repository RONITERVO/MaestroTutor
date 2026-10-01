// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Book controls with recorded native results; no provider/device/file-import calls.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/avatar-selection');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/avatarSelection.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?avatarSelection',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('avatar.model');
  await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Current Maestro model/}).click();
  return JSON.parse(await page.getByLabel('Current fact value',{exact:true}).locator('p').innerText());
 }
 async function action(id,label){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');await page.getByRole('textbox',{name:'Search actions',exact:true}).fill(id);
  await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:label}).click();
 }
 const before=await observe();assert.deepEqual(before,native.before);
 await action('model.library.inspect',/Browse imported models/);await page.getByRole('button',{name:'Run action now',exact:true}).click();
 const result=page.getByLabel('Action result',{exact:true});await result.filter({hasText:'"entries"'}).waitFor();
 const library=JSON.parse(await result.innerText());assert.deepEqual(library,native.library.selected.output);
 await action('avatar.model.select',/Choose Maestro avatar/);await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByLabel('Action inputs modelHash',{exact:true}).fill(library.entries[0].modelHash);await page.getByLabel('Action inputs revision',{exact:true}).fill(String(before.revision));
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await result.filter({hasText:'"target": "maestro"'}).waitFor();
 assert.deepEqual(JSON.parse(await result.innerText()),native.selection.selected.output);
 const after=await observe();assert.deepEqual(after,native.after);await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();assert.equal(await page.getByLabel('Current fact value',{exact:true}).evaluate(node=>node.scrollWidth<=node.clientWidth+1),true,'Fact values must fit the book page');await page.screenshot({path:resolve(out,'book.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[native.library,native.selection].map(view=>({action:'execution',execution:{operation:'start',call:view.selected.call,runId:view.selected.id}})));assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,commands,before,after,acknowledgement:'recorded native output; browser simulation only',errors},null,2)+'\n');console.log('Book discovered an imported model, sent its exact hash/revision and observed the saved/displayed native identity.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());await writeFile(resolve(out,'failure-requests.json'),JSON.stringify(await page.evaluate(()=>window.maestroWorkspaceRequests),null,2));}throw error;}finally{await browser.close();}
