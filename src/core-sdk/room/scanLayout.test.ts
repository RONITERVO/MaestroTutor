// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/scanLayout.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validateFactArguments,validFactValue} from '../../../shared/behaviourFacts';
import {validCatalogView} from '../../../shared/roomCatalog';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('reads real native scan status, paged identities and exact local bounds through shared facts',()=>{
 for(const [id,value] of [['room.scan',native.unavailable],['room.scan',native.status],['room.scan.surfaces',native.page],['room.scan.surfaces',native.last],['room.scan.surfaces',native.empty],['room.scan.surface',native.detail],['room.scan.surface',native.planeOnly]] as const){
  expect(validFactValue(id,value)).toBe(true);expect(behaviourFact(id)?.features).toContain('roomScanLayout.v1');
 }
 expect(native.unavailable).toMatchObject({available:false,stateId:'',count:0});expect(native.status).toMatchObject({available:true,count:5,omitted:1});
 expect(native.page.map(x=>x.id)).toEqual([...native.page.map(x=>x.id)].sort());expect(native.page).toHaveLength(4);expect(native.last).toHaveLength(1);expect(native.empty).toEqual([]);
 expect(native.detail.pose.position).toEqual({x:.25,y:1.234567,z:-.1234567});expect(native.detail.plane).toEqual({present:true,center:{x:0,y:0,z:0},size:{x:2,y:1,z:0}});
 expect(native.planeOnly.volume).toEqual({present:false,center:{x:0,y:0,z:0},size:{x:0,y:0,z:0}});
});
it('requires exact scan query shapes and refuses raw or invented extra geometry in replies',()=>{
 const args={stateId:native.status.stateId,id:native.detail.id};
 expect(validateFactArguments('room.scan.surface',1,args)).toBeNull();expect(validateFactArguments('room.scan.surfaces',1,{stateId:args.stateId,offset:128})).toBeNull();
 for(const invalid of [{id:args.id},{...args,stateId:'last-room'},{...args,includeMesh:true}])expect(validateFactArguments('room.scan.surface',1,invalid)).not.toBeNull();
 for(const offset of [-1,.5,129])expect(validateFactArguments('room.scan.surfaces',1,{stateId:args.stateId,offset})).not.toBeNull();
 expect(validFactValue('room.scan.surface',{...native.detail,mesh:[0,1,2]})).toBe(false);expect(validFactValue('room.scan.surface',{...native.detail,pose:{...native.detail.pose,position:{x:NaN,y:0,z:0}}})).toBe(false);
 const reply={operation:'inspect',category:'facts',capability:'room.scan.surface',version:1,arguments:args,definition:behaviourFact('room.scan.surface'),available:false,value:null,status:'Layout changed; inspect again'};
 expect(validCatalogView(reply)).toBe(true);expect(validCatalogView({...reply,value:native.detail})).toBe(false);
 expect(validCatalogView({...reply,available:true,value:native.detail})).toBe(true);
});
it('uses the same bounded records in programs and declares the native layout requirement',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'layout',initial:native.unavailable}],body:[{id:'read',op:'set',variable:'layout',value:{fact:'room.scan'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('roomScanLayout.v1');
 expect(behaviourFact('room.scan.surface')?.description).toContain('unlike drawing patches');expect(behaviourFact('room.scan')?.description).toContain('not added to background');
});
