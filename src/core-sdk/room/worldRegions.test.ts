// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {capabilityDefinition,capabilityResources,validateCapabilityArguments,validateCapabilityOutput} from '../../../shared/capabilities';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validateFactArguments,validFactValue} from '../../../shared/behaviourFacts';
import {applyCurrentInputs,currentInputRequest} from '../../../shared/currentCapabilityInputs';
import {applyResourceChoice,readResourceChoicePage,resourceChoices} from '../../../shared/resourceChoices';
import {parseProgram} from './programs';
const target='1'.repeat(32),area='a'.repeat(32),home='b'.repeat(32);
it('shares exact area identity and revision choices while excluding world-owned objects',()=>{
 const schema=capabilityDefinition('object.region.assign')!.input;
 const initial={target,revision:1,regionId:'',regionRevision:0};
 expect(currentInputRequest(schema,initial)).toEqual({operation:'inspect',category:'facts',capability:'object.region',version:1,arguments:{target}});
 const value={target,revision:4,regionId:'',regionRevision:0,homeRegionId:home,temporary:false};
 const loaded=applyCurrentInputs(schema,initial,{operation:'inspect',category:'facts',capability:'object.region',version:1,definition:behaviourFact('object.region')!,arguments:{target},available:true,value,status:'Available'});
 const choice=resourceChoices(schema)[0];
 const page=readResourceChoicePage(schema,choice,0,{operation:'inspect',category:'facts',capability:'world.regions',version:1,definition:behaviourFact('world.regions')!,arguments:{offset:0},available:true,value:{offset:0,total:1,pageSize:3,entries:[{id:area,revision:7,name:'Garden'}]},status:'Available'});
 const chosen=applyResourceChoice(schema,choice,loaded,page.entries[0]);expect(chosen).toEqual({target,revision:4,regionId:area,regionRevision:7});expect(applyResourceChoice(schema,choice,chosen,null)).toEqual({...loaded,regionId:'',regionRevision:0});
 for(const id of ['book','maestro'])expect(validateCapabilityArguments('object.region.assign',1,{...chosen,target:id})).not.toBeNull();
});
it('programs declare creation ownership and use catalog actions without a separate region tool',()=>{
 const args={target,revision:4,regionId:area,regionRevision:7};expect(capabilityResources('object.region.assign',args)).toEqual([target]);
 const program={version:2,entry:'main',resources:[target],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[{id:'assign',op:'invoke',capability:'object.region.assign',version:1,arguments:args,bindings:{}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();program.resources=[];expect(parseProgram(JSON.stringify(program)).error).toContain('resource');
});

it('shares actual native retirement support, phase and failure through the same fact schema',()=>{
 const id='world.region.retention';
 for(const value of ['',area])expect(validateFactArguments(id,1,{id:value})).toBeNull();
 expect(validateFactArguments(id,1,{id:'book'})).not.toBeNull();
 const value={id:area,memberCount:2,residentCount:2,retainedCount:2,missingDependencyCount:0,reasons:['audio','ownership'],unloadingSupported:false,retirementState:'disabled',retirementIssue:''};
 expect(validFactValue(id,value)).toBe(true);
 for(const state of ['waiting','retained','ready','preparing','dormant','failed'])expect(validFactValue(id,{...value,unloadingSupported:true,retirementState:state,retirementIssue:state==='failed'?'Geometry preparation failed':''})).toBe(true);
 expect(validFactValue(id,{...value,retirementState:42})).toBe(false);
 expect(validFactValue(id,{...value,retirementIssue:42})).toBe(false);
 expect(validFactValue(id,{...value,retirementState:'failed',retirementIssue:'"'.repeat(128)})).toBe(true);
 expect(validFactValue(id,{...value,retirementIssue:'x'.repeat(129)})).toBe(false);
 expect(validFactValue(id,{...value,retirementIssue:'Line\nbreak'})).toBe(false);
 expect(validFactValue(id,{...value,residentCount:"2"})).toBe(false);
 expect(validFactValue(id,{...value,reasons:[true]})).toBe(false);
 expect(behaviourFact(id)!.description).toContain('transient diagnostic');
});

it('waits for persisted area changes and preserves exact receipt identities',()=>{
 for(const id of ['world.region.save','world.region.remove','object.region.assign'])expect(capabilityDefinition(id)?.duration).toBe('completion');
 expect(validateCapabilityOutput('world.region.save',1,{id:area,revision:14,temporary:false})).toBeNull();
 expect(validateCapabilityOutput('world.region.remove',1,{id:area,revision:0,temporary:false})).toBeNull();
 const assigned={target,revision:15,temporary:true};expect(validateCapabilityOutput('object.region.assign',1,assigned)).toBeNull();
 for(const change of [{target:'book'},{revision:0},{revision:'15'},{temporary:'false'}])expect(validateCapabilityOutput('object.region.assign',1,{...assigned,...change})).not.toBeNull();
});

it('shares explicit independent or together loading while accepting older omitted calls',()=>{
 const legacy={id:'',revision:0,name:'Garden',members:[]};
 expect(validateCapabilityArguments('world.region.save',1,legacy)).toBeNull();
 for(const loading of ['independent','together']){
  expect(validateCapabilityArguments('world.region.save',1,{...legacy,loading})).toBeNull();
  expect(validFactValue('world.region',{id:area,revision:1,name:'Garden',loading,memberCount:0,temporary:false})).toBe(true);
 }
 for(const loading of [null,true,'automatic'])expect(validateCapabilityArguments('world.region.save',1,{...legacy,loading})).not.toBeNull();
 expect(capabilityDefinition('world.region.save')!.example!.loading).toBe('independent');
});
