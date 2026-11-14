// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {join} from 'node:path';
import type {Page} from 'playwright-core';
import {capabilityDefinition,capabilityFeatures} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import {nativeBookObjects} from './native-book-objects';
import {probeModelMaintenance} from './probe-model-maintenance';

/** Visible generated inputs and permanent-action confirmation drive native
 * actions. Only the Android picker is replaced by an explicit editor fixture. */
export async function probeBookModelMaintenance(page:Page,directory:string){
 const book=nativeBookObjects(page);
 const prepare=async(id:string,args:Record<string,unknown>,stale=false)=>{
  const definition=capabilityDefinition(id);assert.ok(definition);
  await book.workshop();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
  await page.getByLabel('Search actions',{exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:definition.label+' '+id+' · v'+definition.version,exact:true}).click();
  if(stale){await page.getByText('Advanced action arguments',{exact:true}).click();await page.getByLabel('Action arguments',{exact:true}).fill(JSON.stringify(args));await page.getByText('Advanced action arguments',{exact:true}).click();}
  else{
   if(definition.input.oneOf){const index=definition.input.oneOf.findIndex(branch=>branch.properties?.operation?.enum?.[0]===args.operation);assert.ok(index>=0);await page.getByLabel(definition.input.title??'Variant',{exact:true}).selectOption(String(index));}
   for(const [name,value] of Object.entries(args))if(name!=='operation'&&name!=='inspectionRevision')await page.getByLabel('Action inputs '+name,{exact:true}).fill(String(value));
   if(id==='model.download.edit'){
    assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
    await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
    await page.getByText('Current room references',{exact:true}).click();const field=page.getByLabel('Action inputs inspectionRevision',{exact:true});assert.equal(await field.inputValue(),args.inspectionRevision);assert.equal(await field.getAttribute('readonly'),'');await page.getByText('Current room references',{exact:true}).click();
   }
  }
  const supported=await page.evaluate(()=>window.nativeBookEvidence!().state!.capabilities??[]);assert.deepEqual(capabilityFeatures(id,args).filter(feature=>!supported.includes(feature)),[]);
 };
 const submit=async(id:string)=>{
  const previous=await page.evaluate(()=>{const state=window.nativeBookEvidence!().state!;return {ack:state.ack,id:state.execution?.selected?.id};});
  await page.getByRole('button',{name:'Run action now',exact:true}).click();
  if(id==='model.download.edit'){const confirm=page.getByRole('button',{name:'Confirm permanent action',exact:true});await confirm.waitFor();await confirm.click();}
  await page.waitForFunction(({id,previous})=>{const state=window.nativeBookEvidence!().state!,receipt=state.execution?.selected;return state.ack!==previous.ack&&!state.ok||receipt?.id!==previous.id&&receipt?.capability===id&&['completed','failed','cancelled'].includes(receipt.phase);},{id,previous},{timeout:120000});
  return page.evaluate(()=>window.nativeBookEvidence!().state!);
 };
 await probeModelMaintenance({
  fact:async(id,args,version=1)=>{const definition=behaviourFact(id);assert.ok(definition);assert.equal(definition.version,version);return book.fact(id,definition.label,Object.fromEntries(Object.entries(args).map(([key,value])=>[key,String(value)])),true);},
  invoke:async(id,args)=>{await prepare(id,args);const state=await submit(id);assert.equal(state.ok,true,state.status);assert.equal(state.execution?.selected?.capability,id);return state.execution!.selected!;},
  reject:async(id,args)=>{await prepare(id,args,true);return submit(id);},
  history:async receipt=>{
   const region=page.getByRole('region',{name:'One-off actions',exact:true});await region.getByText('Recent action results',{exact:true}).click();
   await region.getByRole('button').filter({hasText:receipt.id.slice(0,6)}).click();await page.waitForFunction(id=>window.nativeBookEvidence!().state!.execution?.selected?.id===id,receipt.id);
   await region.getByLabel('Action result',{exact:true}).waitFor();await page.screenshot({path:join(directory,'book-model-maintenance-historical-revisit.png')});
   return page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
  },
  capture:async phase=>{if(['unused-reviewed','download-removed','stale-refused','exact-reimport','saved-object-protected','included-avatar-protected','complete'].includes(phase))await page.screenshot({path:join(directory,'book-model-maintenance-'+phase+'.png')});},
 },directory,'book');
 await book.workshop();
}
