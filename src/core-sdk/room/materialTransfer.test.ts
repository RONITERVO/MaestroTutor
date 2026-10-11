// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {RoomAgentClient} from './roomAgentClient';
import type {RoomAgentState} from './roomAgent';
import type {CapabilityInvocation} from '../../../shared/capabilities';
import {validateCapabilityArguments,capabilityFeatures,capabilityResources} from '../../../shared/capabilities';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/material-transfer-contract.json','utf8')) as {name:string;arguments:Record<string,unknown>;valid:boolean}[];
describe('shared carried and surface material transfer',()=>{
 for(const row of cases)it(row.name,()=>expect(validateCapabilityArguments('object.material.transfer',1,row.arguments)===null).toBe(row.valid));
 it('declares the shared runtime and both exact owners for every route',()=>{
  for(const row of cases.filter(c=>c.valid)){
   expect(capabilityFeatures('object.material.transfer',row.arguments)).toEqual(expect.arrayContaining(['heightFields.v1','materialStores.v1','materialTransfer.v1','actionResults.v1']));
   expect(capabilityResources('object.material.transfer',row.arguments)).toEqual(['a'.repeat(32),'b'.repeat(32)]);
  }
 });
});

it('accepts native transfer and return receipts with measured balances and atomic Undo',()=>{
 const native=JSON.parse(readFileSync('test-fixtures/browser/materialTransferAuthoring.json','utf8')) as Record<string,unknown>&{
  call:CapabilityInvocation;returnCall:CapabilityInvocation;after:RoomAgentState;returned:RoomAgentState;
  sourceBefore:RoomAgentState;storeBefore:RoomAgentState;sourceTaken:RoomAgentState;storeTaken:RoomAgentState;
  sourceReturned:RoomAgentState;storeReturned:RoomAgentState;restoredSource:RoomAgentState;restoredStore:RoomAgentState;
 };
 const states=Object.values(native).filter((value):value is RoomAgentState=>Boolean(value&&typeof value==='object'&&'session' in value));
 expect(states).toHaveLength(15);for(const state of states)expect(new RoomAgentClient().receive(state),state.status).toBe(true);
 const fact=(state:RoomAgentState)=>{
  if(state.catalog?.operation!=='inspect'||!('value' in state.catalog))throw new Error('Expected captured native fact');
  return state.catalog.value as {volumeLitres:number;definition:{amountLitres:number}};
 };
 const volume=(state:RoomAgentState)=>fact(state).volumeLitres,stored=(state:RoomAgentState)=>fact(state).definition.amountLitres;
 for(const [state,call] of [[native.after,native.call],[native.returned,native.returnCall]] as const){
  const receipt=state.execution!.selected!;expect(receipt.phase).toBe('completed');expect(receipt.call).toEqual(call);
  expect(receipt.resources).toEqual(capabilityResources(call.id,call.arguments));expect(receipt.resources).toHaveLength(2);
  const output=receipt.output as {removedLitres:number;addedLitres:number;roundingLitres:number};
  expect(output.removedLitres).toBeGreaterThan(0);expect(output.addedLitres).toBeGreaterThan(0);
  expect(output.roundingLitres).toBeCloseTo(output.addedLitres-output.removedLitres,12);
  expect(Math.abs(output.roundingLitres)).toBeLessThanOrEqual(Math.max(.000001,output.removedLitres*.000001));
 }
 const taken=native.after.execution!.selected!.output!,returned=native.returned.execution!.selected!.output!;
 expect(volume(native.sourceBefore)-volume(native.sourceTaken)).toBeCloseTo(taken.removedLitres as number,8);
 expect(stored(native.storeTaken)-stored(native.storeBefore)).toBeCloseTo(taken.addedLitres as number,8);
 expect(stored(native.storeTaken)-stored(native.storeReturned)).toBeCloseTo(returned.removedLitres as number,8);
 expect(volume(native.sourceReturned)-volume(native.sourceTaken)).toBeCloseTo(returned.addedLitres as number,8);
 expect(volume(native.restoredSource)).toBe(volume(native.sourceBefore));expect(stored(native.restoredStore)).toBe(stored(native.storeBefore));
});
