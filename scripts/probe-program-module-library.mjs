// Real native library observations; browser commands are simulated acknowledgements, not native execution.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5184';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/module-library');await mkdir(out,{recursive:true});const evidence={};
for(const name of ['published','search','inspected','running','removed'])evidence[name]=JSON.parse(await readFile(resolve(out,'native',name+'.json'),'utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});await context.route('**/*',r=>['localhost','127.0.0.1'].includes(new URL(r.request().url()).hostname)?r.continue():r.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));await page.goto(base+'/test-fixtures/browser/quest-workspace.html?moduleLibrary');
 await page.evaluate(e=>window.maestroModuleLibraryEvidence(e),evidence);const click=name=>page.getByRole('button',{name,exact:true}).click();
 await click('Reusable modules');await page.getByLabel('Export main',{exact:true}).uncheck();await page.getByLabel('Export remember',{exact:true}).check();
 await click('Publish module');await page.getByLabel('Library action result').waitFor();assert((await page.getByLabel('Library action result').textContent()).includes(evidence.inspected.catalog.capability));
 await page.getByLabel('Find modules',{exact:true}).fill('remember');await click('Search modules');await page.getByRole('button',{name:/Remember amounts [a-f0-9]{12}/}).click();
 await page.getByLabel('Inspected library module').waitFor();await page.getByLabel('Import name',{exact:true}).fill('counter');
 await page.getByLabel('Connect user.add',{exact:true}).fill('user.request');await page.getByLabel('Connect user.stored',{exact:true}).fill('user.total');
 await page.getByLabel('Import name',{exact:true}).evaluate(e=>e.scrollIntoView({block:'center'}));await page.screenshot({path:resolve(out,'library-import.png')});
 await click('Add pinned import to draft');await page.getByLabel('Pinned module counter').locator('summary').click();await page.getByLabel('Pinned module counter').evaluate(e=>e.scrollIntoView({block:'start'}));await page.screenshot({path:resolve(out,'library-pinned-draft.png')});
 await click('Apply changes');await page.waitForFunction(()=>window.maestroWorkspaceRequests.some(r=>r.commands.some(c=>c.rule?.action==='edit')));
 const requests=await page.evaluate(()=>window.maestroWorkspaceRequests),publish=requests.find(r=>r.commands[0]?.execution?.call?.id==='program.module.publish');
 assert.deepEqual(publish.commands[0].execution.call.arguments,{sequenceId:evidence.published.rules.selected.id,rulesRevision:evidence.published.rules.revision,name:'Remember amounts',exports:['remember']});
 const saved=requests.find(r=>r.commands[0]?.rule?.action==='edit').commands[0].rule.edits[0].sequence;
 const program=JSON.parse(saved.program);assert.deepEqual(program.imports[0],{alias:'counter',hash:evidence.inspected.catalog.capability,module:evidence.inspected.catalog.definition,signals:{'user.add':'user.request','user.stored':'user.total'}});
 assert.equal(requests.some(r=>r.commands.some(c=>c.rule?.action==='play')),false);assert.deepEqual(errors,[]);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({requests,nativeCatalogObservations:true,acknowledgements:'simulated',browserNativeExecution:false,errors},null,2)+'\n');console.log('Library publication, native pin inspection and explicit draft import verified in book UI.');
}finally{await browser.close();}
