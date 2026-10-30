// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import native from '../../../test-fixtures/browser/worldIdentity.json';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validFactValue,validateFactArguments} from '../../../shared/behaviourFacts';
import {validCatalogView} from '../../../shared/roomCatalog';
it('reads the same durable native world and region scope through the shared catalog',()=>{
 const definition=behaviourFact('world.identity')!;
 expect(native.capabilities).toContain('worldIdentity.v1');
 expect(definition.features).toContain('worldIdentity.v1');
 expect(validFactValue(definition.id,native.identity)).toBe(true);
 expect(validateFactArguments(definition.id,1,undefined)).toBeNull();
 expect(validateFactArguments(definition.id,1,{worldId:'another'})).not.toBeNull();
 expect(native.identity.worldId).toMatch(/^[a-f0-9]{32}$/);
 expect(native.identity.regionId).toMatch(/^[a-f0-9]{32}$/);
 expect(native.identity.regionId).not.toBe(native.identity.worldId);
 const view={operation:'inspect',category:'facts',capability:definition.id,version:1,definition,available:true,value:native.identity,status:'Available'};
 expect(validCatalogView(view)).toBe(true);
 expect(validCatalogView({...view,available:false,value:null})).toBe(true);
 for(const bad of [{worldId:native.identity.worldId},{...native.identity,regionId:42}])expect(validFactValue(definition.id,bad)).toBe(false);
});
