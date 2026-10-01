// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/controllerModes.json';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
const views=[native.enable,native.virtualView,native.user,native.mixed];
it('carries exact native state identities through independent opt-ins and MR restoration',()=>{
 let current=native.before;
 for(const view of views){expect(validExecutionView(view)).toBe(true);expect(validFactValue('controller.mode',current)).toBe(true);expect(view.selected.call.arguments.stateId).toBe(current.stateId);expect(view.selected.phase).toBe('completed');expect(view.selected.output.stateId).not.toBe(current.stateId);current=view.selected.output;}
 expect(current).toEqual(native.after);
 expect(native.enable.selected.output).toMatchObject({avatarEnabled:true,userEnabled:false,virtualView:false});
 expect(native.virtualView.selected.output).toMatchObject({avatarEnabled:true,userEnabled:false,virtualView:true});
 expect(native.user.selected.output).toMatchObject({avatarEnabled:true,userEnabled:true,virtualView:true});
 expect(native.after).toMatchObject({avatarEnabled:false,userEnabled:false,virtualView:false});
 expect(capabilityDefinition('controller.mode.set')).toMatchObject({duration:'instant',channels:[]});
});
it('requires explicit supported desired mode, exact state format and native feature support',()=>{
 const call=native.enable.selected.call;
 for(const operation of ['view.virtual','view.mixedReality','maestro.enable','maestro.disable','user.enable','user.disable'])expect(validateCapabilityArguments(call.id,1,{...call.arguments,operation})).toBeNull();
 for(const invalid of [{...call.arguments,operation:'toggle'},{...call.arguments,stateId:''},{operation:'user.enable'},{...call.arguments,move:[1,0]},{...call.arguments,force:true}])expect(validateCapabilityArguments(call.id,1,invalid)).not.toBeNull();
 const commands=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('controllerModes.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','controllerModes.v1']})).not.toThrow();
});
