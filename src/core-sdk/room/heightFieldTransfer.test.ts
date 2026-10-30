// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {validateCapabilityArguments,capabilityFeatures,capabilityResources} from '../../../shared/capabilities';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/height-field-transfer-contract.json','utf8')) as {name:string;arguments:Record<string,unknown>;valid:boolean}[];
describe('shared surface volume transfer',()=>{
 for(const row of cases)it(row.name,()=>expect(validateCapabilityArguments('object.field.transfer',1,row.arguments)===null).toBe(row.valid));
 it('advertises the shared runtime and claims both endpoints',()=>{
  for(const row of cases.filter(c=>c.valid)){
   expect(capabilityFeatures('object.field.transfer',row.arguments)).toEqual(expect.arrayContaining(['heightFields.v1','heightFieldTransfer.v1']));
   expect(capabilityResources('object.field.transfer',row.arguments)).toEqual(['a'.repeat(32),'b'.repeat(32)]);
  }
 });
});
