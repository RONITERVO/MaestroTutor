// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/hand-pack/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/physicalPackingAuthoring.json','utf8')),expected=native.set.execution.selected.call,a=expected.arguments;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1024,height:1100}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-physical-packing.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('physical material packing tool');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Choose the physical material packing tool.*material.pack.tool.set/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByLabel('Action inputs enabled',{exact:true}).selectOption(String(a.enabled));
 for(const key of ['radius','amountLitres','mass'])await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(a[key]));
 await page.getByLabel('Action inputs amountLitres',{exact:true}).fill('21');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await page.getByLabel('Action inputs amountLitres',{exact:true}).fill(String(a.amountLitres));
 await page.getByLabel('Action inputs mass',{exact:true}).fill('0');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await page.getByLabel('Action inputs mass',{exact:true}).fill(String(a.mass));
 await page.screenshot({path:resolve(out,'book-physical-packing-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroPhysicalPackingRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroPhysicalPackingSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroPhysicalPackingRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.set.execution.selected.id);assert.deepEqual(errors,[]);
 await page.getByLabel('Selected action',{exact:true}).getByText('completed',{exact:true}).waitFor();await page.getByLabel('Selected action',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-physical-packing-result.png')});
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,oversizedDoseRejected:true,zeroMassRejected:true,errors},null,2)+'\n');console.log('Chrome physical packing settings matched the native call and receipt.');
}finally{await browser.close();}
