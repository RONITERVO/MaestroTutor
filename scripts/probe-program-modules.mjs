// Book UI and native observation replay. Command acknowledgements are simulated; no device/provider calls.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/modules');await mkdir(out,{recursive:true});const native=JSON.parse(await readFile(resolve(out,'native/running.json'),'utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});await context.route('**/*',r=>['localhost','127.0.0.1'].includes(new URL(r.request().url()).hostname)?r.continue():r.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));await page.goto(base+'/test-fixtures/browser/quest-workspace.html?visualBlocks&structured&modules');
 await page.evaluate(rules=>window.maestroWorkspaceRulesEvidence(rules),native.rules);const click=name=>page.getByRole('button',{name,exact:true}).click();await page.getByLabel('Program editor',{exact:true}).waitFor();
 const live=page.getByLabel('Live program values');assert((await live.textContent()).includes('state.first.inner.count = 3'));assert((await live.textContent()).includes('state.second.count = 5'));
 const imported=page.getByLabel('Pinned module first');await imported.locator('summary').click();await imported.evaluate(e=>e.scrollIntoView({block:'start'}));assert.equal(await page.getByLabel('Edit values first.inner.change',{exact:true}).count(),0);await page.screenshot({path:resolve(out,'pinned-module.png')});
 await live.evaluate(e=>e.scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'native-module-state.png')});
 await click('Edit values first');await page.getByLabel('Called function',{exact:true}).selectOption('second.add');await page.getByLabel('Argument amount value',{exact:true}).fill('7');await page.getByLabel('Called function',{exact:true}).evaluate(e=>e.scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'exported-call.png')});await click('Update draft');await click('Apply changes');
 await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.rule?.action==='edit')));
 const request=await page.evaluate(()=>window.maestroWorkspaceRequests.find(r=>r.commands.some(c=>c.rule?.action==='edit'))),program=JSON.parse(request.commands[0].rule.edits[0].sequence.program),original=JSON.parse(native.rules.selected.program);
 assert.deepEqual(program.imports,original.imports);assert.deepEqual(program.functions[0].body[0],{id:'first',op:'call',module:'second',function:'add',args:[{value:7}]});assert.equal(request.commands.some(c=>c.rule?.action==='play'||c.rule?.action==='signal'),false);
 assert.deepEqual(errors,[]);await writeFile(resolve(out,'browser.json'),JSON.stringify({request,pinsUnchanged:true,nativeRuleObservations:true,surroundingRoom:'fixture',acknowledgements:'simulated',browserNativeExecution:false,errors},null,2)+'\n');console.log('Book displays pinned module internals/native state and edits an exported call without changing snapshots.');
}finally{await browser.close();}
