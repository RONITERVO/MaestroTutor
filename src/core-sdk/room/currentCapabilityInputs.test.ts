// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourCatalog,behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,type CapabilitySchema} from '../../../shared/capabilities';
import {applyCurrentInputs,currentInputIdentity,currentInputRequest,validateCurrentInputMapping,currentInputLocations,currentInputFields,currentInputsIdentity,applyCurrentInputSnapshots} from '../../../shared/currentCapabilityInputs';
import native from '../../../test-fixtures/browser/spatialSettings.json';
import type {CatalogView} from '../../../shared/roomCatalog';
const schema=()=>capabilityDefinition('object.physics.configure')!.input;
const args=()=>({...native.physics.selected.call.arguments});
const view=():CatalogView=>({operation:'inspect',category:'facts',capability:'object.physics.settings',version:1,definition:behaviourFact('object.physics.settings')!,arguments:{target:native.beforePhysics.target},available:true,value:native.beforePhysics,status:'Available'});
it('checks every native current-input annotation against both registered contracts',()=>{
 let count=0;
 const visit=(s:CapabilitySchema)=>{if(s['x-current'])count++;expect(()=>validateCurrentInputMapping(s)).not.toThrow();for(const child of [...s.oneOf??[],...Object.values(s.properties??{}),...s.items?[s.items]:[]])visit(child);};
 for(const action of behaviourCatalog.actions)visit(action.input as CapabilitySchema);expect(count).toBe(63);
});
it('loads exact fact values atomically and distinguishes guards from editable preferences',()=>{
 const next=applyCurrentInputs(schema(),args(),view());expect(next).toEqual({target:native.beforePhysics.target,revision:native.beforePhysics.revision,mode:native.beforePhysics.mode,shape:native.beforePhysics.shape,mass:native.beforePhysics.mass});
 const key=currentInputIdentity(schema(),next,'session');expect(currentInputIdentity(schema(),{...next,mass:7},'session')).toBe(key);
 for(const changed of [{...next,target:'a'.repeat(32)},{...next,revision:100}])expect(currentInputIdentity(schema(),changed,'session')).not.toBe(key);
 expect(currentInputIdentity(schema(),next,'anotherSession')).not.toBe(key);expect(args()).toEqual(native.physics.selected.call.arguments);
});
it('refuses missing, mismatched, unavailable, invalid and out-of-range fact responses',()=>{
 const v=view();if(v.operation!=='inspect'||v.category!=='facts')throw Error();
 for(const bad of [{...v,available:false,value:null},{...v,arguments:{target:'b'.repeat(32)}},{...v,version:2},{...v,value:{...native.beforePhysics,mass:25}},{...v,value:{...native.beforePhysics,revision:0}},{...v,value:{...native.beforePhysics,mode:'cloth'}}])expect(()=>applyCurrentInputs(schema(),args(),bad)).toThrow();
 expect(()=>currentInputRequest(schema(),{...args(),target:'book'})).toThrow();
});
it('refuses bad metadata references, dependency collisions, static fields and prototype paths',()=>{
 for(const change of [(s:CapabilitySchema)=>s['x-current']!.fact='missing.fact',(s:CapabilitySchema)=>s['x-current']!.version=2,(s:CapabilitySchema)=>s['x-current']!.fields.mass=['constructor'],(s:CapabilitySchema)=>s['x-current']!.fields.mass=['mode'],(s:CapabilitySchema)=>s['x-current']!.guards=['other'],(s:CapabilitySchema)=>s['x-current']!.arguments={},(s:CapabilitySchema)=>s['x-current']!.fields.target=['target'],(s:CapabilitySchema)=>s.properties!.mass['x-static']=true]){const s=schema();change(s);expect(()=>validateCurrentInputMapping(s)).toThrow();}
});
it('keeps selected walking identities and desired controller modes when reading only guards',()=>{
 for(const [id,value,fact] of [['avatar.walk.select',{target:'maestro',revision:1,source:'embedded',modelHash:'a'.repeat(64),clipIndex:12},'avatar.walk.settings'],['controller.mode.set',{operation:'user.enable',stateId:'0'.repeat(32)},'controller.mode']] as const){
  const s=capabilityDefinition(id)!.input;expect(currentInputRequest(s,value)).toEqual({operation:'inspect',category:'facts',capability:fact,version:1});
  if(id==='avatar.walk.select')expect(applyCurrentInputs(s,value,{operation:'inspect',category:'facts',capability:fact,version:1,definition:behaviourFact(fact)!,available:true,value:native.beforeWalk,status:'Available'})).toEqual({...value,revision:native.beforeWalk.revision});
 }
});

