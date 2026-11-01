// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_RECTANGLE_EVIDENCE||'.quest-evidence/rectangular-containers/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/rectangularContainerAuthoring.json','utf8')),expected=native.after.execution.selected.call,a=expected.arguments;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-rectangular-container.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Configure or fill a liquid container');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Configure or fill a liquid container.*object.container.edit/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByLabel('Action inputs target',{exact:true}).selectOption(a.target);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs revision',{exact:true}).getAttribute('readonly'),'');
 const field=name=>page.getByLabel('Action inputs definition '+name,{exact:true});
 assert.equal(Number(await field('amountMl').inputValue()),native.current.catalog.value.definition.amountMl);
 assert.equal(Number(await field('rectangle width').inputValue()),native.current.catalog.value.definition.rectangle.width);
 assert.equal(Number(await field('rectangle depth').inputValue()),native.current.catalog.value.definition.rectangle.depth);
 await field('rectangle width').fill('0');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await field('rectangle width').fill(String(a.definition.rectangle.width));await field('rectangle depth').fill(String(a.definition.rectangle.depth));
 await field('rectangle width').evaluate(element=>element.closest('fieldset').scrollIntoView({block:'start'}));await page.screenshot({path:resolve(out,'book-rectangle-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroRectangleRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroRectangleSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroRectangleRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 assert.deepEqual(await page.evaluate(()=>window.maestroRectangleState().execution.selected),native.after.execution.selected);await page.getByLabel('Selected action',{exact:true}).getByText('completed',{exact:true}).waitFor();await page.getByLabel('Selected action',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-rectangle-result.png')});
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,currentShapeAndContentsLoaded:true,partialZeroRejected:true,errors},null,2)+'\n');console.log('Chrome rectangular cavity edit matched the native call/receipt and preserved loaded contents.');
}finally{await browser.close();}
