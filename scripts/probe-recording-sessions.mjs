// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Local book controls using captured native acknowledgements; no device/provider calls.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/recording-sessions');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/recordingSessions.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?recordingSessions',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');
  await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('recording');
  await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Current recording session/}).click();
  const text=await page.getByLabel('Current fact value',{exact:true}).locator('p').innerText();return JSON.parse(text);
 }
 const before=await observe();assert.deepEqual(before,native.before);
 await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');
 await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('animation.record');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Record, save or discard a take/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByLabel('Action inputs sessionId',{exact:true}).fill(before.sessionId);
 await page.getByLabel('Action inputs revision',{exact:true}).fill(String(native.start.selected.call.arguments.revision));
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.getByLabel('Action result',{exact:true}).filter({hasText:'"phase": "recording"'}).waitFor();
 assert.equal(await page.getByRole('button',{name:/Stop action/}).count(),0);
 await page.getByLabel('Recording operation',{exact:true}).selectOption('1');await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.getByLabel('Action result',{exact:true}).filter({hasText:'"phase": "saved"'}).waitFor();await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book.png')});
 const idle=await observe();assert.deepEqual(idle,native.idle);assert.notEqual(idle.sessionId,before.sessionId);
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[native.start,native.finish].map(view=>({action:'execution',execution:{operation:'start',call:view.selected.call,runId:view.selected.id}})));
 assert.deepEqual(errors,[]);await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,exactSession:true,commands,before,idle,acknowledgement:'recorded native output; browser simulation only',errors},null,2)+'\n');
 console.log('Book inspected the recording identity, sent exact start/finish calls, and observed the new idle identity.');
}finally{await browser.close();}
