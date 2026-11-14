// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {writeFile} from 'node:fs/promises';
import {join} from 'node:path';
import {awaitRoomPersistence} from './probe-room-persistence';
import type {Page} from 'playwright-core';
import {capabilityDefinition,capabilityFeatures} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import {nativeBookObjects} from './native-book-objects';
import {assertSupportSavedSource,staticSupportFixture,type SupportReceipt} from './probe-static-support';
import {supportSavedReader} from './probe-native-static-support';

/** Ordinary visible support controls. The advanced editor supplies the compound
 * fixture once; enable/disable use generated fields and current-value guards. */
export async function probeBookStaticSupport(page:Page,directory:string){
 const book=nativeBookObjects(page),saved=await supportSavedReader(directory),before=await saved();
 const evidence:Record<string,unknown>={complete:false,boundary:'Original Book catalog UI: advanced source input creates an editable compound fixture; visible target selection, current-value loading, read-only revision, generated support choice, native save receipts and Undo exercise normal authoring. Saved files are read only. Stale/refused edits and duplicate starts are Headless-only here. No headset, provider, route, collision-contact or prototype acceptance claim.'};
 const historyCheckpoints:unknown[]=[];evidence.historyCheckpoints=historyCheckpoints;
 const checkpoint=async(phase:string,shot=false)=>{evidence.phase=phase;await writeFile(join(directory,'book-static-support.json'),JSON.stringify(evidence,null,2));if(shot)await page.screenshot({path:join(directory,'book-static-support-'+phase+'.png')});};
 const open=async(id:string)=>{
  const definition=capabilityDefinition(id);assert.ok(definition);
  await book.workshop();await page.getByRole('button',{name:'Action catalog',exact:true}).click();await page.getByLabel('Search actions',{exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();
  await page.getByRole('button',{name:definition.label+' '+id+' · v'+definition.version,exact:true}).click();
 };
 const submit=async(id:string,args:Record<string,unknown>):Promise<SupportReceipt>=>{
  const supported=await page.evaluate(()=>window.nativeBookEvidence!().state!.capabilities??[]);assert.deepEqual(capabilityFeatures(id,args).filter(feature=>!supported.includes(feature)),[]);
  const previous=await page.evaluate(()=>{const s=window.nativeBookEvidence!().state!;return {ack:s.ack,id:s.execution?.selected?.id};});
  await page.getByRole('button',{name:'Run action now',exact:true}).click();
  await page.waitForFunction(({id,previous})=>{const s=window.nativeBookEvidence!().state!,r=s.execution?.selected;return s.ack!==previous.ack&&!s.ok||r?.id!==previous.id&&r?.capability===id&&['completed','failed','cancelled'].includes(r.phase);},{id,previous},{timeout:35000});
  const state=await page.evaluate(()=>window.nativeBookEvidence!().state!);assert.equal(state.ok,true,state.status);const receipt=state.execution?.selected;
  assert.equal(receipt?.phase,'completed',receipt?.status);assert.equal(receipt?.capability,id);assert.deepEqual(receipt?.call,{id,version:1,arguments:args});return receipt!;
 };
 const fact=async(id:string,args:Record<string,string>={},unavailable=false)=>{const definition=behaviourFact(id);assert.ok(definition);return book.fact(id,definition.label,args,unavailable);};
 try{
  const initialSimulation=await fact('physics.simulation');assert.equal((initialSimulation.value as {running:boolean}).running,false);
  const fixture=staticSupportFixture();await open('object.create');
  await page.getByText('Advanced action arguments',{exact:true}).click();await page.getByLabel('Action arguments',{exact:true}).fill(JSON.stringify(fixture));await page.getByText('Advanced action arguments',{exact:true}).click();
  const created=await submit('object.create',fixture),target=String(created.output?.objectId);assert.match(target,/^[a-f0-9]{32}$/);
  const createdSaved=await saved(),source=createdSaved.objects.find(item=>item.id===target);assert.ok(source);assert.deepEqual(source.staticSupport,{version:1,enabled:false});
  const otherSources=createdSaved.objects.filter(item=>item.id!==target);assert.deepEqual(otherSources.map(item=>item.id).sort(),before.objects.map(item=>item.id).sort());
  const read=async(enabled:boolean)=>{
   const result=await fact('object.support',{target});assert.equal(result.available,true);const value=result.value as {target:string;revision:number;enabled:boolean;ready:boolean;reason:string;temporary:boolean};
   assert.equal(value.target,target);assert.equal(value.enabled,enabled);assert.equal(value.temporary,false);assert.ok(Number.isSafeInteger(value.revision)&&value.revision>0);
   const snapshot=await saved(),current=snapshot.objects.find(item=>item.id===target);assert.ok(current);assert.deepEqual(current.staticSupport,{version:1,enabled});
   assertSupportSavedSource(source,current);
   return {value,savedSha256:snapshot.sha256,source:current};
  };
  const initial=await read(false);assert.equal(initial.value.ready,false);Object.assign(evidence,{fixture,target,created,initial,createdSavedSha256:createdSaved.sha256});await checkpoint('default-off',true);
  const change=async(current:typeof initial.value,enabled:boolean)=>{
   await open('object.support.set');await book.choose('Action inputs target',target);assert.equal(await page.getByRole('button',{name:'Run action now',exact:true}).isDisabled(),true);
   await page.getByRole('button',{name:'Load current values',exact:true}).click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();
   const choice=page.getByLabel('Action inputs enabled',{exact:true});assert.equal(await choice.inputValue(),String(current.enabled));
   await page.getByText('Current room references',{exact:true}).click();const revision=page.getByLabel('Action inputs revision',{exact:true});assert.equal(await revision.inputValue(),String(current.revision));assert.equal(await revision.getAttribute('readonly'),'');await page.getByText('Current room references',{exact:true}).click();
   await choice.selectOption(String(enabled));await page.getByRole('heading',{name:'Use as walking support',exact:true}).scrollIntoViewIfNeeded();await checkpoint(enabled?'enable-form':'disable-form',true);
   const receipt=await submit('object.support.set',{target,revision:current.revision,enabled});const result=await read(enabled);assert.equal(receipt.output?.revision,result.value.revision);assert.ok(result.value.revision>current.revision);return {receipt,...result};
  };
  let enabled=await change(initial.value,true);
  for(let attempt=0;!enabled.value.ready&&attempt<5;attempt++){await new Promise(resolve=>setTimeout(resolve,100));const next=await read(true);assert.equal(next.value.revision,enabled.value.revision);enabled={receipt:enabled.receipt,...next};}
  assert.equal(enabled.value.ready,true,enabled.value.reason);evidence.enabled=enabled;await checkpoint('enabled',true);
  const disabled=await change(enabled.value,false);assert.equal(disabled.value.ready,false);evidence.disabled=disabled;await checkpoint('disabled',true);
  const undo=async()=>{await book.workshop();const revision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);await page.getByRole('button',{name:'Undo',exact:true}).filter({visible:true}).first().click();await page.waitForFunction(previous=>window.nativeBookEvidence!().state!.sceneRevision>previous,revision);
   const samples=await awaitRoomPersistence(async()=>(await fact('room.persistence')).value);historyCheckpoints.push({direction:'undo',samples});};
  await undo();const restored=await read(true);assert.equal(restored.value.ready,true,restored.value.reason);assert.ok(restored.value.revision>disabled.value.revision);evidence.undo=restored;await checkpoint('undo',true);
  await undo();assert.equal((await read(false)).value.ready,false);await undo();assert.equal(await book.exists(target),false);const removed={target,saved:false,checkedBy:'world.objects exact ID search'};
  const final=await saved();assert.deepEqual(final.objects,otherSources,'Book support editing or cleanup changed another object');const simulation=await fact('physics.simulation');assert.equal((simulation.value as {running:boolean}).running,false);
  Object.assign(evidence,{removed,finalSavedSha256:final.sha256,complete:true});await checkpoint('complete');await book.workshop();
 }catch(error){evidence.complete=false;evidence.error=error instanceof Error?error.message:String(error);await checkpoint('failed');throw error;}
}
