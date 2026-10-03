// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/runtimeDiagnostics.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue} from '../../../shared/behaviourFacts';
import {requireRoomCapabilities} from '../../../shared/roomControls';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';
import {parseRoomCommands} from './roomAgent';

it('accepts actual native observations with an explicit empty window and measurement scope',()=>{
 for(const [id,value] of [['runtime.frameIntervals',native.frames],['runtime.frameIntervals',native.empty],['runtime.modelBudget',native.models],['runtime.motionCache',native.motions]] as const){
  expect(validFactValue(id,value)).toBe(true);expect(behaviourFact(id)?.features).toEqual(['runtimeDiagnostics.v1']);
 }
 expect(native.frames).toMatchObject({active:true,hasSamples:true,editor:true});expect(native.frames.samples).toBeGreaterThan(0);
 expect(native.empty).toMatchObject({active:true,hasSamples:false,samples:0,milliseconds:{mean:0,p95:0,max:0}});
 expect(native.models.limits).toEqual({models:6,vertices:500000,textureMiPixels:64,morphMillionVertices:8});
 expect(native.motions).toMatchObject({clipLimit:8,curveValueLimit:800000});
 expect(behaviourFact('runtime.frameIntervals')?.description).toContain('not GPU times');
 expect(behaviourFact('runtime.modelBudget')?.description).toContain('not measured RAM/VRAM');
});
it('rejects malformed or unbounded observations instead of silently accepting extra telemetry',()=>{
 expect(validFactValue('runtime.frameIntervals',{...native.frames,deviceSerial:'private'})).toBe(false);
 expect(validFactValue('runtime.frameIntervals',{...native.frames,samples:Number.MAX_SAFE_INTEGER+1})).toBe(false);
 expect(validFactValue('runtime.frameIntervals',{...native.frames,milliseconds:{mean:NaN,p95:10,max:20}})).toBe(false);
 expect(validFactValue('runtime.modelBudget',{...native.models,reserved:{...native.models.reserved,textureMiPixels:'unknown'}})).toBe(false);
});
it('keeps catalog observation read-only and requires the native feature for stored programs',()=>{
 const query=parseRoomCommands({commands:[{action:'catalog',catalog:{operation:'inspect',category:'facts',capability:'runtime.frameIntervals',version:1}}]});
 expect(()=>requireRoomCapabilities(query,{capabilities:['catalog.v1','catalogVocabulary.v1']})).not.toThrow();
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'timing',initial:native.empty}],body:[{id:'read',op:'set',variable:'timing',value:{fact:'runtime.frameIntervals'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
 const commands=parseRoomCommands({commands:[{action:'rules',rule:{action:'edit',revision:1,edits:[{kind:'save',sequence:{id:'d'.repeat(32),name:'Observe timing',repeat:false,interruption:0,program:JSON.stringify(program)}}]}}]});
 const capabilities=['rules.v1','eventPrograms.v1','behaviourPrograms.v3','structuredValues.v1'];
 expect(()=>requireRoomCapabilities(commands,{capabilities})).toThrow('runtimeDiagnostics.v1');
 expect(()=>requireRoomCapabilities(commands,{capabilities:[...capabilities,'runtimeDiagnostics.v1']})).not.toThrow();
});
