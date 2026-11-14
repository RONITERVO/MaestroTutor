// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it,vi} from 'vitest';
import type {Locator} from 'playwright-core';
import {capabilityDefinition} from '../shared/capabilities';
import {currentInputFields} from '../shared/currentCapabilityInputs';
import {prepareBookWorldPackInput,verifyBookWorldPackGuard} from './probe-book-world-pack';

// A strict locator fake fails on visibility waits for collapsed inputs and records
// every attempted edit. This exercises the real driver branch, not a UI mutation.
function fieldFixture({visible=false,readonly=true,value='initial',select=false,choiceVisible=false}={}){
 const calls:string[]=[];
 const field={
  waitFor:async({state}:{state:string})=>{calls.push('field:'+state);if(state==='visible'&&!visible)throw new Error('field hidden');},
  getAttribute:async(name:string)=>name==='readonly'&&readonly?'':null,
  inputValue:async()=>value,count:async()=>choiceVisible?0:1,
  evaluate:async()=>{calls.push('inspect tag');return select;},
  fill:async(v:string)=>{calls.push('fill:'+v);if(!visible||readonly)throw new Error('input cannot be filled');},
  selectOption:async(v:string)=>{calls.push('select:'+v);if(!visible||readonly)throw new Error('input cannot be selected');},
 } as unknown as Locator;
 const combined={first:()=>combined,waitFor:async({state}:{state:string})=>{calls.push('combined:'+state);if(state==='visible'&&!visible&&!choiceVisible)throw new Error('field hidden');}};
 const choice={or:()=>combined,isVisible:async()=>choiceVisible} as unknown as Locator;
 const choose=vi.fn(async(label:string,target:string)=>{calls.push('choose:'+label+':'+target);});
 return {field,choice,choose,calls};
}
const label='Action inputs expectedRevision';
describe('Book world pack current-value input guards',()=>{
 it.each([['workspace.pack.prepare',true],['workspace.archive.activate',false],['workspace.review.prepare',false],['workspace.review.complete',false],['workspace.previous.select',false]] as const)('uses the shipped %s current mapping for its revision guard (%s)',(id,guard)=>{
  const definition=capabilityDefinition(id)!;expect(definition).toBeTruthy();
  const fields=currentInputFields(definition.input,definition.example);
  expect(fields.some(field=>field.path==='expectedRevision'&&field.guard)).toBe(guard);
  expect(fields.some(field=>field.path==='packHash'&&field.guard)).toBe(false);
 });
 it('reads a collapsed declared readonly guard without waiting for visibility or editing it',async()=>{
  const f=fieldFixture();expect(await prepareBookWorldPackInput(f.field,f.choice,label,'initial',true,f.choose)).toBe(true);
  await verifyBookWorldPackGuard(f.field,'initial');
  expect(f.calls).toEqual(['combined:attached','field:attached']);expect(f.choose).not.toHaveBeenCalled();
 });
 it('refuses a stale automatically loaded guard instead of filling the requested revision',async()=>{
  const f=fieldFixture({value:'b'.repeat(32)});await prepareBookWorldPackInput(f.field,f.choice,label,'a'.repeat(32),true,f.choose);
  await expect(verifyBookWorldPackGuard(f.field,'a'.repeat(32))).rejects.toThrow('Current guard differs');
  expect(f.calls.every(call=>!call.startsWith('fill:'))).toBe(true);
 });
 it('refuses a declared guard that unexpectedly becomes editable',async()=>{
  const f=fieldFixture({visible:true,readonly:false});
  await expect(prepareBookWorldPackInput(f.field,f.choice,label,'initial',true,f.choose)).rejects.toThrow('Current guard must be readonly');
  await expect(verifyBookWorldPackGuard(f.field,'initial')).rejects.toThrow('Loaded current guard must remain readonly');
  expect(f.calls).toEqual(['combined:attached','field:attached']);
 });
 it('does not exempt a hidden editable input from visibility requirements',async()=>{
  const f=fieldFixture({readonly:false});
  await expect(prepareBookWorldPackInput(f.field,f.choice,'Action inputs packHash','a'.repeat(64),false,f.choose)).rejects.toThrow('field hidden');
  expect(f.calls).toEqual(['combined:attached','combined:visible']);
 });
 it('does not silently skip an unexpected readonly user input',async()=>{
  const f=fieldFixture({visible:true});
  await expect(prepareBookWorldPackInput(f.field,f.choice,'Action inputs packHash','a'.repeat(64),false,f.choose)).rejects.toThrow('not a declared current guard');
  expect(f.calls).toEqual(['combined:attached','combined:visible']);
 });
 it.each([false,true])('keeps normal visible user input editing (select=%s)',async select=>{
  const f=fieldFixture({visible:true,readonly:false,select});
  expect(await prepareBookWorldPackInput(f.field,f.choice,'Action inputs packId','maestro.observatory',false,f.choose)).toBe(false);
  expect(f.calls).toEqual(['combined:attached','combined:visible','inspect tag',(select?'select:':'fill:')+'maestro.observatory']);
 });
 it('keeps object choices on the ordinary visible picker path',async()=>{
  const f=fieldFixture({readonly:false,choiceVisible:true});
  expect(await prepareBookWorldPackInput(f.field,f.choice,'Action inputs target','a'.repeat(32),false,f.choose)).toBe(false);
  expect(f.choose).toHaveBeenCalledExactlyOnceWith('Action inputs target','a'.repeat(32));
  expect(f.calls).toEqual(['combined:attached','combined:visible','choose:Action inputs target:'+'a'.repeat(32)]);
 });
});
