// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import type {Locator,Page} from 'playwright-core';
import {readRoomObjectObservation} from '../src/core-sdk/room/roomObjectContext';
import {objectSearchRequest,readObjectSearchPage} from '../shared/objectSearch';
import {validCatalogView,type CatalogView} from '../shared/roomCatalog';
import {readNativeObjects} from './native-probe-read-set';
import {behaviourFact} from '../shared/behaviourCatalog';
import {validFactValue} from '../shared/behaviourFacts';

/** Ordinary visible book controls only; no hidden native requests or scene hydration. */
export function nativeBookObjects(page:Page){
 const workshop=async()=>{
  const back=page.getByRole('button',{name:'Back to workshop',exact:true}).filter({visible:true});
  if(await back.count())await back.click();
  else if(!await page.getByRole('button',{name:'Action catalog',exact:true}).isVisible())await page.evaluate(()=>window.maestroBook!.command({version:1,type:'workspace.open'}));
  await page.getByRole('button',{name:'Action catalog',exact:true}).waitFor();
 };
 const pick=async(scope:Locator,target:string)=>{
  let cursor='',world='',total=-1,offset=0,last='';
  for(let pages=0;pages<256;pages++){
   await page.waitForFunction(({target,cursor})=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability==='world.objects'&&c.arguments?.query===target&&c.arguments?.cursor===cursor&&c.available===true;},{target,cursor});
   const catalog=await page.evaluate(()=>window.nativeBookEvidence!().state!.catalog),result=readObjectSearchPage(catalog,objectSearchRequest(target,'all',cursor));assert.ok(result);
   if(!pages){world=result.worldId;total=result.total;}
   assert.equal(result.worldId,world);assert.equal(result.total,total);assert.equal(result.offset,offset);assert.ok(result.entries.every(e=>e.id>last));
   const status=total?`${result.offset+1}–${result.offset+result.entries.length} of ${total}`:'No matching objects';await scope.getByText(status,{exact:true}).waitFor();
   const index=result.entries.findIndex(e=>e.id===target);
   if(index>=0){const buttons=scope.locator('.room-object-list').getByRole('button');assert.equal(await buttons.count(),result.entries.length);await buttons.nth(index).click();return;}
   assert.ok(result.next,'Exact object ID was not found in completed search results');offset+=result.entries.length;last=result.entries.at(-1)!.id;cursor=result.next;
   await scope.getByRole('button',{name:'Next objects',exact:true}).click();
  }
  throw new Error('Book picker exceeded its explicit page budget');
 };
 const choose=async(label:string,target:string)=>{
  const scope=page.getByRole('region',{name:label+' object choice',exact:true});await scope.waitFor();
  await scope.getByRole('button',{name:'Find '+label,exact:true}).click();
  await scope.getByLabel('Find '+label+' objects',{exact:true}).fill(target);await scope.getByRole('button',{name:'Search objects',exact:true}).click();
  await pick(scope,target);await scope.locator('code').filter({hasText:target}).waitFor({state:'attached'});
 };
 const fact=async(id:string,label:string,args:Record<string,string>={},allowUnavailable=false)=>{
  await workshop();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
  await page.getByLabel('Catalog category',{exact:true}).selectOption('facts');await page.getByLabel('Search facts',{exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:new RegExp(label+'.*'+id.replaceAll('.','\\.'))}).click();await page.getByRole('heading',{name:label,exact:true}).waitFor();
  for(const [name,value] of Object.entries(args)){
   const fieldLabel='Fact inputs '+name,choice=page.getByRole('region',{name:fieldLabel+' object choice',exact:true}),field=page.getByLabel(fieldLabel,{exact:true});
   await choice.or(field).first().waitFor();
   if(await choice.isVisible())await choose(fieldLabel,value);
   else {if(await field.evaluate(e=>e.tagName==='SELECT'))await field.selectOption(value);else await field.fill(value);}
  }
  await page.getByLabel('Current fact value',{exact:true}).waitFor();
  const submit=page.getByRole('button',{name:'Read fact',exact:true});if(await submit.count())await submit.click();
  await page.waitForFunction(({id,args,allowUnavailable})=>{const c=window.nativeBookEvidence!().state!.catalog;return c?.operation==='inspect'&&c.category==='facts'&&c.capability===id&&(c.available===true||allowUnavailable&&c.available===false)&&Object.entries(args).every(([k,v])=>String(c.arguments?.[k])===v);},{id,args,allowUnavailable});
  const view=await page.evaluate(()=>window.nativeBookEvidence!().state!.catalog);assert.ok(validCatalogView(view)&&view.operation==='inspect'&&view.category==='facts'&&view.capability===id&&(view.available?validFactValue(id,view.value):allowUnavailable));return view as Extract<CatalogView,{operation:'inspect';category:'facts'}>;
 };
 const observation=async(target:string)=>{const value=readRoomObjectObservation(await fact('object.observation','Observe one saved object',{target}),target);assert.ok(value&&value.objectRevision);return value;};
 const directory=async(cursor='',query='')=>{const view=await fact('world.objects','Find saved objects',{query,kind:'all',cursor}),value=readObjectSearchPage(view,objectSearchRequest(query,'all',cursor));assert.ok(value,'Saved directory unavailable or changed');return value;};
 const count=async()=>(await directory()).total;
 const ids=async(query='')=>{
  const ids:string[]=[];let cursor='',world='',total=-1;
  for(let pages=0;pages<256;pages++){
   const value=await directory(cursor,query);if(!pages){world=value.worldId;total=value.total;}
   assert.equal(value.worldId,world);assert.equal(value.total,total);assert.equal(value.offset,ids.length);assert.ok(value.entries.every(e=>!ids.length||e.id>ids.at(-1)!));
   ids.push(...value.entries.map(e=>e.id));cursor=value.next;if(!cursor){assert.equal(ids.length,total);return ids;}
  }
  throw new Error('Book probe directory exceeded its explicit page budget');
 };
 const exists=async(target:string)=>(await ids(target)).includes(target);
 const inspect=async(target:string)=>{
  await workshop();await page.getByLabel('Find saved objects',{exact:true}).fill(target);await page.getByRole('button',{name:'Search objects',exact:true}).click();
  await pick(page.getByRole('region',{name:'Room objects and parts',exact:true}).locator('.room-object-search'),target);
  await page.waitForFunction(target=>window.nativeBookEvidence!().state!.inspection?.id===target,target);
  return page.evaluate(()=>window.nativeBookEvidence!().state!.inspection!);
 };
 return {workshop,choose,fact,observation,exists,count,ids,inspect};
}

/** Exercise the same read-set validation using visible generated forms and the
 * normal object inspector. This adapter never sends a hidden native command. */
export async function readNativeBookObjects(page:Page){
 const book=nativeBookObjects(page);
 return readNativeObjects(async commands=>{
  assert.equal(commands.length,1);const command=commands[0];
  if(command.action==='inspect'){
   assert.ok(command.target);await book.inspect(command.target);
  }else{
   assert.equal(command.action,'catalog');const request=command.catalog;
   assert.ok(request?.operation==='inspect'&&request.category==='facts'&&request.version===1);
   assert.ok(['world.objects','object.observation','object.physics.settings'].includes(request.capability));
   const definition=behaviourFact(request.capability);assert.ok(definition);
   await book.fact(request.capability,definition.label,Object.fromEntries(Object.entries(request.arguments??{}).map(([key,value])=>[key,String(value)])));
  }
  return page.evaluate(()=>window.nativeBookEvidence!().state!);
 });
}
