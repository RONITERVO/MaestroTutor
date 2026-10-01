// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Local controls replay actual native captures; no provider or headset access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/controller-configuration');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/controllerConfiguration.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?controllerConfiguration',{waitUntil:'domcontentloaded',timeout:60000});await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('controller');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Controller settings and bindings/}).click();
  return JSON.parse(await page.getByLabel('Current fact value',{exact:true}).locator('p').innerText());
 }
 async function edit(index){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('controller.configure');await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Configure controller bindings/}).click();await page.getByText('Edit action fields',{exact:true}).click();
  await page.getByLabel('Controller settings',{exact:true}).selectOption(String(index));
 }
 const before=await observe();assert.deepEqual(before,native.before);await edit(1);
 const move=native.movement.selected.call.arguments;
 await page.getByLabel('Action inputs configurationId',{exact:true}).fill(move.configurationId);await page.getByLabel('Action inputs userStick',{exact:true}).selectOption(move.userStick);
 for(const key of ['deadZone','userSpeed'])await page.getByLabel('Action inputs '+key,{exact:true}).fill(String(move[key]));
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:native.afterMovement.configurationId}).waitFor();
 const afterMovement=await observe();assert.deepEqual(afterMovement,native.afterMovement);await edit(3);const bind=native.button.selected.call.arguments;
 await page.getByLabel('Action inputs configurationId',{exact:true}).fill(bind.configurationId);await page.getByLabel('Action inputs button',{exact:true}).selectOption(bind.button);await page.getByLabel('Action inputs programId',{exact:true}).fill(bind.programId);
 const buttonValues=await page.getByLabel('Action inputs button',{exact:true}).locator('option').evaluateAll(options=>options.map(option=>option.value));assert.deepEqual(buttonValues,['x','a','leftStickClick','rightStickClick']);
 await page.getByRole('button',{name:'Run action now',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-button.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:native.after.configurationId}).waitFor();const after=await observe();assert.deepEqual(after,native.after);
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-settings.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[native.movement,native.button].map(v=>({action:'execution',execution:{operation:'start',call:v.selected.call,runId:v.selected.id}})));
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,commands,before,afterMovement,after,acknowledgement:'native capture replay; no controller, provider or headset execution',errors},null,2)+'\n');
 console.log('Book saved independent stick preferences and an exact program binding through generated fields.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
