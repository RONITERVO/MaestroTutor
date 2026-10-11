// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/animationAuthoring.json';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validateFactArguments,validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';

it('accepts the native authored motion receipt and exact-revision frame and joint facts',()=>{
 const {call,output}=native.execution.selected;
 expect(validExecutionView(native.execution)).toBe(true);
 expect(validateCapabilityArguments(call.id,1,call.arguments)).toBeNull();
 expect(validateCapabilityOutput(call.id,1,output)).toBeNull();
 expect(capabilityResources(call.id,call.arguments)).toEqual(['maestro']);
 expect(output).toEqual(native.metadata);expect(native.frame.joints).toHaveLength(17);
 for(const [id,args,value] of [['animation.authored',{target:'maestro'},native.metadata],['animation.frame',native.frameQuery,native.frame],['animation.joint',native.jointQuery,native.joint]] as const){
  expect(validateFactArguments(id,1,args)).toBeNull();expect(validFactValue(id,value)).toBe(true);
 }
 expect(validFactValue('animation.frame',{...native.frame,joints:[],jointChannels:false})).toBe(true);
 expect(validateFactArguments('animation.frame',1,{...native.frameQuery,revision:0})).not.toBeNull();
 expect(validateFactArguments('animation.joint',1,{...native.jointQuery,joint:'Wing'})).not.toBeNull();
});
it('bounds authoring inputs and requires the feature for both direct and saved calls',()=>{
 const {call}=native.execution.selected,definition=capabilityDefinition(call.id)!;
 expect(definition).toMatchObject({duration:'instant',channels:['wholeTarget']});
 for(const argumentsValue of [{...call.arguments,revision:0},{...call.arguments,frames:Array(9).fill(call.arguments.frames[0])},{operation:'pose',target:'book',revision:1,joints:null},{operation:'frames',target:'maestro',revision:1,replace:true,frames:[{...call.arguments.frames[0],rotation:{x:0,y:0,z:0,w:0}}]}])
  expect(validateCapabilityArguments(call.id,1,argumentsValue)).not.toBeNull();
 const direct=[{action:'execution',execution:{operation:'start',call}}];
 expect(()=>requireRoomCapabilities(direct,{capabilities:['execution.v1']})).toThrow('animationAuthoring.v1');
 expect(()=>requireRoomCapabilities(direct,{capabilities:['execution.v1','animationAuthoring.v1']})).not.toThrow();
 const node={id:'edit',op:'invoke',capability:call.id,version:1,arguments:call.arguments,bindings:{}};
 const program={version:3,entry:'main',resources:['maestro'],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[node]}]};
 const commands=[{action:'rules',rule:{action:'edit',edits:[{sequence:{program:JSON.stringify(program)}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','actionResults.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('animationAuthoring.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'animationAuthoring.v1']})).not.toThrow();
});
