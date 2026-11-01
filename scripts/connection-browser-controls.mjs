// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
// Exercise the same sparse, paged object picker used in the book.
export async function chooseConnectionObject(page,label,target){
 const previous=await page.evaluate(()=>window.maestroHingeRequests.length);
 await page.getByRole('button',{name:'Find '+label,exact:true}).click();
 await page.waitForFunction(count=>window.maestroHingeRequests.length>count&&!window.maestroHingeSnapshot().request&&window.maestroHingeState().catalog?.capability==='world.objects'&&window.maestroHingeState().catalog.arguments.cursor==='',previous);
 for(let i=0;i<24;i++){
  await page.getByRole('button',{name:'Search objects',exact:true}).waitFor();
  await page.waitForFunction(()=>!window.maestroHingeSnapshot().request&&window.maestroHingeState().catalog?.capability==='world.objects');
  const entries=await page.evaluate(()=>window.maestroHingeState().catalog.value.entries),index=entries.findIndex(entry=>entry.id===target);
  if(index>=0){await page.getByLabel('Objects',{exact:true}).getByRole('button').nth(index).click();return;}
  const next=page.getByRole('button',{name:'Next objects',exact:true});assert.equal(await next.isEnabled(),true,'Exact connection member not found');
  const cursor=await page.evaluate(()=>window.maestroHingeState().catalog.arguments.cursor);await next.click();
  await page.waitForFunction(previous=>window.maestroHingeState().catalog.arguments.cursor!==previous,cursor);
 }
 throw new Error('Connection directory exceeded its bounded page count');
}
export async function confirmConnectionCompletion(page,native){
 assert.ok(['preparing','completed'].includes(native.started.execution.selected.phase));assert.equal(native.after.execution.selected.phase,'completed');
 assert.equal(await page.getByLabel('Selected action',{exact:true}).locator('strong').innerText(),native.started.execution.selected.phase);
 await page.waitForFunction(()=>window.maestroHingeState().execution.selected.phase==='completed');
 assert.equal(await page.getByLabel('Selected action',{exact:true}).locator('strong').innerText(),'completed');
 const selected=await page.evaluate(()=>window.maestroHingeState().execution.selected);
 assert.deepEqual(selected,native.after.execution.selected);
 if(selected.output)assert.deepEqual(JSON.parse(await page.getByLabel('Action result',{exact:true}).innerText()),selected.output);
 else assert.equal(await page.getByLabel('Action result',{exact:true}).count(),0);
}
