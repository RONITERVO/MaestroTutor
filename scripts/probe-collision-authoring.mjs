// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real schema-generated controls; replayed native acknowledgements, no headset/provider.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/collision');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/collisionAuthoring.json','utf8'));
const expected=native.after.execution.selected.call;
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-collision.html',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByLabel('Search actions',{exact:true}).fill('Edit collision shapes');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Edit collision shapes.*object.collision.edit/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByLabel('Action inputs target',{exact:true}).selectOption(expected.arguments.target);
 await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 await page.getByText('Action inputs collision shapes · 1 entries',{exact:true}).click();
 for(const [index,shape] of expected.arguments.collision.shapes.entries()){
  if(index)await page.getByRole('button',{name:'Add Action inputs collision shapes entry',exact:true}).click();
  const label='Action inputs collision shapes '+(index+1);
  await page.getByLabel(label+' variant',{exact:true}).selectOption(shape.shape==='cylinder'?'2':'3');
  await page.getByLabel(label+' id',{exact:true}).fill(shape.id);
  for(const field of ['position','size','rotation'])for(const [axis,value] of Object.entries(shape[field]))await page.getByLabel(label+' '+field+' '+axis,{exact:true}).fill(String(value));
  await page.getByLabel(label+' segments',{exact:true}).fill(String(shape.segments));
  if(shape.shape==='ring')await page.getByLabel(label+' innerRadius',{exact:true}).fill(String(shape.innerRadius));
  else {await page.getByLabel('Include '+label+' innerRadius',{exact:true}).check();await page.getByLabel(label+' innerRadius',{exact:true}).fill('0');}
 }
 const radius=page.getByLabel('Action inputs collision shapes 2 innerRadius',{exact:true});await radius.fill('0.5');
 assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);assert.equal((await page.evaluate(()=>window.maestroCollisionRequests)).some(r=>r.commands[0].action==='execution'),false);
 await radius.fill('0.4');await radius.scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-collision-editor.png')});
 await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.waitForFunction(()=>window.maestroCollisionRequests.some(r=>r.commands[0].action==='execution')&&!window.maestroCollisionSnapshot().request);await page.getByLabel('Action result',{exact:true}).waitFor();
 const requests=await page.evaluate(()=>window.maestroCollisionRequests);const request=requests.find(r=>r.commands[0].action==='execution');
 assert.deepEqual(request.commands[0].execution.call,expected);assert.equal(request.commands[0].execution.runId,native.after.execution.selected.id);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:native.boundary,request,nativeCallMatched:true,invalidDraftRetained:true,errors},null,2)+'\n');
 console.log('Chrome collision fields: exact native call matched; invalid wall rejected before submission.');
}finally{await browser.close();}
