// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {validateCapabilityArguments,capabilityResources} from '../../../shared/capabilities';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/creation-prototypes-contract.json','utf8')) as {name:string;valid:boolean;arguments:Record<string,unknown>}[];
it.each(cases)('matches native creation-prototype boundaries: $name',c=>expect(validateCapabilityArguments('object.batch.create',1,c.arguments)===null).toBe(c.valid));
it('capture authorizes the exact originals while construction source grants none',()=>{
 const target='a'.repeat(32),args={name:'Castle',members:[{target,slot:'base',revision:1}]};
 expect(validateCapabilityArguments('program.module.captureConstruction',1,args)).toBeNull();expect(capabilityResources('program.module.captureConstruction',args)).toEqual([target]);
 expect(capabilityResources('object.batch.create',cases[0].arguments)).toEqual([]);
});
