// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourCatalog,behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,resolveCapabilitySchema,type CapabilitySchema} from '../../../shared/capabilities';
import {resourceChoices,resourceChoiceRequest,readResourceChoicePage,applyResourceChoice} from '../../../shared/resourceChoices';
import type {CatalogView} from '../../../shared/roomCatalog';
const schema=()=>capabilityDefinition('object.visibility.assign')!.input;
const entry={id:'a'.repeat(32),name:'Distant landscape',revision:7};
const view=(fact='visibility.layers',value:unknown={offset:0,total:1,pageSize:3,entries:[entry]},offset=0):CatalogView=>({operation:'inspect',category:'facts',capability:fact,version:1,definition:behaviourFact(fact)!,arguments:{offset},available:true,value,status:'Available'}) as CatalogView;
it('checks every native resource choice against its paged fact and destination fields',()=>{
 let count=0;const visit=(s:CapabilitySchema)=>{count+=resourceChoices(s).length;for(const child of [...s.oneOf??[],...Object.values(s.properties??{}),...s.items?[s.items]:[]])visit(child);};
 for(const action of behaviourCatalog.actions)visit(action.input as CapabilitySchema);expect(count).toBe(4);
});
it('rejects arbitrary facts, guards, prototype paths and overlapping or incompatible destinations',()=>{
 for(const mutate of [(s:CapabilitySchema)=>s['x-choices']![0].fact='object.visibility',(s:CapabilitySchema)=>s['x-choices']![0].version=2,(s:CapabilitySchema)=>s['x-choices']![0].id='constructor',(s:CapabilitySchema)=>s['x-choices']![0].id='target',(s:CapabilitySchema)=>s['x-choices']![0].revision='revision',(s:CapabilitySchema)=>s['x-choices']![0].revision='layerId',(s:CapabilitySchema)=>s['x-choices']!.push({...s['x-choices']![0]}),(s:CapabilitySchema)=>s.properties!.layerId['x-static']=true]){
  const s=schema();mutate(s);expect(()=>resourceChoices(s)).toThrow();
 }
});
it('reads bounded exact pages without resolving names or substituting a stale revision',()=>{
 const s=schema(),choice=resourceChoices(s)[0];expect(resourceChoiceRequest(s,choice,0)).toMatchObject({capability:'visibility.layers',arguments:{offset:0}});
 const page=readResourceChoicePage(s,choice,0,view());expect(page).toEqual({offset:0,total:1,next:null,entries:[entry]});
 const value={target:'maestro',revision:99,layerId:'b'.repeat(32),layerRevision:4};
 expect(applyResourceChoice(s,choice,value,page.entries[0])).toEqual({...value,layerId:entry.id,layerRevision:7});expect(value.layerRevision).toBe(4);
 expect(applyResourceChoice(s,choice,value,null)).toEqual({...value,layerId:'',layerRevision:0});
});
it('rejects wrong pages, unavailable facts, duplicate IDs and malformed or stalled lists',()=>{
 const s=schema(),choice=resourceChoices(s)[0];
 for(const response of [view('environment.profiles'),view('visibility.layers',undefined,3),{...view(),available:false,value:null},view('visibility.layers',{offset:0,total:1,pageSize:3,entries:[]}),view('visibility.layers',{offset:0,total:17,pageSize:3,entries:[entry]}),view('visibility.layers',{offset:0,total:2,pageSize:3,entries:[entry,entry]}),...[-1,0,1.5,Infinity].map(revision=>view('visibility.layers',{offset:0,total:1,pageSize:3,entries:[{...entry,revision}]}))])expect(()=>readResourceChoicePage(s,choice,0,response as CatalogView)).toThrow();
 expect(()=>resourceChoiceRequest(s,choice,17)).toThrow();
});
it('fills nested appearance references together while preserving every unrelated binding setting',()=>{
 const action=capabilityDefinition('object.appearance.bind')!,s=action.input,choice=resourceChoices(s)[0],value=action.example!;
 const next=applyResourceChoice(s,choice,value,entry);expect(next).toEqual({...value,appearanceRevision:7,binding:{...value.binding as object,appearanceId:entry.id}});
 expect(()=>applyResourceChoice(s,choice,{...value,binding:null},entry)).toThrow();expect(()=>applyResourceChoice(s,choice,value,null)).toThrow();
});
it('uses a shared sound ID without inventing a revision guard, and handles its next cursor',()=>{
 const action=capabilityDefinition('object.audioEmitter.edit')!,s=resolveCapabilitySchema(action.input,action.example)!,choice=resourceChoices(s)[0];
 const entries=Array.from({length:4},(_,i)=>({...entry,id:String(i+1).repeat(32),kind:'tone'}));
 const page=readResourceChoicePage(s,choice,0,view('audio.source.list',{total:5,next:4,entries}));expect(page.next).toBe(4);
 expect(readResourceChoicePage(s,choice,0,view('audio.source.list',{total:1,next:-1,entries:[{...entry,name:'',kind:'tone'}]})).entries[0].name).toBe('');
 const next=applyResourceChoice(s,choice,action.example,page.entries[0]);expect(next).toEqual({...action.example,definition:{...action.example!.definition as object,source:entries[0].id}});expect(choice.revision).toBeUndefined();
 expect(()=>readResourceChoicePage(s,choice,0,view('audio.source.list',{total:5,next:-1,entries}))).toThrow();
});
