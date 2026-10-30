// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityFeatures,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {invocationStep,stepInvocation} from './capabilitySteps';
const original={target:'maestro',seconds:2,loop:false,channel:'wholeTarget',source:{kind:'embedded',modelHash:'a'.repeat(64),clipIndex:0}};
it('keeps old animation calls stationary and requires a native feature only for an explicit movement policy',()=>{
 const old={id:'animation.play',version:1,arguments:original};expect(validateCapabilityArguments(old.id,1,old.arguments)).toBeNull();expect(capabilityFeatures(old.id,old.arguments)).not.toContain('authoredMotion.v1');expect(capabilityFeatures(old.id,old.arguments,['movement'])).toContain('authoredMotion.v1');
 const call={...old,arguments:{...original,movement:'authored'}};expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(capabilityFeatures(call.id,call.arguments)).toContain('authoredMotion.v1');
 const commands=[{action:'execution',execution:{operation:'start',call}}];expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('authoredMotion.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','authoredMotion.v1']})).not.toThrow();
 expect(validateCapabilityArguments(call.id,1,{...call.arguments,movement:'teleport'})).not.toBeNull();
 expect(validateCapabilityArguments(call.id,1,{...call.arguments,target:'b'.repeat(32)})).not.toBeNull();
});
it('retains authored policy in capability blocks instead of silently dropping it through old simple-step controls',()=>{
 const originalCall={id:'animation.play',version:1,arguments:original};expect(stepInvocation(invocationStep(originalCall,'node'))).toEqual(originalCall);
 for(const movement of ['inPlace','authored'])expect(()=>invocationStep({...originalCall,arguments:{...original,movement}},'node')).toThrow('movement policy');
});
