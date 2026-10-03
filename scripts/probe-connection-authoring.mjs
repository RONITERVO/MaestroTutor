// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_CONNECTION_EVIDENCE_DIR||'.quest-evidence/physical-connections/fixed');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/connectionAuthoring.json','utf8')),expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1280,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-hinge.html?kind=fixed',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Connect physical pieces');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Connect physical pieces.*object.connection.edit/}).click();
 await page.getByLabel('Variant',{exact:true}).selectOption({label:'attach'});await page.getByText('Edit action fields',{exact:true}).click();
 for(const name of ['target','connected'])await page.getByLabel('Action inputs '+name,{exact:true}).selectOption(expected.arguments[name]);
 for(const name of ['breakForce','breakTorque'])await page.getByLabel('Action inputs '+name,{exact:true}).fill(String(expected.arguments[name]));
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs breakForce',{exact:true}).fill('-1');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await page.getByLabel('Action inputs breakForce',{exact:true}).fill(String(expected.arguments.breakForce));
 await page.getByLabel('Action inputs connected',{exact:true}).selectOption(expected.arguments.target);assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await page.getByLabel('Action inputs connected',{exact:true}).selectOption(expected.arguments.connected);
 // Refresh the current-value guard after checking invalid members.
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs breakForce',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-fixed-connection.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroHingeRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroHingeSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroHingeRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,nativeReceiptMatched:true,currentRevisionRequired:true,negativeBreakLimitRejected:true,selfConnectionRejected:true,errors},null,2)+'\n');console.log('Chrome rigid connection controls matched native call and receipt; stale guards, negative break limits and self-connection refused.');
}finally{await browser.close();}
