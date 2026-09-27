// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {describe,it,expect} from 'vitest';
import {readFileSync} from 'node:fs';
import {parseProgram,sequenceProgram,simpleProgramSteps,withSimpleProgramSteps,type BehaviourProgram} from './programs';
import {validSequence,newRuleStep,validRuleView} from './rules';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {parseRoomCommands} from './roomAgent';
const fixture=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-contract.json','utf8')) as {cases:{name:string;source:string;valid:boolean}[]};
describe('shared behaviour programs',()=>{
 it.each(fixture.cases)('$name',({source,valid})=>expect(parseProgram(source).program!==null).toBe(valid));
 it('preserves linear steps in a program and rejects mixed formats and unsupported clients',()=>{
  const steps=[{...newRuleStep(1),id:'b'.repeat(32)}],program=sequenceProgram(steps),source=JSON.stringify(program);
  expect(parseProgram(source).program).toEqual(program);expect((program.functions[0].body[0] as {id:string}).id).toBe(steps[0].id);
  const sequence={id:'a'.repeat(32),name:'Wave',interruption:0,repeat:false,program:source};expect(validSequence(sequence)).toBe(true);expect(validSequence({...sequence,steps})).toBe(false);
  const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence}]}}]});
  expect(()=>requireRoomCapabilities(commands,{})).toThrow('behaviour programs');expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v2']})).not.toThrow();
 });
 it('bounds source size, deep expressions, duplicate JSON keys and nested native fields',()=>{
  const source=fixture.cases[0].source;expect(parseProgram(source+' '.repeat(24000)).program).toBeNull();
  const program=JSON.parse(source) as BehaviourProgram;let test:unknown={value:true};for(let i=0;i<10;i++)test={op:'not',args:[test]};(program.functions[0].body[1] as unknown as {test:unknown}).test=test;expect(parseProgram(JSON.stringify(program)).program).toBeNull();
  expect(parseProgram(source.replace('"version":1','"version":1,"ver\\u0073ion":1')).program).toBeNull();
  expect(parseProgram(source.replace('"seconds":0.1','"seconds":null')).program).toBeNull();
 });
 it('accepts actual current Unity wire observations when provided',()=>{
  const path=process.env.MAESTRO_PROGRAM_EVIDENCE;if(!path)return;
  for(const phase of ['running','completed','cancelled']){const state=JSON.parse(readFileSync(`${path}/program-${phase}.json`,'utf8'));expect(validRuleView(state.rules),phase).toBe(true);}
 });
});

it('edits literal action views without losing identities, function names or extra resources',()=>{
 const program=sequenceProgram([{...newRuleStep(0),id:'move',targetId:'book'}]);program.entry='start';program.functions[0].name='start';program.resources.push('maestro');
 const source=JSON.stringify(program),steps=simpleProgramSteps(source)!;steps[0].targetId='e'.repeat(32);
 const edited=parseProgram(withSimpleProgramSteps(source,steps)).program!;expect(edited.entry).toBe('start');expect(edited.resources).toEqual(['maestro','e'.repeat(32)]);expect(edited.functions[0].body[0].id).toBe('move');expect(simpleProgramSteps(source)![0].targetId).toBe('book');
 const block=program.functions[0].body[0];if(block.op!=='action')throw new Error('Expected action');block.bindings.seconds={value:2};
 expect(simpleProgramSteps(JSON.stringify(program))).toBeNull();expect(()=>withSimpleProgramSteps(JSON.stringify(program),steps)).toThrow('function editor');
});
