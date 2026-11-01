// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_CATCH_EVIDENCE_DIR||'.quest-evidence/physical-catching/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/physicalCatching.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-physical-catching.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('heading',{name:'Catch a physical object',exact:true}).waitFor();
 await page.getByText('Edit action fields',{exact:true}).click();
 const args=native.call.arguments;
 assert.equal(await page.getByLabel('Action inputs target',{exact:true}).inputValue(),args.target);
 assert.equal(await page.getByLabel('Action inputs holder objectId',{exact:true}).inputValue(),args.holder.objectId);
 assert.equal(await page.getByLabel('Action inputs holder part',{exact:true}).inputValue(),args.holder.part);
 assert.equal(await page.getByLabel('Action inputs holder revision',{exact:true}).inputValue(),String(args.holder.revision));
 for(const [name,invalid] of [['timeout','16'],['holdSeconds','0'],['gripRadius','1'],['maxSpeed','9']]){
  const field=page.getByLabel('Action inputs '+name,{exact:true});await field.fill(invalid);
  assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
  await field.fill(String(args[name]));
 }
 await page.getByLabel('Action inputs maxSpeed',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-catch-editor.png')});
 await page.getByRole('button',{name:'Check availability',exact:true}).click();
 await page.waitForFunction(()=>window.maestroCatchRequests.some(r=>r.commands[0].catalog?.operation==='check')&&!window.maestroCatchSnapshot().request);
 assert.equal(await page.evaluate(()=>window.maestroCatchState().catalog.available),true);
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>window.maestroCatchState().execution?.selected?.output?.caught===true&&!window.maestroCatchSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroCatchRequests),request=requests.find(r=>r.commands[0].execution?.operation==='start');
 assert.deepEqual(request.commands[0].execution.call,native.call);assert.equal(request.commands[0].execution.runId,native.waiting.execution.selected.id);
 const receipt=await page.evaluate(()=>window.maestroCatchState().execution.selected);assert.deepEqual(receipt,native.after.execution.selected);assert.deepEqual(errors,[]);
 await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-catch-result.png')});
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary+' Browser responses replay those observations; this run checks the actual generated form and shared client, not browser physics.',request,receipt,nativeCallMatched:true,completedReceiptMatched:true,boundedArgumentsRejected:true,errors},null,2)+'\n');
 console.log('Chrome catch controls matched the actual native call and completed receipt; timing, radius and speed bounds enforced.');
}finally{await browser.close();}
