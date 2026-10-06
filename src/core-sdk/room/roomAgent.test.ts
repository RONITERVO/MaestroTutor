import nativeEditProgram from '../../../test-fixtures/browser/objectEditProgram.json';
import {sequenceProgram} from './programs';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,expect,it,vi} from 'vitest';
import {parseRoomCommands,runRoomActionTask,runRoomTutorTurn,type RoomAgentState,type RoomCommand} from './roomAgent';
import {ROOM_AGENT_SCHEMA,ROOM_AGENT_RESPONSE_SCHEMA,ROOM_HANDOFF_TUTOR_INSTRUCTION,ROOM_HANDOFF_LIVE_INSTRUCTION} from '../../../shared/prompts';
import {ROOM_TASK_LIMITS} from '../../../shared/roomTaskBudget';
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
  expect(requests[0][0].config.responseJsonSchema).toEqual(ROOM_AGENT_RESPONSE_SCHEMA);
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

it('shows current room plus prior evidence to a follow-up and refuses blind mutation after an unconfirmed action', async () => {
  const relatedTask = { id: 'parent', action: 'continue' as const, phase: 'interrupted' as const, note: 'Unknown outcome',
    requests: ['Make a blue robot.'], reply: '', operations: [{ commands: [{ action: 'workspace' as const, visible: true }], sceneRevision: 1 }], wasRunning: false, unconfirmed: true };
  const ai = client(['{"commands":[{"action":"workspace","visible":true}]}']), execute = vi.fn();
  const result = await runRoomActionTask(input, { aiClient: ai }, { state: () => scene, valid: () => true, execute }, () => {}, { relatedTask });
  expect(result.needsReview).toBe(true); expect(execute).not.toHaveBeenCalled();
  const request: any = (ai.models.generateContentStream.mock.calls as any)[0][0];
  const data = JSON.parse(request.contents[0].parts[0].text);
  expect(data.request).toBe(input.prompt); expect(data.tutorContext.relatedTask).toEqual(relatedTask); expect(data.scene).toEqual(scene);
});

it('uses live runtime observations after a start without claiming future movement completed',async()=>{
 let current:RoomAgentState={...scene,capabilities:['avatarMotion.v1'],avatar:{active:false,mode:'stopped',status:'Ready',canLook:true,canFollow:true,lookReason:'',followReason:'',distance:1.3,speed:.65}};
 const ai=client(['{"commands":[{"action":"avatarMotion","target":"maestro","operation":"follow"}]}','{"commands":[]}']);
 const execute=vi.fn(async()=>({...current,ack:1,status:'Maestro follow started',avatar:{...current.avatar!,active:true,mode:'follow' as const,status:'Following'}}));
 const task=await runRoomActionTask({...input,prompt:'Follow me'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{},{onReceipt:()=>{current={...current,avatar:{...current.avatar!,active:true,mode:'follow',status:'Path blocked by book'}};}});
 expect(task.receipts[0].status).toContain('started');expect(task.scene.avatar?.status).toContain('blocked');
 const request:any=(ai.models.generateContentStream.mock.calls as any)[1][0];
 expect(JSON.parse(request.contents[0].parts[0].text).scene.avatar.status).toContain('blocked');
});
it('does not persist an action intent or dispatch a new capability to an older native runtime',async()=>{
 const ai=client(['{"commands":[{"action":"physicsRun","operation":"start"}]}']),execute=vi.fn(),beforeDispatch=vi.fn();
 await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{},{beforeDispatch})).rejects.toThrow('does not support');
 expect(execute).not.toHaveBeenCalled();expect(beforeDispatch).not.toHaveBeenCalled();
});

