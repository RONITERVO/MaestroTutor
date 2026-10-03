// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/surface-drawing');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/surfaceAuthoring.json','utf8')),expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1280,height:960}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',error=>errors.push(String(error)));
 await page.goto(base+'/test-fixtures/browser/quest-surface.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Edit a drawing surface');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Edit a drawing surface.*object.surface.edit/}).click();
 await page.getByLabel('Variant',{exact:true}).selectOption({label:'Draw a surface stroke'});
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByLabel('Action inputs target',{exact:true}).selectOption(expected.arguments.target);
 for(const key of ['surface','stroke','red','green','blue','radius'])await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(expected.arguments[key]));
 await page.getByText('Action inputs points · 2 entries',{exact:true}).click();await page.getByRole('button',{name:'Add Action inputs points entry',exact:true}).click();
 for(let i=0;i<3;i++)for(const axis of ['x','y','z'])await page.getByLabel(`Action inputs points ${i+1} ${axis}`,{exact:true}).fill(String(expected.arguments.points[i][axis]));
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs radius',{exact:true}).fill('0');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);await page.getByLabel('Action inputs radius',{exact:true}).fill(String(expected.arguments.radius));
 await page.getByLabel('Action inputs surface',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-surface-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroSurfaceRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroSurfaceSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroSurfaceRequests),request=requests.find(r=>r.commands[0].action==='execution');assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,currentRevisionRequired:true,invalidRadiusRejected:true,errors},null,2)+'\n');console.log('Chrome drawing controls: native call/receipt matched, current revision required and invalid radius refused.');
}finally{await browser.close();}
