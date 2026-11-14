// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {parseRecipe} from '../../../shared/roomRecipe';
import {validateCapabilityArguments} from '../../../shared/capabilities';
import {invocationFeatureRequirements} from '../../../shared/programFeatures';
import {recipeEditCall} from '../../../shared/recipeEdits';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/lathe-contract.json','utf8')) as {name:string;recipe:unknown;valid:boolean}[];
it.each(cases)('shares native lathe contract: $name',entry=>{
 expect(parseRecipe(entry.recipe)!==null).toBe(entry.valid);
 expect(validateCapabilityArguments('object.create',1,{kind:'recipe',name:'Cup',x:0,y:1,z:1,scale:1,recipe:entry.recipe})===null).toBe(entry.valid);
});
it('requires the geometry feature only when creating or editing a lathe part',()=>{
 const recipe=parseRecipe(cases[0].recipe)!,args={kind:'recipe',name:'Cup',x:0,y:1,z:1,scale:1,recipe};
 expect([...invocationFeatureRequirements('object.create',args)]).toContain('latheGeometry.v1');
 const after=structuredClone(recipe);after.parts[0].profile![1].x=.48;
 const call=recipeEditCall('a'.repeat(32),1,recipe,after);
 expect([...invocationFeatureRequirements(call.id,call.arguments)]).toContain('latheGeometry.v1');
 const primitive=parseRecipe(cases.find(c=>c.name==='legacy primitive')!.recipe)!;
 expect([...invocationFeatureRequirements('object.create',{...args,recipe:primitive})]).not.toContain('latheGeometry.v1');
});
