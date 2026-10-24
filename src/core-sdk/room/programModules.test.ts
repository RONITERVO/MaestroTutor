// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseProgram,type BehaviourProgram} from './programs';
import {validRuleView} from './rules';
import {editProgramFunction,functionDraft} from '../../platform/quest/programFunctionEditing';
import {moduleHash} from './programModules';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {editProgramDeclarations,declarationDraft} from '../../platform/quest/programDeclarationEditing';
const fixture=(name:string)=>readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/'+name+'.json','utf8');
const contract=JSON.parse(fixture('program-module-contract')) as {cases:{name:string;source:string;valid:boolean}[];hashes:{value:unknown;hash:string}[]};
it.each(contract.cases)('$name',c=>{const result=parseProgram(c.source);expect(result.program!==null,result.error??'').toBe(c.valid);});
it('uses independent pinned hashes and canonical object order',()=>{
 for(const v of contract.hashes){expect(moduleHash(v.value)).toBe(v.hash);expect(moduleHash(Object.fromEntries(Object.entries(v.value as object).reverse()))).toBe(v.hash);}
 expect(moduleHash({n:-0})).toBe(moduleHash({n:0}));expect(moduleHash({n:1.25,s:'🙂'})).not.toBe(moduleHash({n:1.25,s:'😀'}));
});
it('keeps exact source snapshots, separate imported state and explicit signal wiring',()=>{
 const raw=JSON.parse(fixture('program-modules-nested')),before=JSON.stringify(raw),result=parseProgram(before);
 expect(result.error).toBeNull();expect(result.program).toEqual(raw);expect(JSON.stringify(raw)).toBe(before);
 expect(result.linked!.state!.map(v=>v.name)).toEqual(['first.inner.count','second.count']);
 expect(result.linked!.functions.find(f=>f.name==='first.inner.add')!.body[1]).toMatchObject({event:'user.first',id:'first.inner.send'});
 expect(JSON.stringify(result.linked)).not.toContain('"module":');expect(result.linked!.imports).toBeUndefined();
 const changed=JSON.parse(before);changed.imports[0].module.name='A new version';expect(parseProgram(JSON.stringify(changed)).error).toContain('pinned hash');expect(parseProgram(before).error).toBeNull();
});
it('refactors caller signal connections without rewriting any pinned module',()=>{
 const program=JSON.parse(fixture('program-modules')) as BehaviourProgram,draft=declarationDraft(program);draft.events[0].name='user.renamed';const edited=editProgramDeclarations(program,draft);
 expect(edited.imports![0].signals).toEqual({'user.changed':'user.renamed'});expect(edited.imports![0].module).toEqual(program.imports![0].module);expect(edited.imports![0].hash).toBe(program.imports![0].hash);
 draft.events=[];expect(()=>editProgramDeclarations(program,draft)).toThrow('still used');
});
it('requires the explicit native module feature before sending a saved program',()=>{
 const commands=[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{program:fixture('program-modules')}}]}}],capabilities=['behaviourPrograms.v3','eventPrograms.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('programModules.v1');expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'programModules.v1']})).not.toThrow();
});

it('accepts native qualified state observations when evidence is supplied',()=>{
 const path=process.env.MAESTRO_MODULE_EVIDENCE;if(!path)return;const state=JSON.parse(readFileSync(path+'/running.json','utf8'));
 expect(validRuleView(state.rules)).toBe(true);expect(state.rules.running[0].state).toEqual(expect.arrayContaining([{name:'first.inner.count',type:'number',value:'3'},{name:'second.count',type:'number',value:'5'}]));
});

it('renames local functions without capturing a same-named imported call',()=>{
 const program=JSON.parse(fixture('program-modules')) as BehaviourProgram;
 program.functions.push({name:'add',returns:'void',parameters:[],locals:[],body:[]});program.functions[0].body.push({id:'local',op:'call',function:'add',args:[]});
 const draft=functionDraft(program.functions[1]);draft.name='localAdd';draft.parameters.push({origin:null,name:'unused',type:'number'});
 const changed=editProgramFunction(program,'add',draft);expect(changed.functions[0].body[0]).toEqual(program.functions[0].body[0]);expect(changed.functions[0].body[changed.functions[0].body.length-1]).toMatchObject({function:'localAdd',args:[{value:0}]});expect(changed.imports).toEqual(program.imports);
});
