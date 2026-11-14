// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it,vi} from 'vitest';
// Model a future catalog registration with no numeric simple-editor adapter.
vi.mock('../../../shared/behaviourCatalog',async importOriginal=>{
 const original=await importOriginal<typeof import('../../../shared/behaviourCatalog')>();
 return {...original,behaviourCatalog:{...original.behaviourCatalog,actions:[...original.behaviourCatalog.actions,
  {id:'object.future.action',version:1,label:'Future action',duration:'instant',ownership:'exclusive',channels:[],requirements:[],
   input:{type:'object',properties:{subject:{type:'string',enum:['book'],'x-resource':'object'},amount:{type:'number',minimum:0,maximum:10}},required:['subject','amount'],additionalProperties:false}}]}};
});
import {parseProgram,simpleProgramSteps} from './programs';
it('authors a catalog extension without adding a private RuleStep mapping',()=>{
 const program={version:2,entry:'main',resources:['book'],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[
  {id:'future',op:'invoke',capability:'object.future.action',version:1,arguments:{subject:'book',amount:2},bindings:{}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect(simpleProgramSteps(JSON.stringify(program))).toBeNull();
 program.resources=[];expect(parseProgram(JSON.stringify(program)).error).toContain('resource');
 program.resources=['book'];program.functions[0].body[0].arguments.amount=11;
 expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});
