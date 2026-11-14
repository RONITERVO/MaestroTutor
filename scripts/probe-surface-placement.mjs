// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Browser replay of actual native placement; no headset or provider access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_PROBE_OUTPUT||'.quest-evidence/surface-placement');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/surfacePlacement.json','utf8')),view=native.receipt;
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?surfacePlacement',{waitUntil:'domcontentloaded',timeout:60000});await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('object.surface.place');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/^Place on a detected surface\s*object.surface.place · v1$/}).click();await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByLabel('Action inputs target',{exact:true}).selectOption(native.request.call.arguments.target);await page.getByLabel('Action inputs direction',{exact:true}).selectOption('below');
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isEnabled(),false);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).inputValue(),native.environment.stateId);
 await page.getByRole('button',{name:'Run action now',exact:true}).click();const result=page.getByLabel('Action result',{exact:true});await result.filter({hasText:native.request.call.arguments.target}).waitFor();await result.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-surface-placement.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[{action:'execution',execution:{operation:'start',call:view.selected.call,runId:view.selected.id}}]);
 const shown=JSON.parse(await result.innerText());assert.deepEqual(shown,view.selected.output);
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,commands,result:shown,acknowledgement:'native placement capture replay; no live headset raycast',errors},null,2)+'\n');
 console.log('Book requested exact native surface placement and displayed the saved result.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
