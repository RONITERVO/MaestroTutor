// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/modelSelection.json';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('shares exact native picker, preview and saved object identities without treating selection as acceptance',()=>{
 for(const v of [native.select,native.accept])expect(validExecutionView(v)).toBe(true);
 for(const f of [native.before,native.choosing,native.preview,native.after])expect(validFactValue('model.import.selection',f)).toBe(true);
 expect(native.select.selected.phase).toBe('completed');expect(native.choosing.phase).toBe('selecting');
 expect(native.preview.phase).toBe('preview');expect(native.preview.accepted.objectId).toBe('');
 expect(native.accept.selected.call.arguments.requestId).toBe(native.preview.requestId);
 expect(native.accept.selected.call.arguments.modelHash).toBe(native.preview.preview.modelHash);
 expect(native.after.accepted.objectId).toBe(native.accept.selected.output.objectId);
 expect(native.after.accepted.destination).toBe('object');expect(native.after.phase).toBe('completed');
 expect(capabilityDefinition('model.import')).toMatchObject({duration:'completion',ownership:'importSession',channels:[]});
});
it('requires exact import fields and the shared native feature while refusing paths and priority overrides',()=>{
 for(const v of [native.select,native.accept]){
  const call=v.selected.call;expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
  const commands=[{action:'execution',execution:{operation:'start',call}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('modelImport.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','modelImport.v1']})).not.toThrow();
 }
 const args=native.accept.selected.call.arguments;
 for(const invalid of [{...args,requestId:''},{...args,modelHash:''},{...args,path:'C:/private.glb'},{...args,manual:true},{...args,operation:'maestro'},{operation:'select',requestId:args.requestId}])expect(validateCapabilityArguments('model.import',1,invalid)).not.toBeNull();
 expect(validateCapabilityArguments('model.import',1,{...args,operation:'maestro',target:'maestro',revision:1})).toBeNull();
 expect(validateCapabilityArguments('model.import',1,{operation:'cancel',requestId:args.requestId})).toBeNull();
 expect(validFactValue('model.import.selection',{...native.after,error:'x'.repeat(129)})).toBe(false);
});
