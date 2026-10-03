// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated book controls with real native-state replay; no provider or headset.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/structures');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/structureAuthoring.json','utf8')),expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1280,height:960}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-structures.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Save a structure');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Save a structure.*structure.save/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByLabel('Action inputs source name',{exact:true}).fill(expected.arguments.source.name);
 await page.getByText('Action inputs source members · 1 entries',{exact:true}).click();
 await page.getByRole('button',{name:'Add Action inputs source members entry',exact:true}).click();
 for(let i=0;i<2;i++) {
  await page.getByLabel(`Action inputs source members ${i+1} slot`,{exact:true}).fill(expected.arguments.source.members[i].slot);
  await page.getByLabel(`Action inputs source members ${i+1} target`,{exact:true}).selectOption(expected.arguments.source.members[i].target);
 }
 const slot=page.getByLabel('Action inputs source members 2 slot',{exact:true});await slot.fill(expected.arguments.source.members[0].slot);assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await slot.fill(expected.arguments.source.members[1].slot);
 const target=page.getByLabel('Action inputs source members 2 target',{exact:true});await target.selectOption(expected.arguments.source.members[0].target);assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await target.selectOption(expected.arguments.source.members[1].target);
 await page.getByLabel('Action inputs source members 1 slot',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-structure-editor.png')});await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>window.maestroStructureRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroStructureSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroStructureRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(request.conditions.map(c=>c.id).sort(),expected.arguments.source.members.map(m=>m.target).sort());assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,duplicateSlotRejected:true,duplicateMemberRejected:true,memberPreconditionsVerified:true,errors},null,2)+'\n');console.log('Chrome structure controls: exact native capture call/receipt, member authority and duplicate-slot/member rejection verified.');
}finally{await browser.close();}
