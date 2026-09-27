// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityInput,capabilityResources,validateCapabilityArguments,type CapabilityInvocation} from '../../../shared/capabilities';
import {invocationStep,stepInvocation} from './capabilitySteps';
import {parseProgram,sequenceProgram} from './programs';
import {newRuleStep} from './rules';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/creation-contract.json','utf8')) as {name:string;call:CapabilityInvocation;valid:boolean}[];
it.each(cases)('shares the native creation contract for $name',entry=>{
 expect(validateCapabilityArguments(entry.call.id,entry.call.version,entry.call.arguments)===null).toBe(entry.valid);
 if(entry.valid){expect(capabilityResources(entry.call.id,entry.call.arguments)).toEqual([]);expect(stepInvocation(invocationStep(entry.call,'create'))).toEqual(entry.call);}
});
it('preserves the creation kind, recipe and exact result wiring without exposing old public IDs',()=>{
 expect(capabilityDefinition('object.create.primitive')).toBeNull();expect(capabilityDefinition('object.create.recipe')).toBeNull();
 for(const kind of [12,13]){const program=sequenceProgram([newRuleStep(kind)]);expect(parseProgram(JSON.stringify(program)).program).toEqual(program);expect(program.functions[0].body[0]).toMatchObject({capability:'object.create',arguments:{kind:kind===12?'primitive':'recipe'}});}
});
it('keeps kinds literal and leaves coordinates and recipe scalar settings bindable',()=>{
 const program=sequenceProgram([newRuleStep(13)]),node=program.functions[0].body[0];if(node.op!=='invoke')throw new Error('Expected creation');
 expect(capabilityInput(node.capability,node.arguments)?.['x-features']).toContain('recipeCreation.v1');
 node.bindings={kind:{value:'recipe'}};expect(parseProgram(JSON.stringify(program)).error).toContain('Unsupported');
 node.bindings={x:{value:.4},'recipe.duration':{value:2}};expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 node.bindings={x:{value:'wrong type'}};expect(parseProgram(JSON.stringify(program)).error).toContain('type');
});