it('discovers native motion identities before saving an animation through the existing agent task',async()=>{
 let current:RoomAgentState={...scene,capabilities:['motions.v1','behaviourPrograms.v3']};
 const motionId='b'.repeat(32),query={query:'wave',offset:0,includeShort:false,favouritesOnly:false,archivedOnly:false};
 const search={action:'motions',target:'maestro',motionQuery:query};
 const save={action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'wave',sequence:{id:'',name:'Wave',interruption:0,repeat:false,program:JSON.stringify(sequenceProgram([{id:'wave_motion',action:7,targetId:'maestro',gesture:0,seconds:0,loop:false,motionId}]))}}]}};
 const ai=client([JSON.stringify({commands:[search]}),JSON.stringify({commands:[save]}),'{"commands":[]}']);
 const execute=vi.fn(async(commands:any[])=>{
  if(commands[0].action==='motions')current={...current,ack:1,status:'Found compatible wave',motions:{targetId:'maestro',modelHash:'c'.repeat(64),ready:true,status:'Found',query,offset:0,total:1,pageSize:12,entries:[{id:motionId,name:'Friendly wave',tags:['greeting'],duration:2,shortClip:false,favourite:false,archived:false,downloaded:true}]}};
  else current={...current,ack:2,status:'Behavior saved',created:['d'.repeat(32)]};
  return current;
 });
 const result=await runRoomActionTask({...input,prompt:'Create a waving action using my saved Friendly wave animation'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute.mock.calls.map(call=>call[0])).toEqual([[search],[save]]);expect(result.receipts).toHaveLength(2);expect(result.budgetExhausted).toBe(false);
 const second:any=(ai.models.generateContentStream.mock.calls as any)[1][0];expect(JSON.parse(second.contents[0].parts[0].text).scene.motions.entries[0].id).toBe(motionId);expect(ai.live.connect).not.toHaveBeenCalled();
});

it('assigns a discovered walking motion without starting follow and reports native availability',async()=>{
 const motionId='b'.repeat(32),command={action:'avatarWalk',target:'maestro',motionId};
 let current:RoomAgentState={...scene,capabilities:['avatarWalk.v1'],walk:{source:'included',motionId:'',clipIndex:-1,modelHash:'',name:'Included walk',available:true,status:'Selected',playbackStatus:''}};
 const ai=client([JSON.stringify({commands:[command]}),'{"commands":[]}']);
 const execute=vi.fn(async()=>current={...current,ack:1,walk:{...current.walk!,source:'library',motionId,name:'Walking',available:false,status:'Download no longer available'}});
 const result=await runRoomActionTask({...input,prompt:'Use the walking animation we found'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute.mock.calls).toHaveLength(1);expect(execute).toHaveBeenCalledWith([command],4,scene.objects);expect(result.scene.walk?.available).toBe(false);
 const next:any=(ai.models.generateContentStream.mock.calls as any)[1][0];expect(JSON.parse(next.contents[0].parts[0].text).scene.walk.status).toBe('Download no longer available');
});

it('uses the shared activity revision and returns rejection evidence without replaying a profile edit',async()=>{
 const profile={modelHash:'f'.repeat(64),revision:2,status:'Ready',canAssign:true,readOnly:false,canUndo:true,canRedo:false,roles:[0,1,2,3].map(role=>({role,choices:[]}))};
 let current:RoomAgentState={...scene,capabilities:['avatarActivities.v1'],activityProfile:profile};
 const command={action:'avatarActivities',activities:{modelHash:profile.modelHash,revision:profile.revision,operation:'edit',edits:[{operation:'assign',role:3,choice:{motionId:'b'.repeat(32),weight:1,speed:1,cooldown:0,loop:true}}]}};
 const ai=client([JSON.stringify({commands:[command]}),'{"commands":[]}']);
 const execute=vi.fn(async()=>current={...current,ack:1,ok:false,status:'Tutor-state assignments changed',activityProfile:{...profile,revision:3}});
 const result=await runRoomActionTask({...input,prompt:'Use that saved animation while speaking'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute).toHaveBeenCalledOnce();expect(execute).toHaveBeenCalledWith([command],4,scene.objects);expect(result.receipts[0].ok).toBe(false);
 const next:any=(ai.models.generateContentStream.mock.calls as any)[1][0];const data=JSON.parse(next.contents[0].parts[0].text);
 expect(data.scene.activityProfile.revision).toBe(3);expect(data.receipts[0].status).toContain('changed');
});

it('discovers and checks a capability before saving while keeping a separate bounded query allowance',async()=>{
 const catalogCommands=[
  {action:'catalog',catalog:{operation:'search',query:'wait',offset:0}},
  {action:'catalog',catalog:{operation:'inspect',capability:'time.wait',version:1}},
  {action:'catalog',catalog:{operation:'check',call:{id:'time.wait',version:1,arguments:{seconds:1}}}},
 ];
 const save={action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'wait',sequence:{id:'',name:'Wait',interruption:0,repeat:false,program:JSON.stringify(sequenceProgram([{id:'pause',action:2,targetId:'maestro',seconds:1,gesture:0,loop:false}]))}}]}};
 const plans=[...catalogCommands,save].map(command=>JSON.stringify({commands:[command]}));plans.push('{"commands":[]}');
 const ai=client(plans),current={...scene,capabilities:['catalog.v1','behaviourPrograms.v3']};
 const execute=vi.fn(async(_commands:RoomCommand[])=>({...current,ack:1}));
 const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute).toHaveBeenCalledTimes(4);expect(result.budgetExhausted).toBe(false);
 expect(execute.mock.calls[3][0]).toEqual([save]);
});
it('refuses a proposal over either allowance before journaling or dispatch',async()=>{
 for(const action of [{action:'catalog',catalog:{operation:'search',query:'',offset:0}},{action:'workspace',visible:true}]){
  const ai=client(Array.from({length:10},()=>JSON.stringify({commands:[action]}))),current={...scene,capabilities:['catalog.v1']};
  const execute=vi.fn(async(_commands:RoomCommand[])=>({...current,ack:1})),beforeDispatch=vi.fn(),onReceipt=vi.fn();
  const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{},{beforeDispatch,onReceipt});
  const accepted=action.action==='catalog'?6:3;
  expect(result.budgetExhausted).toBe(true);expect(execute).toHaveBeenCalledTimes(accepted);expect(beforeDispatch).toHaveBeenCalledTimes(accepted);expect(onReceipt).toHaveBeenCalledTimes(accepted);
  expect(ai.models.generateContentStream).toHaveBeenCalledTimes(accepted+1);
 }
});

