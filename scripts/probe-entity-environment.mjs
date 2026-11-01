// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Display captured native profile facts through generic book controls; no room actions.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=(process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5196').replace(/\/$/,'');
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve(process.env.MAESTRO_PROBE_OUTPUT||'.quest-evidence/entity-environment');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/entityEnvironment.json','utf8'));
const manifest=JSON.parse(await readFile('shared/generated/behaviourCatalog.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});let page;
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',r=>['localhost','127.0.0.1'].includes(new URL(r.request().url()).hostname)?r.continue():r.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?environmentProfiles',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 const observations=[];
 for(const [id,args,expected] of [['object.environment',{target:'maestro'},native.after],['environment.profile',{id:native.profile.id},native.profile],['environment.profiles',{offset:0},native.profiles]]){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');
  await page.getByRole('textbox',{name:'Search facts',exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
  const definition=manifest.facts.find(d=>d.id===id);await page.getByRole('button',{name:new RegExp('^'+definition.label+' '+id+' · v1$')}).click();
  for(const [key,value] of Object.entries(args)){
   const input=page.getByLabel('Fact inputs '+key,{exact:true});
   if(await input.evaluate(e=>e.tagName)==='SELECT')await input.selectOption(String(value));else await input.fill(String(value));
  }
  await page.getByRole('button',{name:'Read fact',exact:true}).click();
  await page.waitForFunction(()=>Array.from(document.querySelectorAll('button')).some(b=>b.textContent==='Read fact'&&!b.disabled));
  const fact=page.getByLabel('Current fact value',{exact:true}).locator('p').filter({hasText:/^\{/});
  await fact.waitFor();const value=JSON.parse(await fact.innerText());assert.deepEqual(value,expected);observations.push({id,value});
 }
 await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-environment-profiles.png')});
 await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');
 await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('object.environment.assign');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/^Choose object environment object.environment.assign · v1$/}).click();
 await page.getByLabel('Action inputs target',{exact:true}).waitFor({state:'visible'});
 await page.getByLabel('Action inputs target',{exact:true}).selectOption('maestro');
 await page.getByRole('button',{name:'Load current values',exact:true}).click();
 await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(await page.getByLabel('Action inputs profileId',{exact:true}).inputValue(),native.after.profileId);
 assert.equal(Number(await page.getByLabel('Action inputs profileRevision',{exact:true}).inputValue()),native.after.profileRevision);
 await page.screenshot({path:resolve(out,'book-environment-assignment.png')});
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands));
 assert.equal(commands.some(c=>c.action!=='catalog'),false);assert.deepEqual(errors,[]);
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({observations,commands,errors,scope:'Exact native facts and generated editable inputs; no mutation, provider or headset'},null,2)+'\n');
 console.log('Generated book controls read exact native environment profiles and loaded editable binding fields.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());}throw error;}finally{await browser.close();}
