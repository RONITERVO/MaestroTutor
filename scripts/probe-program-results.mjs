// Browser authoring with real native program/output fixtures; edit acknowledgements are simulated.
// No provider or headset calls. The native PlayMode tests execute the actual creation/physics.
import {chromium} from 'playwright-core';
import {mkdir,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/program-creation');await mkdir(out,{recursive:true});
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage();const errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?creation');
 await page.getByText('Create shape → ball (objectId)',{exact:true}).waitFor();
 await page.getByText('Variables and results',{exact:true}).first().click();
 await page.getByLabel('create new variable for objectId').click();
 await page.getByText('Variables and results',{exact:true}).nth(1).click();
 await page.getByLabel('push argument target variable').selectOption('objectId');
 await page.getByLabel('push argument target variable').scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'result-binding.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>window.maestroBook?.roomSnapshot().request?.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit'));
 const request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);
 const source=request.commands[0].rule.edits.find(e=>e.kind==='save').sequence.program;
 const program=JSON.parse(source);
 assert.deepEqual(program.functions[0].body[0].results,{objectId:'objectId'});
 assert.deepEqual(program.functions[0].body[1].bindings,{target:{var:'objectId'}});
 assert(program.functions[0].locals.some(v=>v.name==='objectId'&&v.initial===''));
 assert.equal(request.commands.some(c=>c.action==='execution'),false);
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser-result-binding.json'),JSON.stringify({request,acknowledgement:'simulated',execution:'not attempted',errors},null,2)+'\n');
 console.log('Chrome result wiring and exact saved program verified; no native action started.');
} finally {await browser.close();}
