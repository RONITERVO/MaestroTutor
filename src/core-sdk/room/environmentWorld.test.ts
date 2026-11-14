// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/environmentWorld.json';
import {capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validFactValue} from '../../../shared/behaviourFacts';
import {validExecutionView} from '../../../shared/roomExecutions';
it('accepts the actual full-world native call, receipt and every bounded membership page',()=>{
 const {call,...summary}=native.receipt;
 expect(native.savedMembers).toBe(258);expect(native.dormantMembers).toBe(256);
 expect(native.nativeInstances).toBe(128);expect(native.headsetMeasured).toBe(false);
 expect(validateCapabilityArguments(call.id,call.version,call.arguments)).toBeNull();
 expect(validateCapabilityOutput(call.id,call.version,native.receipt.output)).toBeNull();
 expect(validExecutionView({selected:native.receipt,running:[],outcomes:[summary]})).toBe(true);
 expect(capabilityResources(call.id,call.arguments).sort()).toEqual([...native.receipt.resources].sort());
 expect(validFactValue('environment.profile.summary',native.summary)).toBe(true);
 expect(native.pages).toHaveLength(17);
 for(const page of native.pages){
  expect(validFactValue('resource.members',page)).toBe(true);
  expect(page.membership).toBe(native.summary.membership);
  expect(page.members.length).toBeLessThanOrEqual(16);
 }
 expect(native.pages.flatMap(page=>page.members)).toEqual(native.call.arguments.members.map(member=>member.target));
 expect(native.undoRedo&&native.cancelPreserved&&native.failedSavePreserved).toBe(true);
});


import {readFileSync} from 'node:fs';
import {RoomAgentClient} from './roomAgentClient';
import {capabilityFeatures} from '../../../shared/capabilities';
import {roomSnapshotTransport} from '../../../shared/behaviourCatalog';
import type {RoomAgentState,RoomCommand} from './roomAgent';
it.each([false,true])('negotiates the full-world frame without dropping late revision guards (supported=%s)',async supported=>{
 const call=structuredClone(native.call);call.arguments.revision=2147483647;
 call.arguments.members.forEach(member=>member.revision=2147483647);
 const state:RoomAgentState={version:1,session:'f'.repeat(32),revision:1,sceneRevision:2147483647,ack:0,ok:true,status:'Ready',created:[],
  canUndo:false,canRedo:false,physicsRunning:false,workspaceReady:true,objects:[],
  capabilities:[...new Set(['execution.v1','catalog.v1',...capabilityFeatures(call.id,call.arguments),...(supported?[roomSnapshotTransport.feature]:[])])]};
 const client=new RoomAgentClient();expect(client.receive(state)).toBe(true);
 const basis=call.arguments.members.map(member=>({id:member.target,objectRevision:member.revision}));
 const command:RoomCommand={action:'execution',execution:{operation:'start',call}};
 const result=client.request([command],state,basis);
 try{
  if(supported){
   const wire=client.snapshot(),size=JSON.stringify(wire).length;
   expect(size).toBeGreaterThan(roomSnapshotTransport.legacyMaximumCharacters);
   expect(size).toBeLessThanOrEqual(roomSnapshotTransport.maximumCharacters);
   expect(wire.request?.conditions).toEqual(basis.map(x=>({id:x.id,revision:x.objectRevision})));
   expect(wire.request?.commands[0]).toEqual(command);
   expect(client.receive({...state,revision:2,ack:wire.request!.sequence})).toBe(true);await result;
  }else{
   await expect(result).rejects.toThrow('too large for the connected app');
   expect(client.snapshot().request).toBeNull();expect(client.getSnapshot().pending).toBe(false);
   const small=client.request([{action:'catalog',catalog:{operation:'check',call:{id:'time.wait',version:1,arguments:{seconds:1}}}}],state,[]);
   expect(client.receive({...state,revision:2,ack:client.snapshot().request!.sequence})).toBe(true);await small;
  }
 }finally{client.cancel();await result.catch(()=>{});}
});
it('keeps the Android input bound equal to the exported native transport and leaves media smaller',()=>{
 const source=readFileSync(new URL('../../../unity/NativeBrowser/src/main/java/com/maestro/quest/browser/BrowserSnapshotStore.java',import.meta.url),'utf8');
 const bound=source.match(/ROOM_MAXIMUM_CHARACTERS\s*=\s*(\d+)/);
 expect(Number(bound?.[1])).toBe(roomSnapshotTransport.maximumCharacters);
 expect(roomSnapshotTransport).toEqual({feature:'roomSnapshot64K.v1',maximumCharacters:65536,legacyMaximumCharacters:32768,mediaMaximumCharacters:32768});
});
