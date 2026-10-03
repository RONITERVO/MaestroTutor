import {validRuleView} from './rules';
import {encodeModuleFile,decodeModuleFile} from './programModuleFile';
import type {ModuleRecord} from '../../../shared/programModuleIdentity';
// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {parseProgram,type BehaviourProgram} from './programs';
import {moduleHash} from './programModules';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {editProgramFunction,functionDraft} from '../../platform/quest/programFunctionEditing';
import {editProgramDeclarations,declarationDraft} from '../../platform/quest/programDeclarationEditing';
const source=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-parallel.json','utf8')) as BehaviourProgram;
const parallel=(p:BehaviourProgram)=>{const n=p.functions[0].body[0];if(n.op!=='parallel')throw Error('fixture');return n;};
describe('parallel programs use the native contract',()=>{
 it('keeps the native fixture exact and requires explicit native support',()=>{
  const p=source();expect(parseProgram(JSON.stringify(p)).program).toEqual(p);
  const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:JSON.stringify(p)}}]}}];
  expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','eventPrograms.v1']})).toThrow('parallelPrograms.v1');
  expect(()=>requireRoomCapabilities(commands,{capabilities:['behaviourPrograms.v3','eventPrograms.v1','parallelPrograms.v1']})).not.toThrow();
 });
 it('rejects missing/unknown versions, ambiguous results, wrong args and recursive branches',()=>{
  const mutations:((p:BehaviourProgram)=>void)[]=[p=>{delete p.parallelVersion;},p=>{p.parallelVersion=2 as 1;},p=>{p.version=2;},p=>{parallel(p).branches.pop();},p=>{parallel(p).branches[1].result='first';},p=>{parallel(p).branches[0].function='main';},p=>{parallel(p).branches[0].args[0]={value:3};},p=>{parallel(p).branches.push(...parallel(p).branches,...parallel(p).branches);}];
  for(const change of mutations){const p=source();change(p);expect(parseProgram(JSON.stringify(p)).program).toBeNull();}
 });
 it('renames functions, arguments, result locals and state through branch calls',()=>{
  let p=source();const draft=functionDraft(p.functions[1]);draft.name='travel';draft.parameters=[draft.parameters[2],draft.parameters[0],draft.parameters[1]];p=editProgramFunction(p,'move',draft);
  expect(parallel(p).branches[0]).toMatchObject({function:'travel',args:[{value:1},{value:'book'},{value:.4}]});
  const parent=functionDraft(p.functions[0]);parent.locals[0].name='left';p=editProgramFunction(p,'main',parent);expect(parallel(p).branches[0].result).toBe('left');
  const declarations=declarationDraft(p);declarations.state[0].name='total';p=editProgramDeclarations(p,declarations);expect(parseProgram(JSON.stringify(p)).error).toBeNull();
 });
 it('links parallel calls to pinned module exports and preserves nested namespaces',()=>{
  const module={version:1 as const,name:'Parallel example',exports:['move'],program:source()};
  const p=source();p.moduleVersion=1;p.imports=[{alias:'motions',hash:moduleHash(module),module,signals:{}}];parallel(p).branches[0].module='motions';
  const parsed=parseProgram(JSON.stringify(p));expect(parsed.error).toBeNull();expect(parallel(parsed.linked!).branches[0].function).toBe('motions.move');
  const nested=parsed.linked!.functions.find(fn=>fn.name==='motions.main')!.body[0];expect(nested.op==='parallel'&&nested.branches[0].function).toBe('motions.move');
  delete p.parallelVersion;expect(parseProgram(JSON.stringify(p)).error).toContain('parallel');
 });
 it('rejects removing a result variable still used by a parallel branch',()=>{
  const p=source(),draft=functionDraft(p.functions[0]);draft.locals=[];expect(()=>editProgramFunction(p,'main',draft)).toThrow('still used');
 });
});

it('accepts actual native parallel observations, private branch values and joined output',()=>{
 const capture=JSON.parse(readFileSync('test-fixtures/browser/parallelProgram.json','utf8'));
 for(const phase of ['saved','running','joined','paused'])expect(validRuleView(capture[phase].rules),phase).toBe(true);
 expect(parseProgram(JSON.stringify(capture.program)).error).toBeNull();expect(capture.saved.rules.running).toHaveLength(0);expect(capture.running.rules.running).toHaveLength(3);
 const parent=capture.running.rules.running.find((r:{parentRunId?:string})=>!r.parentRunId);expect(capture.running.rules.running.filter((r:{parentRunId:string})=>r.parentRunId===parent.id)).toHaveLength(2);
 expect(capture.running.rules.outcomes).toHaveLength(0);expect(capture.joined.rules.outcomes).toHaveLength(0);
 expect(capture.joined.rules.running[0].state).toContainEqual({name:'count',type:'number',value:'17'});expect(capture.paused.rules.running).toHaveLength(0);
});
it('keeps parallel definitions importable through portable module files',()=>{
 const definition={version:1,name:'Parallel example',exports:['move'],program:source()} as unknown as ModuleRecord,hash=moduleHash(definition);
 expect(decodeModuleFile(encodeModuleFile(hash,definition)).definition).toEqual(definition);
});
