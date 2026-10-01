// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/avatarSelection.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('reads native library metadata and distinguishes pending preparation from the saved and displayed avatar',()=>{
 for(const view of [native.library,native.selection])expect(validExecutionView(view)).toBe(true);
 for(const fact of [native.before,native.loading,native.after])expect(validFactValue('avatar.model',fact)).toBe(true);
 expect(native.loading.busy).toBe(true);expect(native.loading.selectedHash).toBe(native.before.selectedHash);
 expect(native.after.busy).toBe(false);expect(native.after.phase).toBe('ready');
 expect(native.after.displayedHash).toBe(native.selection.selected.output.modelHash);expect(native.after.selectedHash).toBe(native.after.displayedHash);
 expect(native.library.selected.output.entries[0].modelHash).toBe(native.after.selectedHash);
 expect(native.selection.selected.call.arguments.revision).toBe(native.before.revision);expect(native.after.revision).toBeGreaterThan(native.before.revision);
 expect(capabilityDefinition('avatar.model.select')).toMatchObject({duration:'completion',channels:['wholeTarget']});
});
it('requires the shared model feature, exact object revision and private content hash, never a file path',()=>{
 for(const call of [native.library.selected.call,native.selection.selected.call]){
  expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
  const commands=[{action:'execution',execution:{operation:'start',call}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('avatarModels.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','avatarModels.v1']})).not.toThrow();
 }
 const call=native.selection.selected.call;expect(capabilityResources(call.id,call.arguments)).toEqual(['maestro']);expect(capabilityResources('model.library.inspect',{offset:0})).toEqual([]);
 for(const args of [{...call.arguments,modelHash:'../model.glb'},{...call.arguments,modelHash:'https://example.com/model.glb'},{...call.arguments,target:'book'},{...call.arguments,revision:0},{...call.arguments,manual:true}])expect(validateCapabilityArguments(call.id,1,args)).not.toBeNull();
 expect(validateCapabilityArguments(call.id,1,{...call.arguments,modelHash:''})).toBeNull();expect(validateCapabilityArguments('model.library.inspect',1,{offset:33})).not.toBeNull();
});
