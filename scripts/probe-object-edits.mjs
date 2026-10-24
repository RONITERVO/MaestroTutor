// Local human authoring probe. Native fixture; simulated acknowledgements; no execution.
import {chromium} from 'playwright-core';
import {mkdir,writeFile,readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/object-edits');await mkdir(out,{recursive:true});
const original=JSON.parse(await readFile('test-fixtures/browser/objectEditProgram.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try{
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?objectEdits');
 await page.getByRole('button',{name:'Edit block paint',exact:true}).click();
 const editor=page.getByLabel('Program JSON'),node=JSON.parse(await editor.inputValue());
 node.arguments.green=.7;await editor.fill(JSON.stringify(node,null,2));
 await page.getByRole('button',{name:'Update draft',exact:true}).click();
 await page.getByText('Variables and results',{exact:true}).nth(1).click();
 assert.equal(await page.getByLabel('paint argument target variable').inputValue(),'ball');
 await page.getByLabel('paint argument target variable').scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'object-edit-program.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit')));
 const request=await page.evaluate(()=>window.maestroWorkspaceRequests.find(r=>r.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit')));
 const program=JSON.parse(request.commands[0].rule.edits[0].sequence.program);
 const expected=structuredClone(original);expected.functions[0].body[1].arguments.green=.7;assert.deepEqual(program,expected);
 assert.equal(request.commands.some(c=>c.action==='execution'),false);
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser-object-edits.json'),JSON.stringify({request,acknowledgement:'simulated',execution:'not attempted',errors},null,2)+'\n');
 console.log('Chrome object-edit program, exact recipe/result bindings and saved call verified; no action started.');
}finally{await browser.close();}
