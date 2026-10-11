// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityResources,validateCapabilityArguments,type CapabilityInvocation} from '../../../shared/capabilities';
import {boundedCapabilityCall} from '../../../shared/roomCatalog';
import {recipeEditCall} from '../../../shared/recipeEdits';
import {parseRecipe} from '../../../shared/roomRecipe';
import robot from '../../../test-fixtures/browser/recipeRobot.json';
const cases=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/recipe-edit-contract.json','utf8')) as {name:string;call:CapabilityInvocation;valid:boolean}[];
it.each(cases)('shares recipe patch validation for $name',entry=>{
 expect(validateCapabilityArguments(entry.call.id,1,entry.call.arguments)===null).toBe(entry.valid);
 if(entry.valid){expect(boundedCapabilityCall(entry.call)).toBe(true);expect(capabilityResources(entry.call.id,entry.call.arguments)).toEqual([entry.call.arguments.target]);}
});
it('projects only changed entries and explicit removals, leaving autoplay outside the patch',()=>{
 const before=parseRecipe(robot)!,after=structuredClone(before);after.parts.find(p=>p.id==='Head')!.size.x+=.01;after.tracks.splice(0,1);after.loop=!before.loop;
 const call=recipeEditCall('a'.repeat(32),7,before,after);expect(call.arguments.parts).toEqual([after.parts.find(p=>p.id==='Head')]);expect(call.arguments.removeParts).toEqual([]);expect(call.arguments.tracks).toEqual([]);expect(call.arguments.removeTracks).toEqual([before.tracks[0].part]);expect(call.arguments.loop).toBe(after.loop);expect(call.arguments).not.toHaveProperty('playing');expect(before).toEqual(parseRecipe(robot));
});
it('rejects invalid draft geometry before submitting a patch',()=>{
 const before=parseRecipe(robot)!,after=structuredClone(before);after.parts[0].parent=after.parts[0].id;expect(()=>recipeEditCall('a'.repeat(32),7,before,after)).toThrow('valid parts');
});

import native from '../../../test-fixtures/browser/recipeAuthoring.json';
import {validExecutionView} from '../../../shared/roomExecutions';
import {validateCapabilityOutput} from '../../../shared/capabilities';
it('uses the actual native patch and saved outcome for the same visual draft',()=>{
 const before=parseRecipe(native.beforeRecipe)!,after=structuredClone(before);after.parts.find(p=>p.id==='Head')!.size.x+=.01;
 expect(recipeEditCall(native.before.target,native.before.revision,before,after)).toEqual(native.call);
 expect(validExecutionView(native.receipt)).toBe(true);expect(validateCapabilityOutput(native.call.id,1,native.receipt.selected.output)).toBeNull();expect(native.receipt.selected.output).toEqual(native.after);expect(native.after.playing).toBe(false);expect(native.after.autoplay).toBe(false);expect(native.after.revision).toBeGreaterThan(native.before.revision);
});
