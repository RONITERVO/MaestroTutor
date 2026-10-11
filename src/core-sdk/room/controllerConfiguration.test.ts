// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/controllerConfiguration.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
it('shares captured native controller preferences and exact save identities without enabling movement',()=>{
 for(const view of [native.movement,native.button])expect(validExecutionView(view)).toBe(true);
 for(const value of [native.before,native.afterMovement,native.after]){expect(validFactValue('controller.settings',value)).toBe(true);expect(value.live).toEqual({avatarEnabled:false,userEnabled:false,virtualView:false});}
 expect(native.movement.selected.call.arguments.configurationId).toBe(native.before.configurationId);
 expect(native.movement.selected.output.configurationId).toBe(native.afterMovement.configurationId);
 expect(native.button.selected.call.arguments.configurationId).toBe(native.afterMovement.configurationId);
 expect(native.button.selected.output.configurationId).toBe(native.after.configurationId);
 expect(new Set([native.before.configurationId,native.afterMovement.configurationId,native.after.configurationId]).size).toBe(3);
 expect(native.afterMovement.buttons).toEqual(native.before.buttons);
 expect(native.after.buttons[1]).toEqual({button:'a',command:'program',programId:native.programId,available:true});
 expect(capabilityDefinition('controller.configure')).toMatchObject({duration:'instant',channels:[]});
});
it('exposes all valid independent stick combinations and rejects reserved inputs and malformed settings',()=>{
 const id='controller.configure',base=native.movement.selected.call.arguments;
 for(const avatar of ['left','right','none'])for(const user of ['left','right','none']){
  const args={...base,operation:'movement.'+avatar,userStick:user};
  expect(validateCapabilityArguments(id,1,args)===null).toBe(avatar==='none'||user==='none'||avatar!==user);
 }
 for(const invalid of [{...base,configurationId:''},{...base,userSpeed:1.21},{...base,deadZone:.05},{...base,enable:true},{...base,avatarStick:'left'}])expect(validateCapabilityArguments(id,1,invalid)).not.toBeNull();
 const bind=native.button.selected.call.arguments;
 expect(validateCapabilityArguments(id,1,{...bind,programId:bind.programId.toUpperCase()})).toBeNull();
 for(const button of ['x','a','leftStickClick','rightStickClick'])expect(validateCapabilityArguments(id,1,{...bind,button})).toBeNull();
 for(const button of ['b','y','menu','system','trigger','grip'])expect(validateCapabilityArguments(id,1,{...bind,button})).not.toBeNull();
 for(const invalid of [{...bind,programId:''},{...bind,operation:'button.none'},{operation:'button.program',configurationId:bind.configurationId,button:'a'}])expect(validateCapabilityArguments(id,1,invalid)).not.toBeNull();
});
it('requires native controller configuration support for both manual and delegated execution',()=>{
 for(const view of [native.movement,native.button]){
  const call=view.selected.call;expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();expect(capabilityResources(call.id,call.arguments)).toEqual([]);
  const commands=[{action:'execution',execution:{operation:'start',call}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1']})).toThrow('controllerConfiguration.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['execution.v1','controllerConfiguration.v1']})).not.toThrow();
 }
});
