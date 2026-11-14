// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {join} from 'node:path';
import type {Page} from 'playwright-core';
import {capabilityDefinition} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import {nativeBookObjects} from './native-book-objects';
import {probeAssetInventory} from './probe-asset-inventory';
export async function probeBookAssetInventory(page:Page,directory:string){
 const book=nativeBookObjects(page);
 await probeAssetInventory({
  invoke:async(id,args)=>{
   const definition=capabilityDefinition(id);assert.ok(definition);
   await book.workshop();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
   await page.getByLabel('Search actions',{exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
   await page.getByRole('button',{name:definition.label+' '+id+' · v'+definition.version,exact:true}).click();
   if(definition.input.oneOf){const index=definition.input.oneOf.findIndex(branch=>branch.properties?.operation?.enum?.[0]===args.operation);assert.ok(index>=0);await page.getByLabel(definition.input.title??'Variant',{exact:true}).selectOption(String(index));}
   if(args.offset!==undefined)await page.getByLabel('Action inputs offset',{exact:true}).fill(String(args.offset));
   const previous=await page.evaluate(()=>window.nativeBookEvidence!().state!.execution?.selected?.id);
   await page.getByRole('button',{name:'Run action now',exact:true}).click();
   await page.waitForFunction(({id,previous})=>{const receipt=window.nativeBookEvidence!().state!.execution?.selected;return receipt?.id!==previous&&receipt?.capability===id&&['completed','failed','cancelled'].includes(receipt.phase);},{id,previous},{timeout:45000});
   return page.evaluate(()=>window.nativeBookEvidence!().state!.execution!.selected!);
  },
  fact:async(id,args)=>{const definition=behaviourFact(id);assert.ok(definition);return book.fact(id,definition.label,Object.fromEntries(Object.entries(args).map(([k,v])=>[k,String(v)])),true);},
  capture:async(kind,phase)=>{
   const notice=page.getByRole('region',{name:'Library inventory status',exact:true});await notice.waitFor();
   await notice.getByText(phase==='partial'?'Library inventory incomplete':'Inventory scan complete',{exact:true}).waitFor();
   await notice.scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-asset-inventory-'+kind+'-'+phase+'.png')});
  },
 },directory,'book');
 await book.workshop();
}
