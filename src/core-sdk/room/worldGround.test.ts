// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/worldGround.json';
import unavailable from '../../../test-fixtures/browser/worldGroundUnavailable.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {validCatalogView} from '../../../shared/roomCatalog';
it('validates the same native accepted-ground results and bounded inputs for programs and agents',()=>{
 const definition=behaviourFact('world.ground')!;
 expect(native.capabilities).toContain('terrainTraversal.v1');expect(definition.features).toContain('terrainTraversal.v1');
 expect(validateFactArguments(definition.id,1,native.arguments)).toBeNull();
 for(const bad of [{...native.arguments,radius:0},{...native.arguments,radius:2},{...native.arguments,position:{x:NaN,y:0,z:0}},{...native.arguments,position:{x:26,y:0,z:0}},{...native.arguments,move:true}])expect(validateFactArguments(definition.id,1,bad)).not.toBeNull();
 for(const value of [native.supported,native.missing,native.pending,unavailable]){
  expect(validFactValue(definition.id,value)).toBe(true);
  expect(validCatalogView({operation:'inspect',category:'facts',capability:definition.id,version:1,definition,arguments:native.arguments,available:true,value,status:'Native read'})).toBe(true);
 }
 expect(native.supported.found).toBe(true);expect(native.missing.found).toBe(false);expect(native.supported.worldId).toBe(native.missing.worldId);
 expect(native.supported.availability).toBe('ready');expect(native.missing.availability).toBe('ready');
 expect(native.supported.dependencyCount).toBe(0);expect(native.supported.dependencies).toEqual([]);
 expect(native.pending.found).toBe(false);expect(native.pending.availability).toBe('pending');
 expect(native.pending.dependencies[0].state).toBe('pending');expect(native.pending.dependencyCount).toBe(1);
 expect(unavailable.found).toBe(false);expect(unavailable.availability).toBe('unavailable');
 expect(unavailable.dependencies[0].state).toBe('unavailable');expect(unavailable.dependencies[0].boundsKnown).toBe(true);
 expect(validFactValue(definition.id,{...native.supported,normal:{x:0,y:1}})).toBe(false);
 expect(validFactValue(definition.id,{...unavailable,dependencies:[{target:'ground',state:'unavailable'}]})).toBe(false);
 expect(validFactValue(definition.id,{...native.supported,dependencyCount:'unknown'})).toBe(false);
});
