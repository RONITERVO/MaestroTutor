// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseRecipe,defaultRecipePattern} from '../../../shared/roomRecipe';
import {validateCapabilityArguments} from '../../../shared/capabilities';
import {invocationFeatureRequirements} from '../../../shared/programFeatures';
import {recipeEditCall} from '../../../shared/recipeEdits';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/pattern-contract.json','utf8')) as {name:string;recipe:unknown;valid:boolean}[];
it.each(cases)('shares native material admission: $name',entry=>{
 expect(parseRecipe(entry.recipe)!==null).toBe(entry.valid);
 expect(validateCapabilityArguments('object.create',1,{kind:'recipe',name:'Panel',x:0,y:1,z:1,scale:1,recipe:entry.recipe})===null).toBe(entry.valid);
});
it('keeps legacy solid recipes usable and carries pattern source through the same feature-gated edit',()=>{
 const before=parseRecipe(cases[0].recipe)!,after=structuredClone(before);after.parts[0].pattern={...defaultRecipePattern(),kind:'checker',plane:'xz',columns:8,rows:8};
 expect([...invocationFeatureRequirements('object.create',{kind:'recipe',recipe:before})]).not.toContain('recipePatterns.v1');
 const call=recipeEditCall('a'.repeat(32),1,before,after);
 expect([...invocationFeatureRequirements(call.id,call.arguments)]).toContain('recipePatterns.v1');expect((call.arguments.parts as unknown[])[0]).toEqual(after.parts[0]);
 after.parts[0].pattern.kind='solid';expect([...invocationFeatureRequirements(recipeEditCall('a'.repeat(32),1,before,after).id,recipeEditCall('a'.repeat(32),1,before,after).arguments)]).not.toContain('recipePatterns.v1');
});

it.each([{kind:['checker']},{plane:['xz']},{secondary:'#FFFFFF\n'}])('rejects coerced pattern fields and trailing text: %j',changed=>{
 const recipe=structuredClone(parseRecipe(cases[1].recipe)!);Object.assign(recipe.parts[0].pattern!,changed);
 expect(parseRecipe(recipe)).toBeNull();expect(validateCapabilityArguments('object.create',1,{kind:'recipe',name:'Panel',x:0,y:1,z:1,scale:1,recipe})).not.toBeNull();
});
