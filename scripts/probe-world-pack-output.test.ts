// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it} from 'vitest';
import {capabilityDefinition} from '../shared/capabilities';
import {activateWorldPackSelection,worldPackActionOutput,worldPackObjects,assertWorldPackObjectsEqual,type WorldPackAdapter} from './probe-world-pack';

const requestId='a'.repeat(32);
describe('world pack probe action outputs from the actual shared catalog',()=>{
 it.each([null,undefined])('accepts the shipped void paint output %s without inventing a record',output=>{
  const paint=capabilityDefinition('object.color.set');expect(paint).not.toBeNull();expect(paint!.output).toBeFalsy();
  expect(worldPackActionOutput(paint!.id,paint!.version,output)).toBeUndefined();
 });
 it.each([{},[],{requestId},false,0,''])('refuses unexpected output %j from a void action',output=>{
  const paint=capabilityDefinition('object.color.set')!;
  expect(()=>worldPackActionOutput(paint.id,paint.version,output)).toThrow('Void action returned an unexpected output');
 });
 it.each(['workspace.pack.prepare','workspace.archive.cancel','workspace.archive.activate','workspace.review.prepare','workspace.review.complete','workspace.previous.select'])('retains the actual %s request output and rejects missing or malformed replacements',id=>{
  const definition=capabilityDefinition(id);expect(definition).not.toBeNull();expect(definition!.output).toBeTruthy();
  const actual={requestId};expect(worldPackActionOutput(id,definition!.version,actual)).toBe(actual);
  for(const output of [undefined,null,{},[],requestId,{requestId:1},{requestId:'broken'},{requestId,success:true}])
   expect(()=>worldPackActionOutput(id,definition!.version,output),JSON.stringify(output)).toThrow('Action output does not match the shared capability contract');
  expect(actual).toEqual({requestId});
 });
 it('refuses unknown identities and versions before considering a void or structured output',()=>{
  expect(()=>worldPackActionOutput('unknown.world.action',1,undefined)).toThrow('Unknown action output contract');
  for(const id of ['object.color.set','workspace.pack.prepare']){
   const definition=capabilityDefinition(id)!;
   expect(()=>worldPackActionOutput(id,definition.version+1,definition.output?{requestId}:undefined)).toThrow('Action output version differs');
  }
 });
});

