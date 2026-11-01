// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {validateCapabilityArguments,capabilityFeatures,capabilityResources} from '../../../shared/capabilities';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/material-packing-contract.json','utf8')) as {name:string;id:string;arguments:Record<string,unknown>;valid:boolean}[];
describe('shared measured material and packing',()=>{
 for(const row of cases)it(row.name,()=>expect(validateCapabilityArguments(row.id,1,row.arguments)===null).toBe(row.valid));
 it('uses the shared feature vocabulary and source ownership',()=>{
  for(const row of cases.filter(c=>c.valid)){
   expect(capabilityFeatures(row.id,row.arguments)).toContain('materialStores.v1');
   expect(capabilityResources(row.id,row.arguments)).toEqual(['a'.repeat(32)]);
   if(row.id==='object.material.pack')expect(capabilityFeatures(row.id,row.arguments)).toEqual(expect.arrayContaining(['heightFields.v1','materialPacking.v1','actionResults.v1']));
  }
 });
});
