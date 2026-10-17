// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {validCollisionRecipe} from '../../../shared/collisionRecipe';
import {validateCapabilityArguments} from '../../../shared/capabilities';
import {invocationFeatureRequirements} from '../../../shared/programFeatures';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/collision-contract.json','utf8')) as {name:string;collision:unknown;valid:boolean}[];
it.each(cases)('shares the native collision contract: $name',c=>{
 expect(validCollisionRecipe(c.collision)).toBe(c.valid);
 expect(validateCapabilityArguments('object.collision.edit',1,{target:'a'.repeat(32),revision:1,collision:c.collision})===null).toBe(c.valid);
});
it('requires collision support for the same agent and program call',()=>{
 expect([...invocationFeatureRequirements('object.collision.edit',{target:'a'.repeat(32),revision:1,collision:cases[0].collision})]).toContain('collisionShapes.v1');
});
