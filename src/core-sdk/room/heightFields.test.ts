// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {describe,it,expect} from 'vitest';
import {validateCapabilityArguments,capabilityFeatures} from '../../../shared/capabilities';
import {validateFactArguments} from '../../../shared/behaviourFacts';
const rows=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/height-field-contract.json','utf8')) as {name:string;capability:string;arguments:Record<string,unknown>;valid:boolean}[];
describe('shared editable height surfaces',()=>{
 for(const row of rows)it(row.name,()=>{expect(validateCapabilityArguments(row.capability,1,row.arguments)===null).toBe(row.valid);});
 it('advertises native support on all surface edits',()=>{for(const row of rows.filter(r=>r.valid))expect(capabilityFeatures(row.capability,row.arguments)).toContain('heightFields.v1');});
 it('pages exact heights at an explicit revision',()=>{expect(validateFactArguments('object.field.samples',1,{target:'a'.repeat(32),revision:1,offset:289})).toBeNull();expect(validateFactArguments('object.field.samples',1,{target:'a'.repeat(32),revision:1,offset:290})).not.toBeNull();});
});
