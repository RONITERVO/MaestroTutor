// Local book confirmation only; recorded native results, no files/devices/providers changed.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/workspace-removal');await mkdir(out,{recursive:true});
const flow=JSON.parse(await readFile('test-fixtures/browser/workspaceRemoval.json','utf8'));
const expected=flow.removeExecution.workspace.selected;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?disposal');
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('workspace.retention.remove');
 await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Permanently discard retained workspace/}).click();
 await page.getByLabel('Action arguments',{exact:true}).fill(JSON.stringify(expected.call.arguments,null,2));
 const starts=()=>page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'&&c.execution?.operation==='start'));
 await page.getByRole('button',{name:'Run action now',exact:true}).click();assert.equal((await starts()).length,0);
 const confirmation=page.getByRole('region',{name:'Confirm permanent action',exact:true});await confirmation.scrollIntoViewIfNeeded();
 assert.match(await confirmation.innerText(),/No Undo/);assert.match(await confirmation.innerText(),new RegExp(expected.call.arguments.generationId));
 await page.screenshot({path:resolve(out,'confirmation.png')});
 await page.getByRole('button',{name:'Cancel confirmation',exact:true}).click();assert.equal((await starts()).length,0);
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.getByRole('button',{name:'Confirm permanent action',exact:true}).click();
 await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.action==='execution'&&c.execution?.operation==='start')));
 const commands=await starts();assert.deepEqual(commands,[{action:'execution',execution:{operation:'start',call:expected.call,runId:expected.id}}]);
 await page.getByLabel('Action result',{exact:true}).filter({hasText:'"removed": true'}).waitFor();
 assert.deepEqual(errors,[]);await writeFile(resolve(out,'browser.json'),JSON.stringify({confirmationRequired:true,cancelledWithoutRequest:true,commands,acknowledgement:'recorded native output; browser simulation only',errors},null,2)+'\n');
 console.log('Book confirmation and cancellation verified in Chrome; only the exact confirmed simulated call was sent.');
}finally{await browser.close();}
