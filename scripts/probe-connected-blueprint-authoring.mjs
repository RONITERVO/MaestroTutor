// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated book controls with real native-state replay; no provider or headset.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/connected-blueprints');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/connectedBlueprintAuthoring.json','utf8')),expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1280,height:960}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-connected-blueprint.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Create a structure');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Create a structure.*object.batch.create/}).click();
 await page.getByLabel('Action arguments',{exact:true}).fill(JSON.stringify(expected.arguments,null,2));
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByText('Action inputs blueprint hinges · 1 entries',{exact:true}).click();
 const connected=page.getByLabel('Action inputs blueprint hinges 1 connected',{exact:true});await connected.fill('missing');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await connected.fill(expected.arguments.blueprint.hinges[0].connected);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),false);
 await connected.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-connected-blueprint-editor.png')});await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.waitForFunction(()=>window.maestroConnectedBlueprintRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroConnectedBlueprintSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroConnectedBlueprintRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,missingConnectedSlotRejected:true,errors},null,2)+'\n');console.log('Chrome connected blueprint controls: exact native call/receipt and missing-slot rejection verified.');
}finally{await browser.close();}
