// Human visual authoring in Chrome. Simulated acknowledgements; Unity executes the shared fixture separately.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/function-authoring');await mkdir(out,{recursive:true});
const expected=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-functions.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?visualBlocks');
 const click=async(name)=>page.getByRole('button',{name,exact:true}).click();
 const input=(label)=>page.getByLabel(label,{exact:true});
 const save=async()=>click('Update draft');
 const add=async(location,kind)=>{
  const summary=input('Add block in '+location);
  if(!await summary.evaluate(e=>e.parentElement.open))await summary.click();
  await click('+ '+kind+' in '+location);
 };
 await click('Functions & code');await click('+ Function');
 await input('Function name').fill('scaledDelay');await input('Function return type').selectOption('number');
 await click('Add parameter');await input('Parameter 1 name').fill('seconds');
 await click('Add parameter');await input('Parameter 2 name').fill('factor');
 await input('Function name').evaluate(e=>e.scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'function-definition.png')});await save();
 await click('Edit values return_1');await input('Return value source').selectOption('op:mul');
 await input('Return value left source').selectOption('var:seconds');await input('Return value right source').selectOption('var:factor');await save();
 await click('Edit function main');await click('Add local variable');await input('Variable 1 name').fill('delay');await save();
 for(const [id,seconds] of [[1,'.25'],[3,'1']]){
  await add('main','Call function');await click('Edit values block_'+id);
  await input('Argument seconds value').fill(seconds);await input('Argument factor value').fill('2');await input('Function result').selectOption('delay');await save();
  await add('main','Action');await click('Edit values block_'+(id+1));
  await input('seconds input mode').selectOption('expression');await input('seconds source').selectOption('var:delay');await save();
 }
 await click('Edit function scaledDelay');await input('Function name').fill('computeDelay');
 await input('Parameter 2 name').fill('scale');await click('Move parameter 2 up');await save();
 assert.equal(await input('Program JSON').count(),0);
 await page.getByText('Pause · delay seconds',{exact:true}).first().waitFor();
 await page.getByText('Define main()',{exact:true}).evaluate(e=>e.scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'reused-function.png')});
 await click('Apply changes');
 await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.rule?.action==='edit')));
 const request=await page.evaluate(()=>window.maestroWorkspaceRequests.find(r=>r.commands.some(c=>c.rule?.action==='edit')));
 assert.deepEqual(JSON.parse(request.commands[0].rule.edits[0].sequence.program),expected);
 assert.equal(request.commands.some(c=>c.action==='execution'||c.rule?.action==='play'),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({request,acknowledgement:'simulated',execution:'native shared-fixture test separately',errors},null,2)+'\n');
 console.log('Visual typed function authoring matches the Unity fixture; Apply starts no run.');
}finally{await browser.close();}
