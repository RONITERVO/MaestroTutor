// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {assertStoppedPaint,assertSteeringLink,confirmedRedPaint} from './probe-agent-steering';
import type {NativeObjectReadSet} from './native-probe-read-set';
import type {RoomAgentState,RoomCommand} from '../src/core-sdk/room/roomAgent';
import type {RoomTaskRecord} from '../src/core-sdk/room/roomTaskHandoff';
const target='a'.repeat(32);
function fixture(){
 const before:NativeObjectReadSet={source:'explicit-native-reads',session:'native',worldId:'b'.repeat(32),sceneRevision:1,temporaryId:null,temporary:false,objects:[{id:target,objectRevision:1,physicsEvidence:'component-fact',physics:{mode:'solid',mass:2,shape:'sphere'},name:'ParityBall',kind:'ball',position:{x:1,y:1,z:1},scale:.5,color:{r:0,g:0,b:1,a:1},animated:false}]};
 const painted=structuredClone(before);painted.sceneRevision=2;painted.objects[0].color={r:1,g:0,b:0,a:1};
 const receipt:RoomAgentState={version:1,session:'native',revision:2,sceneRevision:2,ack:1,ok:true,status:'Painted',created:[],objects:[],physicsRunning:false,canUndo:true,canRedo:false};
 const boundary={commands:[{action:'paint',target,color:{r:1,g:0,b:0,a:1}}] as RoomCommand[],receipt};
 const parent={id:'parent',phase:'stopped',handoff:{nativeSession:'room',accessScope:'byok',conversationId:'es',input:{prompt:'Paint red then resize'}},
  operations:[{...boundary,sceneRevision:1}]} as RoomTaskRecord;
 const child={id:'child',phase:'completed',handoff:{...parent.handoff,input:{prompt:'Revise to blue only'}},directive:{action:'revise',taskId:parent.id},
  relatedTask:{id:parent.id,action:'revise',phase:'stopped',requests:[parent.handoff.input.prompt],operations:structuredClone(parent.operations),wasRunning:false,unconfirmed:false}} as RoomTaskRecord;
 return {before,painted,parent,child,boundary};
}
describe('native steering evidence gates',()=>{
 it('refuses uncertain, completed, resized, foreign or repeated effects without hydrating receipts',()=>{
  const {before,painted,parent,boundary}=fixture();expect(()=>assertStoppedPaint(parent,before,painted,target,boundary)).not.toThrow();
  expect(parent.operations[0].receipt!.objects).toEqual([]);
  for(const mutate of [
   (r:RoomTaskRecord)=>{r.phase='completed';},
   (r:RoomTaskRecord)=>{delete r.operations[0].receipt;},
   (r:RoomTaskRecord)=>{r.operations.push(structuredClone(r.operations[0]));},
   (r:RoomTaskRecord)=>{r.operations[0].commands.push({action:'resize',target,scale:.75});},
   (r:RoomTaskRecord)=>{r.operations[0].receipt!.ack++;},
  ]){const record=structuredClone(parent);mutate(record);expect(()=>assertStoppedPaint(record,before,painted,target,boundary)).toThrow();}
  for(const change of ['size','physics','session','revision']){
   const after=structuredClone(painted);
   if(change==='size')after.objects[0].scale=.75;if(change==='physics')after.objects[0].physics.mass=3;
   if(change==='session')after.session='other';if(change==='revision')after.sceneRevision++;
   expect(()=>assertStoppedPaint(parent,before,after,target,boundary)).toThrow();
  }
 });
 it('recognizes only confirmed matching legacy or issued capability paints',()=>{
  const {boundary}=fixture();expect(confirmedRedPaint([boundary],target)).toEqual(boundary);
  const call={id:'object.color.set',version:1,arguments:{target,red:1,green:0,blue:0}},runId='c'.repeat(32);
  const selected={id:runId,capability:call.id,version:1,resources:[target],phase:'preparing' as const,status:'Preparing',call};
  const start={commands:[{action:'execution',execution:{operation:'start',runId,call}}] as RoomCommand[],receipt:{...boundary.receipt,execution:{selected,running:[selected],outcomes:[]}}};
  const completed={...selected,phase:'completed' as const};
  const inspected={commands:[{action:'execution',execution:{operation:'inspect',runId}}] as RoomCommand[],receipt:{...boundary.receipt,execution:{selected:completed,running:[],outcomes:[completed]}}};
  expect(confirmedRedPaint([start],target)).toBeNull();expect(confirmedRedPaint([inspected],target)).toBeNull();
  expect(confirmedRedPaint([start,inspected],target)).toEqual(inspected);
  expect(confirmedRedPaint([{...start,receipt:inspected.receipt}],target)).not.toBeNull();
  for(const change of ['failed','query','wrongRun','wrongCall','cancelled']){
   const altered=structuredClone(inspected);
   if(change==='failed')altered.receipt.ok=false;
   if(change==='query')altered.commands=[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.running',version:1}}];
   if(change==='wrongRun')altered.receipt.execution.selected.id='d'.repeat(32);
   if(change==='wrongCall')altered.receipt.execution.selected.call.arguments.target='other';
   if(change==='cancelled')altered.receipt.execution.selected.phase='cancelled' as 'completed';
   expect(confirmedRedPaint([start,altered],target)).toBeNull();
  }
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
