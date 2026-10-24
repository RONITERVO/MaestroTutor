// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityOutputType,capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-batch-create.json','utf8'));
it('shares the structured creation-result contract with native execution and rejects scalar or mismatched destinations',()=>{
 const program=source(),call=program.functions[0].body[0];expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect(capabilityOutputType(call.capability,'objectIds')).toEqual({list:'text'});expect(capabilityResources(call.capability,call.arguments)).toEqual([]);
 expect([...programFeatureRequirements(program as BehaviourProgram)]).toEqual(expect.arrayContaining(['batchCreation.v1','structuredResults.v1','structuredValues.v1']));
 program.functions[0].locals[0]={name:'pieces',initial:''};expect(parseProgram(JSON.stringify(program)).error).not.toBeNull();
 const bad=source();bad.functions[0].locals[0]={name:'pieces',type:{list:'number'},initial:[]};expect(parseProgram(JSON.stringify(bad)).error).not.toBeNull();
});
it('rejects duplicate slots, invalid composite transforms, live recipe playback and missing exact template hashes',()=>{
 const initial=source().functions[0].body[0].arguments;
 const check=(change:(value:typeof initial)=>void)=>{const args=structuredClone(initial);change(args);expect(validateCapabilityArguments('object.batch.create',1,args)).not.toBeNull();};
 check(a=>{a.blueprint.pieces[1].slot=a.blueprint.pieces[0].slot;});check(a=>{a.position.x=25;});check(a=>{a.scale=4;a.blueprint.pieces[0].scale=2;});
 check(a=>{a.blueprint.pieces[0].source.templateHash='e'.repeat(64);});
 for(const control of [9,127,133])check(a=>{a.blueprint.pieces[0].name='Piece'+String.fromCharCode(control);});
 const recipe=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Resources/Creation/Templates/robot.json','utf8')).definition.recipe;recipe.playing=true;
 check(a=>{a.blueprint.pieces[0].source={kind:'recipe',recipe};});
 const rotated=structuredClone(initial);rotated.rotation={x:0,y:Math.SQRT1_2,z:0,w:Math.SQRT1_2};rotated.scale=1.5;expect(validateCapabilityArguments('object.batch.create',1,rotated)).toBeNull();
});
it('keeps generic empty-list result destinations typed and unsupported optional shapes unexposed',()=>{
 expect(capabilityOutputType('model.library.inspect','entries')).toEqual({list:{record:{bytes:'number',modelHash:'text',name:'text'}}});
 const program=source();program.functions[0].locals=[{name:'entries',type:capabilityOutputType('model.library.inspect','entries'),initial:[]}];program.functions[0].body=[{id:'read',op:'invoke',capability:'model.library.inspect',version:1,arguments:capabilityDefinition('model.library.inspect')!.example,bindings:{},results:{entries:'entries'}}];expect(parseProgram(JSON.stringify(program)).error).toBeNull();
});
