// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {validateCapabilityArguments,capabilityFeatures} from '../../../shared/capabilities';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/containers-contract.json','utf8')) as {name:string;capability:string;arguments:Record<string,unknown>;valid:boolean}[];
describe('shared liquid container contracts',()=>{
 for(const row of cases)it(row.name,()=>{expect(validateCapabilityArguments(row.capability,1,row.arguments)===null).toBe(row.valid);});
 it('requires native container support for both editing and measured transfer',()=>{for(const row of cases.filter(r=>r.valid))expect(capabilityFeatures(row.capability,row.arguments)).toContain('containers.v1');});
});
