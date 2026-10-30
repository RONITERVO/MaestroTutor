// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/imageResources.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {parseProgram,type BehaviourProgram} from './programs';

it('accepts captured native payload and texture lifetime records without releasing deferred destruction early',()=>{
 expect(validFactValue('runtime.imageResources',native.budget)).toBe(true);
 for(const value of [native.payload,native.ready,native.retiring])expect(validFactValue('runtime.imageResource',value)).toBe(true);
 expect(native.payload).toMatchObject({kind:'payload',phase:'held',role:'appearance',payloadBytes:16777216,textureBytes:0,workPending:false});
 expect(native.ready).toMatchObject({kind:'texture',phase:'ready',textureCount:2,textureBytes:56,decodeScratchBytes:28,payloadBytes:0});
 expect(native.retiring).toEqual({...native.ready,phase:'retiring'});
 expect(native.payload.reservationId).not.toBe(native.ready.reservationId);
 expect(native.payload.imageHash).toBe(native.ready.imageHash);
 expect(native.budget).toMatchObject({textureInstances:1,payloadReads:1,textureBytes:native.ready.textureBytes,payloadBytes:native.payload.payloadBytes,decodeScratchBytes:native.ready.decodeScratchBytes,decodesPerFrame:1});
});
it('shares scoped global diagnostics without redefining current-workspace image accounting',()=>{
 expect(behaviourFact('runtime.imageResources')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.imageResource')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.imageResources')?.description).toContain('Existing runtime.imageBudget remains current-workspace accounting');
 expect(behaviourFact('runtime.imageResource')?.description).toContain('first requester, not every current owner');
 expect(native.budget).toMatchObject({textureInstanceLimit:128,textureByteLimit:134217728,decodeScratchByteLimit:134217728,payloadReadLimit:4,payloadByteLimit:67108864});
 for(const index of [0,131])expect(validateFactArguments('runtime.imageResource',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:132},{index:.5},{index:'0'},{index:0,release:true}])expect(validateFactArguments('runtime.imageResource',1,args)).not.toBeNull();
});
it('rejects malformed image diagnostics and private payload fields',()=>{
 expect(validFactValue('runtime.imageResources',{...native.budget,textureBytes:'unknown'})).toBe(false);
 expect(validFactValue('runtime.imageResource',{...native.ready,encodedPixels:'private'})).toBe(false);
 expect(validFactValue('runtime.imageResource',{...native.payload,workPending:'yes'})).toBe(false);
});
it('requires native diagnostics for user or agent programs that inspect shared image capacity',()=>{
 const program:BehaviourProgram={version:3,dataVersion:1,entry:'main',resources:[],state:[],events:[],functions:[{name:'main',returns:'void',parameters:[],locals:[{name:'budget',initial:native.budget}],body:[{id:'read',op:'set',variable:'budget',value:{fact:'runtime.imageResources'}}]}]};
 expect(parseProgram(JSON.stringify(program)).error).toBeNull();expect([...programFeatureRequirements(program)]).toContain('runtimeDiagnostics.v1');
});
