// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/aimedThrow.json';
import {capabilityResources,capabilityParameterType,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {validCatalogView} from '../../../shared/roomCatalog';
import {validExecutionView} from '../../../shared/roomExecutions';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {RoomAgentClient} from '../../platform/quest/roomAgentBridge';
it('validates the native free-flight preview and launched receipt without treating it as an arrival',()=>{
 expect(validFactValue('object.physics.trajectory',native.preview)).toBe(true);
 for(const phase of [native.inspect,native.previewed,native.ready,native.launched]){
  expect(validCatalogView(phase.catalog)).toBe(true);expect(validExecutionView(phase.execution)).toBe(true);
  expect(new RoomAgentClient().receive(phase)).toBe(true);
 }
 const receipt=native.launched.execution.selected;expect(receipt.phase).toBe('completed');expect(receipt.output.phase).toBe('launched');
 expect(validateCapabilityOutput('object.physics.launch',1,receipt.output)).toBeNull();
 expect(validateCapabilityOutput('object.physics.launch',1,{...receipt.output,phase:'caught'})).not.toBeNull();
 expect(native.previewed.catalog.value).toEqual(native.preview);expect(native.preview.speed).toBeGreaterThan(0);expect(native.preview.speed).toBeLessThanOrEqual(8);
});
it('shares nested target resources, scalar bindings and strict throw limits',()=>{
 const args:Record<string,unknown>=structuredClone(native.arguments);
 expect(validateCapabilityArguments('object.physics.launch',1,args)).toBeNull();expect(validateFactArguments('object.physics.trajectory',1,args)).toBeNull();
 expect(capabilityParameterType('object.physics.launch','destination.position.x',args)).toBe('number');expect(capabilityParameterType('object.physics.launch','destination.kind',args)).toBeNull();
 args.destination={kind:'anchor',anchor:{kind:'recipePart',objectId:'1'.repeat(32),revision:1,part:'RightHand'},offset:{x:0,y:0,z:.5}};
 expect(validateCapabilityArguments('object.physics.launch',1,args)).toBeNull();expect(capabilityResources('object.physics.launch',args)).toEqual([args.target,'1'.repeat(32)]);
 expect(capabilityParameterType('object.physics.launch','destination.anchor.revision',args)).toBe('number');expect(capabilityParameterType('object.physics.launch','destination.position.x',args)).toBeNull();
 for(const patch of [{maxSpeed:9},{seconds:0},{seconds:3},{destination:{kind:'point',position:{x:0,y:NaN,z:0}}},{priority:99}])expect(validateCapabilityArguments('object.physics.launch',1,{...args,...patch})).not.toBeNull();
});
it('requires the aimed-throw feature and action-result support before dispatch',()=>{
 const commands=[{action:'execution',execution:{operation:'start',call:{id:'object.physics.launch',version:1,arguments:native.arguments}}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('objectLaunch.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','objectLaunch.v1']})).toThrow('actionResults.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','objectLaunch.v1','actionResults.v1']})).not.toThrow();
});
