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
 for(const action of behaviourCatalog.actions)visit(action.input as CapabilitySchema);expect(count).toBe(100);
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

it('copies a complete typed container record without aliasing the native snapshot',()=>{
 const s=capabilityDefinition('object.container.edit')!.input,definition={frame:{position:{x:0,y:0,z:0},rotation:{x:0,y:0,z:0,w:1}},radius:.04,rectangle:{width:1.18,depth:.78},height:.29,capacityMl:266916,amountMl:224209.44,liquid:'Water',fluid:{version:1,densityKgM3:1000,linearDrag:2,angularDrag:1},color:{r:.1,g:.4,b:.9,a:1}};
 const args={operation:'configure',target:'a'.repeat(32),revision:1,definition:capabilityDefinition('object.container.edit')!.example!.definition};
 const snapshot:CatalogView={operation:'inspect',category:'facts',capability:'object.container',version:1,definition:behaviourFact('object.container')!,arguments:{target:args.target},available:true,value:{revision:12,configured:true,definition},status:'Available'};
 const loaded=applyCurrentInputs(s,args,snapshot);expect(loaded).toEqual({...args,revision:12,definition});
 const key=currentInputIdentity(s,loaded,'session');(loaded.definition as typeof definition).rectangle.width=1;
 expect(definition.rectangle.width).toBe(1.18);expect(currentInputIdentity(s,loaded,'session')).toBe(key);
 const bad=structuredClone(s);bad.oneOf![0].properties!.definition.properties!.rectangle.properties!.width.type='string';expect(()=>validateCurrentInputMapping(bad.oneOf![0])).toThrow('type differs');
});

it('fills both recall guards from one fact and refuses changing either guard without a fresh read',()=>{
 const input=capabilityDefinition('room.tools.recall')!.input;
 const value={stateId:'a'.repeat(32),revision:7,ready:true,reason:''};
 const observation:CatalogView={operation:'inspect',category:'facts',capability:'room.tools.recovery',version:1,definition:behaviourFact('room.tools.recovery')!,available:true,value,status:'Available'};
 const args=applyCurrentInputs(input,{stateId:'0'.repeat(32),revision:1},observation);
 expect(args).toEqual({stateId:value.stateId,revision:7});
 const identity=currentInputIdentity(input,args,'workspace');
 expect(currentInputIdentity(input,{...args,revision:8},'workspace')).not.toBe(identity);
 expect(currentInputIdentity(input,{...args,stateId:'b'.repeat(32)},'workspace')).not.toBe(identity);
});

it('refreshes the world placement guard without replacing the requested destination',()=>{
 const input=capabilityDefinition('world.viewpoint.set')!.input;
 const value={stateId:'a'.repeat(32),ready:true,reason:'',located:true,position:{x:1,y:0,z:2},yaw:12};
 const observation:CatalogView={operation:'inspect',category:'facts',capability:'world.viewpoint',version:1,definition:behaviourFact('world.viewpoint')!,available:true,value,status:'Available'};
 const target={stateId:'0'.repeat(32),position:{x:3,y:0,z:4},yaw:70};
 const loaded=applyCurrentInputs(input,target,observation);
 expect(loaded).toEqual({...target,stateId:value.stateId});
 const key=currentInputIdentity(input,loaded,'workspace');
 expect(currentInputIdentity(input,{...loaded,position:{x:4,y:0,z:5},yaw:30},'workspace')).toBe(key);
 expect(currentInputIdentity(input,{...loaded,stateId:'b'.repeat(32)},'workspace')).not.toBe(key);
 expect(currentInputIdentity(input,loaded,'other-workspace')).not.toBe(key);
});

it('loads complete region lighting without aliasing state and isolates edit guards by workspace',()=>{
 const input=capabilityDefinition('world.lighting.set')!.input;
 const settings={version:1,enabled:true,ambientColor:'#123456',sunColor:'#FFEEDD',ambientIntensity:.25,sunIntensity:.7,azimuth:45,elevation:30};
 const value={revision:19,worldId:'a'.repeat(32),regionId:'b'.repeat(32),settings,temporary:false};
 const observation:CatalogView={operation:'inspect',category:'facts',capability:'world.lighting',version:1,definition:behaviourFact('world.lighting')!,available:true,value,status:'Available'};
 const draft={...capabilityDefinition('world.lighting.set')!.example};
 const loaded=applyCurrentInputs(input,draft,observation);expect(loaded).toEqual({revision:19,settings});
 const identity=currentInputIdentity(input,loaded,'workspace');
 (loaded.settings as typeof settings).ambientIntensity=.5;expect(settings.ambientIntensity).toBe(.25);
 expect(currentInputIdentity(input,loaded,'workspace')).toBe(identity);
 expect(currentInputIdentity(input,{...loaded,revision:20},'workspace')).not.toBe(identity);
 expect(currentInputIdentity(input,loaded,'other')).not.toBe(identity);
 expect(()=>applyCurrentInputs(input,draft,{...observation,value:{...value,settings:{...settings,sunIntensity:3}}})).toThrow();
});

it('loads clock configuration without turning a moving time observation into an implicit seek',()=>{
 const settings={running:true,rate:60,cycleEnabled:true,frames:[{second:0,ambientColor:'#203060',sunColor:'#8899FF',ambientIntensity:.1,sunIntensity:0,azimuth:180,elevation:-45},{second:43200,ambientColor:'#FFFFFF',sunColor:'#FFDD99',ambientIntensity:.3,sunIntensity:1,azimuth:0,elevation:60}]};
 const value={revision:12,worldId:'a'.repeat(32),regionId:'b'.repeat(32),day:5,second:45001,settings,advancing:true,temporary:false};
 const snapshot:CatalogView={operation:'inspect',category:'facts',capability:'world.time',version:1,definition:behaviourFact('world.time')!,available:true,value,status:'Available'};
 const configure=capabilityDefinition('world.time.configure')!.input,input={revision:1,settings:{running:false,rate:1,cycleEnabled:false,frames:[]}};
 const loaded=applyCurrentInputs(configure,input,snapshot);expect(loaded).toEqual({revision:12,settings});
 const copied=loaded.settings as typeof settings;copied.frames[0].sunColor='#000000';expect(settings.frames[0].sunColor).toBe('#8899FF');
 const seek=capabilityDefinition('world.time.seek')!.input,destination={revision:1,day:2,second:64800};
 expect(applyCurrentInputs(seek,destination,snapshot)).toEqual({...destination,revision:12});
 const later={...snapshot,value:{...value,second:45002}} as CatalogView;
 expect(applyCurrentInputs(seek,destination,later)).toEqual({...destination,revision:12});
 expect(currentInputIdentity(configure,loaded,'room')).toBe(currentInputIdentity(configure,{...loaded,settings:{...copied,rate:120}},'room'));
 expect(()=>applyCurrentInputs(configure,input,{...snapshot,value:{...value,settings:{...settings,frames:Array.from({length:9},()=>settings.frames[0])}}} as CatalogView)).toThrow();
});
