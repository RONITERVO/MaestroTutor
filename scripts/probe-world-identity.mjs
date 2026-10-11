// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Actual native fact capture through generated book UI; no headset or provider access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=(process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5196').replace(/\/$/,'');
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_PROBE_OUTPUT||'.quest-evidence/world-identity');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/worldIdentity.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?worldIdentity',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');
 await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('world.identity');
 await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/World and region identity/}).click();
 const fact=page.getByLabel('Current fact value',{exact:true});
 assert.deepEqual(JSON.parse(await fact.locator('p').innerText()),native.identity);
 await fact.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-world-identity.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands));
 assert.equal(commands.some(c=>c.action!=='catalog'),false);
 assert.equal(commands.filter(c=>c.catalog?.operation==='inspect'&&c.catalog.capability==='world.identity').length,1);
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({identity:native.identity,commands,errors,scope:'Captured native fact through generated book controls; observation only, no headset or provider'},null,2)+'\n');
 console.log('Book displayed exact native world/region identities using only catalog observations.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
