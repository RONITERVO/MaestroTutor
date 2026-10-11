// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseRecipe} from '../../../shared/roomRecipe';
import {validateCapabilityArguments} from '../../../shared/capabilities';
import {invocationFeatureRequirements} from '../../../shared/programFeatures';
import {recipeEditCall} from '../../../shared/recipeEdits';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/sweep-contract.json','utf8')) as {name:string;recipe:unknown;valid:boolean}[];
it.each(cases)('shares native sweep admission: $name',entry=>{
 expect(parseRecipe(entry.recipe)!==null).toBe(entry.valid);
 expect(validateCapabilityArguments('object.create',1,{kind:'recipe',name:'Handle',x:0,y:1,z:1,scale:1,recipe:entry.recipe})===null).toBe(entry.valid);
});
it('uses existing creation/editing with explicit sweep support and exact source',()=>{
 const recipe=parseRecipe(cases[0].recipe)!,args={kind:'recipe',name:'Handle',x:0,y:1,z:1,scale:1,recipe};
 expect([...invocationFeatureRequirements('object.create',args)]).toContain('sweepGeometry.v1');
 expect([...invocationFeatureRequirements('object.create',args)]).not.toContain('latheGeometry.v1');
 const after=structuredClone(recipe);after.parts[0].path![2].z=.05;const call=recipeEditCall('a'.repeat(32),1,recipe,after);
 expect([...invocationFeatureRequirements(call.id,call.arguments)]).toContain('sweepGeometry.v1');expect((call.arguments.parts as unknown[])[0]).toEqual(after.parts[0]);
});
