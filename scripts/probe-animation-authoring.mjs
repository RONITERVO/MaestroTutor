// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Typed book controls against recorded native evidence; no provider/device actions.
import {chromium} from 'playwright-core';
import {mkdir,readFile,writeFile} from 'node:fs/promises';
import {resolve} from 'node:path';
import assert from 'node:assert/strict';
const base=process.env.MAESTRO_HANDOFF_FIXTURE_URL||'http://127.0.0.1:5187';
if(!['localhost','127.0.0.1'].includes(new URL(base).hostname))throw new Error('Local fixture required');
const out=resolve('.quest-evidence/animation-authoring');await mkdir(out,{recursive:true});
const native=JSON.parse(await readFile('test-fixtures/browser/animationAuthoring.json','utf8')),expected=native.execution.selected;
const browser=await chromium.launch({channel:'chrome',headless:true});
try {
 const context=await browser.newContext({viewport:{width:1536,height:1024}});
 await context.route('**/*',route=>['localhost','127.0.0.1'].includes(new URL(route.request().url()).hostname)?route.continue():route.abort());
 const page=await context.newPage(),errors=[];page.on('pageerror',e=>errors.push(String(e)));
 await page.goto(base+'/test-fixtures/browser/quest-workspace.html?animationAuthoring',{waitUntil:'domcontentloaded',timeout:60000});
 await page.getByRole('button',{name:'Action catalog',exact:true}).click();
 await page.getByRole('textbox',{name:'Search actions',exact:true}).fill('animation.author');
 await page.getByRole('button',{name:'Search',exact:true}).click();
 await page.getByRole('button',{name:/Edit pose or recorded motion/}).click();
 await page.getByText('Edit action fields',{exact:true}).click();
 await page.getByLabel('Animation edit',{exact:true}).selectOption('0');
 const args=expected.call.arguments;
 await page.getByLabel('Action inputs revision',{exact:true}).fill(String(args.revision));
 await page.getByLabel('Action inputs replace',{exact:true}).selectOption('true');
 await page.getByText('Action inputs frames · 1 entries',{exact:true}).click();
 for(let index=0;index<args.frames.length;index++){
  if(index)await page.getByRole('button',{name:'Add Action inputs frames entry',exact:true}).click();
  const frame=args.frames[index],label='Action inputs frames '+(index+1);
  await page.getByLabel(label+' time',{exact:true}).fill(String(frame.time));
  await page.getByLabel(label+' scale',{exact:true}).fill(String(frame.scale));
  for(const key of ['position','rotation'])for(const [axis,value] of Object.entries(frame[key]))await page.getByLabel(label+' '+key+' '+axis,{exact:true}).fill(String(value));
  await page.getByText(label+' joints · 0 entries',{exact:true}).click();
  for(let j=0;j<frame.joints.length;j++){
   const joint=frame.joints[j],jointLabel=label+' joints '+(j+1);
   await page.getByRole('button',{name:'Add '+label+' joints entry',exact:true}).click();
   await page.getByLabel(jointLabel+' joint',{exact:true}).selectOption(joint.joint);
   for(const [axis,value] of Object.entries(joint.rotation))await page.getByLabel(jointLabel+' rotation '+axis,{exact:true}).fill(String(value));
  }
  await page.getByText(label+' joints · '+frame.joints.length+' entries',{exact:true}).click();
 }
 assert.deepEqual(JSON.parse(await page.getByLabel('Action arguments',{exact:true}).inputValue()),args);
 await page.getByText('Action inputs frames · 3 entries',{exact:true}).click();
 await page.getByRole('button',{name:'Run action now',exact:true}).click();
 await page.getByLabel('Action result',{exact:true}).filter({hasText:'"frames": 3'}).waitFor();
 const commands=await page.evaluate(()=>window.maestroWorkspaceRequests.flatMap(r=>r.commands).filter(c=>c.action==='execution'));
 assert.deepEqual(commands,[{action:'execution',execution:{operation:'start',call:expected.call,runId:expected.id}}]);
 await page.getByLabel('Action result',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:resolve(out,'book.png')});
 assert.deepEqual(errors,[]);await writeFile(resolve(out,'browser.json'),JSON.stringify({typedFields:true,joints:17,frames:3,commands,acknowledgement:'recorded native output; browser simulation only',errors},null,2)+'\n');
 console.log('Typed book fields produced the exact native 17-joint, three-frame animation edit. No playback request was sent.');
}finally{await browser.close();}
