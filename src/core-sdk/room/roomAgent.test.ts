// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it,vi} from 'vitest';
import {parseRoomCommands,runRoomTutorTurn,type RoomAgentState} from './roomAgent';
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
  expect(execute).toHaveBeenCalledTimes(1);expect(execute.mock.calls[0]).toMatchObject([[{kind:'boxRobot'}],4]);
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
