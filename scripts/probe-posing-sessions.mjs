// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Local schema-generated book fields with real native captures; no headset/provider access.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/pose-sessions');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/posingSessions.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?poseSessions',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 async function observe(){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByRole('textbox',{name:'Search facts',exact:true}).fill('posing');
  await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Current live pose session/}).click();
  return JSON.parse(await page.getByLabel('Current fact value',{exact:true}).locator('p').innerText());
 }
 async function edit(operation,session,number){
  await page.getByLabel('Catalog category',{exact:true}).selectOption('actions');await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('animation.pose');
  await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:/Pose Maestro together/}).click();await page.getByText('Edit action fields',{exact:true}).click();
  await page.getByLabel('Pose operation',{exact:true}).selectOption(String(operation));await page.getByLabel('Action inputs sessionId',{exact:true}).fill(session);
  await page.getByLabel('Action inputs '+(operation===0?'revision':'version'),{exact:true}).fill(String(number));
 }
 async function run(phase){await page.getByRole('button',{name:'Run action now',exact:true}).click();await page.getByLabel('Action result',{exact:true}).filter({hasText:'"phase": "'+phase+'"'}).waitFor();}
 const before=await observe();assert.deepEqual(before,native.before);await edit(0,before.sessionId,before.revision);await run('posing');
 const active=await observe();assert.deepEqual(active,native.active);await edit(1,active.sessionId,active.version);
 await page.getByText('Action inputs joints · 1 entries',{exact:true}).click();
 const joint=native.rotate.selected.call.arguments.joints[0];await page.getByLabel('Action inputs joints 1 joint',{exact:true}).selectOption(joint.joint);
 for(const field of ['x','y','z','w'])await page.getByLabel('Action inputs joints 1 rotation '+field,{exact:true}).fill(String(joint.rotation[field]));
 await page.getByLabel('Action inputs joints 1 joint',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-joints.png')});
 await run('posing');const edited=await observe();assert.deepEqual(edited,native.edited);
 await edit(3,edited.sessionId,edited.version);await run('saved');await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book.png')});const idle=await observe();assert.deepEqual(idle,native.idle);
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[native.start,native.rotate,native.finish].map(v=>({action:'execution',execution:{operation:'start',call:v.selected.call,runId:v.selected.id}})));
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,exactNativeSessions:true,commands,before,active,edited,idle,acknowledgement:'native capture replay, not headset or provider execution',errors},null,2)+'\n');
 console.log('Book read each native pose version and sent exact start, joint rotation and finish requests.');
}finally{await browser.close();}
