// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourCatalog} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityParameterType,validCapabilityInvocation,validateCapabilityArguments} from '../../../shared/capabilities';
import {stepInvocation,invocationStep} from './capabilitySteps';
import {newRuleStep} from './rules';
import {parseProgram,sequenceProgram} from './programs';

it.each(behaviourCatalog.actions)('uses the same named $id contract for simple authoring and saved execution',definition=>{
 const kind=behaviourCatalog.adapters.ruleStep.actionIds.indexOf(definition.id);
 const step={...newRuleStep(kind),seconds:kind===3?0:1},call=stepInvocation(step);
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
it('rejects malformed public calls and keeps returned schemas detached',()=>{
 const schema=capabilityDefinition('time.wait')!;schema.input.properties!.seconds.maximum=10000;
 for(const seconds of [31,-1,NaN,Infinity,'1',null])expect(validateCapabilityArguments('time.wait',1,{seconds})).not.toBeNull();
 expect(validCapabilityInvocation({id:'time.wait',version:1,arguments:{seconds:1},extra:true})).toBe(false);
 expect(validateCapabilityArguments('__proto__',1,{})).not.toBeNull();
 expect(validateCapabilityArguments('time.wait',1,JSON.parse('{"seconds":1,"__proto__":{}}'))).not.toBeNull();
 expect(validateCapabilityArguments('time.wait',1,{seconds:1})).toBeNull();
 expect(capabilityParameterType('avatar.gesture.play','gesture')).toBe('text');
 expect(capabilityParameterType('animation.embedded.play','clipIndex')).toBe('number');
 expect(capabilityParameterType('time.wait','target')).toBeNull();
 expect(capabilityParameterType('avatar.gesture.play','prop')).toBeNull();
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
