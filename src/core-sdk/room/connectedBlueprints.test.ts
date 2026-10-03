// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {readFileSync} from 'node:fs';
import {expect,it} from 'vitest';
import {capabilityResources,validateCapabilityArguments} from '../../../shared/capabilities';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {moduleHash} from './programModules';
import {parseProgram,type BehaviourProgram} from './programs';
const fixtures='unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/';
const source=()=>JSON.parse(readFileSync(fixtures+'program-spring-lever.json','utf8'));
const cases=JSON.parse(readFileSync(fixtures+'connected-blueprints-contract.json','utf8')) as {name:string;valid:boolean;arguments:Record<string,unknown>}[];
it.each(cases)('matches native connected-blueprint boundaries: $name',c=>{
 expect(validateCapabilityArguments('object.batch.create',1,c.arguments)===null).toBe(c.valid);
});
it('includes an editable pinned lever constructor that requires connected native support',()=>{
 const program=source(),module=JSON.parse(readFileSync('unity/MaestroQuest/Assets/Maestro/Resources/Programs/Modules/SpringLever.json','utf8'));
 expect(program.imports[0].module).toEqual(module);expect(program.imports[0].hash).toBe(moduleHash(module));expect(parseProgram(JSON.stringify(program)).error).toBeNull();
 expect([...programFeatureRequirements(program as BehaviourProgram)]).toEqual(expect.arrayContaining(['connectedBlueprints.v2','physicalConnections.v1','batchCreation.v1','collisionShapes.v1','programModules.v1']));
 expect(capabilityResources('object.batch.create',module.program.functions[1].body[0].arguments)).toEqual([]);
});
it('keeps existing independent blueprints free of the connected feature requirement',()=>{
 const program=JSON.parse(readFileSync(fixtures+'program-batch-create.json','utf8')) as BehaviourProgram;
 expect([...programFeatureRequirements(program)]).not.toContain('connectedBlueprints.v2');expect(parseProgram(JSON.stringify(program)).error).toBeNull();
});
