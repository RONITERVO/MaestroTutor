// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Real browser editing, simulated save acknowledgement. The exact saved fixture is
// independently executed by probe-native-room.ts and native PlayMode tests.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5190';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/composition');await mkdir(out,{recursive:true});
const fixture=JSON.parse(await readFile('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-build-structure.json','utf8'));
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?composition');
 await page.getByRole('button',{name:'Edit values capture',exact:true}).click();
 await page.getByLabel('source.members input mode',{exact:true}).selectOption('literal');
 await page.getByLabel('source.members input mode',{exact:true}).selectOption('expression');
 await page.getByLabel('source.members source',{exact:true}).selectOption('var:members');
 await page.getByLabel('source.name',{exact:true}).fill('Castle');
 await page.getByLabel('source.members source',{exact:true}).scrollIntoViewIfNeeded();
 await page.screenshot({path:resolve(out,'book-structured-input.png')});
 await page.getByRole('button',{name:'Update draft',exact:true}).click();
 await page.getByRole('button',{name:'Apply changes',exact:true}).click();
 await page.waitForFunction(()=>window.maestroBook?.roomSnapshot().request?.commands.some(c=>c.action==='rules'&&c.rule?.action==='edit'));
 const request=await page.evaluate(()=>window.maestroBook.roomSnapshot().request);
 const program=JSON.parse(request.commands[0].rule.edits.find(e=>e.kind==='save').sequence.program);
 assert.deepEqual(program,fixture);assert.deepEqual(program.resources,[]);assert.deepEqual(errors,[]);
 assert.equal(request.commands.some(c=>c.action==='execution'||c.action==='rules'&&c.rule.action==='play'),false);
 await writeFile(resolve(out,'browser.json'),JSON.stringify({boundary:'Real Chrome authoring; simulated acknowledgement. Exact fixture independently executed in native probe.',exactSharedProgramMatched:true,saveDidNotStartActions:true,request,errors},null,2)+'\n');
 console.log('Book authoring saved the exact shared build/capture/move/reset program without starting it.');
} finally {await browser.close();}
