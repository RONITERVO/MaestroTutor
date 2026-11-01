// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseProgram,type BehaviourProgram} from '../../core-sdk/room/programs';
import {editProgramFunction,functionDraft} from './programFunctionEditing';
const fixture=(name:string):BehaviourProgram=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-'+name+'.json','utf8'));
it('preserves caller expressions and declaration identity during simultaneous swaps, additions and removals',()=>{
 const source=fixture('functions'),before=JSON.stringify(source),draft=functionDraft(source.functions[1]);
 draft.parameters[0].name='seconds';draft.parameters[1].name='scale';
 draft.parameters.reverse();draft.parameters.push({origin:null,name:'unused',type:'boolean'});
 let result=editProgramFunction(source,'computeDelay',draft);
 expect(source).toEqual(JSON.parse(before));
 expect(result.functions[0].body[0]).toMatchObject({args:[{value:.25},{value:2},{value:false}]});
 expect(result.functions[1].body[0]).toMatchObject({value:{op:'mul',args:[{var:'scale'},{var:'seconds'}]}});
 const remove=functionDraft(result.functions[1]);remove.parameters.pop();
 result=editProgramFunction(result,'computeDelay',remove);
 expect(result.functions[0].body[2]).toMatchObject({args:[{value:1},{value:2}]});
 expect(parseProgram(JSON.stringify(result)).error).toBeNull();
});
it('does not capture a deleted parameter just because a new declaration reuses its name',()=>{
 const source=fixture('functions'),draft=functionDraft(source.functions[1]);
 draft.parameters[0]={...draft.parameters[0],origin:null};
 expect(()=>editProgramFunction(source,'computeDelay',draft)).toThrow('still used');
});
it('keeps incompatible type edits, duplicate declarations and recursive renames out of saved source',()=>{
 const source=fixture('functions'),before=JSON.stringify(source),draft=functionDraft(source.functions[1]);
 draft.parameters[0].type='text';expect(()=>editProgramFunction(source,'computeDelay',draft)).toThrow();
 draft.parameters[0].type='number';draft.name='main';expect(()=>editProgramFunction(source,'computeDelay',draft)).toThrow('duplicate function');
 draft.name='computeDelay';draft.parameters[0].name='seconds';expect(()=>editProgramFunction(source,'computeDelay',draft)).toThrow('duplicate parameter');
 expect(JSON.stringify(source)).toBe(before);
});
it('renames native creation result destinations and later bound references without changing native arguments',()=>{
 const source=fixture('create'),draft=functionDraft(source.functions[0]),before=JSON.stringify(source);
 draft.locals.forEach(v=>v.name='new_'+v.name);const changed=editProgramFunction(source,'main',draft);
 const first=changed.functions[0].body[0],second=changed.functions[0].body[1];
 expect(first).toMatchObject({results:{objectId:'new_ball'}});
 expect(second).toMatchObject({bindings:{target:{var:'new_ball'}}});
 for(let i=0;i<2;i++){const a=source.functions[0].body[i],b=changed.functions[0].body[i];if(a.op!=='invoke'||b.op!=='invoke')throw new Error('Expected native action');expect(b.arguments).toEqual(a.arguments);expect(b.id).toBe(a.id);}
 expect(JSON.stringify(source)).toBe(before);
});
it('renames event destinations and subscription expressions while preserving state, facts and literal payloads',()=>{
 const source=fixture('proximity'),fn=source.functions[0];
 const walk=(body:typeof fn.body):Extract<typeof body[number],{op:'awaitEvent'}>=>{for(const n of body){if(n.op==='awaitEvent')return n;if(n.op==='forever')return walk(n.body);}throw new Error('Missing wait');};
 const wait=walk(fn.body);wait.bindings={radius:{op:'add',args:[{var:'distance'},{value:1}]}};
 fn.locals.push({name:'literal',initial:'distance'});
 const draft=functionDraft(fn);draft.locals.forEach(v=>v.name='new_'+v.name);
 const result=editProgramFunction(source,fn.name,draft),changed=walk(result.functions[0].body);
 expect(changed.received).toBe('new_'+wait.received);expect(changed.value).toBe('new_'+wait.value);
 expect(changed.fields).toEqual(Object.fromEntries(Object.entries(wait.fields!).map(([k,v])=>[k,'new_'+v])));
 expect(changed.bindings).toEqual({radius:{op:'add',args:[{var:'new_distance'},{value:1}]}});
 expect(changed.arguments).toEqual(wait.arguments);expect(result.functions[0].locals[result.functions[0].locals.length-1]).toEqual({name:'new_literal',initial:'distance'});
});
it('does not rename variables in a different function and preserves result ownership',()=>{
 const source=fixture('functions');source.functions[1].locals.push({name:'delay',initial:7});
 const draft=functionDraft(source.functions[0]);draft.locals[0].name='duration';
 const result=editProgramFunction(source,'main',draft);
 expect(result.functions[0].body[0]).toMatchObject({result:'duration'});
 expect(result.functions[0].body[1]).toMatchObject({bindings:{seconds:{var:'duration'}}});
 expect(result.functions[1]).toEqual(source.functions[1]);
});
