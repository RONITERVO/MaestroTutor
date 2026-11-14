// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from './templateAuthoring.json';
import {capabilityDefinition} from '../../shared/capabilities';
import {validCatalogView} from '../../shared/roomCatalog';
import {RoomAgentClient} from '../../src/core-sdk/room/roomAgentClient';
import type {RoomCommand} from '../../src/core-sdk/room/roomAgent';
import {simulatedTemplateReply,templateReplayState,type TemplateCapture} from './questTemplateReplay';
const captured=native as unknown as TemplateCapture;

it('adapts stale captured catalog definitions without changing native evidence or receipts',()=>{
 const original=JSON.stringify(native);
 // Drift remains detectable by the production validator; the capture is historical.
 expect(validCatalogView(captured.definition.catalog)).toBe(false);
 for(const key of ['before','search','definition','after'] as const){
  const state=templateReplayState(captured[key]);
  expect(new RoomAgentClient().receive(state)).toBe(true);
  expect(state.execution).toEqual(captured[key].execution);
  expect(state.objects).toEqual(captured[key].objects);
 }
 const adapted=templateReplayState(captured.definition);
 expect(adapted.catalog?.operation).toBe('inspect');
 if(adapted.catalog?.operation!=='inspect')throw new Error('Missing definition');
 expect(adapted.catalog.definition).toEqual(capabilityDefinition('object.create'));
 expect(JSON.stringify(native)).toBe(original);
});

it('acknowledges the exact recorded journey through the real client without executing native work',async()=>{
 const client=new RoomAgentClient();let state=templateReplayState(captured.before);
 expect(client.receive(state)).toBe(true);
 const recorded=captured.after.execution!.selected!;
 const commands:RoomCommand[]=[
  {action:'catalog',catalog:{operation:'search',query:'object.create',offset:0}},
  {action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:1,category:'actions'}},
  {action:'execution',execution:{operation:'start',call:structuredClone(recorded.call)}},
 ];
 for(const command of commands){
  const pending=client.request([command]);const request=client.snapshot().request!;
  expect(request).not.toBeNull();
  state=simulatedTemplateReply(captured,state,request);
  expect(client.receive(state)).toBe(true);
  await expect(pending).resolves.toEqual(state);
  expect(client.snapshot().request).toBeNull();
 }
 expect(state.execution).toEqual(captured.after.execution);
 expect(state.execution?.selected?.id).toBe(recorded.id);
 expect(state.execution?.selected?.status).toBe('Action completed');
 expect(()=>simulatedTemplateReply(captured,state,{sequence:state.ack+1,commands:[{action:'execution',execution:{operation:'start',runId:recorded.id,call:recorded.call}}]})).toThrow('Unsupported request');
});

it('refuses unrelated actions, calls, versions, identities and multiple commands instead of replaying success',()=>{
 const current=templateReplayState(captured.definition),recorded=captured.after.execution!.selected!;
 const valid:RoomCommand={action:'execution',execution:{operation:'start',runId:recorded.id,call:recorded.call}};
 const attempts:RoomCommand[][]=[
  [{action:'catalog',catalog:{operation:'search',query:'different',offset:0}}],
  [{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'object.recipe.part',version:1}}],
  [{action:'catalog',catalog:{operation:'inspect',capability:'object.create',version:2}}],
  [{action:'execution',execution:{operation:'start',runId:recorded.id,call:{...recorded.call,arguments:{...recorded.call.arguments,name:'Different result'}}}}],
  [{action:'execution',execution:{operation:'start',runId:'f'.repeat(32),call:recorded.call}}],
  [valid,valid],
 ];
 const original=JSON.stringify(captured);
 for(const commands of attempts)expect(()=>simulatedTemplateReply(captured,current,{sequence:current.ack+1,commands})).toThrow();
 expect(()=>simulatedTemplateReply(captured,current,{sequence:current.ack,commands:[valid]})).toThrow();
 expect(JSON.stringify(captured)).toBe(original);
});


it('preserves completed room state and receipt identity across re-search and re-inspect, refusing another start',async()=>{
 const client=new RoomAgentClient();let state=templateReplayState(captured.definition);
 expect(client.receive(state)).toBe(true);
 const recorded=captured.after.execution!.selected!;
 const started=client.request([{action:'execution',execution:{operation:'start',call:recorded.call}}]);
 state=simulatedTemplateReply(captured,state,client.snapshot().request!);
 expect(client.receive(state)).toBe(true);await started;
 const completed=structuredClone(state);
 expect(completed.execution?.nextRunId).not.toBe(recorded.id);
 for(const catalog of [
  {operation:'search' as const,query:'object.create',offset:0},
  {operation:'inspect' as const,capability:'object.create',version:1,category:'actions' as const},
 ]){
  const browsing=client.request([{action:'catalog',catalog}]);
  state=simulatedTemplateReply(captured,state,client.snapshot().request!);
  expect(client.receive(state)).toBe(true);await browsing;
  expect(state.execution).toEqual(completed.execution);
  expect(state.objects).toEqual(completed.objects);
  expect(state.sceneRevision).toBe(completed.sceneRevision);
  expect(state.created).toEqual(completed.created);
  expect(state.ack).toBeGreaterThan(completed.ack);
 }
 for(const runId of [recorded.id,state.execution!.nextRunId!]){
  expect(()=>simulatedTemplateReply(captured,state,{sequence:state.ack+1,commands:[{action:'execution',execution:{operation:'start',runId,call:recorded.call}}]})).toThrow('Unsupported request');
 }
 expect(client.getSnapshot().state?.execution).toEqual(completed.execution);
 expect(client.snapshot().request).toBeNull();
});
