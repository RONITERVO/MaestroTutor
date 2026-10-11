// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect,vi} from 'vitest';
import {decodeRoomPlannerResponse} from './roomPlannerResponse';
import {parseRoomCommands,runRoomActionTask,type RoomAgentState,type RoomCommand} from './roomAgent';
import {ROOM_AGENT_SCHEMA,ROOM_AGENT_RESPONSE_SCHEMA,ROOM_PLANNER_ARGUMENT_GUIDE} from '../../../shared/prompts/room';
const target='a'.repeat(32),args={target,destination:{kind:'point',position:{x:3,y:.7,z:.6}},seconds:.6,maxSpeed:5};
const call={id:'object.physics.launch',version:2,arguments:args};
const encoded=()=>({commands:[{action:'execution',execution:{operation:'start',call:{...call,arguments:JSON.stringify(args)}}}]});
const scene={version:1,session:'native',revision:1,sceneRevision:1,ack:0,ok:true,status:'Ready',created:[],objects:[],canUndo:false,canRedo:false,physicsRunning:true,capabilities:['execution.v1','objectLaunch.v1','actionResults.v1']} as RoomAgentState;
function provider(outputs:unknown[]){
 return {models:{generateContent:vi.fn(),generateContentStream:vi.fn(async()=>{
  const text=JSON.stringify(outputs.shift());return (async function*(){yield {text,usageMetadata:{totalTokenCount:10},candidates:[{content:{role:'model',parts:[{text}]}}]};})();
 })},live:{connect:vi.fn(),music:{connect:vi.fn()}}};
}
describe('provider-only argument encoding',()=>{
 it('uses strings in provider decoding while preserving the canonical native and program formats',()=>{
  const native=ROOM_AGENT_SCHEMA.properties.commands.items.properties;
  expect(native.execution.properties.call.properties.arguments.type).toBe('object');
  expect(native.catalog.properties.arguments.type).toBe('object');
  const s=ROOM_AGENT_RESPONSE_SCHEMA as typeof ROOM_AGENT_SCHEMA & {properties:{commands:{items:{anyOf:Array<{properties:any}>}}}};
  const variants=s.properties.commands.items.anyOf;
  expect(variants.find(v=>v.properties.action.enum[0]==='execution')!.properties.execution.properties.call.properties.arguments.type).toBe('string');
  const catalog=variants.find(v=>v.properties.action.enum[0]==='catalog')!.properties.catalog.anyOf;
  expect(catalog.find((v:any)=>v.properties.operation.enum[0]==='inspect').properties.arguments.type).toBe('string');
  expect(catalog.find((v:any)=>v.properties.operation.enum[0]==='check').properties.call.properties.arguments.type).toBe('string');
  expect(ROOM_PLANNER_ARGUMENT_GUIDE).toContain(JSON.stringify({arguments:JSON.stringify({target:'exact-id',position:{x:1,y:2,z:3}})}));
 });
 it('decodes only the three provider slots and retains nested objects, arrays, text and scalar values',()=>{
  const nested={points:[{x:1,y:2,z:3}],enabled:false,zero:0,text:'Unicode: español; quoted "text"'};
  for(const command of [
   {action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.position',version:2,arguments:JSON.stringify(nested)}},
   {action:'catalog',catalog:{operation:'check',call:{...call,arguments:JSON.stringify(nested)}}},
   {action:'execution',execution:{operation:'start',call:{...call,arguments:JSON.stringify(nested)}}}
  ]){
   const decoded=decodeRoomPlannerResponse(JSON.stringify({commands:[command]})) as any;
   expect(decoded.commands[0].catalog?.arguments??decoded.commands[0].catalog?.call.arguments??decoded.commands[0].execution?.call.arguments).toEqual(nested);
  }
  const program={commands:[{action:'rules',rule:{action:'edit',edits:[{sequence:{program:JSON.stringify({arguments:'do not decode'})}}]}}]};
  expect(decodeRoomPlannerResponse(JSON.stringify(program))).toEqual(program);
  expect(decodeRoomPlannerResponse(JSON.stringify({commands:[{action:'execution',execution:{operation:'start',call}}]}))).toEqual({commands:[{action:'execution',execution:{operation:'start',call}}]});
 });
 it('rejects non-object, malformed and oversized encoded payloads without evaluating code',()=>{
  for(const text of ['[]','null','true','123','"text"','(()=>({}))()','{broken']){
   const value=encoded();value.commands[0].execution.call.arguments=text;expect(()=>decodeRoomPlannerResponse(JSON.stringify(value))).toThrow();
  }
  const oversized=encoded();oversized.commands[0].execution.call.arguments=' '.repeat(24001);expect(()=>decodeRoomPlannerResponse(JSON.stringify(oversized))).toThrow(/limit/);
  const unsupported=encoded();unsupported.commands[0].execution.call.id='unsupported.action';
  expect(()=>parseRoomCommands(decodeRoomPlannerResponse(JSON.stringify(unsupported)))).toThrow(/Invalid action execution/); // Decoding grants no new capability support.
 });
 it('journals and dispatches only the decoded canonical command through unchanged validation',async()=>{
  const ai=provider([encoded(),{commands:[]}]),execute=vi.fn<(commands:RoomCommand[])=>Promise<RoomAgentState>>(async()=>({...scene,ack:1})),beforeDispatch=vi.fn();
  await runRoomActionTask({model:'test',prompt:'Throw once',history:[]},{aiClient:ai},{valid:()=>true,state:()=>scene,execute},()=>{},{beforeDispatch});
  expect(execute).toHaveBeenCalledOnce();
  const canonical=[{action:'execution',execution:{operation:'start',call}}];
  expect(beforeDispatch.mock.calls[0][0]).toEqual(canonical);expect(execute.mock.calls[0][0]).toEqual(canonical);
 });
 it('can correct malformed argument JSON before dispatch without replaying native effects',async()=>{
  const broken=encoded();broken.commands[0].execution.call.arguments='{broken';
  const ai=provider([broken,encoded(),{commands:[]}]),execute=vi.fn<(commands:RoomCommand[])=>Promise<RoomAgentState>>(async()=>({...scene,ack:1})),usage=vi.fn();
  await runRoomActionTask({model:'test',prompt:'Throw once',history:[]},{aiClient:ai},{valid:()=>true,state:()=>scene,execute},usage);
  expect(execute).toHaveBeenCalledOnce();expect(usage).toHaveBeenCalledTimes(3);
 });
});
