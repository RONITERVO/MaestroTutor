// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/entityEnvironment.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {validExecutionView} from '../../../shared/roomExecutions';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('validates actual native profile receipts and exact target assignments without a second schema',()=>{
 for(const receipt of [native.saveReceipt,native.assignReceipt]){
  const {call,...summary}=receipt;
  expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
  expect(validateCapabilityOutput(call.id,1,receipt.output)).toBeNull();
  expect(validExecutionView({selected:receipt,running:[],outcomes:[summary]})).toBe(true);
  const commands=[{action:'execution',execution:{operation:'start',call}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('environmentProfiles.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','environmentProfiles.v1']})).not.toThrow();
 }
 expect(capabilityResources(native.saveCall.id,native.saveCall.arguments)).toEqual([]);
 expect(capabilityResources(native.assignCall.id,native.assignCall.arguments)).toEqual(['maestro']);
 expect(native.after.profileId).toBe(native.saveReceipt.output.id);
 expect(native.after.state.effectiveRealCollisions).toBe(false);
 expect(native.before.profileId).toBe('');
});
it('preserves actor-specific readiness, reusable profile membership and empty fact pages',()=>{
 for(const value of [native.before,native.after])expect(validFactValue('object.environment',value)).toBe(true);
 expect(validFactValue('environment.profile',native.profile)).toBe(true);
 for(const value of [native.profiles,native.emptyPage])expect(validFactValue('environment.profiles',value)).toBe(true);
 expect(native.profile.members).toEqual(['maestro']);expect(native.emptyPage.entries).toEqual([]);
 expect(validateFactArguments('object.environment',1,{target:'maestro'})).toBeNull();
 expect(validateFactArguments('environment.profiles',1,{offset:17})).not.toBeNull();
 expect(validFactValue('object.environment',{...native.after,state:{...native.after.state,ready:'yes'}})).toBe(false);
});
it('keeps shared profile edits bounded and exposes every affected member as a target resource',()=>{
 const save={...native.saveCall.arguments,id:native.profile.id,revision:native.profile.revision,members:['maestro','book']};
 expect(validateCapabilityArguments('environment.profile.save',1,save)).toBeNull();
 expect(capabilityResources('environment.profile.save',save)).toEqual(['maestro','book']);
 for(const bad of [{...save,members:Array.from({length:17},(_,i)=>String(i).padStart(32,'0'))},{...save,realCollisions:'false'},{...save,revision:-1},{...save,name:'bad\nname'},{...save,force:true}])expect(validateCapabilityArguments('environment.profile.save',1,bad)).not.toBeNull();
 const assign=native.assignCall.arguments;
 for(const bad of [{...assign,profileId:'missing'},{...assign,profileRevision:-1},{...assign,target:'anyone'}])expect(validateCapabilityArguments('object.environment.assign',1,bad)).not.toBeNull();
 expect(capabilityDefinition('object.environment.assign')?.input['x-current']?.fields).toEqual({revision:['revision'],profileId:['profileId'],profileRevision:['profileRevision']});
});