it('runs a one-off action and inspects its native phase without saving a behaviour',async()=>{
 const runId='c'.repeat(32),call={id:'time.wait',version:1,arguments:{seconds:1}};
 const command={action:'execution',execution:{operation:'start',call}};
 const inspect={action:'execution',execution:{operation:'inspect',runId}};
 const current:RoomAgentState={...scene,capabilities:['execution.v1']};
 const summary={id:runId,capability:call.id,version:1,resources:[],phase:'running' as const,status:'Action running'};
 const ai=client([JSON.stringify({commands:[command]}),JSON.stringify({commands:[inspect]}),'{"commands":[]}']);
 let count=0;
 const execute=vi.fn(async()=>({...current,ack:++count,execution:{selected:{...summary,call,phase:count===1?'running' as const:'completed' as const},running:count===1?[summary]:[],outcomes:count===1?[]:[{...summary,phase:'completed' as const}]}}));
 const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute).toHaveBeenCalledTimes(2);expect(result.receipts[0].execution!.selected!.phase).toBe('running');
 expect(result.receipts[1].execution!.selected!.phase).toBe('completed');expect(result.budgetExhausted).toBe(false);
 const next:any=(ai.models.generateContentStream.mock.calls as any)[1][0];expect(JSON.parse(next.contents[0].parts[0].text).receipts[0].execution.selected.id).toBe(runId);
});
it('permits an exact execution inspection after an unconfirmed turn but never retries the start',async()=>{
 const runId='c'.repeat(32),relatedTask={id:'parent',action:'continue' as const,phase:'interrupted' as const,note:'Unknown start',requests:['Wave'],reply:'',operations:[],wasRunning:false,unconfirmed:true};
 const commands=[{action:'execution',execution:{operation:'inspect',runId}},{action:'execution',execution:{operation:'start',call:{id:'time.wait',version:1,arguments:{seconds:1}}}}];
 const ai=client(commands.map(command=>JSON.stringify({commands:[command]}))),execute=vi.fn(async()=>({...scene,ack:1,status:'Unknown action'}));
 const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>({...scene,capabilities:['execution.v1']}),valid:()=>true,execute},()=>{},{relatedTask});
 expect(execute).toHaveBeenCalledOnce();expect(result.needsReview).toBe(true);
});

