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
 for(const path of ['source','source.kind','source.members.0.target'])expect(capabilityParameterType(capture.capability,path,capture.arguments)).toBeNull();
 program.functions[0].locals[3].type={list:'text'};expect(parseProgram(JSON.stringify(program)).error).toContain('type');
 delete program.dataVersion;expect(parseProgram(JSON.stringify(program)).error).not.toBeNull();
});
it('revalidates complete computed lists rather than granting their shape permission',()=>{
 const call=source().functions[0].body[2],args=call.arguments;
 args.source.members=[{slot:'left',target:'a'.repeat(32)},{slot:'right',target:'b'.repeat(32)}];expect(validateCapabilityArguments(call.capability,1,args)).toBeNull();
 args.source.members[1].target=args.source.members[0].target;expect(validateCapabilityArguments(call.capability,1,args)).not.toBeNull();
 args.source.members=[];expect(validateCapabilityArguments(call.capability,1,args)).not.toBeNull();
});
