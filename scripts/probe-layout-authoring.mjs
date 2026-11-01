// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated book controls with captured native acknowledgements; no headset/provider.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/layout');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/layoutAuthoring.json','utf8')),expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1280,height:960}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-layout.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Arrange or reset objects');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Arrange or reset objects.*object.layout.apply/}).click();await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByText('Action inputs placements · 1 entries',{exact:true}).click();await page.getByRole('button',{name:'Add Action inputs placements entry',exact:true}).click();
 for(let i=0;i<expected.arguments.placements.length;i++){
  const p=expected.arguments.placements[i],prefix='Action inputs placements '+(i+1)+' ';
  await page.getByLabel(prefix+'target',{exact:true}).selectOption(p.target);
  for(const group of ['position','rotation'])for(const [key,value] of Object.entries(p[group]))await page.getByLabel(prefix+group+' '+key,{exact:true}).fill(String(value));
  await page.getByLabel(prefix+'scale',{exact:true}).fill(String(p.scale));
 }
 const second=page.getByLabel('Action inputs placements 2 target',{exact:true});await second.selectOption(expected.arguments.placements[0].target);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await second.selectOption(expected.arguments.placements[1].target);
 await page.getByLabel('Action inputs placements 1 target',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-layout-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>window.maestroLayoutRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroLayoutSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroLayoutRequests),request=requests.find(r=>r.commands[0].action==='execution');
 assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);
 for(const p of expected.arguments.placements)assert.ok(request.conditions.some(c=>c.id===p.target&&c.revision===native.before.objects.find(o=>o.id===p.target).objectRevision));
 assert.deepEqual(errors,[]);await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,allMemberConditions:true,duplicateMemberRejected:true,errors},null,2)+'\n');
 console.log('Chrome layout controls: exact native call/receipt, all member revision guards and duplicate rejection verified.');
}finally{await browser.close();}
