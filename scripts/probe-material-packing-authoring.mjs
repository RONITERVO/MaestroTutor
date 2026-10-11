// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/material-packing/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/materialPackingAuthoring.json','utf8')),expected=native.after.execution.selected.call,a=expected.arguments;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1024,height:1100}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-material-packing.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Pack surface material');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Pack surface material.*object.material.pack/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();
 for(const endpoint of ['source'])await page.getByLabel('Action inputs '+endpoint+' target',{exact:true}).selectOption(a[endpoint].target);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 for(const endpoint of ['source']){
  assert.equal(await page.getByLabel('Action inputs '+endpoint+' revision',{exact:true}).getAttribute('readonly'),'');
  for(const axis of ['x','z'])await page.getByLabel('Action inputs '+endpoint+' centre '+axis,{exact:true}).fill(String(a[endpoint].centre[axis]));
  await page.getByLabel('Action inputs '+endpoint+' radius',{exact:true}).fill(String(a[endpoint].radius));
 }
 await page.getByLabel('Action inputs amountLitres',{exact:true}).fill('20.01');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByLabel('Action inputs amountLitres',{exact:true}).fill(String(a.amountLitres));
 await page.getByLabel('Action inputs name',{exact:true}).fill(a.name);await page.getByLabel('Action inputs mass',{exact:true}).fill(String(a.mass));
 for(const axis of ['x','y','z'])await page.getByLabel('Action inputs position '+axis,{exact:true}).fill(String(a.position[axis]));
 await page.getByLabel('Action inputs source radius',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-material-packing-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroMaterialPackingRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroMaterialPackingSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroMaterialPackingRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await page.getByLabel('Action result',{exact:true}).waitFor();await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-material-packing-result.png')});
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,currentSourceRevisionRequired:true,oversizedTransferRejected:true,errors},null,2)+'\n');console.log('Chrome material packing matched the native call/receipt with the current source revision and bounded inputs.');
}finally{await browser.close();}
