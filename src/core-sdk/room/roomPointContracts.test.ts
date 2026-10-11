// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {behaviourFact} from '../../../shared/behaviourCatalog';
import {validateFactArguments} from '../../../shared/behaviourFacts';
import {capabilityDefinition,validateCapabilityArguments} from '../../../shared/capabilities';
import {parseProgram} from './programs';
for(const file of ['program-contact.json','program-physical-catch.json','program-anchor-zone.json'])it('refuses old or missing point-event versions in '+file,()=>{
 const source=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/'+file,'utf8'));
 expect(parseProgram(JSON.stringify(source)).error).toBeNull();
 const node=file==='program-contact.json'?source.functions[0].body[0].body[0]:source.functions[0].body[0];
 for(const version of [1,3,'2',null,undefined]){
  node.version=version;const saved=JSON.stringify(source);
  expect(parseProgram(saved).error).toMatch(/version/);expect(JSON.stringify(source)).toBe(saved);
 }
});
it('requires explicit current room-point facts and launch contracts',()=>{
 for(const id of ['object.position','object.anchor','object.recipe.pose','object.physics.trajectory']){
  const fact=behaviourFact(id)!;expect(fact.version).toBe(2);expect(validateFactArguments(id,1,fact.example)).not.toBeNull();expect(validateFactArguments(id,2,fact.example)).toBeNull();
 }
 const launch=capabilityDefinition('object.physics.launch')!;expect(launch.version).toBe(2);
 expect(validateCapabilityArguments(launch.id,1,launch.example)).not.toBeNull();expect(validateCapabilityArguments(launch.id,2,launch.example)).toBeNull();
});
