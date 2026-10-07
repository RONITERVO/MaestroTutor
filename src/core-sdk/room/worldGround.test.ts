// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/worldGround.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {validCatalogView} from '../../../shared/roomCatalog';
it('validates the same native accepted-ground results and bounded inputs for programs and agents',()=>{
 const definition=behaviourFact('world.ground')!;
 expect(native.capabilities).toContain('terrainTraversal.v1');expect(definition.features).toContain('terrainTraversal.v1');
 expect(validateFactArguments(definition.id,1,native.arguments)).toBeNull();
 for(const bad of [{...native.arguments,radius:0},{...native.arguments,radius:2},{...native.arguments,position:{x:NaN,y:0,z:0}},{...native.arguments,position:{x:26,y:0,z:0}},{...native.arguments,move:true}])expect(validateFactArguments(definition.id,1,bad)).not.toBeNull();
 for(const value of [native.supported,native.missing]){
  expect(validFactValue(definition.id,value)).toBe(true);
  expect(validCatalogView({operation:'inspect',category:'facts',capability:definition.id,version:1,definition,arguments:native.arguments,available:true,value,status:'Native read'})).toBe(true);
 }
 expect(native.supported.found).toBe(true);expect(native.missing.found).toBe(false);expect(native.supported.worldId).toBe(native.missing.worldId);
 expect(validFactValue(definition.id,{...native.supported,normal:{x:0,y:1}})).toBe(false);
});
