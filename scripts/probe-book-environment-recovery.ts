// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {join} from 'node:path';
import type {Page} from 'playwright-core';
import {capabilityDefinition} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import {nativeBookObjects} from './native-book-objects';
import {probeEnvironmentRecovery} from './probe-environment-recovery';

/** Visible controls carry every mutation. Generated fields and current-value
 * loading cover repair/retry; the optional source editor submits stale snapshots
 * explicitly, exactly as an advanced user can. No hidden native scene writes. */
export async function probeBookEnvironmentRecovery(page:Page,directory:string){
 const book=nativeBookObjects(page);
 const open=async(id:string)=>{
  const definition=capabilityDefinition(id);assert.ok(definition);
  await book.workshop();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
  await page.getByLabel('Search actions',{exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:definition.label+' '+id+' · v1',exact:true}).click();
 };
 const prepare=async(id:string,args:Record<string,unknown>,stale=false)=>{
  await open(id);
  if(!stale&&(id==='object.environment.recheck'||id==='object.position.set')){
   await book.choose('Action inputs target',String(args.target));
   if(id==='object.position.set')for(const axis of ['x','y','z'])await page.getByLabel('Action inputs '+axis,{exact:true}).fill(String(args[axis]));
   else {
    assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
    await page.getByRole('button',{name:'Load current values',exact:true}).click();
    await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
    await page.getByText('Current room references',{exact:true}).click();
    for(const key of ['revision','stateId']){
     const field=page.getByLabel('Action inputs '+key,{exact:true});assert.equal(await field.inputValue(),String(args[key]));assert.equal(await field.getAttribute('readonly'),'');
    }
    await page.getByText('Current room references',{exact:true}).click();
   }
  }else {
   await page.getByText('Advanced action arguments',{exact:true}).click();
   await page.getByLabel('Action arguments',{exact:true}).fill(JSON.stringify(args));
   await page.getByText('Advanced action arguments',{exact:true}).click();
  }
 };
 const submit=async(id:string)=>{
  const previous=await page.evaluate(()=>{const s=window.nativeBookEvidence!().state!;return {ack:s.ack,id:s.execution?.selected?.id};});
  await page.getByRole('button',{name:'Run action now',exact:true}).click();
  await page.waitForFunction(({id,previous})=>{const s=window.nativeBookEvidence!().state!,r=s.execution?.selected;return s.ack!==previous.ack&&!s.ok||r?.id!==previous.id&&r?.capability===id&&['completed','failed','cancelled'].includes(r.phase);},{id,previous},{timeout:35000});
  return page.evaluate(()=>window.nativeBookEvidence!().state!);
 };
 await probeEnvironmentRecovery({
  fact:async(id,args={})=>{const definition=behaviourFact(id);assert.ok(definition);return book.fact(id,definition.label,Object.fromEntries(Object.entries(args).map(([key,value])=>[key,String(value)])),true);},
  count:book.count,
  invoke:async(id,args)=>{await prepare(id,args);const state=await submit(id);assert.equal(state.ok,true,state.status);assert.equal(state.execution?.selected?.capability,id);assert.equal(state.execution?.selected?.phase,'completed',state.execution?.selected?.status);return state.execution!.selected!;},
  reject:async(id,args)=>{await prepare(id,args,true);const state=await submit(id);await page.getByRole('status').filter({hasText:/changed|again/i}).waitFor();await page.screenshot({path:join(directory,'book-environment-recovery-stale-'+(args.revision)+'-'+String(args.stateId).slice(0,8)+'.png')});return state;},
  capture:async phase=>{if(phase==='blocked'||phase==='recovered'){
   await page.getByLabel('Current fact value',{exact:true}).scrollIntoViewIfNeeded();await page.screenshot({path:join(directory,'book-environment-recovery-'+phase+'.png')});
  }}
 },directory,'book');
 await book.workshop();
}
