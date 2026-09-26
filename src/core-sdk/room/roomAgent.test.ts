// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it,vi} from 'vitest';
import {parseRoomCommands,runRoomActionTask,runRoomTutorTurn,type RoomAgentState} from './roomAgent';
import {ROOM_AGENT_SCHEMA} from '../../../shared/prompts';
const scene:RoomAgentState={version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',objects:[],created:[],canUndo:false,canRedo:false,physicsRunning:false};
const input={model:'gemini-3.8-flash',prompt:'Make a robot and have it wave.',history:[],nativeLanguageCode:'en',systemInstruction:'Tutor fixture'};
function client(outputs:string[]) {
 return {models:{generateContent:vi.fn(),generateContentStream:vi.fn(async()=>{
  const text=outputs.shift();return (async function*(){yield {text,usageMetadata:{promptTokenCount:10,candidatesTokenCount:8},candidates:[{content:{role:'model',parts:[{text}]}}]};})();
 })},live:{connect:vi.fn(),music:{connect:vi.fn()}}};
}
describe('shared room tutor journey',()=>{
 it('executes structured actions, reads receipts, then uses the ordinary tutor response path',async()=>{
  const ai=client([JSON.stringify({commands:[{action:'create',reference:'robot',name:'Robot',kind:'boxRobot'}]}),'{"commands":[]}','Hola.\n[EN]Hello.']);
  const execute=vi.fn(async()=>({...scene,ack:1,sceneRevision:5,ok:true,status:'Created robot',created:['b'.repeat(32)]}));const usage=vi.fn();
  const result=await runRoomTutorTurn(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},usage);
  expect(execute).toHaveBeenCalledTimes(1);expect(execute.mock.calls[0]).toMatchObject([[{kind:'boxRobot'}],4,scene.objects]);
  expect(usage).toHaveBeenCalledTimes(2);expect(result.rawResponse).toContain('Hola');
  const requests=ai.models.generateContentStream.mock.calls as unknown as [any][];
  expect(requests[0][0].config.responseJsonSchema).toEqual(ROOM_AGENT_SCHEMA);
  expect(requests[2][0].config.systemInstruction).toContain('Created robot');
  expect(requests[0][0].config.tools).toBeUndefined();
 });
 it('uses the supplied access resolver and cannot apply a late model plan',async()=>{
  let valid=true;const ai=client(['{"commands":[]}']);const resolve=vi.fn(async()=>{valid=false;return ai;});const execute=vi.fn();
  await expect(runRoomTutorTurn(input,{resolveAiClient:resolve},{state:()=>scene,valid:()=>valid,execute},()=>{})).rejects.toThrow('interrupted');
  expect(resolve).toHaveBeenCalledOnce();expect(execute).not.toHaveBeenCalled();
 });
 it('rejects code-like fields, unknown actions, nonfinite coordinates and mixed undo',()=>{
  expect(()=>parseRoomCommands({commands:[{action:'eval',code:'x'}]})).toThrow();
  expect(()=>parseRoomCommands({commands:[{action:'move',target:'book',position:{x:NaN,y:1,z:1}}]})).toThrow();
  expect(()=>parseRoomCommands({commands:[{action:'create',reference:'r',name:'Robot',kind:'boxRobot',code:'x'}]})).toThrow();
  expect(()=>parseRoomCommands({commands:[{action:'undo'},{action:'redo'}]})).toThrow();
  expect(parseRoomCommands({commands:[]})).toEqual([]);
 });
});


describe('app-owned room tool task',()=>{
 it('returns native results independently of a tutor or Live speech response',async()=>{
  const ai=client(['{"commands":[{"action":"create","reference":"r","name":"Robot","kind":"boxRobot"}]}','{"commands":[]}']);
  const acknowledgement={...scene,ack:1,ok:true,status:'Created robot',created:['b'.repeat(32)]};
  const execute=vi.fn(async()=>acknowledgement),saved:RoomAgentState[]=[];
  const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{},{onReceipt:receipt=>{saved.push(receipt);}});
  expect(result.receipts).toEqual([acknowledgement]);expect(saved).toEqual([acknowledgement]);
  expect(result.budgetExhausted).toBe(false);expect(ai.models.generateContentStream).toHaveBeenCalledTimes(2);expect(ai.live.connect).not.toHaveBeenCalled();
 });
 it('publishes a raced native acknowledgement before honoring cancellation',async()=>{
  const ai=client(['{"commands":[{"action":"workspace","visible":true}]}']);const controller=new AbortController();const saved=vi.fn();
  const execute=vi.fn(async()=>{controller.abort();return {...scene,ack:1,status:'Workspace opened'};});
  await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{},{signal:controller.signal,onReceipt:saved})).rejects.toMatchObject({name:'AbortError'});
  expect(execute).toHaveBeenCalledWith([{action:'workspace',visible:true}],4,[],controller.signal);
  expect(saved).toHaveBeenCalledWith(expect.objectContaining({ack:1,status:'Workspace opened'}));
  expect(ai.models.generateContentStream).toHaveBeenCalledTimes(1);
 });
 it('retains receipts when a later provider response fails validation',async()=>{
  const ai=client(['{"commands":[{"action":"workspace","visible":true}]}','bad JSON']);const saved=vi.fn();
  await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute:async()=>({...scene,ack:1})},()=>{},{onReceipt:saved})).rejects.toThrow();
  expect(saved).toHaveBeenCalledOnce();
 });
 it('does not plan when the owning conversation has already stopped',async()=>{
  const ai=client([]),execute=vi.fn();const controller=new AbortController();controller.abort();
  await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{},{signal:controller.signal})).rejects.toMatchObject({name:'AbortError'});
  expect(execute).not.toHaveBeenCalled();expect(ai.models.generateContentStream).not.toHaveBeenCalled();
 });
});
