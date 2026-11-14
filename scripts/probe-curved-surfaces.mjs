// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/curved-surfaces/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/curvedSurfaceAuthoring.json','utf8')),expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-surface.html?curved',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Edit a drawing surface');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Edit a drawing surface.*object.surface.edit/}).click();
 await page.getByLabel('Variant',{exact:true}).selectOption({label:'Configure a drawing patch'});
 await page.getByText('Edit action fields',{exact:true}).click();await page.getByLabel('Action inputs target',{exact:true}).selectOption(expected.arguments.target);
 await page.getByLabel('Action inputs surface',{exact:true}).fill(expected.arguments.surface);
 const d=expected.arguments.definition;
 for(const key of ['part','width','height'])await page.getByLabel('Action inputs definition '+key,{exact:true}).fill(String(d[key]));
 for(const group of ['position','rotation'])for(const [axis,value] of Object.entries(d[group]))await page.getByLabel('Action inputs definition '+group+' '+axis,{exact:true}).fill(String(value));
 await page.getByLabel('Action inputs definition enabled',{exact:true}).selectOption(String(d.enabled));
 await page.getByLabel('Include Action inputs definition shape',{exact:true}).check();await page.getByLabel('Action inputs definition shape',{exact:true}).selectOption(d.shape);
 await page.getByLabel('Include Action inputs definition curvatureRadius',{exact:true}).check();await page.getByLabel('Action inputs definition curvatureRadius',{exact:true}).fill(String(d.curvatureRadius));
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByLabel('Action inputs definition curvatureRadius',{exact:true}).fill('.01');assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
 await page.getByLabel('Action inputs definition curvatureRadius',{exact:true}).fill(String(d.curvatureRadius));assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),false);
 await page.getByLabel('Action inputs definition width',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'curved-fields.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroSurfaceRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroSurfaceSnapshot().request);
 await page.getByLabel('Action result',{exact:true}).waitFor();const requests=await page.evaluate(()=>window.maestroSurfaceRequests),request=requests.find(r=>r.commands[0].action==='execution');
 assert.deepEqual(request.commands[0].execution.call,expected);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,nativeCallMatched:true,invalidCurveRejected:true,revisionLoaded:true,request,errors},null,2)+'\n');
 console.log('Curved surface book controls matched native configuration; impossible radius refused.');
}finally{await browser.close();}
