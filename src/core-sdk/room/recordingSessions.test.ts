// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/recordingSessions.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('validates native recording identities and distinguishes a completed start receipt from the active take',()=>{
 for(const view of [native.start,native.finish])expect(validExecutionView(view)).toBe(true);
 for(const view of [native.before,native.active,native.idle])expect(validFactValue('animation.recording',view)).toBe(true);
 expect(native.start.selected.phase).toBe('completed');expect(native.start.selected.output.phase).toBe('recording');
 expect(native.finish.selected.output.phase).toBe('saved');expect(native.active.phase).toBe('recording');expect(native.idle.phase).toBe('idle');
 expect(native.before.sessionId).toBe(native.active.sessionId);expect(native.idle.sessionId).not.toBe(native.active.sessionId);
 expect(native.finish.selected.call.arguments.sessionId).toBe(native.start.selected.output.sessionId);
 expect(capabilityDefinition('animation.record')).toMatchObject({duration:'instant',ownership:'authoringSession',channels:[]});
});
it('keeps exact take identities, grants no object authority from results, and rejects unsupported native runtimes',()=>{
 const start=native.start.selected.call,finish=native.finish.selected.call;
 expect(capabilityResources(start.id,start.arguments)).toEqual(['maestro']);
 for(const call of [start,finish]){
  expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
  const commands=[{action:'execution',execution:{operation:'start',call}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('animationRecording.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','animationRecording.v1']})).not.toThrow();
 }
 const discard={operation:'discard',sessionId:native.active.sessionId};expect(validateCapabilityArguments(start.id,1,discard)).toBeNull();expect(capabilityResources(start.id,discard)).toEqual([]);
 for(const args of [{...start.arguments,sessionId:''},{...start.arguments,revision:0},{...start.arguments,manual:true},{...discard,target:'maestro'}])expect(validateCapabilityArguments(start.id,1,args)).not.toBeNull();
 expect(validFactValue('animation.recording',{...native.idle,error:'x'.repeat(129)})).toBe(false);
});