it('journals the exact issued native identity before an action can lose its acknowledgement',async()=>{
 const id='d'.repeat(32),call={id:'time.wait',version:1,arguments:{seconds:1}};
 const ai=client([JSON.stringify({commands:[{action:'execution',execution:{operation:'start',call}}]})]);
 const current:RoomAgentState={...scene,capabilities:['execution.v1','executionReceipts.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:id,storageError:null}};
 let journal:RoomCommand[]=[];
 const beforeDispatch=vi.fn(async(commands:RoomCommand[])=>{journal=structuredClone(commands);});
 const execute=vi.fn(async(commands:RoomCommand[])=>{
  expect(journal).toEqual(commands);expect(journal[0].execution).toMatchObject({runId:id});
  throw new DOMException('Connection lost','AbortError');
 });
 await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{},{beforeDispatch})).rejects.toThrow('Connection lost');
 expect(execute).toHaveBeenCalledTimes(1);expect(beforeDispatch).toHaveBeenCalledTimes(1);
});

it('lets the original-app agent save and trigger the native-tested creation/edit chain through existing tools',async()=>{
 const programId='f'.repeat(32);
 const save:RoomCommand={action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'redBall',sequence:{id:'',name:'Make a red ball',interruption:0,repeat:false,program:JSON.stringify(nativeEditProgram)}}]}};
 const play:RoomCommand={action:'rules',rule:{action:'play',revision:2,target:programId}};
 const ai=client([JSON.stringify({commands:[save]}),JSON.stringify({commands:[play]}),'{"commands":[]}']);
 let current:RoomAgentState={...scene,capabilities:['behaviourPrograms.v3','eventPrograms.v1','actionResults.v1','objectEdits.v1']};
 const execute=vi.fn(async(commands:RoomCommand[])=>{current={...current,revision:current.revision+1,ack:current.ack+1,created:commands[0].rule?.action==='edit'?[programId]:[],status:'Native-test fixture accepted'};return current;});
 const result=await runRoomActionTask({...input,prompt:'Make a red ball, enlarge it, and place it in front of me.'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute.mock.calls.map(call=>call[0])).toEqual([[save],[play]]);
 expect(result.receipts).toHaveLength(2);expect(result.budgetExhausted).toBe(false);
 expect(ai.live.connect).not.toHaveBeenCalled();
});

