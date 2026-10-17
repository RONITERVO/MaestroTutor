// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/posingSessions.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('validates real native pose previews, clamped joint readback and final saved session turnover',()=>{
 for(const view of [native.start,native.rotate,native.finish])expect(validExecutionView(view)).toBe(true);
 for(const value of [native.before,native.active,native.edited,native.idle])expect(validFactValue('animation.posing',value)).toBe(true);
 expect(validFactValue('animation.pose.joint',native.joint)).toBe(true);
 expect(native.start.selected.phase).toBe('completed');expect(native.active.phase).toBe('posing');
 expect(native.rotate.selected.call.arguments.version).toBe(native.active.version);expect(native.edited.version).toBeGreaterThan(native.active.version);
 expect(native.edited.revision).toBe(native.active.revision);expect(native.jointQuery.version).toBe(native.edited.version);
 expect(native.finish.selected.call.arguments.version).toBe(native.edited.version);expect(native.finish.selected.output.phase).toBe('saved');
 expect(native.idle.sessionId).not.toBe(native.active.sessionId);expect(native.idle.joints).toEqual([]);expect(native.idle.version).toBe(0);
 expect(2*Math.acos(Math.abs(native.joint.w))*180/Math.PI).toBeCloseTo(75,1);
 expect(capabilityDefinition('animation.pose')).toMatchObject({duration:'instant',ownership:'authoringSession',channels:[]});
});
it('keeps pose session versions and feature requirements in the shared human/agent contract',()=>{
 for(const view of [native.start,native.rotate,native.finish]){
  const call=view.selected.call;expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(capabilityResources(call.id,call.arguments)).toEqual(['maestro']);
  const commands=[{action:'execution',execution:{operation:'start',call}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('animationPosing.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','animationPosing.v1']})).not.toThrow();
 }
 const args=native.rotate.selected.call.arguments;
 for(const invalid of [{...args,version:0},{...args,sessionId:''},{...args,manual:true},{...args,target:'book'},{...args,joints:[]},{...args,joints:Array(9).fill(args.joints[0])}])expect(validateCapabilityArguments('animation.pose',1,invalid)).not.toBeNull();
 expect(validFactValue('animation.posing',{...native.active,error:'x'.repeat(129)})).toBe(false);
});
