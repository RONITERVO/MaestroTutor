// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/runtimeDiagnostics.json';
import reservations from '../../../test-fixtures/browser/modelReservations.json';
import queue from '../../../test-fixtures/browser/modelQueue.json';
import images from '../../../test-fixtures/browser/imageReservations.json';
import audio from '../../../test-fixtures/browser/audioReservations.json';
import collision from '../../../test-fixtures/browser/collisionResources.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
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

it('preserves native model lease ownership and phases through the shared indexed fact',()=>{
 const id='runtime.modelReservation';
 for(const value of [reservations.loading,reservations.ready])expect(validFactValue(id,value)).toBe(true);
 expect(reservations.ready).toEqual({...reservations.loading,state:'ready'});
 expect(reservations.ready.role).toBe('object');expect(reservations.ready.worldId).toHaveLength(32);expect(reservations.ready.modelHash).toHaveLength(64);
 expect(behaviourFact(id)?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 for(const index of [0,5])expect(validateFactArguments(id,1,{index})).toBeNull();
 for(const args of [{index:-1},{index:6},{index:0.5},{index:'0'},{index:0,evict:true}])expect(validateFactArguments(id,1,args)).not.toBeNull();
 expect(validFactValue(id,{...reservations.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue(id,{...reservations.ready,vertices:'unknown'})).toBe(false);
});

it('preserves shared image residency and distinct immutable binding owners from Unity',()=>{
 expect(validFactValue('runtime.imageBudget',images.budget)).toBe(true);
 for(const value of [images.loading,images.ready])expect(validFactValue('runtime.imageReservation',value)).toBe(true);
 expect(images.ready).toEqual({...images.loading,state:'ready',textureBytes:28});
 expect(images.budget).toMatchObject({entries:1,owners:2,textureBytes:28,entryLimit:128,ownerLimitPerImage:132,textureByteLimit:67108864});
 expect(new Set(images.owners.map(owner=>owner.leaseId)).size).toBe(2);
 expect(new Set(images.owners.map(owner=>owner.target)).size).toBe(2);
 for(const owner of images.owners){
  expect(validFactValue('runtime.imageOwner',owner)).toBe(true);
  expect(owner).toMatchObject({reservationId:images.ready.reservationId,imageHash:images.ready.imageHash,worldId:images.owners[0].worldId,regionId:images.owners[0].regionId,role:'appearance'});
  expect(owner.worldId).toHaveLength(32);expect(owner.regionId).toHaveLength(32);
 }
 expect(behaviourFact('runtime.imageBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 for(const id of ['runtime.imageReservation','runtime.imageOwner'])expect(behaviourFact(id)?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 for(const index of [0,127])expect(validateFactArguments('runtime.imageReservation',1,{index})).toBeNull();
 for(const index of [-1,128,.5,'0'])expect(validateFactArguments('runtime.imageReservation',1,{index})).not.toBeNull();
 for(const index of [0,131])expect(validateFactArguments('runtime.imageOwner',1,{reservationId:images.ready.reservationId,index})).toBeNull();
 for(const args of [{reservationId:'old',index:0},{reservationId:images.ready.reservationId,index:132},{reservationId:images.ready.reservationId,index:0,release:true}])expect(validateFactArguments('runtime.imageOwner',1,args)).not.toBeNull();
 expect(validFactValue('runtime.imageBudget',{...images.budget,textureBytes:'unknown'})).toBe(false);
 expect(validFactValue('runtime.imageReservation',{...images.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.imageOwner',{...images.owners[0],leaseId:123})).toBe(false);
});

it('preserves actual shared PCM ownership without counting each voice as a separate buffer',()=>{
 expect(validFactValue('runtime.audioBudget',audio.budget)).toBe(true);
 expect(validFactValue('runtime.audioReservation',audio.ready)).toBe(true);
 expect(audio.budget).toMatchObject({sources:1,owners:2,retiring:0,readyPcmBytes:240000,reservedPcmBytes:240008,sourceLimit:8,ownerLimit:8,pcmByteLimit:11520064});
 expect(audio.ready).toMatchObject({kind:'tone',assetHash:'',state:'ready',owners:2,readyPcmBytes:audio.budget.readyPcmBytes});
 expect(new Set(audio.owners.map(owner=>owner.instanceId)).size).toBe(2);
 expect(new Set(audio.owners.map(owner=>owner.target)).size).toBe(2);
 for(const owner of audio.owners){
  expect(validFactValue('runtime.audioOwner',owner)).toBe(true);
  expect(owner).toMatchObject({reservationId:audio.ready.reservationId,worldId:audio.ready.worldId,regionId:audio.ready.regionId,sourceId:audio.ready.sourceId,role:'audio',emitter:'sound'});
  expect(owner.sourceRevision).toBeGreaterThan(0);
 }
 expect(behaviourFact('runtime.audioBudget')?.features).toEqual(['runtimeDiagnostics.v1']);
 for(const id of ['runtime.audioReservation','runtime.audioOwner'])expect(behaviourFact(id)?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 for(const index of [0,7]){
  expect(validateFactArguments('runtime.audioReservation',1,{index})).toBeNull();
  expect(validateFactArguments('runtime.audioOwner',1,{reservationId:audio.ready.reservationId,index})).toBeNull();
 }
 for(const index of [-1,8,.5,'0'])expect(validateFactArguments('runtime.audioReservation',1,{index})).not.toBeNull();
 for(const args of [{reservationId:'old',index:0},{reservationId:audio.ready.reservationId,index:8},{reservationId:audio.ready.reservationId,index:0,stop:true}])expect(validateFactArguments('runtime.audioOwner',1,args)).not.toBeNull();
 expect(validFactValue('runtime.audioBudget',{...audio.budget,readyPcmBytes:'unknown'})).toBe(false);
 expect(validFactValue('runtime.audioReservation',{...audio.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.audioOwner',{...audio.owners[0],sourceRevision:'unknown'})).toBe(false);
 expect(behaviourFact('runtime.audioBudget')?.description).toContain('not measured');
});

it('preserves actual native collision owners, accepted costs and deferred destruction',()=>{
 expect(validFactValue('runtime.collisionResources',collision.budget)).toBe(true);
 expect(collision.budget).toEqual({entries:1,preparing:0,ready:1,retiring:0,colliders:11,meshColliders:9,sourceTriangles:124,activeColliders:11});
 for(const value of [collision.ready,collision.active,collision.retiring])expect(validFactValue('runtime.collisionResource',value)).toBe(true);
 expect(collision.ready).toMatchObject({kind:'compound',role:'collision',phase:'ready',colliders:11,meshColliders:9,sourceTriangles:124,activeColliders:0});
 expect(collision.active).toEqual({...collision.ready,activeColliders:11});
 expect(collision.retiring).toEqual({...collision.ready,phase:'retiring'});
 expect(collision.ready.worldId).toHaveLength(32);expect(collision.ready.regionId).toHaveLength(32);expect(collision.ready.leaseId).toHaveLength(32);
 expect(behaviourFact('runtime.collisionResources')?.features).toEqual(['runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.collisionResource')?.features).toEqual(['factQueries.v1','runtimeDiagnostics.v1']);
 expect(behaviourFact('runtime.collisionResources')?.description).toContain('not measured RAM');
 expect(behaviourFact('runtime.collisionResources')?.description).toContain('not proof of simulation');
 for(const index of [0,65536,2147483647])expect(validateFactArguments('runtime.collisionResource',1,{index})).toBeNull();
 for(const args of [{index:-1},{index:2147483648},{index:.5},{index:'0'},{index:0,evict:true}])expect(validateFactArguments('runtime.collisionResource',1,args)).not.toBeNull();
 expect(validFactValue('runtime.collisionResources',{...collision.budget,sourceTriangles:'unknown'})).toBe(false);
 expect(validFactValue('runtime.collisionResource',{...collision.ready,privatePath:'hidden'})).toBe(false);
 expect(validFactValue('runtime.collisionResource',{...collision.ready,leaseId:123})).toBe(false);
});

it('keeps one admitted native model identity and cost while waiting, importing and ready',()=>{
 const id='runtime.modelReservation';
 for(const [state,value] of Object.entries(queue)){
  expect(validFactValue(id,value)).toBe(true);
  expect(value).toEqual({...queue.queued,state});
 }
 expect(queue.queued.state).toBe('queued');expect(queue.queued.vertices).toBeGreaterThan(0);
 expect(queue.queued.worldId).toHaveLength(32);expect(queue.queued.target).toHaveLength(32);
 expect(behaviourFact(id)?.description).toContain('queued reserves source costs before waiting');
 expect(behaviourFact(id)?.description).toContain('Disposed queued requests cancel');
});
