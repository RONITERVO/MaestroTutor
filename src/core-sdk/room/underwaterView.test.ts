// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityFeatures,validateCapabilityArguments} from '../../../shared/capabilities';
import {applyCurrentInputs} from '../../../shared/currentCapabilityInputs';
import {parseProgram} from './programs';
const action=()=>capabilityDefinition('object.container.view.configure')!;
it('loads saved optics and revision together without changing the existing container fact',()=>{
 const a=action(),args={...a.example!,target:'a'.repeat(32)},settings={version:1,enabled:false,tint:'#102030',densityPerMetre:.8};
 const value=applyCurrentInputs(a.input,args,{operation:'inspect',category:'facts',capability:'object.container.view',version:1,definition:behaviourFact('object.container.view')!,arguments:{target:args.target},available:true,value:{revision:12,configured:true,settings},status:'Available'});
 expect(value).toEqual({...args,revision:12,settings});expect(validateCapabilityArguments(a.id,1,value)).toBeNull();
 expect(capabilityFeatures(a.id,value)).toContain('underwaterView.v1');
 expect(capabilityDefinition('object.container.edit')!.input.oneOf![0].properties!.definition.properties).not.toHaveProperty('underwater');
 (value.settings as typeof settings).tint='#ffffff';expect(settings.tint).toBe('#102030');
});
it('authors the same bounded call as a program and rejects malformed optics',()=>{
 const a=action(),args:Record<string,unknown>={...a.example!,target:'b'.repeat(32)};
 const program={version:2,entry:'main',resources:[args.target],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[
  {id:'view',op:'invoke',capability:a.id,version:1,arguments:args,bindings:{}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 for(const change of [{version:2},{tint:'#ffffff00'},{densityPerMetre:-1},{enabled:'true'}]){
  expect(validateCapabilityArguments(a.id,1,{...args,settings:{...(args.settings as object),...change}})).not.toBeNull();
 }
 program.resources=[];expect(parseProgram(JSON.stringify(program)).program).toBeNull();
});
