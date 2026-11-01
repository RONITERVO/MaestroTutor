// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/physical-sculpt/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/physicalSculptAuthoring.json','utf8')),expected=native.after.execution.selected.call,a=expected.arguments;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1024,height:1100}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-physical-sculpt.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('sculpt tip');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Configure an object's sculpt tip.*object.sculptTip.edit/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByLabel('Action inputs target',{exact:true}).selectOption(a.target);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs revision',{exact:true}).getAttribute('readonly'),'');
 await page.getByLabel('Action inputs definition radius',{exact:true}).fill('3');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByLabel('Action inputs definition mode',{exact:true}).selectOption(a.definition.mode);
 for(const key of ['radius','height','part'])await page.getByLabel('Action inputs definition '+key,{exact:true}).fill(String(a.definition[key]));
 for(const field of ['position','rotation'])for(const [axis,value] of Object.entries(a.definition[field]))await page.getByLabel('Action inputs definition '+field+' '+axis,{exact:true}).fill(String(value));
 if('amountLitres' in a.definition){await page.getByLabel('Include Action inputs definition amountLitres',{exact:true}).check();await page.getByLabel('Action inputs definition amountLitres',{exact:true}).fill(String(a.definition.amountLitres));}
 await page.getByLabel('Action inputs definition enabled',{exact:true}).selectOption(String(a.definition.enabled));
 await page.getByLabel('Action inputs definition radius',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-sculpt-tip-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroPhysicalSculptRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroPhysicalSculptSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroPhysicalSculptRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await page.getByLabel('Selected action',{exact:true}).getByText('completed',{exact:true}).waitFor();await page.getByLabel('Selected action',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-sculpt-tip-result.png')});
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,currentRevisionRequired:true,oversizedBrushRejected:true,errors},null,2)+'\n');console.log('Chrome sculpt tip settings matched the native call/receipt with exact revision and bounded inputs.');
}finally{await browser.close();}
