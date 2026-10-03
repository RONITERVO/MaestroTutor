// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Exercises real workshop controls against captured native states. Acknowledgements are replayed.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/lathe');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/latheAuthoring.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-lathe.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('img',{name:'Lathe cross section'}).waitFor();
 await page.getByLabel('Profile point 2 radius').fill('-0.1');await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.getByText('This recipe needs valid sizes, joints and animation keys before it can be applied.').waitFor();
 assert.equal(await page.getByLabel('Profile point 2 radius').inputValue(),'-0.1');
 assert.equal(await page.evaluate(()=>window.maestroLatheRequests.length),0);
 await page.getByLabel('Profile point 2 radius').fill('0.48');await page.getByLabel('Lathe segments').fill('32');
 await page.getByRole('button',{name:'lathe',exact:true}).click();assert.equal(await page.getByLabel('Profile point 2 radius').inputValue(),'0.48');
 await page.screenshot({path:resolve(out,'book-profile-editor.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>window.maestroLatheRequests.length===1&&!window.maestroLatheSnapshot().request);
 const request=await page.evaluate(()=>window.maestroLatheRequests[0]);
 assert.deepEqual(request.commands[0].execution.call,native.after.execution.selected.call);
 assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);
 assert.equal(await page.getByRole('button',{name:'Apply changes',exact:true}).isDisabled(),true);
 assert.equal(await page.getByLabel('Profile point 2 radius').inputValue(),'0.48');
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,invalidDraftRetained:true,nativeCallMatched:true,errors},null,2)+'\n');
 console.log('Chrome profile editor: invalid draft retained; exact native edit call and completed receipt matched.');
}finally{await browser.close();}
