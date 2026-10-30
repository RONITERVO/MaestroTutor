// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseProgram,type BehaviourProgram} from '../../core-sdk/room/programs';
import {declarationDraft,editProgramDeclarations} from './programDeclarationEditing';
const fixture=(name='declarations'):BehaviourProgram=>JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-'+name+'.json','utf8'));
it('renames state and custom signals across nested functions without changing literals or local namespaces',()=>{
 const p=fixture(),before=JSON.stringify(p);p.functions[1].locals=[{name:'total',initial:'total'}];
 const draft=declarationDraft(p);draft.state[0].name='sum';draft.state[1].name='history';draft.events[0].name='user.input';draft.events[1].name='user.output';
 const result=editProgramDeclarations(p,draft);expect(parseProgram(JSON.stringify(result)).error).toBeNull();
 expect(result.functions[1].body).toMatchObject([{variable:'sum',value:{args:[{state:'sum'},{var:'amount'}]}},{variable:'history',value:{args:[{state:'history'},{var:'amount'}]}},{event:'user.output',value:{state:'sum'}}]);
 expect(result.functions[0].body[0]).toMatchObject({body:[{event:'user.input'},{then:[{function:'remember',args:[{var:'amount'}]}]}]});
 expect(result.functions[1].locals).toEqual([{name:'total',initial:'total'}]);p.functions[1].locals=[];expect(JSON.stringify(p)).toBe(before);
});
it('preserves declaration identity through simultaneous swaps and rejects removed used declarations even if names are reused',()=>{
 const p=fixture(),draft=declarationDraft(p);draft.state[0].name='amounts';draft.state[1].name='total';draft.events.reverse();draft.events[0].name='user.add';draft.events[1].name='user.stored';
 const swapped=editProgramDeclarations(p,draft);expect(swapped.functions[1].body[0]).toMatchObject({variable:'amounts',value:{args:[{state:'amounts'},{var:'amount'}]}});
 expect(swapped.functions[1].body[2]).toMatchObject({event:'user.add'});
 for(const kind of ['state','events'] as const){const d=declarationDraft(p);d[kind][0].origin=null;expect(()=>editProgramDeclarations(p,d)).toThrow('still used');}
});
it('rejects incompatible types, duplicate identities and excessive declarations without modifying source',()=>{
 const p=fixture(),before=JSON.stringify(p),d=declarationDraft(p);d.state[0].type='text';d.state[0].initial='';expect(()=>editProgramDeclarations(p,d)).toThrow();
 const e=declarationDraft(p);e.events[0].type='text';expect(()=>editProgramDeclarations(p,e)).toThrow();
 const dup=declarationDraft(p);dup.state[1].origin=0;expect(()=>editProgramDeclarations(p,dup)).toThrow('Declarations changed');
 const many=declarationDraft(p);for(let i=0;i<17;i++)many.events.push({origin:null,name:'user.extra_'+i,type:'text'});expect(()=>editProgramDeclarations(p,many)).toThrow();expect(JSON.stringify(p)).toBe(before);
});
it('renames state in native input bindings and subscription expressions without rewriting native arguments',()=>{
 const p=fixture('proximity');p.state!.push({name:'radius',initial:.5});
 const loop=p.functions[0].body[0];if(loop.op!=='forever'||loop.body[0].op!=='awaitEvent')throw new Error('Expected wait');const wait=loop.body[0];wait.bindings={radius:{state:'radius'}};
 p.functions[0].body.push({id:'pauseWithState',op:'invoke',capability:'time.wait',version:1,arguments:{seconds:1},bindings:{seconds:{state:'radius'}}});
 const before=JSON.stringify(wait.arguments),d=declarationDraft(p);d.state[d.state.length-1].name='threshold';const result=editProgramDeclarations(p,d);
 expect(result.functions[0].body[0]).toMatchObject({body:[{bindings:{radius:{state:'threshold'}}},{}]});expect(JSON.stringify(wait.arguments)).toBe(before);
 expect(result.functions[0].body[1]).toMatchObject({arguments:{seconds:1},bindings:{seconds:{state:'threshold'}}});
});
it('removes unused declarations and retains inferred types when their initial lists are emptied',()=>{
 const p=fixture();p.state!.push({name:'unused',initial:{items:[1]}});p.events!.push({name:'user.unused',type:'text'});
 const d=declarationDraft(p);d.state[2].initial={items:[]};d.events.pop();const result=editProgramDeclarations(p,d);
 expect(result.state![2]).toEqual({name:'unused',initial:{items:[]},type:{record:{items:{list:'number'}}}});
 const remove=declarationDraft(result);remove.state.pop();expect(editProgramDeclarations(result,remove).state).toHaveLength(2);
});

it('detaches nested draft values and rejects a declared scalar type that differs from its initial value',()=>{
 const p=fixture(),d=declarationDraft(p);(d.state[1].initial as number[]).push(7);expect(p.state![1].initial).toEqual([]);
 d.state[0].type='boolean';expect(()=>editProgramDeclarations(p,d)).toThrow('Expected bounded');expect(p.state![0].initial).toBe(0);
});
