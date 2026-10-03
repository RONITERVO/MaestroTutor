// Copyright 2026 Roni Tervo
// SPDX-License-Identifier: Apache-2.0
import {expect,it} from 'vitest';
import included from '../../../unity/MaestroQuest/Assets/Maestro/Resources/Programs/Modules/StructureWatch.json';
import fixture from '../../../unity/MaestroQuest/Assets/Maestro/Tests/Fixtures/program-structure-watch.json';
import {moduleHash,type ModuleRecord} from '../../../shared/programModuleIdentity';
import {programFeatureRequirements} from '../../../shared/programFeatures';
import {decodeModuleFile,encodeModuleFile} from './programModuleFile';
import {parseProgram} from './programs';
it('pins the shipped structure module in the same portable source accepted by the editor and agent',()=>{
 const module=included as ModuleRecord,hash=moduleHash(module);expect(fixture.imports[0]).toMatchObject({hash,module});
 expect(decodeModuleFile(encodeModuleFile(hash,module)).definition).toEqual(module);
 const result=parseProgram(JSON.stringify(fixture));expect(result.error).toBeNull();expect(result.program?.resources).toEqual([]);
 expect([...programFeatureRequirements(result.program!)]).toEqual(expect.arrayContaining(['conditionWaits.v1','structuredValues.v1','programModules.v1','structures.v1']));
});
it('keeps the included source editable while refusing a modified definition under its old pin',()=>{
 const copy=structuredClone(included) as ModuleRecord,hash=moduleHash(copy);copy.name='My structure response';
 expect(parseProgram(JSON.stringify(copy.program)).error).toBeNull();expect(()=>encodeModuleFile(hash,copy)).toThrow('mismatched content ID');
 expect(decodeModuleFile(encodeModuleFile(moduleHash(copy),copy)).definition.name).toBe('My structure response');
});
