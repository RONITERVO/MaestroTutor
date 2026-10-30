// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Exercises real workshop controls against captured native states. Acknowledgements are replayed.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/sweep/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/sweepAuthoring.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-sweep.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('img',{name:'Sweep cross section'}).waitFor();
 await page.getByLabel('Path point 3 z').fill('0.6');await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.getByText('This recipe needs valid sizes, profiles, paths, joints and animation keys before it can be applied.').waitFor();
 assert.equal(await page.getByLabel('Path point 3 z').inputValue(),'0.6');
 assert.equal(await page.evaluate(()=>window.maestroSweepRequests.length),0);
 await page.getByLabel('Path point 3 z').fill('0.05');
 await page.getByRole('button',{name:'sweep',exact:true}).click();assert.equal(await page.getByLabel('Path point 3 z').inputValue(),'0.05');
 await page.getByRole('img',{name:'Sweep path X Y',exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-sweep-paths.png')});
 await page.getByLabel('Path point 3 z').scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-sweep-editor.png')});
 await page.getByRole('img',{name:'Sweep cross section'}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-sweep-profile.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>window.maestroSweepRequests.length===1&&!window.maestroSweepSnapshot().request);
 const request=await page.evaluate(()=>window.maestroSweepRequests[0]);
 assert.deepEqual(request.commands[0].execution.call,native.after.execution.selected.call);
 assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);
 assert.equal(await page.getByRole('button',{name:'Apply changes',exact:true}).isDisabled(),true);
 assert.equal(await page.getByLabel('Path point 3 z').inputValue(),'0.05');
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,invalidDraftRetained:true,nativeCallMatched:true,errors},null,2)+'\n');
 console.log('Chrome sweep editor: invalid draft retained; exact native edit call and completed receipt matched.');
}finally{await browser.close();}
