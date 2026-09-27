// Local human authoring; simulated native acknowledgements, no provider/device execution.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/visual-blocks');await mkdir(out,{recursive:true});
const expected=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-visual.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?visualBlocks');
 await page.getByRole('button',{name:'Functions & code',exact:true}).click();
 const add=async(location,kind)=>{
  const summary=page.getByLabel('Add block in '+location,{exact:true});
  await summary.click();
  await page.getByRole('button',{name:'+ '+kind+' in '+location,exact:true}).click();
 };
 const edit=async(id)=>page.getByRole('button',{name:'Edit values '+id,exact:true}).click();
 const save=async()=>page.getByRole('button',{name:'Update draft',exact:true}).click();
 await add('main','If');await edit('block_1');
 await page.getByLabel('Condition source',{exact:true}).selectOption('op:eq');
 await page.getByLabel('Condition compared type',{exact:true}).selectOption('text');
 await page.getByLabel('Condition left source',{exact:true}).selectOption('fact:maestro.state');
 await page.getByLabel('Condition right value',{exact:true}).fill('speaking');await save();
 await add('block_1 Then','Repeat');await add('block_2 Repeat these','Action');
 await edit('block_3');await page.getByLabel('Block action',{exact:true}).selectOption('avatar.gesture.play');
 await page.getByLabel('gesture',{exact:true}).selectOption('greeting');
 await page.getByLabel('seconds',{exact:true}).fill('0.2');
 await page.screenshot({path:resolve(out,'action-values.png')});await save();
 await add('block_1 Otherwise','Action');
 assert.equal(await page.getByLabel('Program JSON',{exact:true}).count(),0);
 await page.getByText('If (maestro.state = "speaking")',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'nested-blocks.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit')));
 const request=await page.evaluate(()=>window.maestroWorkspaceRequests.find(r=>r.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit')));
 assert.deepEqual(JSON.parse(request.commands[0].rule.edits[0].sequence.program),expected);
 assert.equal(request.commands.some(c=>c.action==='execution'||c.rule?.action==='play'),false);
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({request,acknowledgement:'simulated',execution:'not attempted',errors},null,2)+'\\n');
 console.log('Visual nested authoring exactly matches the Unity fixture; no action started.');
}finally{await browser.close();}
