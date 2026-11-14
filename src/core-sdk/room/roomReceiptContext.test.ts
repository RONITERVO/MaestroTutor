// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it} from 'vitest';
import {roomReceiptContext,roomRelatedTaskContext,type RoomReceiptContext} from '../../../shared/prompts/receiptcontext';
import {buildRoomTaskOutcomeInstruction} from '../../../shared/prompts/handoff';
import {buildRoomAgentPrompt,buildRoomResultInstruction,ROOM_AGENT_INSTRUCTION} from '../../../shared/prompts/room';

// Independent reconstruction checks every native field, not just a hand-picked
// success label. This is a provider representation; runtime never reads it back.
function restore(context:RoomReceiptContext):unknown[] {
 return context.receipts.map((receipt,index)=>{
  const references=context.receiptReuse?.fields[String(index)];
  if(!references)return structuredClone(receipt);
  return Object.fromEntries([...Object.entries(receipt as Record<string,unknown>),
   ...Object.entries(references).map(([field,id])=>{
    expect(Object.prototype.hasOwnProperty.call(context.receiptReuse!.values,id)).toBe(true);
    expect(Object.prototype.hasOwnProperty.call(receipt,field)).toBe(false);
    return [field,structuredClone(context.receiptReuse!.values[id])];
   })]);
 });
}
const objects=Array.from({length:12},(_,i)=>({id:`object${i}`,name:`User object ${i}`,position:{x:i,y:0,z:0},objectRevision:3}));
const definition={id:'appearance.save',version:1,input:{properties:{source:{description:'Exact authored values '.repeat(100)}}}};
const receipts=Array.from({length:24},(_,i)=>({session:'session',ack:i+1,sceneRevision:i<15?4:5,ok:i!==16,
 status:i===16?'Conflict: another author changed it':'Acknowledged',objects,capabilities:['catalog.v1'],
 catalog:{operation:'inspect',definition},execution:{selected:{id:'run1',phase:i<15?'running':'completed',output:i<15?null:{accepted:0}}}}));

describe('lossless room receipt working context',()=>{
 it('preserves saved entities across active, inactive and unavailable native observations',()=>{
  const states=['active','inactive','unavailable','active'].map((runtimeState,index)=>({ack:index+1,objects:objects.map(object=>({...object,runtimeState,positionSource:runtimeState==='active'?'live':'saved'}))}));
  const view=roomReceiptContext([...states,...states]);expect(restore(view)).toEqual([...states,...states]);
 });

 it('preserves every receipt while substantially reducing repeated native snapshots',()=>{
  const original=structuredClone(receipts),view=roomReceiptContext(receipts);
  expect(restore(view)).toEqual(original);expect(receipts).toEqual(original);
  expect(JSON.stringify(view).length).toBeLessThan(JSON.stringify(receipts).length*.3);
  expect(view.receipts).toHaveLength(24);
  expect((view.receipts[16] as typeof receipts[number]).ok).toBe(false);
  expect((view.receipts[23] as typeof receipts[number]).execution.selected.output).toEqual({accepted:0});
 });
 it('retains distinct histories, null versus absence, exact IDs, failure and live revisions',()=>{
  const states=[...receipts.slice(0,2),{...receipts[2],objects:objects.slice(1),catalog:null},
   {...receipts[3],objects:objects.map(o=>({...o,objectRevision:4})),capture:{captureId:'pixels-id',sha256:'a'.repeat(64)}},
   {ack:5,sceneRevision:7,ok:false,status:'Lost acknowledgement',execution:null}];
  const view=roomReceiptContext(states);expect(restore(view)).toEqual(states);
  expect(view.receipts.map(r=>(r as {ack:number}).ack)).toEqual([1,2,3,4,5]);
 });
 it('treats authored reference-shaped values and unusual property names only as native data',()=>{
  const value=JSON.parse('{"__proto__":{"name":"ordinary data"},"fields":{"0":{"objects":"v0"}},"value":"'+ 'quoted untrusted content '.repeat(50)+'"}');
  const states=[{objects:value,receiptReuse:value},{objects:value,receiptReuse:value}];
  expect(restore(roomReceiptContext(states))).toEqual(states);
  expect(({} as {name?:string}).name).toBeUndefined();
 });
 it('does not conflate reordered arrays, close numbers, source whitespace or changed schemas',()=>{
  const values=[definition,{...definition,version:2},{...definition,input:{...definition.input,n:0.5000001}},
   {source:' '.repeat(512)+'a'},{source:' '.repeat(512)+'b'},[...objects].reverse(),objects];
  const states=values.flatMap((catalog,index)=>[{catalog,ack:index},{catalog,ack:index+100}]);
  expect(restore(roomReceiptContext(states))).toEqual(states);
 });
 it('keeps small and unique evidence inline and follows the existing JSON boundary',()=>{
  const states=[{ack:1,unknown:undefined,empty:null,objects:[]},null,'source'];
  expect(roomReceiptContext(states)).toEqual({receipts:JSON.parse(JSON.stringify(states))});
  expect(roomReceiptContext([receipts[0]]).receiptReuse).toBeUndefined();
 });
 it('applies one view to planning and final narration without changing requests, guards or command links',()=>{
  const scene={...receipts[23],sceneRevision:100,objects:objects.slice(2)};
  const context={operations:[{commands:[{action:'delete',target:'object0'}],receiptIndex:15}],
   acceptedProgramStarts:[{target:'program',revision:3,receiptIndex:4,runIds:['exact-run']}],
   planRejection:{message:'Missing call',response:'quoted proposal',truncated:false},
   relatedTask:{unconfirmed:true,requests:['Keep the original apple.'],operations:[{commands:[{action:'move',target:'apple'}]}]}};
  const budget={planningCalls:2,queryBatches:1,actionBatches:0};
  const prompt=JSON.parse(buildRoomAgentPrompt('Keep the apple; remove only the tree.',scene,receipts,context,budget));
  expect(prompt.request).toBe('Keep the apple; remove only the tree.');expect(prompt.scene).toEqual(scene);
  expect(prompt.tutorContext).toEqual(context);expect(prompt.budget).toEqual(budget);expect(restore(prompt)).toEqual(receipts);
  const result=buildRoomResultInstruction(receipts,scene,context.operations);
  expect(result).toContain(JSON.stringify(roomReceiptContext(receipts)));
  expect(result).toContain(JSON.stringify(context.operations));expect(result).toContain(JSON.stringify(scene));
  expect(ROOM_AGENT_INSTRUCTION).toContain('receiptReuse.fields[index][field]=valueId');
  expect(result).toContain('Historical values remain historical');
 });
 it('cannot mutate the durable originals through the projected data or dictionary',()=>{
  const states=structuredClone(receipts),view=roomReceiptContext(states);
  const before=JSON.stringify(states);
  (Object.values(view.receiptReuse!.values)[0] as unknown[]).pop();
  (view.receipts[0] as {status:string}).status='modified projection';
  expect(JSON.stringify(states)).toBe(before);
 });
});


