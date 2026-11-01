// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/objectCopy.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
it('validates the captured native copy and its exact source and returned identity',()=>{
 const {call,receipt,before,copied}=native;
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(validExecutionView(receipt)).toBe(true);expect(validateCapabilityOutput(call.id,1,receipt.selected.output)).toBeNull();
 expect(capabilityResources(call.id,call.arguments)).toEqual([before.target]);expect(receipt.selected.phase).toBe('completed');expect(receipt.selected.output.objectId).toBe(copied.target);expect(copied.target).not.toBe(before.target);expect(native.sourceUnchanged).toBe(true);
 expect(copied.content).toEqual(before.content);expect(copied.position).toEqual({x:call.arguments.x,y:call.arguments.y,z:call.arguments.z});expect(copied.rotation).toEqual(before.rotation);expect(copied.scale).toBe(before.scale);
});
it('gates copies by native support and reads only the source revision into the reviewed request',()=>{
 const {call,before}=native,definition=capabilityDefinition(call.id)!;
 const commands=[{action:'execution',execution:{operation:'start',call}}];expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('objectCopy.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','objectCopy.v1']})).not.toThrow();
 const draft={...call.arguments,revision:1,name:'My copy'};
 expect(applyCurrentInputs(definition.input,draft,{operation:'inspect',category:'facts',capability:'object.definition',version:1,definition:behaviourFact('object.definition')!,arguments:{target:before.target},available:true,value:before,status:'Native definition'})).toEqual({...draft,revision:before.revision});
 expect(capabilityDefinition('object.create.copy')).toBeNull();
});
