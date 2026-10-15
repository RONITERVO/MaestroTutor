// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {isDeepStrictEqual} from 'node:util';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import {nativeProbeObjects} from './native-probe-objects';
import {checkedProbeReply} from './native-probe-contract';
export interface NativeObjectIdentity {
 version:1;source:'explicit-native-object-directory';session:string;worldId:string;sceneRevision:number;ids:string[];
}
const record=(v:unknown):v is Record<string,unknown>=>!!v&&typeof v==='object'&&!Array.isArray(v);
const guid=(v:unknown):v is string=>typeof v==='string'&&/^[a-f0-9]{32}$/.test(v);
export function validNativeObjectIdentity(v:unknown):v is NativeObjectIdentity {
 if(!record(v)||Object.keys(v).sort().join(',')!=='ids,sceneRevision,session,source,version,worldId'||v.version!==1||v.source!=='explicit-native-object-directory'
  ||!guid(v.session)||!guid(v.worldId)||!Number.isSafeInteger(v.sceneRevision)||Number(v.sceneRevision)<0||!Array.isArray(v.ids)||v.ids.length>768)return false;
 return v.ids.every((id,i,all)=>typeof id==='string'&&/^(book|maestro|[a-fA-F0-9]{32})$/.test(id)&&(i===0||all[i-1]<id))&&v.ids.includes('book')&&v.ids.includes('maestro');
}
export const isNativeIdentityRead=(command:RoomCommand)=>command.action==='catalog'&&command.catalog?.operation==='inspect'&&command.catalog.category==='facts'&&command.catalog.capability==='world.objects';
/** Durable membership evidence, read only while the agent is idle. Saved pose
 * revisions may advance; directory generations and the second pass guard IDs. Temporary/pending rooms cannot
 * describe the saved workspace restored in another native process. */
export async function readNativeObjectIdentity(execute:(commands:RoomCommand[])=>Promise<RoomAgentState>):Promise<NativeObjectIdentity>{
 let scope:{session:string;worldId:string}|undefined,sceneRevision=0;
 const native=nativeProbeObjects(async commands=>{
  const state=checkedProbeReply(commands,await execute(commands)),view=state.catalog;
  assert.ok(state.ok&&state.workspaceReady===true&&!state.temporaryRoom?.active&&!state.temporaryRoom?.pending,'Saved room identity is unavailable or temporary');
  assert.ok(view?.operation==='inspect'&&view.category==='facts'&&view.capability==='world.objects'&&view.available);
  const worldId=(view.value as {worldId:string}).worldId,current={session:state.session,worldId};sceneRevision=state.sceneRevision;
  if(scope)assert.deepEqual(current,scope,'Saved room scope changed during identity reads');else scope=current;
  return state;
 });
 const ids=await native.ids();assert.deepEqual(await native.ids(),ids,'Saved room membership changed during identity reads');assert.ok(scope);
 const result:NativeObjectIdentity={version:1,source:'explicit-native-object-directory',...scope,sceneRevision,ids};assert.ok(validNativeObjectIdentity(result),'Invalid explicit saved room identity');return result;
}
export function assertResumedNativeIdentity(expected:unknown,actual:unknown){
 assert.ok(validNativeObjectIdentity(expected)&&validNativeObjectIdentity(actual),'Resume requires complete explicit saved-object identity evidence');
 assert.notEqual(actual.session,expected.session,'Resume must open a new native session');
 assert.equal(actual.worldId,expected.worldId,'Resume changed the saved world identity');
 assert.ok(isDeepStrictEqual(actual.ids,expected.ids),'Restored room must preserve the exact original object identities');
}
export interface NativeResumeReceipt {version:1;sourceRun:string;nativeIdentity:NativeObjectIdentity;boundary:string}
export interface LearnerResumeReceipt extends NativeResumeReceipt {accessMode:'managed'|'byok';profileSha256:string}
export function readNativeResumeReceipt(value:unknown):NativeResumeReceipt {
 assert.ok(record(value)&&value.version===1&&guid(value.sourceRun)&&validNativeObjectIdentity(value.nativeIdentity)&&typeof value.boundary==='string',
  'Restore needs a closed run with explicit final identity evidence; periodic object arrays are insufficient');
 return value as unknown as NativeResumeReceipt;
}
export function readLearnerResumeReceipt(value:unknown):LearnerResumeReceipt {
 const native=readNativeResumeReceipt(value);assert.ok(record(value)&&(value.accessMode==='managed'||value.accessMode==='byok')
  &&typeof value.profileSha256==='string'&&/^[a-fA-F0-9]{64}$/.test(value.profileSha256),'Learner restore needs the original access mode and profile digest');
 return {...native,accessMode:value.accessMode,profileSha256:value.profileSha256} as LearnerResumeReceipt;
}
