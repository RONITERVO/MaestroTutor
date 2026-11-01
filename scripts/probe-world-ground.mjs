// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Replay a real native ground fact through generated book controls. No provider/device calls.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=(process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5196').replace(/\/$/,'');
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_PROBE_OUTPUT||'.quest-evidence/world-ground');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/worldGround.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?worldGround',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');
 await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('world.ground');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Accepted ground near a point/}).click();
 const fact=page.getByLabel('Current fact value',{exact:true});await fact.locator('p').filter({hasText:'"found":false'}).waitFor();assert.deepEqual(JSON.parse(await fact.locator('p').innerText()),native.missing);
 for(const [axis,value] of Object.entries(native.arguments.position))await page.getByLabel('Fact inputs position '+axis,{exact:true}).fill(String(value));
 assert.match(await fact.innerText(),/Not read yet/);await page.getByRole('button',{name:'Read fact',exact:true}).click();
 await fact.locator('p').filter({hasText:'"found":true'}).waitFor();assert.deepEqual(JSON.parse(await fact.locator('p').innerText()),native.supported);
 await fact.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-world-ground.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands));
 assert.equal(commands.some(c=>c.action!=='catalog'),false);
 assert.deepEqual(commands.at(-1).catalog.arguments,native.arguments);
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).count(),0);
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({ground:native.supported,commands,errors,scope:'Captured native fact through generated book inputs; no movement, provider or headset'},null,2)+'\n');
 console.log('Generated book inputs displayed the exact native ground result using observation commands only.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
