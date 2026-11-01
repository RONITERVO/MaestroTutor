// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/scooping/browser');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/containerScooping.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1440,height:1080}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-scooping.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');
 await page.getByLabel('Search facts',{exact:true}).fill('Live liquid scooping');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Live liquid scooping.*object.container.scooping/}).click();
 await page.getByLabel('Fact inputs target',{exact:true}).selectOption(native.live.catalog.arguments.target);
 assert.match(await page.getByLabel('Current fact value',{exact:true}).innerText(),/Not read yet/);
 await page.getByRole('button',{name:'Read fact',exact:true}).click();
 await page.waitForFunction(()=>document.querySelector('[aria-label="Current fact value"]')?.textContent.includes('"scoopedMl":0')&&!window.maestroScoopingSnapshot().request);
 assert.match(await page.getByLabel('Current fact value',{exact:true}).innerText(),/"scoopedMl":0/);
 await page.screenshot({path:resolve(out,'book-live-container.png')});
 await page.getByLabel('Catalog category',{exact:true}).selectOption('events');
 await page.getByLabel('Search events',{exact:true}).fill('A liquid scoop was saved');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/A liquid scoop was saved.*object.container.scooped/}).click();
 await page.getByLabel('Event definition',{exact:true}).waitFor();
 const event=await page.getByLabel('Event definition',{exact:true}).innerText();for(const field of ['scoopedMl','donors','liquid','temporary'])assert.ok(event.includes(field));
 assert.ok(event.includes('Event wait'));await page.screenshot({path:resolve(out,'book-scoop-event.png')});
 const requests=await page.evaluate(()=>window.maestroScoopingRequests);
 assert.deepEqual(requests.map(r=>r.commands[0]),native.steps.map(s=>s.request));assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'scooping-browser.json'),JSON.stringify({boundary:native.boundary,nativeQueriesMatched:true,notReadUntilRequested:true,typedEventFieldsVisible:true,requests,errors},null,2)+'\n');
 console.log('Chrome scooping fact read and scoop event inspection matched exact native queries.');
}finally{await browser.close();}
