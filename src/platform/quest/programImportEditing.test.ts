// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {readFileSync} from 'node:fs';
import {moduleHash,type ModuleRecord} from '../../../shared/programModuleIdentity';
import {validCatalogRequest,validCatalogView} from '../../../shared/roomCatalog';
import {parseProgram,type BehaviourProgram} from '../../core-sdk/room/programs';
import {editProgramImport} from './programImportEditing';
const source=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-declarations.json','utf8'));
const definition=():ModuleRecord=>({version:1,name:'Remember amounts',exports:['remember'],program:structuredClone(source)});
const caller=():BehaviourProgram=>({version:3,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[],body:[]}]});
const draft={alias:'counter',signals:{'user.add':'user.request','user.stored':'user.total'},grantResources:false};
it('imports the inspected pin as a detached draft with explicit typed signals',()=>{
 const m=definition(),p=caller(),hash=moduleHash(m),next=editProgramImport(p,hash,m,draft);
 expect(next.imports?.[0]).toMatchObject({alias:'counter',hash,signals:draft.signals});
 expect(next.events).toEqual([{name:'user.request',type:'number'},{name:'user.total',type:'number'}]);
 expect(next.dataVersion).toBe(1);expect(parseProgram(JSON.stringify(next)).error).toBeNull();
 m.name='Changed later';expect(next.imports?.[0].module.name).toBe('Remember amounts');expect(p.imports).toBeUndefined();
});
it('requires explicit additional object access and compatible signal types without damaging the draft',()=>{
 const m=definition(),p=caller();m.program.resources=['maestro'];const hash=moduleHash(m),before=JSON.stringify(p);
 expect(()=>editProgramImport(p,hash,m,draft)).toThrow('additional objects');expect(JSON.stringify(p)).toBe(before);
 expect(editProgramImport(p,hash,m,{...draft,grantResources:true}).resources).toEqual(['maestro']);
 p.events=[{name:'user.total',type:'text'}];expect(()=>editProgramImport(p,hash,m,{...draft,grantResources:true})).toThrow('different payload type');
 expect(()=>editProgramImport(caller(),hash,m,{...draft,grantResources:true,signals:{}})).toThrow('every module signal');
});
it('rejects tampered pins and incompatible explicit upgrades while retaining the prior source',()=>{
 const m=definition(),hash=moduleHash(m),p=editProgramImport(caller(),hash,m,draft);
 p.functions[0].body.push({id:'call',op:'call',module:'counter',function:'remember',args:[{value:2}]});
 const before=JSON.stringify(p),changed=definition();changed.name='Revision two';
 expect(()=>editProgramImport(p,hash,changed,{...draft,replace:'counter'})).toThrow('inspected pin');
 expect(()=>editProgramImport(p,hash,m,draft)).toThrow('explicitly replace');
 changed.exports=['main'];expect(()=>editProgramImport(p,moduleHash(changed),changed,{...draft,replace:'counter'})).toThrow();
 expect(JSON.stringify(p)).toBe(before);
 changed.exports=['remember'];const upgraded=editProgramImport(p,moduleHash(changed),changed,{...draft,replace:'counter'});
 expect(upgraded.imports?.[0].hash).toBe(moduleHash(changed));expect(upgraded.functions).toEqual(p.functions);expect(p.imports?.[0].hash).toBe(hash);
});
it('checks library wire identity separately from action catalog IDs and rejects changed contents',()=>{
 const m=definition(),hash=moduleHash(m),query={operation:'inspect',category:'modules',capability:hash,version:1};
 expect(validCatalogRequest(query)).toBe(true);expect(validCatalogRequest({...query,category:'actions'})).toBe(false);
 const view={...query,definition:m,revision:2,ready:true,pending:false,status:'Inspected'};
 expect(validCatalogView(view)).toBe(true);expect(validCatalogView({...view,ready:false})).toBe(false);
 expect(validCatalogView({...view,definition:{...m,name:'Wrong pin'}})).toBe(false);
 expect(validCatalogView({...view,definition:null,status:'Unavailable'})).toBe(true);
 const page={operation:'search',category:'modules',query:'',offset:0,pageSize:6,total:1,entries:[{id:hash,version:1,label:m.name}],revision:2,ready:true,pending:false,status:'Read'};
 expect(validCatalogView(page)).toBe(true);expect(validCatalogView({...page,total:273})).toBe(false);expect(validCatalogView({...page,entries:[...page.entries,...page.entries]})).toBe(false);
});
