// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {it,expect} from 'vitest';
import {parseProgram,type BehaviourProgram} from './programs';
import {moduleHash} from './programModules';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {editProgramFunction,functionDraft} from '../../platform/quest/programFunctionEditing';
import {declarationDraft,editProgramDeclarations} from '../../platform/quest/programDeclarationEditing';
const fixture=()=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-conditions.json','utf8')) as BehaviourProgram;
it('shares typed condition source and gates saving against the connected native runtime',()=>{
 const p=fixture(),source=JSON.stringify(p);expect(parseProgram(source).error).toBeNull();expect(p.resources).toEqual([]);
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{id:'a'.repeat(32),name:'Watch',repeat:false,interruption:0,program:source}}]}}];
 const capabilities=['behaviourPrograms.v3','eventPrograms.v1','structuredValues.v1','factQueries.v1'];expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('conditionWaits.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'conditionWaits.v1']})).not.toThrow();
});
it('rejects ambiguous destinations, unknown policies, ill-typed calculations and extra fields',()=>{
 for(const [key,value] of [['transition','future'],['initial','later'],['received','conditionValue'],['value','missing'],['test',{value:1}],['timeout',{value:true}],['extra',1]]){
  const p=fixture();Object.assign(p.functions[0].body[0],{[String(key)]:value});expect(parseProgram(JSON.stringify(p)).program,key as string).toBeNull();
 }
 const p=fixture();p.version=2;delete p.state;delete p.events;expect(parseProgram(JSON.stringify(p)).program).toBeNull();
});
it('renames condition inputs and destinations together without rewriting literal fact arguments',()=>{
 const p=fixture(),d=functionDraft(p.functions[0]);d.locals[0].name='seen';d.locals[1].name='truth';let next=editProgramFunction(p,'main',d);
 const state=declarationDraft(next);state.state[0].name='watched';state.state[1].name='limit';next=editProgramDeclarations(next,state);
 expect(next.functions[0].body[0]).toMatchObject({received:'seen',value:'truth',test:{args:[{args:[{arguments:{target:'book'},bindings:{target:{state:'watched'}}},{value:'x'}]},{state:'limit'}]}});expect(parseProgram(JSON.stringify(next)).error).toBeNull();
});
it('links private module state inside condition tests and timing expressions',()=>{
 const program=fixture(),watch=program.functions[0].body[0];if(watch.op!=='awaitCondition')throw new Error();program.state!.push({name:'duration',initial:.2});watch.stableSeconds={state:'duration'};
 const module={version:1 as const,name:'Condition module',exports:['main'],program},caller=fixture();caller.moduleVersion=1;caller.state=[];caller.imports=[{alias:'sensor',hash:moduleHash(module),module,signals:{}}];caller.functions[0].body=[{id:'call',op:'call',module:'sensor',function:'main',args:[]}];
 const parsed=parseProgram(JSON.stringify(caller));expect(parsed.error).toBeNull();expect(parsed.linked?.functions.find(f=>f.name==='sensor.main')?.body[0]).toMatchObject({stableSeconds:{state:'sensor.duration'},test:{args:[{args:[{bindings:{target:{state:'sensor.target'}}},{value:'x'}]},{state:'sensor.threshold'}]}});
});
