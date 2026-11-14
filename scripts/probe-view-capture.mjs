// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Native camera evidence replayed in Chrome; this does not execute Unity or Gemini.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import {createHash} from 'node:crypto';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/view-capture/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/viewCapture.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-view-capture.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 const img=page.getByRole('img',{name:'Virtual room snapshot',exact:true});await img.waitFor();
 const pixels=await img.evaluate(el=>({src:el.src,width:el.naturalWidth,height:el.naturalHeight}));
 assert.equal(pixels.width,512);assert.equal(pixels.height,384);
 assert.equal(createHash('sha256').update(Buffer.from(pixels.src.split(',')[1],'base64')).digest('hex'),native.payload.capture.sha256);
 await img.evaluate(el=>el.closest('figure').scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'book-virtual-view.png')});
 const requests=await page.evaluate(()=>window.maestroCaptureRequests);
 const request=requests.find(r=>r.commands[0].action==='execution');
 assert.deepEqual(request.commands[0].execution.call,native.after.execution.selected.call);
 assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);
 assert.equal(await page.evaluate(()=>window.maestroCaptureSnapshot().captureAck),native.payload.capture.captureId);
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,hash:native.payload.capture.sha256,dimensions:[pixels.width,pixels.height],exactPixelsVerified:true,errors},null,2)+'\n');
 console.log('Chrome virtual view: same native action, receipt, SHA-256 image and image acknowledgement verified.');
}finally{await browser.close();}
