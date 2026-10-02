// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseProgram,type BehaviourProgram} from './programs';
import {validRuleRequest,validRuleView} from './rules';
import {parseRoomCommands,isRoomQuery} from './roomAgent';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {boundedCapabilityCall} from '../../../shared/roomCatalog';
import {moduleHash} from './programModules';
import {validProgramMemoryView,type ProgramMemoryView} from './programMemory';
import {declarationDraft,editProgramDeclarations} from '../../platform/quest/programDeclarationEditing';
const fixture=():BehaviourProgram=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-memory.json','utf8'));
const A='a'.repeat(32),C='c'.repeat(32);
it('preserves remembered identities through renames and does not convert per-run declarations',()=>{
 const source=fixture(),draft=declarationDraft(source);draft.state[0].name='score';const next=editProgramDeclarations(source,draft);
 expect(next.state).toEqual([{name:'score',initial:0,memory:C},{name:'perRun',initial:5}]);expect(next.functions[0].body[0]).toMatchObject({variable:'score',value:{args:[{state:'score'},{value:1}]}});expect(parseProgram(JSON.stringify(next)).error).toBeNull();expect(source.state?.[0].name).toBe('count');
 const ordinary=fixture();ordinary.functions[0].body=ordinary.functions[0].body.filter(n=>n.op!=='checkpoint');const perRun=declarationDraft(ordinary);delete perRun.state[0].memory;expect(editProgramDeclarations(ordinary,perRun).memoryVersion).toBeUndefined();
});
it('rejects missing extension, duplicate IDs, unsupported versions and unknown checkpoint fields',()=>{
 const source=fixture();delete source.memoryVersion;expect(parseProgram(JSON.stringify(source)).program).toBeNull();
 const duplicate=fixture();duplicate.state?.push({name:'other',initial:1,memory:C});expect(parseProgram(JSON.stringify(duplicate)).program).toBeNull();
 expect(parseProgram(JSON.stringify({...fixture(),memoryVersion:2})).program).toBeNull();
 const extra=fixture();extra.functions[0].body[2]={...extra.functions[0].body[2],unknown:true} as never;expect(parseProgram(JSON.stringify(extra)).program).toBeNull();
 const empty=fixture();delete empty.state![0].memory;expect(parseProgram(JSON.stringify(empty)).program).toBeNull();
});
it('requires the feature for both authoring and read-only memory queries',()=>{
 const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',reference:'new',sequence:{id:'',name:'Remember',interruption:0,repeat:false,program:JSON.stringify(fixture())}}]}}]});
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1'];expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('rememberedVariables.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'rememberedVariables.v1']})).not.toThrow();
 const query=parseRoomCommands({commands:[{action:'rules',rule:{action:'memory',target:A.toUpperCase(),page:0}}]});expect(isRoomQuery(query[0])).toBe(true);expect(validRuleRequest(query[0].rule)).toBe(true);expect(()=>requireRoomCapabilities(query,{capabilities})).toThrow('rememberedVariables.v1');
});
it('prevents reusable modules from silently acquiring a caller memory namespace',()=>{
 const module={version:1 as const,name:'Counter',exports:['main'],program:fixture()};const caller={...fixture(),moduleVersion:1,imports:[{alias:'counter',hash:moduleHash(module),module,signals:{}}]};expect(parseProgram(JSON.stringify(caller)).error).toContain('remembered');
});
const view=():ProgramMemoryView=>({ready:true,pending:false,busy:false,error:'',revision:'initial',programId:A,page:0,count:1,programs:[],cells:[{id:C,name:'items',typeJson:'{"list":"number"}',valueJson:'[]',saved:false,declared:true}]});
it('validates complete typed observations, including empty structured values, and rejects corrupt or oversized pages',()=>{
 expect(validProgramMemoryView(view())).toBe(true);for(const valueJson of ['[true]','{"a":1,"a":2}',JSON.stringify(Array(33).fill(1)),JSON.stringify('x'.repeat(129))]){const v=view();v.cells[0].valueJson=valueJson;expect(validProgramMemoryView(v)).toBe(false);}
 expect(validProgramMemoryView({...view(),cells:Array(5).fill(view().cells[0])})).toBe(false);expect(validProgramMemoryView({...view(),programs:[{id:A,name:'x',cells:33}]})).toBe(false);
});
it('allows bounded structured edit JSON without increasing generic argument string limits',()=>{
 const call={id:'program.memory.edit',version:1,arguments:{kind:'set',programId:A,variableId:C,revision:'initial',rulesRevision:1,valueJson:JSON.stringify(Array(32).fill(123456))}};
 expect(boundedCapabilityCall(call)).toBe(true);expect(boundedCapabilityCall({...call,arguments:{...call.arguments,valueJson:'x'.repeat(8193)}})).toBe(false);expect(boundedCapabilityCall({...call,arguments:{...call.arguments,programId:'x'.repeat(129)}})).toBe(false);
});
it('accepts actual native saved, running, stopped and reset observations',()=>{
 const evidence=JSON.parse(readFileSync('test-fixtures/browser/rememberedProgramState.json','utf8'));
 for(const [phase,state] of Object.entries(evidence))expect(validRuleView((state as {rules:unknown}).rules),phase).toBe(true);
});
