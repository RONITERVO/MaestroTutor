// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real book UI driven against recorded native selection/capture receipts.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_CONSTRUCTION_EVIDENCE_DIR||'.quest-evidence/construction-selection');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/constructionSelectionAuthoring.json','utf8')),expected=native.captured.execution.selected.call;
const label=id=>{const object=native.before.objects.find(o=>o.id===id),duplicates=native.before.objects.filter(o=>o.name===object.name);let size=8;while(size<32&&duplicates.some(o=>o.id!==id&&o.id.slice(0,size)===id.slice(0,size)))size+=2;return duplicates.length>1?object.name+' · '+id.slice(0,size):object.name;};
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-construction-selection.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByText('Construction pieces · 0 selected',{exact:true}).click();
 for(let i=0;i<2;i++){
  await page.getByLabel('Include '+label(expected.arguments.members[i].target),{exact:true}).click();
  await page.getByText(`Construction pieces · ${i+1} selected`,{exact:true}).waitFor();
 }
 assert.equal(await page.getByLabel('Construction piece order',{exact:true}).locator('li').count(),2);
 await page.getByRole('button',{name:'Locate '+label(expected.arguments.members[0].target),exact:true}).click();
 await page.waitForFunction(id=>window.maestroConstructionSelectionSnapshot().state.selectedId===id&&!window.maestroConstructionSelectionSnapshot().request,expected.arguments.members[0].target);
 await page.getByRole('button',{name:'Review reusable construction',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-construction-selection.png')});
 await page.getByRole('button',{name:'Review reusable construction',exact:true}).click();
 await page.getByLabel('Action arguments',{exact:true}).waitFor({state:'attached'});
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 assert.equal((await page.evaluate(()=>window.maestroConstructionSelectionRequests)).filter(r=>r.commands[0].execution?.call?.id==='program.module.captureConstruction').length,0);
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByText('Action inputs members · 2 entries',{exact:true}).click();
 for(let i=0;i<2;i++){assert.equal(await page.getByLabel(`Action inputs members ${i+1} target`,{exact:true}).inputValue(),expected.arguments.members[i].target);assert.equal(await page.getByLabel(`Action inputs members ${i+1} revision`,{exact:true}).getAttribute('readonly'),'');}
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 const slot=page.getByLabel('Action inputs members 1 slot',{exact:true});await slot.fill('invalid slot');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await slot.fill(expected.arguments.members[0].slot);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),false);
 await slot.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-construction-capture.png')});await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroConstructionSelectionRequests.some(r=>r.commands[0].execution?.call?.id==='program.module.captureConstruction')&&!window.maestroConstructionSelectionSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroConstructionSelectionRequests);
 const selections=requests.filter(r=>r.commands[0].execution?.call?.id==='room.selection.set');assert.equal(selections.length,2);for(let i=0;i<2;i++){const receipt=[native.first,native.both][i].execution.selected;assert.deepEqual(selections[i].commands[0].execution.call,receipt.call);assert.equal(selections[i].commands[0].execution.runId,receipt.id);}
 assert.deepEqual(requests.filter(r=>r.commands[0].catalog?.category==='facts').map(r=>r.commands[0].catalog.arguments.target),expected.arguments.members.map(m=>m.target));
 const request=requests.find(r=>r.commands[0].execution?.call?.id==='program.module.captureConstruction');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.captured.execution.selected.id);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,selectionCallsMatched:true,locateVerified:true,draftDoesNotExecute:true,invalidSlotRejected:true,currentValuesLoadedTogether:true,guardsReadOnly:true,errors},null,2)+'\n');console.log('Chrome selection, Locate, reviewed capture and exact native receipts verified.');
}finally{await browser.close();}