it('lets an explicitly requested recovery use the existing agent path without replaying any action',async()=>{
 const recoveryId='e'.repeat(32),command:RoomCommand={action:'execution',execution:{operation:'recover',recoveryId}};
 const ai=client([JSON.stringify({commands:[command]}),'{"commands":[]}']);
 let current:RoomAgentState={...scene,capabilities:['execution.v1','executionReceipts.v1','actionRecovery.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:null,storageError:'History unavailable',recovery:{id:recoveryId,status:'Archive and recover; no replay'}}};
 const execute=vi.fn(async(_commands:RoomCommand[])=>{current={...current,revision:2,ack:1,status:'Recovered without replay',execution:{selected:null,running:[],outcomes:[],nextRunId:'f'.repeat(32),storageError:null,recovery:null}};return current;});
 const result=await runRoomActionTask({...input,prompt:'Recover the damaged action history. Do not retry my earlier actions.'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute).toHaveBeenCalledTimes(1);expect(execute.mock.calls[0][0]).toEqual([command]);expect(result.receipts[0].execution?.recovery).toBeNull();
 expect(ai.live.connect).not.toHaveBeenCalled();
});
it('does not use recovery to bypass an unconfirmed earlier task',async()=>{
 const command={action:'execution',execution:{operation:'recover',recoveryId:'e'.repeat(32)}};
 const ai=client([JSON.stringify({commands:[command]})]),execute=vi.fn();
 const relatedTask={id:'parent',action:'continue' as const,phase:'interrupted' as const,note:'Unknown start',requests:['Wave'],reply:'',operations:[],wasRunning:false,unconfirmed:true};
 const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>({...scene,capabilities:['execution.v1','actionRecovery.v1']}),valid:()=>true,execute},()=>{},{relatedTask});
 expect(execute).not.toHaveBeenCalled();expect(result.needsReview).toBe(true);
});

import {behaviourEvent} from '../../../shared/behaviourEvents';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import contactProgram from '../../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-contact.json';
it('discovers event payloads and live facts before saving a program without starting it',async()=>{
 const queries:RoomCommand[]=[
  {action:'catalog',catalog:{operation:'search',category:'events',query:'contact',offset:0}},
  {action:'catalog',catalog:{operation:'inspect',category:'events',capability:'object.collided',version:1}},
  {action:'catalog',catalog:{operation:'search',category:'facts',query:'ready',offset:0}},
  {action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'physics.ready',version:1}},
 ];
 const save:RoomCommand={action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'contact',sequence:{id:'',name:'Observe contacts',interruption:0,repeat:false,program:JSON.stringify(contactProgram)}}]}};
 const ai=client([...queries,save].map(command=>JSON.stringify({commands:[command]})).concat('{"commands":[]}'));
 let current:RoomAgentState={...scene,capabilities:['catalog.v1','catalogVocabulary.v1','behaviourPrograms.v3','eventPrograms.v1','eventFields.v1']};
 const execute=vi.fn(async(commands:RoomCommand[])=>{
  const query=commands[0].catalog;
  if(query&&query.operation!=='check') {
   const definition=query.category==='events'?behaviourEvent('object.collided')!:behaviourFact('physics.ready')!;
   current={...current,ack:current.ack+1,catalog:query.operation==='search'?{...query,pageSize:6,total:1,entries:[{id:definition.id,version:1,label:definition.label}],status:'Test search'}:
    query.category==='events'?{...query,category:'events',definition:behaviourEvent(query.capability),status:'Test event schema'}:
    {...query,category:'facts',definition:behaviourFact(query.capability),available:true,value:false,status:'Test readiness'}};
  }else current={...current,ack:current.ack+1,status:'Saved without starting'};
  return current;
 });
 const result=await runRoomActionTask({...input,prompt:'Prepare a contact observer; do not start it yet'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute.mock.calls.map(x=>x[0])).toEqual([...queries,save].map(x=>[x]));expect(result.budgetExhausted).toBe(false);
 const requests=ai.models.generateContentStream.mock.calls as unknown as [any][];
 expect(JSON.parse(requests[2][0].contents[0].parts[0].text).scene.catalog.definition.fields.properties.speed.type).toBe('number');
 expect(JSON.parse(requests[4][0].contents[0].parts[0].text).scene.catalog).toMatchObject({category:'facts',available:true,value:false});
 expect(ai.live.connect).not.toHaveBeenCalled();
});

import proximityProgram from '../../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-proximity.json';
it('inspects native subscription parameters then saves the same program without activating it',async()=>{
 const query:RoomCommand={action:'catalog',catalog:{operation:'inspect',category:'events',capability:'object.proximity.changed',version:1}};
 const save:RoomCommand={action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'near',sequence:{id:'',name:'Near the book',interruption:0,repeat:false,program:JSON.stringify(proximityProgram)}}]}};
 const ai=client([query,save].map(command=>JSON.stringify({commands:[command]})).concat('{"commands":[]}'));
 let current:RoomAgentState={...scene,capabilities:['catalog.v1','catalogVocabulary.v1','behaviourPrograms.v3','eventPrograms.v1','eventFields.v1','eventSubscriptions.v1']};
 const execute=vi.fn(async(commands:RoomCommand[])=>{
  current={...current,ack:current.ack+1,status:commands[0].action==='catalog'?'Definition':'Saved without starting',catalog:{operation:'inspect',category:'events',capability:'object.proximity.changed',version:1,definition:behaviourEvent('object.proximity.changed'),status:'Test native definition'}};return current;
 });
 const result=await runRoomActionTask({...input,prompt:'Prepare a behaviour that notices when Maestro comes near the book. Leave it stopped.'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute.mock.calls.map(x=>x[0])).toEqual([[query],[save]]);expect(result.budgetExhausted).toBe(false);
 const requests=ai.models.generateContentStream.mock.calls as unknown as [any][];
 expect(JSON.parse(requests[1][0].contents[0].parts[0].text).scene.catalog.definition.input.properties.radius.maximum).toBe(10);
 expect(result.scene.status).toBe('Saved without starting');
});

