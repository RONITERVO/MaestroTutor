// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/surfacePlacement.json';
import {capabilityDefinition,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
it('validates the real native surface placement receipt and saved outcome',()=>{
 const call=native.request.call,result=native.receipt.selected.output;
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(validExecutionView(native.receipt)).toBe(true);expect(validateCapabilityOutput(call.id,1,result)).toBeNull();
 expect(native.receipt.selected.phase).toBe('completed');expect(result.target).toBe(call.arguments.target);expect(result.revision).toBeGreaterThan(native.beforeRevision);expect(result.position).not.toEqual(native.before);expect(result.normal.y).toBeCloseTo(1);expect(result.temporary).toBe(false);
 expect(capabilityDefinition(call.id)).toMatchObject({duration:'completion',channels:['wholeTarget']});
});
it('requires live placement support and preserves target and direction when reading the current room guard',()=>{
 const call=native.request.call,definition=capabilityDefinition(call.id)!;const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('surfacePlacement.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','surfacePlacement.v1']})).not.toThrow();
 const draft={...call.arguments,stateId:'0'.repeat(32),direction:'gaze'};
 expect(applyCurrentInputs(definition.input,draft,{operation:'inspect',category:'facts',capability:'room.environment',version:1,definition:behaviourFact('room.environment')!,available:true,value:native.environment,status:'Native room'})).toEqual({...draft,stateId:native.environment.stateId});
 for(const args of [{...call.arguments,direction:'guess'},{...call.arguments,stateId:''},{...call.arguments,force:true},{...call.arguments,x:0}])expect(validateCapabilityArguments(call.id,1,args)).not.toBeNull();
});
