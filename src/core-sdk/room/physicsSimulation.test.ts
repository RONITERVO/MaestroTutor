// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/physicsSimulation.json';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const views=[native.start,native.pause];
it('carries exact native state identities through explicit physics start and pause',()=>{
 let current=native.before;
 for(const view of views){expect(validExecutionView(view)).toBe(true);expect(validFactValue('physics.simulation',current)).toBe(true);expect(view.selected.call.arguments.stateId).toBe(current.stateId);expect(view.selected.phase).toBe('completed');expect(view.selected.output.stateId).not.toBe(current.stateId);current=view.selected.output;}
 expect(current).toEqual(native.after);
 expect(native.start.selected.output).toMatchObject({ready:true,running:true,active:true,held:false,canStart:true});
 expect(native.after).toMatchObject({ready:true,running:false,active:true,held:false,canStart:true});
 expect(capabilityDefinition('physics.simulation.set')).toMatchObject({duration:'instant',channels:[]});
});
it('requires explicit supported desired mode, exact state format and native feature support',()=>{
 const call=native.start.selected.call;
 for(const operation of ['start','pause'])expect(validateCapabilityArguments(call.id,1,{...call.arguments,operation})).toBeNull();
 for(const invalid of [{...call.arguments,operation:'toggle'},{...call.arguments,stateId:''},{operation:'start'},{...call.arguments,move:[1,0]},{...call.arguments,force:true}])expect(validateCapabilityArguments(call.id,1,invalid)).not.toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('physicsSimulation.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','physicsSimulation.v1']})).not.toThrow();
});
