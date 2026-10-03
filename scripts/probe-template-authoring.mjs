// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated book controls with captured native acknowledgements; no headset/provider.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/templates');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/templateAuthoring.json','utf8')),expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1280,height:960}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-template.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill(native.search.catalog.query);await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Create object.*object.create/}).click();
 await page.getByLabel('Creation kind',{exact:true}).selectOption({label:'Create starter object'});
 await page.getByText('Edit action fields',{exact:true}).click();
 const select=page.getByLabel('Action inputs templateHash',{exact:true});await select.selectOption(expected.arguments.templateHash);
 for(const key of ['name','x','y','z','scale'])await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(expected.arguments[key]));
 await page.getByLabel('Action inputs scale',{exact:true}).fill('0');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByLabel('Action inputs scale',{exact:true}).fill(String(expected.arguments.scale));
 const preview=page.getByAltText('Cup preview',{exact:true});await preview.waitFor();await preview.evaluate(image=>image.decode());assert.equal(await preview.evaluate(image=>image.naturalWidth),512);
 await select.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-template-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>window.maestroTemplateRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroTemplateSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroTemplateRequests),request=requests.find(r=>r.commands[0].action==='execution');
 assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,invalidScaleRejected:true,nativePreviewLoaded:true,errors},null,2)+'\n');
 console.log('Chrome starter chooser: readable labels and native preview, exact native call/receipt matched, invalid scale rejected.');
}finally{await browser.close();}