import objectFactProgram from '../../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-object-facts.json';
import conditionProgram from '../../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-conditions.json';
it.each([objectFactProgram,conditionProgram])('passes queried object records back to the existing agent and saves the shared observer without starting it',async(program)=>{
 const inspect:RoomCommand={action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.position',version:1}};
 const read:RoomCommand={action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.position',version:1,arguments:{target:'book'}}};
 const save:RoomCommand={action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'position',sequence:{id:'',name:'Observe position',interruption:0,repeat:false,program:JSON.stringify(program)}}]}};
 const ai=client([inspect,read,save].map(command=>JSON.stringify({commands:[command]})).concat('{"commands":[]}'));
 let current:RoomAgentState={...scene,capabilities:['catalog.v1','catalogVocabulary.v1','factQueries.v1','behaviourPrograms.v3','eventPrograms.v1','structuredValues.v1','conditionWaits.v1']};
 const execute=vi.fn(async(commands:RoomCommand[])=>{
  const query=commands[0].catalog;
  if(query?.operation==='inspect')current={...current,ack:current.ack+1,catalog:{...query,category:'facts',definition:behaviourFact(query.capability),available:query.arguments!==undefined,value:query.arguments?{x:0,y:1.2,z:-.4}:null,status:'Simulated native query'}};
  else current={...current,ack:current.ack+1,status:'Saved without starting'};return current;
 });
 const result=await runRoomActionTask({...input,prompt:'Read the book position and prepare a position-change observer. Leave it stopped.'},{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute.mock.calls.map(x=>x[0])).toEqual([[inspect],[read],[save]]);expect(result.budgetExhausted).toBe(false);
 const requests=ai.models.generateContentStream.mock.calls as unknown as [{contents:{parts:{text:string}[]}[]}][];
 expect(JSON.parse(requests[1][0].contents[0].parts[0].text).scene.catalog.definition.type).toEqual({record:{x:'number',y:'number',z:'number'}});
 expect(JSON.parse(requests[2][0].contents[0].parts[0].text).scene.catalog).toMatchObject({arguments:{target:'book'},available:true,value:{x:0,y:1.2,z:-.4}});
 expect(result.scene.status).toBe('Saved without starting');expect(ai.live.connect).not.toHaveBeenCalled();
});


it('can use discovered capabilities after six queries without increasing the total planning budget',async()=>{
 const query:RoomCommand={action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1}};
 const create:RoomCommand={action:'create',reference:'ball',name:'Ball',kind:'ball'};
 const paint:RoomCommand={action:'paint',target:'a'.repeat(32),color:{r:0,g:1,b:0,a:1}};
 const commands=[...Array.from({length:6},()=>query),create,paint];
 const ai=client([...commands.map(command=>JSON.stringify({commands:[command]})),'{"commands":[]}']);
 let current={...scene,capabilities:['catalog.v1']};
 const execute=vi.fn(async(_commands:RoomCommand[])=>{current={...current,revision:current.revision+1,ack:current.ack+1};return current;});
 const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute.mock.calls.map(call=>call[0])).toEqual(commands.map(command=>[command]));expect(result.budgetExhausted).toBe(false);
 const requests=ai.models.generateContentStream.mock.calls as unknown as [any][];
 expect(requests).toHaveLength(ROOM_TASK_LIMITS.planningCalls);
 const payload=(index:number)=>JSON.parse(requests[index][0].contents[0].parts[0].text);
 expect(payload(0).budget).toEqual({planningCalls:9,queryBatches:6,actionBatches:3});
 expect(payload(6).budget).toEqual({planningCalls:3,queryBatches:0,actionBatches:3});
 expect(payload(8).budget).toEqual({planningCalls:1,queryBatches:0,actionBatches:1});
 expect(payload(8).receipts).toHaveLength(8);expect(payload(8).scene.ack).toBe(8);
});

