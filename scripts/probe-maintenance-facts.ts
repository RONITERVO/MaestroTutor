// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import assert from 'node:assert/strict';
import {behaviourFact} from '../shared/behaviourCatalog';
import {validateFactArguments} from '../shared/behaviourFacts';
import type {CatalogRequest} from '../shared/roomCatalog';

/** Preserve the catalog distinction between no arguments and an empty record. */
export function maintenanceFactRequest(id:string,args:Record<string,unknown>,version=1):Extract<CatalogRequest,{operation:'inspect'}>{
 const definition=behaviourFact(id);assert.ok(definition,'Unknown maintenance fact');assert.equal(definition.version,version,'Unexpected fact version');
 assert.ok(definition.input||Object.keys(args).length===0,'A no-input fact cannot receive argument values');
 const input=definition.input?args:undefined;assert.equal(validateFactArguments(id,version,input),null);
 return {operation:'inspect',category:'facts',capability:id,version,...(input===undefined?{}:{arguments:input})};
}
