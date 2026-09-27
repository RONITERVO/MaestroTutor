// Browser editing uses real native recipe fixtures and simulated edit acknowledgements.
// Unity PlayMode verifies actual creation and animation; this probe starts no action.
import {chromium} from 'playwright-core';
import {mkdir,writeFile,readFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/recipe-creation');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/recipeCreationProgram.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?recipeCreation');
 await page.getByText('Create object → robot (objectId)',{exact:true}).waitFor();
 await page.getByRole('button',{name:'Edit block create',exact:true}).click();
 const editor=page.getByLabel('Program JSON'),node=JSON.parse(await editor.inputValue());
 assert.deepEqual(node.arguments.recipe,native.functions[0].body[0].arguments.recipe);
 node.arguments.name='My study robot';node.arguments.scale=.4;
 await editor.fill(JSON.stringify(node,null,2));await page.getByRole('button',{name:'Update draft',exact:true}).click();
 await page.getByText('Variables and results',{exact:true}).nth(1).click();
 assert.equal(await page.getByLabel('animate argument target variable').inputValue(),'robot');
 await page.screenshot({path:resolve(out,'recipe-program.png')});
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 try {await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit')),null,{timeout:5000});}
 catch(error){console.log(JSON.stringify({statuses:await page.getByRole('status').allTextContents(),errors,snapshot:await page.evaluate(()=>window.maestroBook.roomSnapshot())}));await page.screenshot({path:resolve(out,'recipe-save-diagnostic.png')});throw error;}
 const request=await page.evaluate(()=>window.maestroWorkspaceRequests.find(r=>r.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit')));
 const program=JSON.parse(request.commands[0].rule.edits.find(e=>e.kind==='save').sequence.program);
 assert.equal(program.functions[0].body[0].arguments.name,'My study robot');
 assert.equal(program.functions[0].body[0].arguments.scale,.4);
 assert.deepEqual(program.functions[0].body[0].arguments.recipe,native.functions[0].body[0].arguments.recipe);
 assert.deepEqual(program.functions[0].body[0].results,{objectId:'robot'});
 assert.deepEqual(program.functions[0].body[1].bindings,{target:{var:'robot'}});
 assert.equal(request.commands.some(c=>c.action==='execution'),false);
 await page.getByRole('button',{name:'Apply changes',exact:true}).waitFor({state:'visible'});
 await page.waitForFunction(()=>!window.maestroBook.roomSnapshot().request);
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByLabel('Search actions').fill('create');await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Create object object.create/}).click();
 await page.getByLabel('Creation kind',{exact:true}).selectOption('1');
 const args=JSON.parse(await page.getByLabel('Action arguments').inputValue());
 assert.equal(args.recipe.parts.length,19);assert.equal(args.recipe.tracks.length,2);assert.equal(args.recipe.playing,false);
 assert.equal(await page.getByRole('button',{name:'Check availability',exact:true}).isEnabled(),true);
 await page.screenshot({path:resolve(out,'recipe-catalog.png')});
 assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser-recipe.json'),JSON.stringify({request,sourceRecipeUnchanged:true,nativeExampleValid:true,acknowledgement:'simulated',execution:'not attempted',errors},null,2)+'\n');
 console.log('Chrome recipe editing, result binding and native catalog example verified; no action started.');
} finally {await browser.close();}
