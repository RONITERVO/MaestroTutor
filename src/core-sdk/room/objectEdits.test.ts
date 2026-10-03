// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import nativeProgram from '../../../test-fixtures/browser/objectEditProgram.json';
import nativeResults from '../../../test-fixtures/browser/objectEditResults.json';
import nativeRoom from '../../../test-fixtures/browser/programBookState.json';
import {validExecutionView} from '../../../shared/roomExecutions';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
import {newRuleStep} from './rules';
import {parseProgram,sequenceProgram} from './programs';
import {parseRoomCommands,type RoomAgentState} from './roomAgent';
const target='a'.repeat(32);
const calls=[
 {id:'object.position.set',version:1,arguments:{target,x:.2,y:1.5,z:.8}},
 {id:'object.scale.set',version:1,arguments:{target,scale:1.5}},
 {id:'object.color.set',version:1,arguments:{target,red:1,green:.2,blue:.1}},
 {id:'object.delete',version:1,arguments:{target}}
];
it.each(calls)('declares a scoped instant $id action and rejects hidden duration/code fields',call=>{
 const definition=capabilityDefinition(call.id)!;
 expect(definition.duration).toBe('instant');expect(definition.channels).toEqual(['wholeTarget']);
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 expect(capabilityResources(call.id,call.arguments)).toEqual([target]);
 for(const extra of [{seconds:1},{engineCode:'anything'},{unknown:true}])
  expect(validateCapabilityArguments(call.id,1,{...call.arguments,...extra})).not.toBeNull();
});
it('protects built-ins from paint/deletion and bounds authorable scalar values',()=>{
 for(const id of ['book','maestro']){
  expect(validateCapabilityArguments('object.delete',1,{target:id})).not.toBeNull();
  expect(validateCapabilityArguments('object.color.set',1,{target:id,red:1,green:0,blue:0})).not.toBeNull();
  expect(validateCapabilityArguments('object.position.set',1,{target:id,x:0,y:1,z:0})).toBeNull();
 }
 for(const scale of [-1,0,4.1,NaN])expect(validateCapabilityArguments('object.scale.set',1,{target,scale})).not.toBeNull();
 expect(validateCapabilityArguments('object.color.set',1,{target,red:1.1,green:0,blue:0})).not.toBeNull();
});
it('gates program saves on edit support without changing the named program representation',()=>{
 for(let kind=14;kind<=17;kind++){
  const program=sequenceProgram([{...newRuleStep(kind),targetId:target}]);
  const source=JSON.stringify(program);expect(parseProgram(source).error).toBeNull();
  const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'change',sequence:{id:'',name:'Change object',interruption:0,repeat:false,program:source}}]}}]});
  expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3']})).toThrow('edit objects');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','objectEdits.v1']})).not.toThrow();
 }
});
it('uses current target revisions and a native-issued ID for manual edit requests',async()=>{
 const state:RoomAgentState={version:1,session:'b'.repeat(32),revision:1,sceneRevision:7,ack:0,ok:true,status:'Ready',created:[],canUndo:false,canRedo:false,physicsRunning:false,
  capabilities:['execution.v1','executionReceipts.v1','objectEdits.v1'],execution:{selected:null,running:[],outcomes:[],nextRunId:'c'.repeat(32),storageError:null},
  objects:[{id:target,objectRevision:7,name:'Ball',kind:'Ball',position:{x:0,y:1,z:0},scale:1,color:{r:1,g:1,b:1,a:1},animated:false}]};
 const client=new RoomAgentClient();expect(client.receive(state)).toBe(true);
 const pending=client.request([{action:'execution',execution:{operation:'start',call:calls[2]}}]);
 expect(client.snapshot().request?.conditions).toEqual([{id:target,revision:7}]);
 expect(client.snapshot().request?.commands[0].execution).toMatchObject({runId:'c'.repeat(32),call:calls[2]});
 client.receive({...state,revision:2,ack:1,ok:false,status:'The object changed'});
 expect((await pending).ok).toBe(false);expect(client.snapshot().request).toBeNull();client.cancel();
});

it('accepts actual native edit programs and historical results even after deletion',()=>{
 expect(parseProgram(JSON.stringify(nativeProgram)).program).toEqual(nativeProgram);
 for(const execution of Object.values(nativeResults)){
  expect(validExecutionView(execution)).toBe(true);
  const client=new RoomAgentClient();expect(client.receive({...nativeRoom,execution})).toBe(true);
  expect(client.snapshot().request).toBeNull();client.cancel();
 }
 expect(nativeResults.deleted.selected.phase).toBe('completed');
 expect(nativeResults.deleted.selected.call.id).toBe('object.delete');
});
