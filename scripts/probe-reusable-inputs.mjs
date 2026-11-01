// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Browser authoring only. The generated fixture is executed separately by Unity tests.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/reusable-inputs');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/spatialSettings.json','utf8'));
const original=JSON.parse(JSON.parse(await readFile('test-fixtures/browser/programBookState.json','utf8')).rules.selected.program);
const browser=await chromium.launch({channel:'chrome',headless:true});let page;const errors=[];
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 page=await context.newPage();page.setDefaultTimeout(15000);page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?spatialSettings',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('avatar.movement.configure');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/^Configure Maestro movement/}).click();await page.getByText('Edit action fields',{exact:true}).click();await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
 assert.equal(Number(await page.getByLabel('Action inputs revision',{exact:true}).inputValue()),native.beforeMovement.revision);
 const distance=page.getByLabel('Keep current distance when running',{exact:true}),speed=page.getByLabel('Keep current speed when running',{exact:true});assert.equal(await distance.isChecked(),true);assert.equal(await speed.isChecked(),true);
 await page.getByLabel('Action inputs speed',{exact:true}).fill('0.9');assert.equal(await speed.isChecked(),false);assert.equal(await distance.isChecked(),true);
 await distance.uncheck();await distance.check();await page.getByRole('region',{name:'Behaviour input choices',exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-behaviour-inputs.png')});
 await page.getByRole('button',{name:'Add read and action to draft',exact:true}).click();await page.getByText(/^Read Maestro movement settings/).waitFor();
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.action==='rules'&&c.rule.action==='edit')));
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands));assert.equal(commands.some(c=>c.action==='execution'||c.action==='rules'&&c.rule.action==='play'),false);
 const edits=commands.filter(c=>c.action==='rules'&&c.rule.action==='edit');assert.equal(edits.length,1);const program=JSON.parse(edits[0].rule.edits[0].sequence.program);
 assert.equal(program.dataVersion,1);const fn=program.functions.find(f=>f.name===program.entry),before=original.functions.find(f=>f.name===original.entry);assert.deepEqual(fn.body.slice(2),before.body);
 const [read,action]=fn.body;assert.equal(read.op,'set');assert.deepEqual(read.value,{fact:'avatar.movement.settings'});assert.equal(action.capability,'avatar.movement.configure');assert.equal(action.arguments.speed,.9);
 assert.deepEqual(action.bindings,{revision:{op:'field',args:[{var:read.variable},{value:'revision'}]},distance:{op:'field',args:[{var:read.variable},{value:'distance'}]}});
 await page.getByText(/^Read Maestro movement settings/).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book-generated-program.png')});
 assert.equal(await page.evaluate(()=>document.documentElement.scrollWidth>document.documentElement.clientWidth),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({commands,program,nativeSnapshot:native.beforeMovement,noAutoRun:true,oneRead:true,acknowledgement:'browser authoring replay; native fixture execution tested separately',errors},null,2)+'\n');console.log('Book loaded current settings, authored one shared read and action, and saved without running it.');
}catch(error){if(page){await page.screenshot({path:resolve(out,'failure.png')});await writeFile(resolve(out,'failure.txt'),await page.locator('body').innerText());await writeFile(resolve(out,'failure-errors.json'),JSON.stringify(errors));}throw error;}finally{await browser.close();}