it('can inspect the actual result after its third action instead of returning an unchecked start',async()=>{
 const runId='b'.repeat(32),call={id:'time.wait',version:1,arguments:{seconds:1}};
 const commands:RoomCommand[]=[{action:'workspace',visible:true},{action:'workspace',visible:false},
  {action:'execution',execution:{operation:'start',call}}, {action:'execution',execution:{operation:'inspect',runId}}];
 const ai=client([...commands.map(command=>JSON.stringify({commands:[command]})),'{"commands":[]}']);
 let current:RoomAgentState={...scene,capabilities:['execution.v1']};
 const execute=vi.fn(async(batch:RoomCommand[])=>{
  const operation=batch[0].execution?.operation;
  const summary={id:runId,capability:call.id,version:1,phase:operation==='inspect'?'completed' as const:'running' as const,status:operation==='inspect'?'Finished waiting':'Waiting',resources:[]};
  current={...current,revision:current.revision+1,ack:current.ack+1,...(operation?{execution:{selected:{...summary,call},running:operation==='inspect'?[]:[summary],outcomes:operation==='inspect'?[summary]:[]}}:{})};return current;
 });
 const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{});
 expect(execute).toHaveBeenCalledTimes(4);expect(result.budgetExhausted).toBe(false);
 expect(result.receipts[2].execution!.selected!.phase).toBe('running');expect(result.scene.execution!.selected!.phase).toBe('completed');
 const fourth:any=(ai.models.generateContentStream.mock.calls as any)[3][0];
 expect(JSON.parse(fourth.contents[0].parts[0].text).budget).toEqual({planningCalls:6,queryBatches:6,actionBatches:0});
});

it('ends at nine planning calls even when both batch allowances are used exactly',async()=>{
 const query:RoomCommand={action:'catalog',catalog:{operation:'search',query:'create',offset:0}};
 const action:RoomCommand={action:'workspace',visible:true};
 const commands=[query,action,query,action,query,action,query,query,query];
 const ai=client([...commands.map(command=>JSON.stringify({commands:[command]})),'{"commands":[]}']);
 const execute=vi.fn(async()=>({...scene,capabilities:['catalog.v1'],ack:1}));
 const result=await runRoomActionTask(input,{aiClient:ai},{state:()=>({...scene,capabilities:['catalog.v1']}),valid:()=>true,execute},()=>{});
 expect(result.receipts).toHaveLength(9);expect(result.budgetExhausted).toBe(true);expect(ai.models.generateContentStream).toHaveBeenCalledTimes(9);
});

it('honours cancellation at the discovery boundary before using the remaining action allowance',async()=>{
 const query={action:'catalog',catalog:{operation:'search',query:'create',offset:0}};
 const ai=client([...Array.from({length:6},()=>JSON.stringify({commands:[query]})),'{"commands":[{"action":"workspace","visible":true}]}']);
 const controller=new AbortController(),current={...scene,capabilities:['catalog.v1']};let acknowledged=0;
 const execute=vi.fn(async()=>current);
 await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>current,valid:()=>true,execute},()=>{},{signal:controller.signal,onReceipt:()=>{if(++acknowledged===6)controller.abort();}})).rejects.toMatchObject({name:'AbortError'});
 expect(execute).toHaveBeenCalledTimes(6);expect(ai.models.generateContentStream).toHaveBeenCalledTimes(6);
});

it('uses catalog-based narration and native import handoffs without promising picker completion',async()=>{
 const ai=client(['{"commands":[]}','Hola.\n[EN]Hello.']);
 await runRoomTutorTurn(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute:vi.fn()},()=>{});
 const request:any=(ai.models.generateContentStream.mock.calls as any)[1][0];
 expect(request.config.systemInstruction).toContain('advertised native catalog');
 expect(request.config.systemInstruction).toContain('typed output determines the actual result');
 expect(request.config.systemInstruction).toContain('File-picker selection and platform permission screens require the user');
 expect(request.config.systemInstruction).not.toContain('Other room tools remain manual for now');
 for(const guide of [ROOM_HANDOFF_TUTOR_INSTRUCTION,ROOM_HANDOFF_LIVE_INSTRUCTION]){
  expect(guide).toContain('shared native catalog');expect(guide).toContain('system');expect(guide).toContain('file paths');expect(guide).toContain('Only one room task');
  expect(guide).not.toMatch(/(?:Importing files|File import, account changes).*not (?:available|supported)/);
 }
});

