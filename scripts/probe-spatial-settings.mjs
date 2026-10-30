// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Generated book fields replay native settings receipts, without device/provider access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_PROBE_OUTPUT||'.quest-evidence/spatial-settings');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/spatialSettings.json','utf8'));
const manifest=JSON.parse(await readFile('shared/generated/behaviourCatalog.json','utf8'));
const views=[native.physics,native.movement,native.walk];
const ids=['object.physics.settings','avatar.movement.settings','avatar.walk.settings'];
const expectedBefore=[native.beforePhysics,native.beforeMovement,native.beforeWalk];
const expectedAfter=[native.afterPhysics,native.afterMovement,native.afterWalk];
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?spatialSettings',{waitUntil:'domcontentloaded',timeout:60000});await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(id){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByRole('textbox',{name:'Search facts',exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
  const definition=manifest.facts.find(f=>f.id===id);await page.getByRole('button',{name:new RegExp('^'+definition.label+' '+id)}).click();
  if(id==='object.physics.settings'){await page.getByLabel('Fact inputs target',{exact:true}).selectOption(native.beforePhysics.target);await page.getByRole('button',{name:'Read fact',exact:true}).click();}
  return JSON.parse(await page.getByLabel('Current fact value',{exact:true}).locator('p').filter({hasText:/^\{/}).innerText());
 }
 const before=[];for(const id of ids)before.push(await observe(id));assert.deepEqual(before,expectedBefore);
 const after=[];
 for(let i=0;i<views.length;i++){
  const view=views[i],call=view.selected.call,definition=manifest.actions.find(d=>d.id===call.id);
  await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');await page.getByRole('textbox',{name:'Search actions',exact:true}).fill(call.id);await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:new RegExp('^'+definition.label+'\\s*'+call.id+' · v1$')}).click();await page.getByText('Edit action fields',{exact:true}).click();
  if('source' in call.arguments)await page.getByLabel('Walking animation source',{exact:true}).selectOption(String(['included','library','embedded'].indexOf(call.arguments.source)));
  if('target' in call.arguments)await page.getByLabel('Action inputs target',{exact:true}).selectOption(call.arguments.target);
  await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();assert.equal(Number(await page.getByLabel('Action inputs revision',{exact:true}).inputValue()),call.arguments.revision);
  for(const [key,value] of Object.entries(call.arguments)){
   if(key==='source'||key==='revision')continue;const input=page.getByLabel('Action inputs '+key,{exact:true});
   if(await input.evaluate(el=>el.tagName)==='SELECT')await input.selectOption(String(value));else await input.fill(String(value));
  }
  await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:String(view.selected.output.revision)}).waitFor();
  if(i===0)await page.screenshot({path:resolve(out,'book-physics-action.png')});
  after.push(await observe(ids[i]));assert.deepEqual(after[i],expectedAfter[i]);
 }
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-walk-readback.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,views.map(v=>({action:'execution',execution:{operation:'start',call:v.selected.call,runId:v.selected.id}})));
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,commands,before,after,acknowledgement:'native capture replay; no headset or provider execution',errors},null,2)+'\n');
 console.log('Book saved physics, movement and walking preferences using native revisions and readback.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
