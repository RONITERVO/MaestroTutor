// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {join} from 'node:path';
import type {Page} from 'playwright-core';
import {capabilityDefinition,capabilityFeatures} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import {nativeBookObjects} from './native-book-objects';
import {probeMotionMaintenance} from './probe-motion-maintenance';

/** Generated visible action fields and exact current-value loading carry the
 * maintenance flow. Only the deliberately stale draft uses the visible source
 * editor. Facts and receipts come from the actual native browser transport. */
export async function probeBookMotionMaintenance(page:Page,directory:string){
 const book=nativeBookObjects(page);
 const prepare=async(id:string,args:Record<string,unknown>,stale=false)=>{
  const definition=capabilityDefinition(id);assert.ok(definition,'Export the current native capability catalog before the book journey');
  await book.workshop();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
  await page.getByLabel('Search actions',{exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:definition.label+' '+id+' · v'+definition.version,exact:true}).click();
  if(stale){
   await page.getByText('Advanced action arguments',{exact:true}).click();await page.getByLabel('Action arguments',{exact:true}).fill(JSON.stringify(args));await page.getByText('Advanced action arguments',{exact:true}).click();
  }else{
   if(definition.input.oneOf){const index=definition.input.oneOf.findIndex(branch=>branch.properties?.operation?.enum?.[0]===args.operation);assert.ok(index>=0);await page.getByLabel(definition.input.title??'Variant',{exact:true}).selectOption(String(index));}
   if(args.motionId)await page.getByLabel('Action inputs motionId',{exact:true}).fill(String(args.motionId));
   if(args.operation==='save'){
    await page.getByLabel('Action inputs name',{exact:true}).fill(String(args.name));await page.getByLabel('Action inputs favourite',{exact:true}).selectOption(String(args.favourite));
    await page.getByText(/^Action inputs tags · \d+ entries$/).click();
    const removals=page.getByRole('button',{name:/^Remove Action inputs tags \d+$/}),count=await removals.count();assert.ok(count<=16);for(let index=0;index<count;index++)await removals.last().click();
    const tags=args.tags as string[];assert.ok(tags.length<=16);
    for(let index=0;index<tags.length;index++){await page.getByRole('button',{name:'Add Action inputs tags entry',exact:true}).click();await page.getByLabel('Action inputs tags '+(index+1),{exact:true}).fill(tags[index]);}
   }
   assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
   await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
   await page.getByText('Current room references',{exact:true}).click();const guard=args.entryRevision?'entryRevision':'manifestHash',field=page.getByLabel('Action inputs '+guard,{exact:true});
   assert.equal(await field.inputValue(),String(args[guard]));assert.equal(await field.getAttribute('readonly'),'');await page.getByText('Current room references',{exact:true}).click();
  }
  const supported=await page.evaluate(()=>window.nativeBookEvidence!().state!.capabilities??[]);assert.deepEqual(capabilityFeatures(id,args).filter(feature=>!supported.includes(feature)),[]);
 };
 const submit=async(id:string)=>{
  const previous=await page.evaluate(()=>{const state=window.nativeBookEvidence!().state!;return {ack:state.ack,id:state.execution?.selected?.id};});
  await page.getByRole('button',{name:'Run action now',exact:true}).click();
  const confirm=page.getByRole('button',{name:'Confirm permanent action',exact:true});if(await confirm.isVisible())await confirm.click();
  await page.waitForFunction(({id,previous})=>{const state=window.nativeBookEvidence!().state!,receipt=state.execution?.selected;return state.ack!==previous.ack&&!state.ok||receipt?.id!==previous.id&&receipt?.capability===id&&['completed','failed','cancelled'].includes(receipt.phase);},{id,previous},{timeout:120000});
  return page.evaluate(()=>window.nativeBookEvidence!().state!);
 };
 await probeMotionMaintenance({
  fact:async(id,args={})=>{const definition=behaviourFact(id);assert.ok(definition);return book.fact(id,definition.label,Object.fromEntries(Object.entries(args).map(([key,value])=>[key,String(value)])),true);},
  invoke:async(id,args)=>{await prepare(id,args);const state=await submit(id);assert.equal(state.ok,true,state.status);assert.equal(state.execution?.selected?.capability,id);assert.equal(state.execution?.selected?.phase,'completed',state.execution?.selected?.status);return state.execution!.selected!;},
  reject:async(id,args)=>{await prepare(id,args,true);return submit(id);},
  capture:async phase=>{if(['saved-and-stale-refused','download-removed','identity-forgotten','complete'].includes(phase))await page.screenshot({path:join(directory,'book-motion-maintenance-'+phase+'.png')});},
 },directory,'book');
 await book.workshop();
}
