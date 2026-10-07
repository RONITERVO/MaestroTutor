// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/physicsEnvironment.json';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('preserves native collision policy receipts and distinguishes authored ground from scan readiness',()=>{
 for(const value of [native.before,native.virtual,native.restored])expect(validFactValue('physics.environment',value)).toBe(true);
 for(const view of [native.disabled,native.enabled])expect(validExecutionView(view)).toBe(true);
 expect(native.disabled.selected.call.arguments).toEqual({realCollisions:false,stateId:native.before.stateId});
 expect(native.virtual).toEqual(native.disabled.selected.output);
 expect(native.enabled.selected.call.arguments).toEqual({realCollisions:true,stateId:native.virtual.stateId});
 expect(native.restored).toEqual(native.enabled.selected.output);
 expect(native.virtual).toMatchObject({realCollisions:false,ready:true,authoredReady:true,scanReady:native.before.scanReady,scope:'acceptedGroundColumns'});
 expect(native.restored.realCollisions).toBe(true);
 expect(capabilityDefinition('physics.environment.set')).toMatchObject({duration:'instant',channels:[]});
});
it('requires the collision feature and refuses ambiguous or stale-shaped requests',()=>{
 const call=native.disabled.selected.call;
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 for(const invalid of [{...call.arguments,realCollisions:'false'},{...call.arguments,stateId:''},{realCollisions:false},{...call.arguments,opacity:0},{...call.arguments,force:true}])expect(validateCapabilityArguments(call.id,1,invalid)).not.toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicsSimulation.v1']})).toThrow('physicsEnvironment.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicsEnvironment.v1']})).not.toThrow();
});
