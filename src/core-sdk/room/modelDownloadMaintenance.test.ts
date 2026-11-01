// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {capabilityDefinition,capabilityOutputType,validateCapabilityArguments} from '../../../shared/capabilities';
import {requireRoomCapabilities} from '../../../shared/roomControls';

const modelHash='a'.repeat(64),inspectionRevision='b'.repeat(64);
it('requires native model maintenance support for both actions and the parameterized review fact',()=>{
 const feature='modelDownloadMaintenance.v1';
 for(const [id,args] of [['model.download.inspect',{modelHash}],['model.download.edit',{modelHash,inspectionRevision}]] as const){
  expect(validateCapabilityArguments(id,1,args)).toBeNull();
  const command={action:'execution',execution:{operation:'start',call:{id,version:1,arguments:args}}};
  expect(()=>requireRoomCapabilities([command],{capabilities:['execution.v1','avatarModels.v1']})).toThrow(feature);
  expect(()=>requireRoomCapabilities([command],{capabilities:['execution.v1',feature]})).not.toThrow();
 }
 const query={action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'model.download.review',version:1,arguments:{modelHash}}};
 const capabilities=['catalog.v1','catalogVocabulary.v1','factQueries.v1'];
 expect(()=>requireRoomCapabilities([query],{capabilities})).toThrow(feature);
 expect(()=>requireRoomCapabilities([query],{capabilities:[...capabilities,feature]})).not.toThrow();
 expect(behaviourFact('model.download.review')!.features).toEqual(expect.arrayContaining(['factQueries.v1',feature]));
});

it('accepts exact model identity and file revision without path, URL, or replacement identity inputs',()=>{
 for(const value of ['', '../model.glb','https://example.com/model.glb','A'.repeat(64),'a'.repeat(63)]){
  expect(validateCapabilityArguments('model.download.inspect',1,{modelHash:value})).not.toBeNull();
  expect(validateCapabilityArguments('model.download.edit',1,{modelHash:value,inspectionRevision})).not.toBeNull();
  expect(validateCapabilityArguments('model.download.edit',1,{modelHash,inspectionRevision:value})).not.toBeNull();
 }
 for(const args of [{modelHash},{modelHash,inspectionRevision,path:'model.glb'},{assetHash:modelHash,inspectionRevision},{modelHash,inspectionRevision,force:true}])expect(validateCapabilityArguments('model.download.edit',1,args)).not.toBeNull();
 expect(validateCapabilityArguments('model.download.inspect',1,{modelHash,inspectionRevision})).not.toBeNull();
 expect(validateCapabilityArguments('model.download.edit',2,{modelHash,inspectionRevision})).not.toBeNull();
});

it('keeps model browsing v1 independent of guarded download maintenance',()=>{
 const browse=capabilityDefinition('model.library.inspect')!;
 expect(browse.version).toBe(1);expect(browse.input['x-current']).toBeUndefined();
 expect(browse.input['x-features']).toEqual(['avatarModels.v1']);
 expect(validateCapabilityArguments(browse.id,1,{offset:0})).toBeNull();
 for(const args of [{offset:33},{offset:0,modelHash},{offset:0,inspectionRevision}])expect(validateCapabilityArguments(browse.id,1,args)).not.toBeNull();
 expect(capabilityOutputType(browse.id,'entries')).toEqual({list:{record:{bytes:'number',modelHash:'text',name:'text'}}});
 const command={action:'execution',execution:{operation:'start',call:{id:browse.id,version:1,arguments:{offset:0}}}};
 expect(()=>requireRoomCapabilities([command],{capabilities:['execution.v1','avatarModels.v1']})).not.toThrow();
});
