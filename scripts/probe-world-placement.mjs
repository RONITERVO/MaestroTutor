// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Actual native receipt replay through the generated book UI; no headset or provider access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=(process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5196').replace(/\/$/,'');
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_PROBE_OUTPUT||'.quest-evidence/world-placement');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/worldPlacement.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?worldPlacement',{waitUntil:'domcontentloaded',timeout:60000});await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('world.viewpoint');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Your world location/}).click();
  return JSON.parse(await page.getByLabel('Current fact value',{exact:true}).locator('p').innerText());
 }
 assert.deepEqual(await observe(),native.before);
 await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('world.viewpoint.set');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Go to a world location/}).click();
 assert.equal(await page.getByLabel('Behaviour input timing',{exact:true}).inputValue(),'current');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs stateId',{exact:true}).inputValue(),native.before.stateId);
 for(const axis of ['x','y','z'])await page.getByLabel('Action inputs position '+axis,{exact:true}).fill(String(native.request.call.arguments.position[axis]));
 await page.getByLabel('Action inputs yaw',{exact:true}).fill(String(native.request.call.arguments.yaw));
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:native.receipt.selected.output.stateId}).waitFor();await page.getByLabel('Action result',{exact:true}).filter({hasText:native.receipt.selected.output.stateId}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-world-placement.png')});
 assert.deepEqual(await observe(),native.after);
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[{action:'execution',execution:{operation:'start',call:native.receipt.selected.call,runId:native.receipt.selected.id}}]);
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({currentGuards:true,commands,before:native.before,after:native.after,acknowledgement:'Native capture replay through actual generated book controls; no headset or provider execution',errors},null,2)+'\n');
 console.log('Book selected a world location with a fresh native guard and displayed the native receipt.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
