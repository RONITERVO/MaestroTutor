// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityBindingFields,capabilityParameterType,literalCapabilityResources,separateCapabilityBindings,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-build-structure.json','utf8'));
it('composes creation lists into structure inputs and requires connected support',()=>{
 const program=source(),capture=program.functions[0].body[2];expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect(capabilityParameterType(capture.capability,'source.members',capture.arguments)).toEqual({list:{record:{slot:'text',target:'text'}}});
 expect(literalCapabilityResources(capture.capability,capture.arguments,capture.bindings,3)).toEqual([]);
 const features=[...programFeatureRequirements(program as BehaviourProgram),'behaviourPrograms.v3','eventPrograms.v1'];expect(features).toContain('structuredInputs.v1');
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(program)}}]}}];
 expect(()=>requireRoomCapabilities(commands,{capabilities:features.filter(f=>f!=='structuredInputs.v1')})).toThrow('structuredInputs.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:features})).not.toThrow();
});
it('uses fixed record types and prevents ambiguous parent and child bindings',()=>{
 const program=source(),create=program.functions[0].body[0];
 expect(capabilityBindingFields(create.capability,create.arguments)).toHaveProperty('position');
 create.bindings.position={value:create.arguments.position};expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 create.bindings['position.x']={value:1};expect(parseProgram(JSON.stringify(program)).error).toContain('child');
 expect(separateCapabilityBindings({value:{},'value-other':{},'value.x':{}})).toBe(false);
});
it('keeps static selectors, variable-shaped values and array indexes out of bindings',()=>{
 const program=source(),capture=program.functions[0].body[2];
 for(const path of ['source','source.kind'])expect(capabilityParameterType(capture.capability,path,capture.arguments)).toBeNull();
 program.functions[0].locals[3].type={list:'text'};expect(parseProgram(JSON.stringify(program)).error).toContain('type');
 delete program.dataVersion;expect(parseProgram(JSON.stringify(program)).error).not.toBeNull();
});
it('revalidates complete computed lists rather than granting their shape permission',()=>{
 const call=source().functions[0].body[2],args=call.arguments;
 args.source.members=[{slot:'left',target:'a'.repeat(32)},{slot:'right',target:'b'.repeat(32)}];expect(validateCapabilityArguments(call.capability,1,args)).toBeNull();
 args.source.members[1].target=args.source.members[0].target;expect(validateCapabilityArguments(call.capability,1,args)).not.toBeNull();
 args.source.members=[];expect(validateCapabilityArguments(call.capability,1,args)).not.toBeNull();
});

it('binds only existing canonical list indexes and retains unbound sibling authority',()=>{
 const args={name:'Parts',members:[{target:'a'.repeat(32),revision:1,slot:'first'},{target:'b'.repeat(32),revision:2,slot:'second'}]},id='program.module.captureConstruction';
 expect(capabilityParameterType(id,'members.0.target',args)).toBe('text');expect(capabilityParameterType(id,'members.1.revision',args)).toBe('number');expect(capabilityBindingFields(id,args)).toHaveProperty('members.1.revision');
 for(const path of ['members.01.target','members.-1.target','members.2.target','members.0.unknown','members.1e0.target','members.0.target.x'])expect(capabilityParameterType(id,path,args)).toBeNull();
 expect(literalCapabilityResources(id,args,{'members.0.target':{value:'c'.repeat(32)}},3)).toEqual(['b'.repeat(32)]);
 expect(literalCapabilityResources(id,args,{'members.0':{}},3)).toEqual(['b'.repeat(32)]);expect(args.members[1].target).toBe('b'.repeat(32));
 expect(separateCapabilityBindings({'members.0':{},'members.0.target':{}})).toBe(false);expect(separateCapabilityBindings({'members.0.target':{},'members.1.target':{}})).toBe(true);
 const recipe=source().functions[0].body[0].arguments;expect(capabilityParameterType('object.batch.create','batch.pieces.0.source.kind',recipe)).toBeNull();
});
