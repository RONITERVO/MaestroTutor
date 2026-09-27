// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourCatalog} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityParameterType,resolveCapabilitySchema,validCapabilityInvocation,validateCapabilityArguments} from '../../../shared/capabilities';
import {stepInvocation,invocationStep} from './capabilitySteps';
import {newRuleStep} from './rules';
import {parseProgram,sequenceProgram} from './programs';

it.each(behaviourCatalog.adapters.ruleStep.actionIds.map((id,kind)=>({id,kind})))('uses the same named $id contract for simple authoring and saved execution',({kind})=>{
 const definition=capabilityDefinition(stepInvocation(newRuleStep(kind)).id)!;
 const step={...newRuleStep(kind),targetId:definition.duration==='instant'?'f'.repeat(32):'maestro',seconds:kind===3||definition.duration==='instant'?0:1},call=stepInvocation(step);
 expect(validCapabilityInvocation(call)).toBe(true);
 expect(validateCapabilityArguments(call.id,call.version,call.arguments)).toBeNull();
 expect(stepInvocation(invocationStep(call,step.id))).toEqual(call);
 const program=sequenceProgram([step]);
 expect(parseProgram(JSON.stringify(program)).program).toEqual(program);
 expect(program.functions[0].body[0]).toMatchObject({op:'invoke',capability:definition.id,version:1,arguments:call.arguments});
 expect(program.functions[0].body[0]).not.toHaveProperty('step');
 expect(validCapabilityInvocation({...call,version:2})).toBe(false);
 expect(validCapabilityInvocation({...call,arguments:{...call.arguments,engineCode:'unsupported'}})).toBe(false);
});
it('declares one animation verb with source-specific channel and gesture limits',()=>{
 const definition=capabilityDefinition('animation.play')!;
 const full={target:'maestro',source:{kind:'gesture',gesture:'greeting'},channel:'wholeTarget',seconds:2};
 const upper={...full,channel:'upperBody'};
 expect(resolveCapabilitySchema(definition.input,full)?.['x-channels']).toEqual(['wholeTarget']);
 expect(resolveCapabilitySchema(definition.input,upper)?.['x-channels']).toEqual(['upperBody']);
 expect(capabilityDefinition('avatar.follow.user')?.channels).toEqual(['locomotion','gaze']);
 expect(validateCapabilityArguments(definition.id,1,upper)).toBeNull();
 expect(validateCapabilityArguments(definition.id,1,{...upper,source:{kind:'gesture',gesture:'walk'}})).not.toBeNull();
 expect(validateCapabilityArguments(definition.id,1,{...upper,target:'book'})).not.toBeNull();
});
it('rejects malformed public calls and keeps returned schemas detached',()=>{
 const schema=capabilityDefinition('time.wait')!;schema.input.properties!.seconds.maximum=10000;
 for(const seconds of [31,-1,NaN,Infinity,'1',null])expect(validateCapabilityArguments('time.wait',1,{seconds})).not.toBeNull();
 expect(validCapabilityInvocation({id:'time.wait',version:1,arguments:{seconds:1},extra:true})).toBe(false);
 expect(validateCapabilityArguments('__proto__',1,{})).not.toBeNull();
 expect(validateCapabilityArguments('time.wait',1,JSON.parse('{"seconds":1,"__proto__":{}}'))).not.toBeNull();
 expect(validateCapabilityArguments('time.wait',1,{seconds:1})).toBeNull();
 expect(capabilityParameterType('animation.play','source.gesture',{source:{kind:'gesture'},channel:'wholeTarget'})).toBe('text');
 expect(capabilityParameterType('animation.play','source.clipIndex',{source:{kind:'embedded'},channel:'wholeTarget'})).toBe('number');
 expect(capabilityParameterType('time.wait','target')).toBeNull();
 expect(capabilityParameterType('animation.play','prop',{source:{kind:'gesture'},channel:'wholeTarget'})).toBeNull();
});
it('retains prop arguments and validates declared vector constraints without using the simple editor adapter',()=>{
 const step={...newRuleStep(1),propId:'a'.repeat(32),propAvatarHash:'b'.repeat(64),propHand:0,propRelease:2,
  propOffset:{x:.1,y:.2,z:0},propRotation:{x:0,y:0,z:0,w:1},propReleaseAt:.5};
 const program=sequenceProgram([step]),node=program.functions[0].body[0];
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 const call=stepInvocation(step);expect(invocationStep(call,step.id)).toMatchObject(step);
 if(node.op!=='invoke')throw new Error('Expected invocation');
 (node.arguments.prop as {offset:unknown}).offset={x:1,y:1,z:1};
 expect(validateCapabilityArguments(node.capability,node.version,node.arguments)).not.toBeNull();
 expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});

it('declares instant physical effects with bounded scalar arguments that can be computed by programs',()=>{
 const target='e'.repeat(32),definition=capabilityDefinition('object.physics.impulse')!;
 expect(definition.duration).toBe('instant');expect(definition.description).toContain('Newton-seconds');
 expect(definition.channels).toEqual(['wholeTarget']);
 expect(capabilityParameterType(definition.id,'y')).toBe('number');
 const args={target,x:0,y:1.2,z:0};expect(validateCapabilityArguments(definition.id,1,args)).toBeNull();
 for(const bad of [{...args,target:'maestro'},{...args,target:'book'},{...args,y:21},{...args,y:Infinity},{...args,seconds:1}])
  expect(validateCapabilityArguments(definition.id,1,bad)).not.toBeNull();
 const program=sequenceProgram([{...newRuleStep(10),targetId:target,impulse:{x:0,y:1.2,z:0}}]);
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 const node=program.functions[0].body[0];if(node.op!=='invoke')throw new Error('Expected invocation');
 program.functions[0].locals.push({name:'force',initial:1.2});node.bindings.y={var:'force'};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 program.functions[0].locals[0].initial='bad';expect(parseProgram(JSON.stringify(program)).program).toBeNull();
 expect(validateCapabilityArguments('object.physics.stop',1,{target})).toBeNull();
});

it('discovers and validates rotation without adding a numeric action adapter',()=>{
 const id='object.rotation.set',definition=capabilityDefinition(id)!;
 expect(behaviourCatalog.adapters.ruleStep.actionIds).not.toContain(id);
 expect(definition.duration).toBe('instant');expect(definition.channels).toEqual(['wholeTarget']);
 const args={target:'book',pitch:20,yaw:90,roll:-10};expect(validateCapabilityArguments(id,1,args)).toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,yaw:181})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,yaw:'90'})).not.toBeNull();
 expect(validateCapabilityArguments(id,1,{...args,seconds:1})).not.toBeNull();
 expect(validCapabilityInvocation({id,version:1,arguments:args})).toBe(true);
});
