// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated book controls with real native-state replay; no provider or headset.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_CONSTRUCTION_EVIDENCE_DIR||'.quest-evidence/construction-capture');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/constructionCaptureAuthoring.json','utf8')),expected=native.captured.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1280,height:960}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-construction-capture.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Save construction');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Save construction.*program.module.captureConstruction/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByText('Action inputs members · 1 entries',{exact:true}).click();
 await page.getByLabel('Action inputs name',{exact:true}).fill(expected.arguments.name);
 for(let i=0;i<expected.arguments.members.length;i++){
  if(i)await page.getByRole('button',{name:'Add Action inputs members entry',exact:true}).click();
  await page.getByLabel(`Action inputs members ${i+1} target`,{exact:true}).selectOption(expected.arguments.members[i].target);
  await page.getByLabel(`Action inputs members ${i+1} slot`,{exact:true}).fill(expected.arguments.members[i].slot);
  assert.equal(await page.getByLabel(`Action inputs members ${i+1} revision`,{exact:true}).getAttribute('readonly'),'');
 }
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const slot=page.getByLabel('Action inputs members 1 slot',{exact:true});await slot.fill('invalid slot');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await slot.fill(expected.arguments.members[0].slot);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),false);
 await slot.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-construction-capture.png')});await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>window.maestroConstructionCaptureRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroConstructionCaptureSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroConstructionCaptureRequests);assert.deepEqual(requests.filter(r=>r.commands[0].catalog?.category==='facts').map(r=>r.commands[0].catalog.arguments.target),expected.arguments.members.map(m=>m.target));const request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.captured.execution.selected.id);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,invalidSlotRejected:true,currentValuesLoadedTogether:true,guardsReadOnly:true,errors},null,2)+'\n');console.log('Chrome construction capture: generated fields, current member facts, exact native call/receipt and invalid-slot rejection verified.');
}finally{await browser.close();}
