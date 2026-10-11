// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {assertStoppedPaint,assertSteeringLink} from './probe-agent-steering';
import type {RoomAgentState} from '../src/core-sdk/room/roomAgent';
import type {RoomTaskRecord} from '../src/core-sdk/room/roomTaskHandoff';
const target='a'.repeat(32);
function fixture(){
 const before={objects:[{id:target,name:'ParityBall',kind:'ball',position:{x:1,y:1,z:1},scale:.5,color:{r:0,g:0,b:1,a:1}}]} as RoomAgentState;
 const painted={...structuredClone(before),ok:true};painted.objects[0].color={r:1,g:0,b:0,a:1};
 const parent={id:'parent',phase:'stopped',handoff:{nativeSession:'room',accessScope:'byok',conversationId:'es',input:{prompt:'Paint red then resize'}},
  operations:[{commands:[{action:'paint',target,color:{r:1,g:0,b:0,a:1}}],sceneRevision:1,receipt:painted}]} as RoomTaskRecord;
 const child={id:'child',phase:'completed',handoff:{...parent.handoff,input:{prompt:'Revise to blue only'}},directive:{action:'revise',taskId:parent.id},
  relatedTask:{id:parent.id,action:'revise',phase:'stopped',requests:[parent.handoff.input.prompt],operations:structuredClone(parent.operations),wasRunning:false,unconfirmed:false}} as RoomTaskRecord;
 return {before,painted,parent,child};
}
describe('native steering evidence gates',()=>{
 it('refuses uncertain, completed, resized or repeated effects at the Stop boundary',()=>{
  const {before,painted,parent}=fixture();expect(()=>assertStoppedPaint(parent,before,painted,target)).not.toThrow();
  for(const mutate of [
   (r:RoomTaskRecord)=>{r.phase='completed';},
   (r:RoomTaskRecord)=>{delete r.operations[0].receipt;},
   (r:RoomTaskRecord)=>{r.operations.push(structuredClone(r.operations[0]));},
   (r:RoomTaskRecord)=>{r.operations[0].receipt!.objects[0].scale=.75;},
  ]){const record=structuredClone(parent);mutate(record);expect(()=>assertStoppedPaint(record,before,painted,target)).toThrow();}
  const resized=structuredClone(painted);resized.objects[0].scale=.75;
  const resizedRecord=structuredClone(parent);resizedRecord.operations[0].receipt=resized;
  expect(()=>assertStoppedPaint(resizedRecord,before,resized,target)).toThrow();
 });
 it('requires the exact same-scope parent, request chain and confirmed receipts',()=>{
  const {parent,child}=fixture();expect(()=>assertSteeringLink(child,parent,'revise')).not.toThrow();
  for(const mutate of [
   (r:RoomTaskRecord)=>{r.directive!.taskId='unrelated';},
   (r:RoomTaskRecord)=>{r.relatedTask!.requests=[];},
   (r:RoomTaskRecord)=>{r.relatedTask!.operations=[];},
   (r:RoomTaskRecord)=>{r.relatedTask!.unconfirmed=true;},
   (r:RoomTaskRecord)=>{r.handoff.nativeSession='different';},
   (r:RoomTaskRecord)=>{r.handoff.accessScope='another-owner';},
  ]){const record=structuredClone(child);mutate(record);expect(()=>assertSteeringLink(record,parent,'revise')).toThrow();}
 });
});
