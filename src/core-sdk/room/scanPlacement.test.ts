// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/scanPlacement.json';
import {capabilityDefinition,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {behaviourFact} from '../../../shared/behaviourCatalog';
it('accepts the real native scanned-floor receipt and preserves exact anchor identity',()=>{
 const call=native.request.call,result=native.receipt.selected.output;
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(validExecutionView(native.receipt)).toBe(true);expect(validateCapabilityOutput(call.id,1,result)).toBeNull();
 expect(native.receipt.selected.phase).toBe('completed');expect(result.target).toBe(call.arguments.target);expect(result.anchorId).toBe(call.arguments.anchorId);expect(result.roomId).toBe(native.scan.roomId);
 expect(result.revision).toBeGreaterThan(native.beforeRevision);expect(result.position).not.toEqual(native.before);expect(result.point.y).toBeCloseTo(.35);expect(result.normal.y).toBeCloseTo(1);expect(result.temporary).toBe(false);
 expect(capabilityDefinition(call.id)).toMatchObject({duration:'instant',channels:['wholeTarget']});
});
it('refreshes only the scan guard while retaining the user-selected plane and coordinates',()=>{
 const call=native.request.call,definition=capabilityDefinition(call.id)!;const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','surfacePlacement.v1']})).toThrow('scanPlacement.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','scanPlacement.v1']})).not.toThrow();
 const draft={...call.arguments,stateId:'0'.repeat(32)};
 expect(applyCurrentInputs(definition.input,draft,{operation:'inspect',category:'facts',capability:'room.scan',version:1,definition:behaviourFact('room.scan')!,available:true,value:native.scan,status:'Native scan'})).toEqual(call.arguments);
 for(const args of [{...call.arguments,anchorId:'FLOOR'},{...call.arguments,stateId:''},{...call.arguments,y:26},{...call.arguments,direction:'below'},{...call.arguments,force:true}])expect(validateCapabilityArguments(call.id,1,args)).not.toBeNull();
});
