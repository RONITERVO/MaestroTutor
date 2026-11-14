// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_SNAP_EVIDENCE_DIR||'.quest-evidence/snap-points/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/snapAuthoring.json','utf8')),expected=native.after.execution.selected.call,a=expected.arguments;
assert.equal(native.started.execution.selected.phase,'preparing');assert.equal(native.after.execution.selected.phase,'completed');
const browser=await chromium.launch({channel:'chrome',headless:true});let page;const errors=[];
try{
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-snap.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Snap a construction to a point');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Snap a construction to a point.*object.layout.snap/}).click();
 await page.getByLabel('Variant',{exact:true}).selectOption({label:'Place and join'});
 if(!await page.getByRole('button',{name:'Find Action inputs destination target',exact:true}).isVisible())await page.getByText('Edit action fields',{exact:true}).click();
 if(!await page.getByRole('button',{name:'Find Action inputs members 1 target',exact:true}).isVisible())await page.getByText('Action inputs members · 1 entries',{exact:true}).click();
 async function choose(label,target){
  const previousRequests=await page.evaluate(()=>window.maestroSnapRequests.length);
  await page.getByRole('button',{name:'Find '+label,exact:true}).click();
  await page.waitForFunction(count=>window.maestroSnapRequests.length>count&&!window.maestroSnapSnapshot().request&&window.maestroSnapState().catalog?.capability==='world.objects'&&window.maestroSnapState().catalog.arguments.cursor==='',previousRequests);
  for(let i=0;i<24;i++){
   await page.getByRole('button',{name:'Search objects',exact:true}).waitFor();
   await page.waitForFunction(()=>!window.maestroSnapSnapshot().request&&window.maestroSnapState().catalog?.capability==='world.objects');
   const entries=await page.evaluate(()=>window.maestroSnapState().catalog.value.entries),index=entries.findIndex(entry=>entry.id===target);
   if(index>=0){await page.getByLabel('Objects',{exact:true}).getByRole('button').nth(index).click();return;}
   const next=page.getByRole('button',{name:'Next objects',exact:true});assert.equal(await next.isEnabled(),true,'Exact native target was not found');
   const cursor=await page.evaluate(()=>window.maestroSnapState().catalog.arguments.cursor);await next.click();
   await page.waitForFunction(previous=>window.maestroSnapState().catalog.arguments.cursor!==previous,cursor);
  }
  throw new Error('Object directory exceeded its bounded page count');
 }
 await choose('Action inputs members 1 target',a.members[0].target);
 await choose('Action inputs destination target',a.destination.target);
 await page.getByLabel('Action inputs point',{exact:true}).fill(a.point);await page.getByLabel('Action inputs destination point',{exact:true}).fill(a.destination.point);
 for(const key of ['turn','breakForce','breakTorque'])await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(a[key]));
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs members 1 revision',{exact:true}).getAttribute('readonly'),'');assert.equal(await page.getByLabel('Action inputs destination revision',{exact:true}).getAttribute('readonly'),'');
 await page.getByLabel('Action inputs turn',{exact:true}).fill('181');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await page.getByLabel('Action inputs turn',{exact:true}).fill(String(a.turn));
 await page.getByLabel('Action inputs turn',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-snap-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroSnapRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroSnapSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroSnapRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 const pending=await page.evaluate(()=>window.maestroSnapState().execution.selected);assert.equal(pending.phase,'preparing');assert.equal(pending.id,native.after.execution.selected.id);
 assert.equal(await page.getByLabel('Selected action',{exact:true}).locator('strong').innerText(),'preparing');
 await page.waitForFunction(()=>window.maestroSnapState().execution.selected.phase==='completed');
 await page.getByLabel('Action result',{exact:true}).waitFor();assert.equal(await page.getByLabel('Selected action',{exact:true}).locator('strong').innerText(),'completed');assert.deepEqual(JSON.parse(await page.getByLabel('Action result',{exact:true}).innerText()),native.after.execution.selected.output);assert.deepEqual(errors,[]);await page.screenshot({path:resolve(out,'book-snap-result.png')});
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,bothCurrentRevisionsRequired:true,invalidTurnRejected:true,preparingReceiptObserved:true,completedReceiptObserved:true,pagedNativeObjectSelection:true,errors},null,2)+'\n');console.log('Chrome snap editor matched exact native call and receipt with both current revisions.');
}catch(error){
 const diagnostic={error:String(error),errors,url:page?.url(),boundary:'Local replay fixture failure; no headset or provider evidence.'};
 if(page&&!page.isClosed()){
  try{diagnostic.requests=await page.evaluate(()=>window.maestroSnapRequests??[]);diagnostic.pending=await page.evaluate(()=>window.maestroSnapSnapshot?.().request??null);await page.screenshot({path:resolve(out,'book-snap-failure.png')});}catch{}
 }
 await writeFile(resolve(out,'browser-failure.json'),JSON.stringify(diagnostic,null,2)+'\n');throw error;
}finally{await browser.close();}
