// Book authoring with simulated acknowledgements, then replay of actual native program values.
// The surrounding browser room is a fixture; Chrome executes no Unity effects.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/structured-values');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile(resolve(out,'native/collections.json'),'utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?visualBlocks&structured');
 const click=name=>page.getByRole('button',{name,exact:true}).click();const input=label=>page.getByLabel(label,{exact:true});
 await click('Functions & code');await click('Edit function main');await click('Add local variable');await input('Variable 1 name').fill('items');
 await input('Variable 1 type').selectOption('list');await input('Variable 1 type item type').selectOption('record');
 for(const field of ['red','id']){await input('Variable 1 type item type new field name').fill(field);await click('Add Variable 1 type item type field');}
 await input('Variable 1 type item type id type').selectOption('text');await click('Add Initial value 1 item');
 await input('Initial value 1 item 1 red').fill('.2');await input('Initial value 1 item 1 id').fill('book');
 await input('Variable 1 type').evaluate(e=>e.scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'typed-record-list.png')});await click('Update draft');
 await input('Add block in main').click();await click('+ Set variable in main');await click('Edit values block_1');
 const append=await input('Assigned value source').locator('option').filter({hasText:'append in items'}).getAttribute('value');await input('Assigned value source').selectOption(append);
 await input('Assigned value item value red').fill('.7');await input('Assigned value item value id').fill('maestro');await click('Update draft');
 assert.equal(await input('Program JSON').count(),0);await click('Apply changes');
 await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.rule?.action==='edit')));
 const request=await page.evaluate(()=>window.maestroWorkspaceRequests.find(r=>r.commands.some(c=>c.rule?.action==='edit')));
 const program=JSON.parse(request.commands[0].rule.edits[0].sequence.program);assert.equal(program.dataVersion,1);assert.deepEqual(program.functions[0].locals[0].initial,[{red:.2,id:'book'}]);assert.equal(program.functions[0].body[0].value.op,'append');
 await page.evaluate(rules=>window.maestroWorkspaceRulesEvidence(rules),native.rules);
 const live=page.locator('[aria-label="Live program values"]');await live.waitFor();
 const text=await live.textContent();const item=JSON.parse(native.rules.running[0].state.find(v=>v.name==='items').value)[0];assert(text.includes(item.id));assert(text.includes('0.9'));assert(text.includes('0.2'));
 await live.evaluate(e=>e.scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'native-collection-values.png')});
 await click('Edit values paintItem');const source=await input('red source').inputValue();assert.equal(source,'op:field');
 await page.screenshot({path:resolve(out,'record-field-binding.png')});await click('Discard editor draft');
 assert.deepEqual(errors,[]);assert.equal(request.commands.some(c=>c.action==='execution'||c.rule?.action==='play'),false);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({program,request,simulatedAcknowledgements:true,nativeProgramValues:true,surroundingRoom:'browser fixture',browserNativeExecution:false,errors},null,2)+'\n');
 console.log('Book authored typed records/lists and displayed native collection state and field bindings.');
}finally{await browser.close();}