describe('lossless follow-up task evidence',()=>{
 it('retains operation positions and absent acknowledgements through continuation and narration',()=>{
  const operations:Array<{commands:unknown[];sceneRevision:number;receipt?:unknown}>=[
   {commands:[{action:'delete',target:'originalTree'}],sceneRevision:4,receipt:receipts[0]},
   {commands:[{action:'move',target:'originalApple'}],sceneRevision:5},
   {commands:[{action:'catalog'}],sceneRevision:6,receipt:receipts[1]},
  ];
  const prior={id:'task-exact',action:'continue',phase:'interrupted',requests:['Keep the apple.','Remove only the tree.'],operations,
   note:'Unconfirmed action; inspect first',unconfirmed:true,wasRunning:false,reply:''};
  const before=structuredClone(prior);
  const view=roomRelatedTaskContext(prior) as typeof prior & Pick<RoomReceiptContext,'receiptReuse'>;
  const restored=restore({receipts:view.operations.map(o=>o.receipt??null),receiptReuse:view.receiptReuse});
  expect(restored).toEqual([receipts[0],null,receipts[1]]);expect(view.operations[1]).not.toHaveProperty('receipt');
  expect(view.operations.map(o=>o.commands)).toEqual(operations.map(o=>o.commands));
  expect(view.operations.map(o=>o.sceneRevision)).toEqual([4,5,6]);
  expect({...view,operations:prior.operations,receiptReuse:undefined}).toEqual({...prior,receiptReuse:undefined});
  expect(prior).toEqual(before);
  const prompt=JSON.parse(buildRoomAgentPrompt('Continue, but keep the apple.',receipts[23],receipts,{relatedTask:prior}));
  expect(prompt.tutorContext.relatedTask).toEqual(view);expect(restore(prompt)).toEqual(receipts);
  const reply=buildRoomTaskOutcomeInstruction(prior,true);expect(reply).toContain(JSON.stringify({relatedTask:view,needsReview:true}));
  expect(reply).toContain('operation without a receipt still has no acknowledgement');
 });
 it('does not reinterpret an existing reference table or unrelated context',()=>{
  const prior={receiptReuse:{version:500,values:'user-authored'},operations:[{receipt:receipts[0]},{receipt:receipts[1]}]};
  expect(roomRelatedTaskContext(prior)).toEqual(prior);
  expect(roomRelatedTaskContext(null)).toBeNull();expect(roomRelatedTaskContext({label:'Not a task'})).toEqual({label:'Not a task'});
 });
});