const constructionSchema=()=>capabilityDefinition('program.module.captureConstruction')!.input;
const construction=()=>({name:'My construction',members:[{target:'a'.repeat(32),revision:1,slot:'left'},{target:'b'.repeat(32),revision:2,slot:'right'}]});
const definitionView=(target:string,revision:number):CatalogView=>({operation:'inspect',category:'facts',capability:'object.definition',version:1,definition:behaviourFact('object.definition')!,arguments:{target},available:true,status:'Available',value:{target,revision,kind:'block',name:'Part',position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1},scale:1,content:{points:0,parts:0,frames:0,modelHash:'',recipePlaying:false}}});
it('reads every nested member in order and applies all guards atomically without replacing preferences',()=>{
 const input=construction(),schema=constructionSchema(),views=input.members.map((m,i)=>definitionView(m.target,100+i));
 expect(currentInputLocations(schema,input).map(l=>l.path)).toEqual([['members',0],['members',1]]);
 expect(currentInputFields(schema,input).map(f=>[f.path,f.guard])).toEqual([['members.0.revision',true],['members.1.revision',true]]);
 const next=applyCurrentInputSnapshots(schema,input,views);expect(next).toEqual({...input,members:input.members.map((m,i)=>({...m,revision:100+i}))});expect(input).toEqual(construction());
 const key=currentInputsIdentity(schema,input,'session');expect(currentInputsIdentity(schema,{...input,name:'Rename',members:input.members.map(m=>({...m,slot:m.slot+'_copy'}))},'session')).toBe(key);
 for(const members of [[...input.members].reverse(),input.members.slice(0,1),input.members.map((m,i)=>({...m,revision:m.revision+i})),input.members.map((m,i)=>i?{...m,target:'c'.repeat(32)}:m)])expect(currentInputsIdentity(schema,{...input,members},'session')).not.toBe(key);
 expect(currentInputsIdentity(schema,input,'next')).not.toBe(key);
 for(const bad of [views.slice(0,1),[...views].reverse(),[views[0],{...views[1],available:false,value:null}]]){expect(()=>applyCurrentInputSnapshots(schema,input,bad as CatalogView[])).toThrow();expect(input).toEqual(construction());}
});
it('bounds recursive current reads and traverses only selected present schema branches',()=>{
 const input=construction(),member=constructionSchema().properties!.members.items!;
 const list:CapabilitySchema={type:'array',items:member,minItems:0,maxItems:40};
 expect(()=>currentInputLocations(list,Array.from({length:33},()=>input.members[0]))).toThrow('32');
 expect(()=>currentInputLocations({...list,maxItems:1},input.members)).toThrow('contract');
 const nested:CapabilitySchema={type:'object',properties:{optional:member},required:[]};expect(currentInputLocations(nested,{})).toEqual([]);
 let schema=member,value:unknown=input.members[0];for(let i=0;i<13;i++){schema={type:'object',properties:{child:schema},required:['child']};value={child:value};}expect(()=>currentInputLocations(schema,value)).toThrow('12');
});

it('invalidates a reviewed nested snapshot when an ancestor variant changes',()=>{
 const branch=(kind:string):CapabilitySchema=>({type:'object',properties:{kind:{type:'string',enum:[kind]},child:constructionSchema().properties!.members.items!},required:['kind','child']});
 const schema:CapabilitySchema={type:'object',oneOf:[branch('first'),branch('second')],'x-discriminators':['kind']},child=construction().members[0];
 expect(currentInputsIdentity(schema,{kind:'first',child},'session')).not.toBe(currentInputsIdentity(schema,{kind:'second',child},'session'));
});
