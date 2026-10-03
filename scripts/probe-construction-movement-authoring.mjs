// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Production book controls and generated form against recorded native calls.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_CONSTRUCTION_EVIDENCE_DIR||'.quest-evidence/construction-movement');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/constructionMovementAuthoring.json','utf8')),expected=native.transformed.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-construction-movement.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByText('Construction pieces · 2 selected',{exact:true}).click();
 await page.getByRole('button',{name:'Move together in room',exact:true}).click();
 await page.getByText('Grip the solid handle to move or turn all pieces; use two hands to resize. Physics must stay paused.',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Hide move handle',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-move-handle.png')});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();await page.getByLabel('Search actions',{exact:true}).fill('Move, turn or resize a construction');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Move, turn or resize a construction.*object.layout.transform/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByText('Action inputs members · 1 entries',{exact:true}).click();
 await page.getByRole('button',{name:'Add Action inputs members entry',exact:true}).click();
 for(let i=0;i<2;i++)await page.getByLabel(`Action inputs members ${i+1} target`,{exact:true}).selectOption(expected.arguments.members[i].target);
 for(const field of ['position','rotation'])for(const [axis,value] of Object.entries(expected.arguments[field]))await page.getByLabel(`Action inputs ${field} ${axis}`,{exact:true}).fill(String(value));
 for(let i=0;i<2;i++)assert.equal(await page.getByLabel(`Action inputs members ${i+1} revision`,{exact:true}).getAttribute('readonly'),'');
 const scale=page.getByLabel('Action inputs scale',{exact:true});await scale.fill('5');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await scale.fill(String(expected.arguments.scale));
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),false);
 await scale.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-group-transform.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroConstructionMovementRequests.some(r=>r.commands[0].execution?.call?.id==='object.layout.transform')&&!window.maestroConstructionMovementSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Back to workshop',exact:true}).click();await page.getByRole('button',{name:'Undo',exact:true}).click();await page.waitForFunction(()=>window.maestroConstructionMovementRequests.some(r=>r.commands[0].action==='undo')&&!window.maestroConstructionMovementSnapshot().request);
 await page.getByRole('button',{name:'Move together in room',exact:true}).waitFor();assert.equal(await page.getByRole('button',{name:'Hide move handle',exact:true}).count(),0);
 const requests=await page.evaluate(()=>window.maestroConstructionMovementRequests),calls=requests.filter(r=>r.commands[0].execution?.operation==='start');assert.equal(calls.length,2);
 for(let i=0;i<2;i++){const receipt=[native.shown,native.transformed][i].execution.selected;assert.deepEqual(calls[i].commands[0].execution.call,receipt.call);assert.equal(calls[i].commands[0].execution.runId,receipt.id);}
 assert.deepEqual(requests.filter(r=>r.commands[0].catalog?.category==='facts').map(r=>r.commands[0].catalog.arguments.target),expected.arguments.members.map(m=>m.target));assert.equal(requests.filter(r=>r.commands[0].action==='undo').length,1);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,nativeCallMatched:true,handleCallsMatched:true,oneUndoVerified:true,undoHidesHandle:true,invalidScaleRejected:true,currentValuesLoadedTogether:true,guardsReadOnly:true,requests,errors},null,2)+'\n');console.log('Chrome handle, generated transform, one Undo and exact native calls verified.');
}finally{await browser.close();}
