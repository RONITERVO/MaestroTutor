// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,type CapabilityInvocation} from '../../../shared/capabilities';
import {boundedCapabilityCall} from '../../../shared/roomCatalog';
import native from '../../../test-fixtures/browser/drawingAuthoring.json';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validateCapabilityOutput} from '../../../shared/capabilities';
import {applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/drawing-contract.json','utf8')) as {name:string;call:CapabilityInvocation;valid:boolean}[];
it.each(cases)('shares the native drawing contract for $name',entry=>{
 expect(validateCapabilityArguments(entry.call.id,1,entry.call.arguments)===null).toBe(entry.valid);
 if(entry.valid)expect(boundedCapabilityCall(entry.call)).toBe(true);
 if(entry.valid)expect(capabilityResources(entry.call.id,entry.call.arguments)).toEqual(entry.call.id==='object.drawing.edit'?[entry.call.arguments.target]:[]);
});
it('requires drawing support for creation, edits and retained-draft resolution',()=>{
 const definition=capabilityDefinition('object.create')!,call={id:definition.id,version:1,arguments:definition.input.oneOf![3].examples![0]};
 for(const entry of [call,...cases.filter(e=>e.valid).map(e=>e.call)]){
  const commands=[{action:'execution',execution:{operation:'start',call:entry}}];expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1']})).toThrow('drawingEdits.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','actionResults.v1','drawingEdits.v1']})).not.toThrow();
 }
 expect(capabilityDefinition('object.create.drawing')).toBeNull();
});

it('validates actual native creation/edit receipts and the exact updated stroke revision',()=>{
 for(const view of [native.creation,native.edit]){expect(validExecutionView(view)).toBe(true);expect(validateCapabilityOutput(view.selected.call.id,1,view.selected.output)).toBeNull();expect(boundedCapabilityCall(view.selected.call)).toBe(true);expect(view.selected.phase).toBe('completed');}
 expect(native.creation.selected.output.objectId).toBe(native.before.target);expect(native.after.target).toBe(native.before.target);expect(native.after.points).toBe(native.before.points+1);expect(native.after.revision).toBeGreaterThan(native.before.revision);expect(native.edit.selected.output).toEqual(native.after);
});
it('reads only the exact revision while keeping the chosen drawing edit',()=>{
 const call=native.editCall,definition=capabilityDefinition(call.id)!;const draft={...call.arguments,revision:1};
 expect(applyCurrentInputs(definition.input,draft,{operation:'inspect',category:'facts',capability:'object.drawing',version:1,definition:behaviourFact('object.drawing')!,arguments:{target:native.before.target},available:true,value:native.before,status:'Current native stroke'})).toEqual(call.arguments);
});

import recovery from '../../../test-fixtures/browser/drawingRecovery.json';
it('uses the retained physical draft identity for retry and preserves a historical native outcome',()=>{
 expect(validExecutionView(recovery.receipt)).toBe(true);
 expect(validateCapabilityOutput(recovery.call.id,1,recovery.receipt.selected.output)).toBeNull();
 expect(boundedCapabilityCall(recovery.call)).toBe(true);
 expect(recovery.before.phase).toBe('unsaved');expect(recovery.after.phase).toBe('idle');
 expect(recovery.after.sessionId).not.toBe(recovery.before.sessionId);
 expect(recovery.receipt.selected.output).toMatchObject({sessionId:recovery.before.sessionId,phase:'saved',points:recovery.before.points});
 expect(capabilityResources(recovery.call.id,recovery.call.arguments)).toEqual([]);
 const definition=capabilityDefinition(recovery.call.id)!;
 expect(applyCurrentInputs(definition.input,{operation:'discard',sessionId:'0'.repeat(32)},{operation:'inspect',category:'facts',capability:'object.drawing.capture',version:1,definition:behaviourFact('object.drawing.capture')!,available:true,value:recovery.before,status:'Retained native stroke'})).toEqual({operation:'discard',sessionId:recovery.before.sessionId});
});
