// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/grip-snapping/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/gripSnappingAuthoring.json','utf8')),expected=native.after.execution.selected.call,a=expected.arguments;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-grip-snap.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Configure construction grip snapping');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Configure construction grip snapping.*room.selection.snapSettings/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).getAttribute('readonly'),'');assert.equal(await page.getByLabel('Action inputs mode',{exact:true}).inputValue(),'off');
 await page.getByLabel('Action inputs mode',{exact:true}).selectOption(a.mode);
 for(const key of ['distance','turnStep','breakForce','breakTorque'])await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(a[key]));
 await page.getByLabel('Action inputs distance',{exact:true}).fill('.3');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await page.getByLabel('Action inputs distance',{exact:true}).fill(String(a.distance));
 await page.getByLabel('Action inputs mode',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'grip-snap-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroGripSnapRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroGripSnapSnapshot().request);
 const requests=await page.evaluate(()=>window.maestroGripSnapRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await page.getByLabel('Action result',{exact:true}).waitFor();await page.screenshot({path:resolve(out,'grip-snap-result.png')});
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,currentSettingsRequired:true,invalidDistanceRejected:true,errors},null,2)+'\n');console.log('Chrome grip snapping editor matched native settings, call and receipt.');
}finally{await browser.close();}
