// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityInput,capabilityParameterType,literalCapabilityResources,validateCapabilityArguments,type CapabilityInvocation} from '../../../shared/capabilities';
import {parseProgram,sequenceProgram} from './programs';
import {newRuleStep} from './rules';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/animation-contract.json','utf8')) as {name:string;call:CapabilityInvocation;valid:boolean;channels?:string[]}[];
it.each(cases)('shares the native source/channel contract for $name',entry=>{
 expect(validateCapabilityArguments(entry.call.id,entry.call.version,entry.call.arguments)===null).toBe(entry.valid);
 if(entry.valid){const schema=capabilityInput(entry.call.id,entry.call.arguments)!;expect(schema['x-channels']).toEqual([entry.channels![0]]);}
});
it('preserves exact sources in simple/program round trips while retiring the parallel public verbs',()=>{
 for(const name of ['animation.library.play','animation.embedded.play','avatar.gesture.play','avatar.gesture.upperBody','animation.recording.play','animation.recipe.play'])expect(capabilityDefinition(name)).toBeNull();
 const motion='b'.repeat(32),program=sequenceProgram([{...newRuleStep(7),motionId:motion}]);
 const node=program.functions[0].body[0];expect(node).toMatchObject({capability:'animation.play',arguments:{source:{kind:'library',motionId:motion},channel:'wholeTarget'}});
 expect(parseProgram(JSON.stringify(program)).program).toEqual(program);
});
it('allows typed nested scalar expressions without binding the source/channel selectors or undeclared placeholders',()=>{
 const program=sequenceProgram([newRuleStep(1)]),node=program.functions[0].body[0];if(node.op!=='invoke')throw new Error('Expected invocation');
 node.bindings={'source.gesture':{value:'pointing'}};expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect(capabilityParameterType(node.capability,'source.gesture',node.arguments)).toBe('text');
 for(const key of ['source.kind','channel','source.missing','constructor','__proto__']){
  node.bindings={[key]:{value:'greeting'}};expect(parseProgram(JSON.stringify(program)).program).toBeNull();
 }
 node.bindings={'prop.objectId':{value:'c'.repeat(32)}};expect(parseProgram(JSON.stringify(program)).error).toContain('placeholder');
});
it('removes bound nested resources only from the detached declaration view',()=>{
 const args=structuredClone(cases[cases.length-1].call.arguments),before=structuredClone(args);
 const literal=literalCapabilityResources('animation.play',args,{'prop.objectId':{value:'d'.repeat(32)}},3);
 expect(literal).toEqual(['maestro']);expect(args).toEqual(before);
 expect(literalCapabilityResources('animation.play',args,{'prop.objectId':{value:'d'.repeat(32)}},2)).toContain('c'.repeat(32));
});
