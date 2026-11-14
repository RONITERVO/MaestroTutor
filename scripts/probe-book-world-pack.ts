// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {join} from 'node:path';
import type {Locator,Page} from 'playwright-core';
import {currentInputFields} from '../shared/currentCapabilityInputs';
import {capabilityDefinition} from '../shared/capabilities';
import {behaviourFact} from '../shared/behaviourCatalog';
import {nativeBookObjects} from './native-book-objects';
import {probeWorldPack,worldPackActionOutput} from './probe-world-pack';
/** Only catalog-declared current-value guards may remain inside collapsed reference
 * details. They must be attached and readonly; ordinary inputs still require visibility. */
export async function prepareBookWorldPackInput(field:Locator,choice:Locator,label:string,value:unknown,guard:boolean,choose:(label:string,target:string)=>Promise<void>):Promise<boolean>{
 await choice.or(field).first().waitFor({state:'attached'});
 if(guard){await field.waitFor({state:'attached'});assert.notEqual(await field.getAttribute('readonly'),null,'Current guard must be readonly');return true;}
 await choice.or(field).first().waitFor({state:'visible'});
 assert.ok(!await field.count()||await field.getAttribute('readonly')===null,'Readonly input is not a declared current guard');
 if(await choice.isVisible())await choose(label,String(value));
 else if(await field.evaluate(e=>e.tagName==='SELECT'))await field.selectOption(String(value));
 else await field.fill(String(value));
 return false;
}
export async function verifyBookWorldPackGuard(field:Locator,value:unknown):Promise<void>{
 assert.notEqual(await field.getAttribute('readonly'),null,'Loaded current guard must remain readonly');
 assert.equal(await field.inputValue(),String(value),'Current guard differs from intended origin');
}
/** Uses generated user forms and visible navigation; no hidden native commands. */
export async function probeBookWorldPack(page:Page,directory:string){
 const book=nativeBookObjects(page);
 const settled=async(previousSession?:string)=>{await page.waitForFunction(previous=>{const value=window.nativeBookEvidence?.();return value?.state&&value.state.workspaceReady!==false&&!value.inputBlocked&&(!previous||value.state.session!==previous);},previousSession,{timeout:previousSession?120000:60000});};
 await probeWorldPack({settled,session:async()=>page.evaluate(()=>window.nativeBookEvidence!().state!.session),
  fact:async(id,args={})=>{const definition=behaviourFact(id);assert.ok(definition);const result=await book.fact(id,definition.label,Object.fromEntries(Object.entries(args).map(([key,value])=>[key,String(value)])));assert.equal(result.available,true);return result.value as Record<string,unknown>;},
  action:async(id,args)=>{
   const definition=capabilityDefinition(id);assert.ok(definition);await settled();await book.workshop();await page.getByRole('button',{name:'Action catalog',exact:true}).click();
   await page.getByLabel('Search actions',{exact:true}).fill(id);await page.getByRole('button',{name:'Search',exact:true}).click();await page.getByRole('button',{name:definition.label+' '+id+' · v'+definition.version,exact:true}).click();
   const references=page.getByText('Current room references',{exact:true});if(await references.isVisible())await references.click();
   const guards:string[]=[],declaredGuards=new Set(currentInputFields(definition.input,args).filter(field=>field.guard).map(field=>field.path));
   for(const [name,value] of Object.entries(args)){
    const label='Action inputs '+name,choice=page.getByRole('region',{name:label+' object choice',exact:true}),field=page.getByLabel(label,{exact:true});
    if(await prepareBookWorldPackInput(field,choice,label,value,declaredGuards.has(name),book.choose))guards.push(name);
   }
   const load=page.getByRole('button',{name:'Load current values',exact:true});if(await load.isVisible()){await load.click();await page.getByText('Current values loaded. Review your changes before running.',{exact:true}).waitFor();}
   for(const name of guards)await verifyBookWorldPackGuard(page.getByLabel('Action inputs '+name,{exact:true}),args[name]);
   const workspace=definition.domain==='workspace';const previous=await page.evaluate(workspace=>{const s=window.nativeBookEvidence!().state!;return {session:s.session,id:(workspace?s.execution?.workspace:s.execution)?.nextRunId};},workspace);
   await page.getByRole('button',{name:'Run action now',exact:true}).click();
   await page.waitForFunction(({workspace,previous,id})=>{const s=window.nativeBookEvidence!().state!,r=(workspace?s.execution?.workspace:s.execution)?.selected;return s.session!==previous.session||r?.id===previous.id&&r?.capability===id&&['completed','failed','cancelled'].includes(r.phase);},{workspace,previous,id},{timeout:40000});
   const state=await page.evaluate(()=>window.nativeBookEvidence!().state!),receipt=(workspace?state.execution?.workspace:state.execution)?.selected;
   assert.equal(state.session,previous.session,'Action response was lost during workspace change; inspect before claiming success');assert.equal(receipt?.id,previous.id);assert.equal(receipt?.capability,id);assert.equal(receipt?.version,definition.version);assert.equal(receipt?.phase,'completed',receipt?.status);assert.deepEqual(receipt?.call,{id,version:definition.version,arguments:args});return worldPackActionOutput(id,definition.version,receipt?.output);
  },
  undo:async()=>{await book.workshop();const revision=await page.evaluate(()=>window.nativeBookEvidence!().state!.sceneRevision);await page.getByRole('button',{name:'Undo',exact:true}).filter({visible:true}).first().click();await page.waitForFunction(previous=>window.nativeBookEvidence!().state!.sceneRevision>previous,revision);},
  checkpoint:async phase=>{await page.screenshot({path:join(directory,'book-world-pack-'+phase+'.png')});},
 },directory,'book');
}
