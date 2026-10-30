// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
// Exercises the real client validation/envelope and
// durable task journal with a scripted transport acknowledgement, not Unity effects.
import {expect,it,vi} from 'vitest';
import referenceFixtures from '../../../test-fixtures/browser/objectReferences.json';
import {RoomAgentClient} from './roomAgentClient';
import {runRoomActionTask,type RoomAgentState,type RoomCommand,type RoomObjectObservation} from './roomAgent';
import {RoomTaskHandoff,type RoomHandoff,type RoomTaskPorts,type RoomTaskRecord} from './roomTaskHandoff';
import type {CatalogRequest,CatalogView} from '../../../shared/roomCatalog';

const target=referenceFixtures.definitions[0].request.arguments.target;
const targetRevision=referenceFixtures.definitions[0].view.value!.revision;
const nextRunId='b'.repeat(32),oldRunId='c'.repeat(32);
const remove=(runId?:string):RoomCommand=>({action:'execution',execution:{operation:'start',
 call:{id:'object.delete',version:1,arguments:{target}},...(runId?{runId}:{})}});
const read:RoomCommand={action:'catalog',catalog:referenceFixtures.definitions[0].request as CatalogRequest};
const builtin=(id:'book'|'maestro'):RoomObjectObservation=>({id,name:id,kind:id==='book'?'Book':'Maestro',
 position:{x:0,y:0,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false,objectRevision:1});
const initial=():RoomAgentState=>({version:1,session:'a'.repeat(32),revision:1,sceneRevision:4,ack:0,ok:true,status:'Ready',
 objects:[builtin('book'),builtin('maestro')],created:[],canUndo:false,canRedo:false,physicsRunning:false,
 capabilities:['execution.v1','executionReceipts.v1','catalog.v1','catalogVocabulary.v1','factQueries.v1'],
 execution:{selected:null,running:[],outcomes:[],nextRunId,storageError:null}});
const handoff:RoomHandoff={version:1,id:'task-preflight',sourceAssistantId:'assistant',sourceUserId:'user',conversationId:'pair',
 nativeSession:initial().session,accessScope:'byok',input:{model:'fixture',prompt:'Remove that saved object.',history:[],systemInstruction:'Fixture',nativeLanguageCode:'en'}};
const reply={rawResponse:'Result.',parsed:{visibleText:'Result.',translations:[],hasSkippedNonLanguageContent:false}};

function harness(plans:RoomCommand[][],options:{legacy?:boolean}={}) {
 const client=new RoomAgentClient();let state=initial();
 if(options.legacy) {
  state={...state,capabilities:['execution.v1'],execution:undefined,
   objects:state.objects.map(({objectRevision:_,...object})=>object)};
 }
 expect(client.receive(state)).toBe(true);const lease=client.lease()!;expect(lease).not.toBeNull();
 const saved=new Map<string,RoomTaskRecord>(),wire:Array<NonNullable<ReturnType<typeof client.snapshot>['request']>>=[];
 let localThrow=false,loseAck=false,loseLeaseOnIntent=false;
 const outputs=plans.map(commands=>JSON.stringify({commands}));
 const ai={models:{generateContent:vi.fn(),generateContentStream:vi.fn(async()=>{
  const text=outputs.shift();expect(text).toBeDefined();
  return(async function*(){yield {text,candidates:[{content:{role:'model',parts:[{text}]}}]};})();
 })}} as any;
 const execute=vi.fn((...args:Parameters<typeof lease.execute>)=>{
  const pending=saved.get(handoff.id)!.operations.slice(-1)[0]!;
  expect(pending.receipt).toBeUndefined();expect(pending.commands).toEqual(args[0]);
  if(localThrow)throw new Error('Synchronous dispatch failure after durable intent');
  const result=lease.execute(...args),request=client.snapshot().request;
  if(!request)return result;
  wire.push(structuredClone(request));
  if(loseAck){client.cancel();return result;}
  const command=request.commands[0];
  state={...state,revision:state.revision+1,ack:request.sequence,status:'Scripted transport acknowledgement',
   ...(command.action==='catalog'?{catalog:structuredClone(referenceFixtures.definitions[0].view) as CatalogView}:{})};
  expect(client.receive(state)).toBe(true);
  return result;
 });
 const ports:RoomTaskPorts={
  store:{get:async id=>structuredClone(saved.get(id)),
   claim:async record=>{const existing=saved.get(record.id);if(existing)return{claimed:false,record:structuredClone(existing)};
    saved.set(record.id,structuredClone(record));return{claimed:true,record};},
   save:async record=>{saved.set(record.id,structuredClone(record));
    if(loseLeaseOnIntent&&record.operations.some(op=>!op.receipt))client.cancel();}},
  lease:()=>({...lease,execute}),
  run:vi.fn((input,current,control)=>runRoomActionTask(input,{aiClient:ai},current,()=>{},control)),
  reply:vi.fn(async()=>reply),changed:vi.fn(),activity:vi.fn(),now:()=>100
 };
 const manager=new RoomTaskHandoff(ports);manager.capture(handoff,async()=>true);
 return{manager,ports,client,saved,wire,execute,ai,
  localFailure:()=>{localThrow=true;},lostAcknowledgement:()=>{loseAck=true;},leaseLossOnIntent:()=>{loseLeaseOnIntent=true;}};
}

it.each([undefined,nextRunId])('refuses a fresh edit without an exact read before durable intent, runId=%s',async runId=>{
 const h=harness([[remove(runId)]]);
 try {
  const result=await h.manager.start('assistant');
  expect(result.phase).toBe('failed');expect(result.note).not.toContain('unconfirmed');
  expect(result.operations).toEqual([]);expect(h.execute).not.toHaveBeenCalled();
  expect(h.wire).toEqual([]);expect(h.client.snapshot().request).toBeNull();
  await h.manager.start('assistant');
  expect(h.ports.run).toHaveBeenCalledOnce();expect(h.ai.models.generateContentStream).toHaveBeenCalledOnce();
 } finally {h.client.cancel();}
});
it('carries the exact read revision through durable intent and actual client dispatch',async()=>{
 const h=harness([[read],[remove()],[]]);
 try {
  const result=await h.manager.start('assistant');
  expect(result.phase).toBe('completed');expect(result.operations).toHaveLength(2);
  expect(result.operations.every(op=>Boolean(op.receipt))).toBe(true);
  expect(h.wire).toHaveLength(2);expect(h.wire[1].conditions).toEqual([{id:target,revision:targetRevision}]);
  expect(h.wire[1].commands[0].execution).toMatchObject({runId:nextRunId,call:{id:'object.delete',arguments:{target}}});
 } finally {h.client.cancel();}
});
it('retains explicit old-identity reconciliation without inventing a target revision',async()=>{
 const h=harness([[remove(oldRunId)],[]]);
 try {
  const result=await h.manager.start('assistant');
  expect(result.phase).toBe('completed');expect(h.wire).toHaveLength(1);
  expect(h.wire[0].commands[0].execution).toMatchObject({runId:oldRunId});
  expect(h.wire[0].conditions).toEqual([{id:target,revision:0}]);
  await h.manager.start('assistant');expect(h.wire).toHaveLength(1);expect(h.ports.run).toHaveBeenCalledOnce();
 } finally {h.client.cancel();}
});
it('preserves legacy undefined-revision envelopes rather than inventing modern guards',async()=>{
 const h=harness([[remove()],[]],{legacy:true});
 try {
  const result=await h.manager.start('assistant');
  expect(result.phase).toBe('completed');expect(h.wire).toHaveLength(1);
  expect(h.wire[0].version).toBe(1);expect(h.wire[0].conditions).toEqual([]);
 } finally {h.client.cancel();}
});
it('keeps a synchronous dispatch-boundary failure conservative and never retries it',async()=>{
 const h=harness([[read],[remove()]]);const original=h.execute.getMockImplementation()!;
 h.execute.mockImplementation((...args)=>{if(args[0][0].action==='execution')h.localFailure();return original(...args);});
 try {
  const result=await h.manager.start('assistant');
  expect(result.phase).toBe('interrupted');expect(result.note).toContain('unconfirmed');
  expect(result.operations).toHaveLength(2);expect(result.operations[0].receipt).toBeDefined();expect(result.operations[1].receipt).toBeUndefined();
  expect(h.wire).toHaveLength(1);expect(h.client.snapshot().request).toBeNull();
  await h.manager.start('assistant');expect(h.execute).toHaveBeenCalledTimes(2);expect(h.ports.run).toHaveBeenCalledOnce();
 } finally {h.client.cancel();}
});
it('rechecks the lease after journal persistence and does not dispatch when that lease is lost',async()=>{
 const h=harness([[read],[remove()]]);const save=h.ports.store.save;
 h.ports.store.save=async record=>{if(record.operations.some(op=>!op.receipt&&op.commands[0].action==='execution'))h.leaseLossOnIntent();await save(record);};
 try {
  const result=await h.manager.start('assistant');
  expect(result.phase).toBe('interrupted');expect(result.operations.slice(-1)[0]?.receipt).toBeUndefined();
  expect(h.execute).toHaveBeenCalledOnce();expect(h.wire).toHaveLength(1);
  await h.manager.start('assistant').catch(()=>undefined);
  expect(h.execute).toHaveBeenCalledOnce();expect(h.ports.run).toHaveBeenCalledOnce();
 } finally {h.client.cancel();}
});
it('preserves genuine queued-but-unacknowledged uncertainty and does not silently replay',async()=>{
 const h=harness([[read],[remove()]]);const original=h.execute.getMockImplementation()!;
 h.execute.mockImplementation((...args)=>{if(args[0][0].action==='execution')h.lostAcknowledgement();return original(...args);});
 try {
  const result=await h.manager.start('assistant');
  expect(result.phase).toBe('interrupted');expect(result.note).toContain('unconfirmed');
  expect(result.operations.slice(-1)[0]?.receipt).toBeUndefined();expect(h.wire).toHaveLength(2);
  await h.manager.start('assistant').catch(()=>undefined);
  expect(h.wire).toHaveLength(2);expect(h.ports.run).toHaveBeenCalledOnce();
 } finally {h.client.cancel();}
});