describe('world pack activation crosses only its acknowledged workspace boundary',()=>{
 const origin='a'.repeat(32),next='b'.repeat(32),request='c'.repeat(32),selectionRequest='d'.repeat(32),generation='e'.repeat(32),manifest='f'.repeat(64),revision='1'.repeat(32);
 const selection={generationId:generation,manifestHash:manifest};
 function fixture(){
  const calls:{id:string;args?:Record<string,unknown>}[]=[];let currentSession=origin;
  const result={requestId:request,selectionRequestId:selectionRequest,originRevision:'initial',committedRevision:revision,phase:'review',preview:{generationId:generation,manifestHash:manifest}};
  const current={generationId:generation,revision,activationRequestId:request,reviewRequired:true,available:true,changing:false};
  const adapter:WorldPackAdapter={
   session:async()=>currentSession,
   settled:async previous=>{calls.push({id:'wait',args:{previous}});expect(previous).toBe(origin);currentSession=next;},
   action:async(id,args)=>{calls.push({id,args});return {requestId:request};},
   fact:async(id,args)=>{calls.push({id,args});expect(currentSession).toBe(next);return id==='workspace.archive.activation'?result:current;},
   undo:async()=>{throw new Error('Activation must not call Undo');},
  };
  return {adapter,calls,result,current};
 }
 it('starts once and waits for a different ready session before any status read',async()=>{
  const f=fixture();let release!:()=>void;const gate=new Promise<void>(resolve=>{release=resolve;});const settle=f.adapter.settled;
  f.adapter.settled=async previous=>{await gate;await settle(previous);};
  const done=activateWorldPackSelection(f.adapter,selection,selectionRequest,'initial');
  await Promise.resolve();await Promise.resolve();await Promise.resolve();
  expect(f.calls).toEqual([{id:'workspace.archive.activate',args:{selectionRequestId:selectionRequest,generationId:generation,manifestHash:manifest,expectedRevision:'initial'}}]);
  release();expect(await done).toEqual({result:f.result,current:f.current});
  expect(f.calls.map(c=>c.id)).toEqual(['workspace.archive.activate','wait','workspace.archive.activation','workspace.current']);
  expect(f.calls[2].args).toEqual({requestId:request});
 });
 it('fails if transition waiting returns without a new session, with no read or action replay',async()=>{
  const f=fixture();f.adapter.settled=async()=>{};
  await expect(activateWorldPackSelection(f.adapter,selection,selectionRequest,'initial')).rejects.toThrow('Activation did not change');
  expect(f.calls.map(c=>c.id)).toEqual(['workspace.archive.activate']);
 });
 it('preserves action-response loss without waiting, inventing a request, or replaying',async()=>{
  const f=fixture(),failure=new DOMException('lost action reply','AbortError');
  f.adapter.action=async(id,args)=>{f.calls.push({id,args});throw failure;};
  await expect(activateWorldPackSelection(f.adapter,selection,selectionRequest,'initial')).rejects.toBe(failure);
  expect(f.calls.map(c=>c.id)).toEqual(['workspace.archive.activate']);
 });
 it('preserves an unexpected reset during re-observation instead of retrying the read',async()=>{
  const f=fixture(),failure=new DOMException('unrelated reset','AbortError');
  f.adapter.fact=async(id,args)=>{f.calls.push({id,args});throw failure;};
  await expect(activateWorldPackSelection(f.adapter,selection,selectionRequest,'initial')).rejects.toBe(failure);
  expect(f.calls.map(c=>c.id)).toEqual(['workspace.archive.activate','wait','workspace.archive.activation']);
 });
 it('does not treat a transition timeout as a completed activation',async()=>{
  const f=fixture(),failure=new Error('transition timeout');f.adapter.settled=async()=>{throw failure;};
  await expect(activateWorldPackSelection(f.adapter,selection,selectionRequest,'initial')).rejects.toBe(failure);
  expect(f.calls.map(c=>c.id)).toEqual(['workspace.archive.activate']);
 });
 it.each(['requestId','selectionRequestId','originRevision','phase','generationId','manifestHash'])('rejects the wrong tracked activation %s',async field=>{
  const f=fixture();if(field==='generationId'||field==='manifestHash')f.result.preview[field]='9'.repeat(field==='manifestHash'?64:32);
  else f.result[field as 'requestId'|'selectionRequestId'|'originRevision'|'phase']=field==='phase'?'failed':'9'.repeat(32);
  await expect(activateWorldPackSelection(f.adapter,selection,selectionRequest,'initial')).rejects.toThrow();
  expect(f.calls.filter(c=>c.id==='workspace.archive.activate')).toHaveLength(1);expect(f.calls.some(c=>c.id==='workspace.current')).toBe(false);
 });
 it.each(['generationId','revision','activationRequestId','reviewRequired','available','changing'])('rejects a current workspace with incorrect %s after the tracked activation',async field=>{
  const f=fixture();
  if(field==='reviewRequired'||field==='available'||field==='changing')f.current[field]=!f.current[field];
  else f.current[field as 'generationId'|'revision'|'activationRequestId']='9'.repeat(32);
  await expect(activateWorldPackSelection(f.adapter,selection,selectionRequest,'initial')).rejects.toThrow();
  expect(f.calls.filter(c=>c.id==='workspace.archive.activate')).toHaveLength(1);
 });
});

describe('world pack saved objects compare exact identities across canonical outer sorting',()=>{
 const original=()=>[{id:'book',parts:[{id:'first',value:1},{id:'second',value:2}],settings:{enabled:true}},{id:'a'.repeat(32),parts:[],settings:{enabled:false}}];
 it('accepts only a reordered outer list without mutating either input',()=>{
  const a=original(),b=structuredClone(a).reverse(),beforeA=structuredClone(a),beforeB=structuredClone(b);
  expect(()=>assertWorldPackObjectsEqual(a,b,'objects changed')).not.toThrow();
  expect(a).toEqual(beforeA);expect(b).toEqual(beforeB);expect(worldPackObjects(a)[1]).toBe(a[0]);
 });
 it.each(['added','removed','duplicate','changed','nested-order','nested-value','missing-field'])('refuses %s records while retaining exact nested content',kind=>{
  const a=original(),b=structuredClone(a).reverse();
  if(kind==='added')b.push({id:'b'.repeat(32),parts:[],settings:{enabled:false}});
  if(kind==='removed')b.pop();
  if(kind==='duplicate')b.push(structuredClone(b[0]));
  if(kind==='changed')b[0].settings.enabled=true;
  if(kind==='nested-order')b[1].parts.reverse();
  if(kind==='nested-value')b[1].parts[0].value=9;
  if(kind==='missing-field')delete (b[1] as {settings?:unknown}).settings;
  expect(()=>assertWorldPackObjectsEqual(a,b,'objects changed')).toThrow();
 });
 it.each([null,{},[null],[{id:'bad'}],[{id:1}]])('refuses invalid saved object identity input %j',value=>{
  expect(()=>worldPackObjects(value)).toThrow();
 });
});
