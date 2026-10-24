// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it,vi} from 'vitest';
import {parseRoomCommands,runRoomActionTask,type RoomAgentState,type RoomCommand} from './roomAgent';
import {ROOM_DISCOVERY_PLAN_LIMIT,ROOM_TASK_LIMITS} from '../../../shared/roomTaskBudget';
const scene:RoomAgentState={version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',objects:[],created:[],canUndo:false,canRedo:false,physicsRunning:false,capabilities:['catalog.v1','catalogVocabulary.v1']};
const query=(name:string):RoomCommand=>({action:'catalog',catalog:{operation:'search',query:name,offset:0}});
const reads=Array.from({length:ROOM_DISCOVERY_PLAN_LIMIT},(_,i)=>query('query'+i));
const input={model:'gemini-3.8-flash',prompt:'Inspect the requested room features.',history:[]};
function client(plans:unknown[]){return {models:{generateContent:vi.fn(),generateContentStream:vi.fn(async()=>{
 const text=JSON.stringify({commands:plans.shift()});return (async function*(){yield {text,candidates:[{content:{role:'model',parts:[{text}]}}]};})();
})},live:{connect:vi.fn(),music:{connect:vi.fn()}}};}
const payload=(ai:ReturnType<typeof client>,index:number)=>JSON.parse((ai.models.generateContentStream.mock.calls as any)[index][0].contents[0].parts[0].text);
describe('bounded planner discovery groups',()=>{
 it('uses one planning response but individual native requests, current read guards and durable receipts',async()=>{
  const ai=client([reads,[]]);let current={...scene};const events:string[]=[];
  const execute=vi.fn(async(commands:RoomCommand[])=>{parseRoomCommands({commands});events.push('dispatch'+current.ack);current={...current,ack:current.ack+1,sceneRevision:current.sceneRevision+1};return current;});
  const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{}, {
   beforeDispatch:async(commands,state)=>{expect(commands).toHaveLength(1);events.push('intent'+state.ack);},
   onReceipt:async(receipt)=>{events.push('receipt'+receipt.ack);},
  });
  expect(execute.mock.calls.map(c=>c[0])).toEqual(reads.map(c=>[c]));
  expect((execute.mock.calls as any).map((c:any)=>c[1])).toEqual([4,5,6,7]);
  expect(events).toEqual(reads.flatMap((_,i)=>['intent'+i,'dispatch'+i,'receipt'+(i+1)]));
  expect(result.operations).toEqual(reads.map((c,i)=>({commands:[c],receiptIndex:i})));
  expect(result.receipts.map(r=>r.ack)).toEqual([1,2,3,4]);expect(ai.models.generateContentStream).toHaveBeenCalledTimes(2);
  expect(payload(ai,1).budget).toEqual({...ROOM_TASK_LIMITS,planningCalls:31,queryBatches:20});
  expect(payload(ai,1).tutorContext.discovery).toEqual({proposed:reads,acknowledged:4,rejected:false});
  expect(()=>parseRoomCommands({commands:reads})).toThrow('on its own');
 });
 it('preserves the planning-time guard for mutations even if the target changes during generation',async()=>{
  const move:RoomCommand={action:'move',target:'book',position:{x:1,y:1,z:1}};
  let current={...scene};const ai=client([[move],[]]);const stream=ai.models.generateContentStream;
  stream.mockImplementationOnce(async()=>{current={...current,sceneRevision:5};const text=JSON.stringify({commands:[move]});return (async function*(){yield {text,candidates:[{content:{role:'model',parts:[{text}]}}]};})();});
  const execute=vi.fn(async()=>({...current,ok:false,status:'Stale target'}));
  await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
  expect(execute).toHaveBeenCalledWith([move],4,scene.objects);
 });
 it.each(['cancel','session','capability','receiptLost'] as const)('stops remaining reads after %s and never replays the first query',async(kind)=>{
  const ai=client([reads,[]]),controller=new AbortController();let current={...scene};const saved=vi.fn();
  const execute=vi.fn(async()=>{
   if(kind==='receiptLost')throw new Error('Receipt lost');
   current={...current,ack:1};
   if(kind==='cancel')controller.abort();if(kind==='session')current={...current,session:'b'.repeat(32)};
   if(kind==='capability')current={...current,capabilities:[]};return current;
  });
  await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{},{signal:controller.signal,onReceipt:saved})).rejects.toThrow();
  expect(execute).toHaveBeenCalledOnce();expect(saved).toHaveBeenCalledTimes(kind==='receiptLost'?0:1);expect(ai.models.generateContentStream).toHaveBeenCalledOnce();
 });
 it('records a rejected native read, stops its group and explains the undispatched remainder',async()=>{
  const ai=client([reads,[]]);let current={...scene};const saved=vi.fn();
  const execute=vi.fn(async()=>{current={...current,ack:current.ack+1,ok:current.ack===0,status:current.ack===0?'Read':'Query rejected'};return current;});
  const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{},{onReceipt:saved});
  expect(execute).toHaveBeenCalledTimes(2);expect(saved).toHaveBeenCalledTimes(2);expect(result.operations).toHaveLength(2);
  expect(result.receipts[1].ok).toBe(false);expect(payload(ai,1).tutorContext.discovery).toEqual({proposed:reads,acknowledged:2,rejected:true});
  expect(payload(ai,1).budget.queryBatches).toBe(22);
 });
 it('rejects any invalid query or unsupported category before the first native dispatch',async()=>{
  for(const bad of [{action:'catalog',catalog:{operation:'inspect',capability:'missing-version'}},{action:'catalog',catalog:{operation:'search',category:'guides',query:'program',offset:0}}]){
   const ai=client([[reads[0],bad],[]]),execute=vi.fn(),beforeDispatch=vi.fn();
   await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{},{beforeDispatch})).rejects.toThrow();
   expect(execute).not.toHaveBeenCalled();expect(beforeDispatch).not.toHaveBeenCalled();
  }
 });
 it.each(['oversized','mixed'] as const)('returns bounded repair feedback for a %s proposal without dispatch',async(kind)=>{
  const bad=kind==='oversized'?[...reads,reads[0]]:[reads[0],{action:'workspace',visible:true}];
  const ai=client([bad,[reads[0]],[]]);const execute=vi.fn(async()=>({...scene,ack:1}));
  await runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{});
  expect(execute).toHaveBeenCalledOnce();expect(execute).toHaveBeenCalledWith([reads[0]],4,[]);
  expect(payload(ai,1).tutorContext.planRejection).toBeDefined();expect(payload(ai,1).budget.queryBatches).toBe(24);
 });
 it('charges every native read and keeps the action allowance after discovery is exhausted',async()=>{
  const edit:RoomCommand={action:'workspace',visible:true};const plans=[...Array.from({length:6},()=>reads),[edit],[]];
  const ai=client(plans);let current={...scene};const execute=vi.fn(async()=>{current={...current,ack:current.ack+1};return current;});
  const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
  expect(execute).toHaveBeenCalledTimes(25);expect(result.budgetExhausted).toBe(false);
  expect(payload(ai,6).budget).toEqual({planningCalls:26,queryBatches:0,actionBatches:3});
  expect(payload(ai,7).budget.actionBatches).toBe(2);
 });
 it('refuses the whole next group if remaining query allowance cannot cover it',async()=>{
  const ai=client([...Array.from({length:5},()=>reads),reads.slice(0,3),reads.slice(0,2)]);
  const execute=vi.fn(async()=>scene),beforeDispatch=vi.fn();
  const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{},{beforeDispatch});
  expect(execute).toHaveBeenCalledTimes(23);expect(beforeDispatch).toHaveBeenCalledTimes(23);expect(result.budgetExhausted).toBe(true);
 });
});
