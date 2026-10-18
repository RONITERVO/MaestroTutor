// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/keyframeSessions.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('validates the native physical keyframe edit and its shared completion receipt',()=>{
 for(const view of [native.beforeExecution,native.preparing,native.complete])expect(validExecutionView(view)).toBe(true);
 for(const value of [native.before,native.pending,native.idle])expect(validFactValue('animation.edit.pending',value)).toBe(true);
 expect(native.before.phase).toBe('idle');expect(native.pending.phase).toBe('saving');expect(native.idle.phase).toBe('idle');
 expect(native.preparing.selected.phase).toBe('preparing');expect(native.complete.selected.phase).toBe('completed');
 expect(native.preparing.selected.call).toEqual(native.complete.selected.call);expect(native.preparing.selected.id).toBe(native.complete.selected.id);
 expect(native.complete.selected.call.arguments.sessionId).toBe(native.pending.sessionId);
 expect(native.complete.selected.output).toMatchObject({sessionId:native.pending.sessionId,target:native.pending.target,phase:'saved',temporary:false});
 expect(native.complete.selected.output.revision).toBeGreaterThan(native.pending.revision);
 expect(native.idle.sessionId).not.toBe(native.pending.sessionId);expect(native.idle.frames).toBe(0);expect(native.idle.target).toBe('');
 expect(capabilityDefinition('animation.edit.resolve')).toMatchObject({duration:'completion',ownership:'authoringSession',channels:[]});
});
it('requires the shared keyframe feature and exact session without taking target ownership',()=>{
 const call=native.complete.selected.call;
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 expect(capabilityResources(call.id,call.arguments)).toEqual([]);
 const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('animationEditResolution.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','animationEditResolution.v1']})).not.toThrow();
 expect(validateCapabilityArguments(call.id,1,{...call.arguments,operation:'discard'})).toBeNull();
 for(const invalid of [{...call.arguments,sessionId:''},{...call.arguments,sessionId:'a'.repeat(33)},{...call.arguments,manual:true},{...call.arguments,target:'maestro'},{...call.arguments,operation:'capture'}])expect(validateCapabilityArguments(call.id,1,invalid)).not.toBeNull();
});