describe('planner response schema compatibility',()=>{
 it('keeps native limits enforced even when structured decoding omits array bounds',()=>{
  expect(ROOM_AGENT_SCHEMA.properties.commands.maxItems).toBe(8);
  const encoded=JSON.stringify(ROOM_AGENT_RESPONSE_SCHEMA);
  expect(encoded).not.toMatch(/"(?:minItems|maxItems)":/);
  expect(encoded).toContain('Array length must be 0..8');
  expect(()=>parseRoomCommands({commands:Array.from({length:9},()=>({action:'create',reference:'r',name:'Ball',kind:'ball'}))})).toThrow(/too large/);
 });
 it('rejects an oversized provider plan before journaling or native dispatch',async()=>{
  const ai=client([JSON.stringify({commands:Array.from({length:9},()=>({action:'create',reference:'r',name:'Ball',kind:'ball'}))})]);
  const execute=vi.fn(),beforeDispatch=vi.fn();
  await expect(runRoomActionTask(input,{aiClient:ai},{state:()=>scene,valid:()=>true,execute},()=>{},{beforeDispatch})).rejects.toThrow(/too large/);
  expect(beforeDispatch).not.toHaveBeenCalled();expect(execute).not.toHaveBeenCalled();
 });
});


describe('program save provider schema', () => {
 it('requires the creation reference the real program response omitted', () => {
  const schema = ROOM_AGENT_RESPONSE_SCHEMA as any;
  const rules = schema.properties.commands.items.anyOf.find((item: any) => item.properties.action.enum[0] === 'rules').properties.rule.anyOf;
  const edit = rules.find((item: any) => item.properties.action.enum[0] === 'edit');
  expect(edit.required).toEqual(['action', 'revision', 'edits']);
  const saves = edit.properties.edits.items.anyOf.filter((item: any) => item.properties.kind.enum[0] === 'save');
  const fresh = saves.find((item: any) => item.properties.sequence.properties.id.enum?.[0] === '');
  expect(fresh.required).toEqual(['kind', 'reference', 'sequence']);
  expect(fresh.properties.reference.pattern).toBe('^[a-zA-Z0-9_]{1,32}$');
  const existing = saves.find((item: any) => item.properties.sequence.properties.id.pattern);
  expect(existing.required).toEqual(['kind', 'sequence']);
  expect(existing.properties.sequence.properties.id.pattern).toBe('^[a-f0-9]{32}$');
  const program = JSON.stringify({ version: 2, entry: 'main', resources: [], functions: [{ name: 'main', returns: 'void', parameters: [], locals: [], body: [] }] });
  const sequence = { id: '', name: 'ParitySignal', interruption: 0, repeat: false, program };
  const command = (edit: unknown) => ({ commands: [{ action: 'rules', rule: { action: 'edit', revision: 1, edits: [edit] } }] });
  expect(() => parseRoomCommands(command({ kind: 'save', sequence }))).toThrow();
  expect(parseRoomCommands(command({ kind: 'save', reference: 'parity_signal', sequence }))).toHaveLength(1);
  expect(parseRoomCommands(command({ kind: 'save', sequence: { ...sequence, id: 'a'.repeat(32) } }))).toHaveLength(1);
  for (const action of ['play', 'signal', 'undo', 'redo']) expect(rules.find((item: any) => item.properties.action.enum[0] === action).required).toContain('revision');
  expect(rules.find((item: any) => item.properties.action.enum[0] === 'play').required).toContain('target');
  expect(rules.find((item: any) => item.properties.action.enum[0] === 'signal').required).toEqual(['action', 'revision', 'eventName', 'value']);
 });
});
